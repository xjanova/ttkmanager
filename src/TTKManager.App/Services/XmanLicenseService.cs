using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TTKManager.App.Services;

public sealed class XmanLicenseService
{
    private const string ApiBase = "https://xman4289.com/api/v1/products/ttkmanager";
    private const string KeyPrefix = "TTK-";
    private const string LicenseStateKey = "license_key";
    private const string LicenseTypeStateKey = "license_type";
    private const string LicenseExpiryStateKey = "license_expires";

    private readonly HttpClient _http;
    private readonly Database _db;
    private readonly ILogger<XmanLicenseService> _log;
    private readonly string _machineId;

    private LicenseStatus _cached = new();
    public LicenseStatus CachedStatus => _cached;
    public string MachineId => _machineId;

    public event Action<LicenseStatus>? StateChanged;

    public XmanLicenseService(Database db, ILogger<XmanLicenseService> log)
    {
        _db = db;
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Add("User-Agent", "TTKManager-License/0.1");
        _machineId = GenerateMachineId();
    }

    public static string GenerateMachineId()
    {
        try
        {
            var raw = $"{Environment.MachineName}:{Environment.UserName}:{Environment.ProcessorCount}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch { return "unknown"; }
    }

    public static bool IsValidKeyFormat(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Trim().ToUpperInvariant().StartsWith(KeyPrefix);
    }

    public async Task LoadCachedAsync()
    {
        var key = await _db.GetStateAsync(LicenseStateKey);
        var type = await _db.GetStateAsync(LicenseTypeStateKey);
        var expires = await _db.GetStateAsync(LicenseExpiryStateKey);
        if (!string.IsNullOrEmpty(key))
        {
            _cached = new LicenseStatus
            {
                IsActive = true,
                LicenseKey = key,
                LicenseType = type ?? "free",
                ExpiresAt = DateTime.TryParse(expires, out var dt) ? dt : null
            };
            StateChanged?.Invoke(_cached);
        }
    }

    public async Task<LicenseResult> ActivateAsync(string licenseKey)
    {
        if (!IsValidKeyFormat(licenseKey))
            return new LicenseResult(false, "Invalid key format. Keys look like TTK-XXXX-XXXX-XXXX.");

        try
        {
            var request = new
            {
                license_key = licenseKey.Trim(),
                machine_id = _machineId,
                device_name = Environment.MachineName,
                app_version = AppVersion()
            };
            using var resp = await _http.PostAsJsonAsync($"{ApiBase}/activate", request);
            var result = await resp.Content.ReadFromJsonAsync<LicenseApiResponse>() ?? new LicenseApiResponse();

            if (result.Success)
            {
                _cached = new LicenseStatus
                {
                    IsActive = true,
                    LicenseKey = licenseKey.Trim().ToUpperInvariant(),
                    LicenseType = result.LicenseType ?? "free",
                    ExpiresAt = result.ExpiresAt,
                    DaysRemaining = result.DaysRemaining
                };
                await PersistAsync();
                StateChanged?.Invoke(_cached);
                return new LicenseResult(true, result.Message ?? "License activated");
            }
            return new LicenseResult(false, result.Message ?? "Activation failed");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "License activation failed");
            return new LicenseResult(false, $"Could not reach license server ({ex.GetType().Name})");
        }
    }

    public async Task<LicenseResult> ValidateAsync()
    {
        if (string.IsNullOrEmpty(_cached.LicenseKey))
            return new LicenseResult(false, "No license stored");
        try
        {
            var request = new
            {
                license_key = _cached.LicenseKey,
                machine_id = _machineId,
                app_version = AppVersion()
            };
            using var resp = await _http.PostAsJsonAsync($"{ApiBase}/validate", request);
            var result = await resp.Content.ReadFromJsonAsync<LicenseApiResponse>() ?? new LicenseApiResponse();

            if (result.Success)
            {
                _cached = new LicenseStatus
                {
                    IsActive = true,
                    LicenseKey = _cached.LicenseKey,
                    LicenseType = result.LicenseType ?? _cached.LicenseType,
                    ExpiresAt = result.ExpiresAt,
                    DaysRemaining = result.DaysRemaining
                };
                await PersistAsync();
                StateChanged?.Invoke(_cached);
                return new LicenseResult(true, result.Message ?? "License valid");
            }
            // explicit invalid → clear
            _cached = new LicenseStatus();
            await ClearAsync();
            StateChanged?.Invoke(_cached);
            return new LicenseResult(false, result.Message ?? "License invalid");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Validate failed — keeping cached license (offline grace)");
            return new LicenseResult(_cached.IsActive, "Offline — using cached license");
        }
    }

