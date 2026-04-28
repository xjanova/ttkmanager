using Microsoft.Extensions.Logging;
using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class BudgetPacer
{
    private readonly Database _db;
    private readonly ITikTokApiClient _api;
    private readonly ITokenProtector _tokens;
    private readonly ILogger<BudgetPacer> _log;

    public BudgetPacer(Database db, ITikTokApiClient api, ITokenProtector tokens, ILogger<BudgetPacer> log)
    {
        _db = db; _api = api; _tokens = tokens; _log = log;
    }

    public async Task<int> CheckCapsAsync(CancellationToken ct = default)
    {
        var caps = await _db.ListBudgetCapsAsync();
        var paused = 0;
        foreach (var cap in caps)
        {
            var since = StartOfPeriod(cap.Period);
            var spent = await _db.SumSpendSinceAsync(cap.AdvertiserId, since, cap.CampaignIdScope);
            if (spent < cap.CapAmount) continue;

            await _db.InsertAlertAsync(new Alert
            {
                Severity = AlertSeverity.Critical,
                Source = AlertSource.BudgetCap,
                Title = $"Budget cap exceeded: {cap.Period} {cap.CapAmount} {cap.Currency}",
                Body = $"Spent {spent:F2} {cap.Currency} since {since:yyyy-MM-dd HH:mm} — auto-pause: {cap.AutoPauseOnCap}",
                AdvertiserId = cap.AdvertiserId,
                CampaignId = cap.CampaignIdScope
            });

            if (!cap.AutoPauseOnCap) continue;

            var accounts = await _db.ListAccountsAsync();
            var account = accounts.FirstOrDefault(a => a.AdvertiserId == cap.AdvertiserId);
            if (account is null) continue;
            var token = _tokens.Unprotect(account.EncryptedAccessToken);
            try
            {
                if (cap.CampaignIdScope is not null)
                {
                    await _api.UpdateCampaignStatusAsync(cap.AdvertiserId, cap.CampaignIdScope, CampaignStatus.Disable, token, ct);
                    paused++;
                }
                else
                {
                    var campaigns = await _api.GetCampaignsAsync(cap.AdvertiserId, token, ct);
                    foreach (var c in campaigns.Where(c => c.Status == CampaignStatus.Enable))
                    {
                        await _api.UpdateCampaignStatusAsync(cap.AdvertiserId, c.CampaignId, CampaignStatus.Disable, token, ct);
                        paused++;
                    }
                }

                await _db.InsertAuditAsync(new AuditLogEntry
                {
                    AdvertiserId = cap.AdvertiserId,
                    CampaignId = cap.CampaignIdScope ?? "(all)",
                    Action = "BudgetPacer:Pause",
                    Status = AuditStatus.Success,
                    Detail = $"cap={cap.CapAmount} spent={spent:F2}"
                });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "BudgetPacer pause failed for cap {Id}", cap.Id);
            }
        }
        return paused;
    }

    private static DateTimeOffset StartOfPeriod(CapPeriod period)
    {
        var now = DateTimeOffset.UtcNow;
        return period switch
        {
            CapPeriod.Daily => new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero),
            CapPeriod.Weekly => now.AddDays(-(int)now.DayOfWeek).Date,
            CapPeriod.Monthly => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero),
            _ => now.AddDays(-1)
        };
    }
}
