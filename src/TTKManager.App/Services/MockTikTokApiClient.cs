using Microsoft.Extensions.Logging;
using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class MockTikTokApiClient : ITikTokApiClient
{
    private readonly ILogger<MockTikTokApiClient> _log;
    private readonly Dictionary<string, Campaign> _campaigns = new();

    public MockTikTokApiClient(ILogger<MockTikTokApiClient> log)
    {
        _log = log;
        SeedMockData();
    }

    private void SeedMockData()
    {
        var demoAdv = "7000000000000000001";
        for (int i = 1; i <= 4; i++)
        {
            var id = $"campaign_{i:D3}";
            _campaigns[id] = new Campaign
            {
                CampaignId = id,
                AdvertiserId = demoAdv,
                Name = $"Demo Campaign {i}",
                Status = i % 2 == 0 ? CampaignStatus.Disable : CampaignStatus.Enable,
                BudgetMode = BudgetMode.Day,
                Budget = 1000m * i,
                Objective = i % 2 == 0 ? "TRAFFIC" : "CONVERSIONS"
            };
        }
    }

    public Task<IReadOnlyList<TikTokAccount>> ListAuthorizedAdvertisersAsync(string accessToken, CancellationToken ct = default)
    {
        IReadOnlyList<TikTokAccount> result = new List<TikTokAccount>
        {
            new() {
                AdvertiserId = "7000000000000000001",
                Name = "Demo Advertiser (Mock)",
                Currency = "THB",
                Country = "TH",
                EncryptedRefreshToken = Array.Empty<byte>(),
                EncryptedAccessToken = Array.Empty<byte>(),
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
            }
        };
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Campaign>> GetCampaignsAsync(string advertiserId, string accessToken, CancellationToken ct = default)
    {
        IReadOnlyList<Campaign> result = _campaigns.Values
            .Where(c => c.AdvertiserId == advertiserId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateCampaignBudgetAsync(string advertiserId, string campaignId, decimal newDailyBudget, string accessToken, CancellationToken ct = default)
    {
        if (_campaigns.TryGetValue(campaignId, out var c))
        {
            _campaigns[campaignId] = new Campaign
            {
                CampaignId = c.CampaignId,
                AdvertiserId = c.AdvertiserId,
                Name = c.Name,
                Status = c.Status,
                BudgetMode = c.BudgetMode,
                Budget = newDailyBudget,
                Objective = c.Objective
            };
            _log.LogInformation("Mock: set {Campaign} budget to {Budget}", campaignId, newDailyBudget);
        }
        return Task.CompletedTask;
    }

    public Task UpdateCampaignStatusAsync(string advertiserId, string campaignId, CampaignStatus newStatus, string accessToken, CancellationToken ct = default)
    {
        if (_campaigns.TryGetValue(campaignId, out var c))
        {
            _campaigns[campaignId] = new Campaign
            {
                CampaignId = c.CampaignId,
                AdvertiserId = c.AdvertiserId,
                Name = c.Name,
                Status = newStatus,
                BudgetMode = c.BudgetMode,
                Budget = c.Budget,
                Objective = c.Objective
            };
            _log.LogInformation("Mock: set {Campaign} status to {Status}", campaignId, newStatus);
        }
        return Task.CompletedTask;
    }

    public Task<TokenPair> ExchangeAuthCodeAsync(string authCode, CancellationToken ct = default)
    {
        var pair = new TokenPair(
            AccessToken: $"mock_access_{Guid.NewGuid():N}",
            RefreshToken: $"mock_refresh_{Guid.NewGuid():N}",
            AccessTokenExpiresAt: DateTimeOffset.UtcNow.AddHours(24),
            RefreshTokenExpiresAt: DateTimeOffset.UtcNow.AddDays(365));
        return Task.FromResult(pair);
    }

    public Task<TokenPair> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var pair = new TokenPair(
            AccessToken: $"mock_access_{Guid.NewGuid():N}",
            RefreshToken: refreshToken,
            AccessTokenExpiresAt: DateTimeOffset.UtcNow.AddHours(24),
            RefreshTokenExpiresAt: DateTimeOffset.UtcNow.AddDays(365));
        return Task.FromResult(pair);
    }
}
