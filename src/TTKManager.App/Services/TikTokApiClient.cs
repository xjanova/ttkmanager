using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class TikTokApiClient : ITikTokApiClient
{
    private const string BaseUrl = "https://business-api.tiktok.com/open_api/v1.3/";
    private const string OAuthExchangeUrl = "https://business-api.tiktok.com/open_api/v1.3/oauth2/access_token/";

    private readonly HttpClient _http;
    private readonly ILogger<TikTokApiClient> _log;
    private readonly TikTokAppCredentials _creds;

    public TikTokApiClient(HttpClient http, ILogger<TikTokApiClient> log, TikTokAppCredentials creds)
    {
        _http = http;
        _log = log;
        _creds = creds;
        _http.BaseAddress = new Uri(BaseUrl);
    }

    public Task<IReadOnlyList<TikTokAccount>> ListAuthorizedAdvertisersAsync(string accessToken, CancellationToken ct = default)
        => throw new NotImplementedException("Implement after Marketing API approval — calls /oauth2/advertiser/get/");

    public Task<IReadOnlyList<Campaign>> GetCampaignsAsync(string advertiserId, string accessToken, CancellationToken ct = default)
        => throw new NotImplementedException("Implement after Marketing API approval — calls /campaign/get/");

    public Task UpdateCampaignBudgetAsync(string advertiserId, string campaignId, decimal newDailyBudget, string accessToken, CancellationToken ct = default)
        => throw new NotImplementedException("Implement after Marketing API approval — calls /campaign/update/");

    public Task UpdateCampaignStatusAsync(string advertiserId, string campaignId, CampaignStatus newStatus, string accessToken, CancellationToken ct = default)
        => throw new NotImplementedException("Implement after Marketing API approval — calls /campaign/status/update/");

    public async Task<TokenPair> ExchangeAuthCodeAsync(string authCode, CancellationToken ct = default)
    {
        var payload = new
        {
            app_id = _creds.AppId,
            secret = _creds.Secret,
            auth_code = authCode
        };
        using var resp = await _http.PostAsJsonAsync(OAuthExchangeUrl, payload, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return ParseTokenPair(json);
    }

    public Task<TokenPair> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
        => throw new NotImplementedException("TikTok Marketing API tokens are long-lived — refresh path TBD per Developer App config");

    private static TokenPair ParseTokenPair(JsonElement root)
    {
        var data = root.GetProperty("data");
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
}

public sealed record TikTokAppCredentials(string AppId, string Secret, string RedirectUri);