    public async Task<LicenseResult> DeactivateAsync()
    {
        if (string.IsNullOrEmpty(_cached.LicenseKey))
        {
            _cached = new LicenseStatus();
            await ClearAsync();
            return new LicenseResult(true, "Cleared local license");
        }
        try
        {
            var request = new { license_key = _cached.LicenseKey, machine_id = _machineId };
            using var resp = await _http.PostAsJsonAsync($"{ApiBase}/deactivate", request);
            var result = await resp.Content.ReadFromJsonAsync<LicenseApiResponse>() ?? new LicenseApiResponse();
            _cached = new LicenseStatus();
            await ClearAsync();
            StateChanged?.Invoke(_cached);
            return new LicenseResult(result.Success, result.Message ?? "Deactivated");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Deactivate failed — clearing locally anyway");
            _cached = new LicenseStatus();
            await ClearAsync();
            StateChanged?.Invoke(_cached);
            return new LicenseResult(true, "Deactivated locally (server unreachable)");
        }
    }

    public async Task RegisterDeviceAsync()
    {
        try
        {
            var request = new
            {
                machine_id = _machineId,
                device_name = Environment.MachineName,
                os_version = Environment.OSVersion.VersionString,
                app_version = AppVersion()
            };
            await _http.PostAsJsonAsync($"{ApiBase}/register-device", request);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Device registration failed (non-fatal)");
        }
    }

    private async Task PersistAsync()
    {
        await _db.SetStateAsync(LicenseStateKey, _cached.LicenseKey);
        await _db.SetStateAsync(LicenseTypeStateKey, _cached.LicenseType);
        await _db.SetStateAsync(LicenseExpiryStateKey, _cached.ExpiresAt?.ToString("o") ?? "");
    }

    private async Task ClearAsync()
    {
        await _db.SetStateAsync(LicenseStateKey, "");
        await _db.SetStateAsync(LicenseTypeStateKey, "");
        await _db.SetStateAsync(LicenseExpiryStateKey, "");
    }

    private static string AppVersion() => "0.1.0";
}

public sealed class LicenseStatus
{
    public bool IsActive { get; set; }
    public string LicenseKey { get; set; } = "";
    public string LicenseType { get; set; } = "free";
    public DateTime? ExpiresAt { get; set; }
    public int DaysRemaining { get; set; }

    public string DisplayType => LicenseType switch
    {
        "lifetime" => "Lifetime",
        "yearly" => "Pro (Yearly)",
        "monthly" => "Pro (Monthly)",
        "weekly" => "Weekly",
        "daily" => "Daily",
        "demo" => "Demo Trial",
        "free" => "Free",
        _ => LicenseType
    };

    public bool IsPremium => LicenseType is "lifetime" or "yearly" or "monthly";
    public string MaskedKey => string.IsNullOrEmpty(LicenseKey) ? "" :
        LicenseKey.Length > 8 ? LicenseKey[..4] + "-•••• -" + LicenseKey[^4..] : LicenseKey;
}

public sealed record LicenseResult(bool Success, string Message);

internal sealed class LicenseApiResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("license_type")] public string? LicenseType { get; set; }
    [JsonPropertyName("expires_at")] public DateTime? ExpiresAt { get; set; }
    [JsonPropertyName("days_remaining")] public int DaysRemaining { get; set; }
}
