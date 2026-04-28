using TTKManager.App.Models;

namespace TTKManager.App.Services;

public interface ITikTokApiClient
{
    Task<IReadOnlyList<TikTokAccount>> ListAuthorizedAdvertisersAsync(string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<Campaign>> GetCampaignsAsync(string advertiserId, string accessToken, CancellationToken ct = default);
    Task UpdateCampaignBudgetAsync(string advertiserId, string campaignId, decimal newDailyBudget, string accessToken, CancellationToken ct = default);
    Task UpdateCampaignStatusAsync(string advertiserId, string campaignId, CampaignStatus newStatus, string accessToken, CancellationToken ct = default);
    Task<TokenPair> ExchangeAuthCodeAsync(string authCode, CancellationToken ct = default);
    Task<TokenPair> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default);
}

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt);
