using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class TikTokApiClient : ITikTokApiClient
{
    private const string BaseUrl = "https://business-api.tiktok.com/open_api/v1.3/";
    private const string AccessTokenHeader = "Access-Token";

    private readonly HttpClient _http;
    private readonly ILogger<TikTokApiClient> _log;
    private readonly TikTokAppCredentials _creds;
    private readonly ResiliencePipeline<HttpResponseMessage> _retry;

    public TikTokApiClient(HttpClient http, ILogger<TikTokApiClient> log, TikTokAppCredentials creds)
    {
        _http = http;
        _log = log;
        _creds = creds;
        _http.BaseAddress = new Uri(BaseUrl);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TTKManager/0.1 (+https://github.com/xjanova/ttkmanager)");

        _retry = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode == 429 || (int)r.StatusCode >= 500)
                    .Handle<HttpRequestException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _log.LogWarning("Retry {Attempt} after {Delay}ms (status={Status})",
                        args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<IReadOnlyList<TikTokAccount>> ListAuthorizedAdvertisersAsync(string accessToken, CancellationToken ct = default)
    {
        var url = $"oauth2/advertiser/get/?app_id={Uri.EscapeDataString(_creds.AppId)}&secret={Uri.EscapeDataString(_creds.Secret)}";
        using var resp = await SendAsync(HttpMethod.Get, url, body: null, accessToken: null, ct);
        var data = await ReadDataAsync(resp, ct);
        var list = data.GetProperty("list");
        var result = new List<TikTokAccount>();
        foreach (var item in list.EnumerateArray())
        {
            var advId = item.GetProperty("advertiser_id").GetString() ?? "";
            var name = item.GetProperty("advertiser_name").GetString() ?? advId;
            result.Add(new TikTokAccount
            {
                AdvertiserId = advId,
                Name = name,
                EncryptedRefreshToken = Array.Empty<byte>(),
                EncryptedAccessToken = Array.Empty<byte>(),
                AccessTokenExpiresAt = DateTimeOffset.UtcNow
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<Campaign>> GetCampaignsAsync(string advertiserId, string accessToken, CancellationToken ct = default)
    {
        var url = $"campaign/get/?advertiser_id={Uri.EscapeDataString(advertiserId)}&page=1&page_size=100";
        using var resp = await SendAsync(HttpMethod.Get, url, body: null, accessToken, ct);
        var data = await ReadDataAsync(resp, ct);
        var list = data.GetProperty("list");
        var result = new List<Campaign>();
        foreach (var item in list.EnumerateArray())
        {
            result.Add(new Campaign
            {
                CampaignId = item.GetProperty("campaign_id").GetString() ?? "",
                AdvertiserId = advertiserId,
                Name = item.TryGetProperty("campaign_name", out var n) ? (n.GetString() ?? "") : "",
                Status = ParseStatus(item.TryGetProperty("operation_status", out var s) ? s.GetString() : null),
                BudgetMode = ParseBudgetMode(item.TryGetProperty("budget_mode", out var bm) ? bm.GetString() : null),
                Budget = item.TryGetProperty("budget", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetDecimal() : 0m,
                Objective = item.TryGetProperty("objective_type", out var o) ? o.GetString() : null
            });
        }
        return result;
    }

    public async Task UpdateCampaignBudgetAsync(string advertiserId, string campaignId, decimal newDailyBudget, string accessToken, CancellationToken ct = default)
    {
        var payload = new
        {
            advertiser_id = advertiserId,
            campaign_id = campaignId,
            budget = newDailyBudget,
            budget_mode = "BUDGET_MODE_DAY"
        };
        using var resp = await SendAsync(HttpMethod.Post, "campaign/update/", payload, accessToken, ct);
        await ReadDataAsync(resp, ct);
    }

    public async Task UpdateCampaignStatusAsync(string advertiserId, string campaignId, CampaignStatus newStatus, string accessToken, CancellationToken ct = default)
    {
        var payload = new
        {
            advertiser_id = advertiserId,
            campaign_ids = new[] { campaignId },
            operation_status = newStatus switch
            {
                CampaignStatus.Enable => "ENABLE",
                CampaignStatus.Disable => "DISABLE",
                CampaignStatus.Delete => "DELETE",
                _ => throw new ArgumentOutOfRangeException(nameof(newStatus))
            }
        };
        using var resp = await SendAsync(HttpMethod.Post, "campaign/status/update/", payload, accessToken, ct);
        await ReadDataAsync(resp, ct);
    }

    public async Task<TokenPair> ExchangeAuthCodeAsync(string authCode, CancellationToken ct = default)
    {
        var payload = new
        {
            app_id = _creds.AppId,
            secret = _creds.Secret,
            auth_code = authCode
        };
        using var resp = await SendAsync(HttpMethod.Post, "oauth2/access_token/", payload, accessToken: null, ct);
        var data = await ReadDataAsync(resp, ct);
        return ParseTokenPair(data);
    }

    public async Task<TokenPair> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var payload = new
        {
            app_id = _creds.AppId,
            secret = _creds.Secret,
            refresh_token = refreshToken,
            grant_type = "refresh_token"
        };
        using var resp = await SendAsync(HttpMethod.Post, "oauth2/refresh_token/", payload, accessToken: null, ct);
        var data = await ReadDataAsync(resp, ct);
        return ParseTokenPair(data);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUrl, object? body, string? accessToken, CancellationToken ct)
    {
        return await _retry.ExecuteAsync(async token =>
        {
            using var req = new HttpRequestMessage(method, relativeUrl);
            if (!string.IsNullOrEmpty(accessToken))
                req.Headers.Add(AccessTokenHeader, accessToken);
            if (body is not null)
            {
                var json = JsonSerializer.Serialize(body);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return await _http.SendAsync(req, token);
        }, ct);
    }

    private async Task<JsonElement> ReadDataAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new TikTokApiException($"HTTP {(int)resp.StatusCode}: {raw}");
        var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : -1;
        if (code != 0)
        {
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            throw new TikTokApiException($"API error code={code}: {msg}");
        }
        return root.TryGetProperty("data", out var d) ? d.Clone() : default;
    }

    private static TokenPair ParseTokenPair(JsonElement data)
    {
        var access = data.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("missing access_token");
        var refresh = data.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        var accessExpiresIn = data.TryGetProperty("access_token_expire_in", out var ae) ? ae.GetInt64() : 86400;
        var refreshExpiresIn = data.TryGetProperty("refresh_token_expire_in", out var re) ? re.GetInt64() : 31536000;
        return new TokenPair(
            AccessToken: access,
            RefreshToken: refresh,
            AccessTokenExpiresAt: DateTimeOffset.UtcNow.AddSeconds(accessExpiresIn),
            RefreshTokenExpiresAt: DateTimeOffset.UtcNow.AddSeconds(refreshExpiresIn));
    }

    private static CampaignStatus ParseStatus(string? operationStatus) => operationStatus switch
    {
        "ENABLE" => CampaignStatus.Enable,
        "DISABLE" => CampaignStatus.Disable,
        "DELETE" => CampaignStatus.Delete,
        _ => CampaignStatus.Unknown
    };

    private static BudgetMode ParseBudgetMode(string? mode) => mode switch
    {
        "BUDGET_MODE_INFINITE" => BudgetMode.Infinite,
        "BUDGET_MODE_DAY" => BudgetMode.Day,
        "BUDGET_MODE_TOTAL" => BudgetMode.Total,
        _ => BudgetMode.Unknown
    };
}

public sealed record TikTokAppCredentials(string AppId, string Secret, string RedirectUri);

public sealed class TikTokApiException : Exception
{
    public TikTokApiException(string message) : base(message) { }
}
