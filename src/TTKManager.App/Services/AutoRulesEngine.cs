using Microsoft.Extensions.Logging;
using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class AutoRulesEngine
{
    private readonly Database _db;
    private readonly ITikTokApiClient _api;
    private readonly ITokenProtector _tokens;
    private readonly ILogger<AutoRulesEngine> _log;

    public AutoRulesEngine(Database db, ITikTokApiClient api, ITokenProtector tokens, ILogger<AutoRulesEngine> log)
    {
        _db = db; _api = api; _tokens = tokens; _log = log;
    }

    public async Task<int> EvaluateAllAsync(CancellationToken ct = default)
    {
        var rules = await _db.ListAutoRulesAsync();
        var fired = 0;
        foreach (var rule in rules.Where(r => r.Enabled))
        {
            if (rule.LastFiredAt.HasValue &&
                DateTimeOffset.UtcNow - rule.LastFiredAt.Value < TimeSpan.FromMinutes(rule.CooldownMinutes))
                continue;

            try
            {
                if (await EvaluateAsync(rule, ct)) fired++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Auto-rule {Id} '{Name}' evaluation failed", rule.Id, rule.Name);
            }
        }
        return fired;
    }

    private async Task<bool> EvaluateAsync(AutoRule rule, CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddMinutes(-rule.WindowMinutes);
        var campaigns = string.IsNullOrEmpty(rule.CampaignIdScope)
            ? await ListCampaignIdsForAdvertiserAsync(rule.AdvertiserId, ct)
            : new[] { rule.CampaignIdScope };

        var matched = false;
        foreach (var campaignId in campaigns)
        {
            var samples = await _db.ListSamplesAsync(campaignId, since);
            if (samples.Count == 0) continue;
            var aggregate = Aggregate(samples);
            var value = rule.Metric switch
            {
                AutoMetric.Cpc => aggregate.Cpc,
                AutoMetric.Cpm => aggregate.Cpm,
                AutoMetric.Ctr => aggregate.Ctr,
                AutoMetric.Roas => aggregate.Roas,
                AutoMetric.Spend => aggregate.Spend,
                AutoMetric.Impressions => aggregate.Impressions,
                AutoMetric.Conversions => aggregate.Conversions,
                AutoMetric.Frequency => aggregate.Impressions == 0 ? 0 : (decimal)aggregate.Impressions / Math.Max(1, aggregate.Clicks),
                _ => 0m
            };

            if (Compare(value, rule.Comparator, rule.Threshold))
            {
                await ApplyAsync(rule, campaignId, value, ct);
                matched = true;
            }
        }

        if (matched) await _db.TouchAutoRuleAsync(rule.Id);
        return matched;
    }

    private async Task<IReadOnlyList<string>> ListCampaignIdsForAdvertiserAsync(string advertiserId, CancellationToken ct)
    {
        var accounts = await _db.ListAccountsAsync();
        var account = accounts.FirstOrDefault(a => a.AdvertiserId == advertiserId);
        if (account is null) return Array.Empty<string>();
        try
        {
            var token = _tokens.Unprotect(account.EncryptedAccessToken);
            var campaigns = await _api.GetCampaignsAsync(advertiserId, token, ct);
            return campaigns.Select(c => c.CampaignId).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static (decimal Spend, long Impressions, long Clicks, long Conversions, decimal Revenue, decimal Cpc, decimal Cpm, decimal Ctr, decimal Roas) Aggregate(IReadOnlyList<MetricSample> samples)
    {
        var spend = samples.Sum(s => s.Spend);
        var imp = samples.Sum(s => s.Impressions);
        var clk = samples.Sum(s => s.Clicks);
        var conv = samples.Sum(s => s.Conversions);
        var rev = samples.Sum(s => s.Revenue);
        var cpc = clk > 0 ? spend / clk : 0m;
        var cpm = imp > 0 ? spend / imp * 1000m : 0m;
        var ctr = imp > 0 ? (decimal)clk / imp : 0m;
        var roas = spend > 0 ? rev / spend : 0m;
        return (spend, imp, clk, conv, rev, cpc, cpm, ctr, roas);
    }

    private static bool Compare(decimal value, AutoComparator op, decimal threshold) => op switch
    {
        AutoComparator.GreaterThan => value > threshold,
        AutoComparator.LessThan => value < threshold,
        AutoComparator.GreaterOrEqual => value >= threshold,
        AutoComparator.LessOrEqual => value <= threshold,
        _ => false
    };

    private async Task ApplyAsync(AutoRule rule, string campaignId, decimal value, CancellationToken ct)
    {
        var accounts = await _db.ListAccountsAsync();
        var account = accounts.FirstOrDefault(a => a.AdvertiserId == rule.AdvertiserId);
        var detail = $"metric={rule.Metric}={value:F2} threshold={rule.Threshold}";

        await _db.InsertAlertAsync(new Alert
        {
            Severity = rule.Action == AutoAction.AlertOnly ? AlertSeverity.Info : AlertSeverity.Warning,
            Source = AlertSource.AutoRule,
            Title = $"Auto-rule fired: {rule.Name}",
            Body = $"Campaign {campaignId} · {detail} · action={rule.Action}",
            AdvertiserId = rule.AdvertiserId,
            CampaignId = campaignId
        });

        if (rule.Action == AutoAction.AlertOnly || account is null) return;

        var token = _tokens.Unprotect(account.EncryptedAccessToken);
        try
        {
            switch (rule.Action)
            {
                case AutoAction.PauseCampaign:
                    await _api.UpdateCampaignStatusAsync(rule.AdvertiserId, campaignId, CampaignStatus.Disable, token, ct);
                    break;
                case AutoAction.EnableCampaign:
                    await _api.UpdateCampaignStatusAsync(rule.AdvertiserId, campaignId, CampaignStatus.Enable, token, ct);
                    break;
                case AutoAction.SetBudget when rule.ActionAmount.HasValue:
                    await _api.UpdateCampaignBudgetAsync(rule.AdvertiserId, campaignId, rule.ActionAmount.Value, token, ct);
                    break;
                case AutoAction.IncreaseBudgetPercent when rule.ActionAmount.HasValue:
                case AutoAction.DecreaseBudgetPercent when rule.ActionAmount.HasValue:
                    var campaigns = await _api.GetCampaignsAsync(rule.AdvertiserId, token, ct);
                    var current = campaigns.FirstOrDefault(c => c.CampaignId == campaignId);
                    if (current is not null)
                    {
                        var pct = rule.ActionAmount.Value / 100m;
                        var newBudget = rule.Action == AutoAction.IncreaseBudgetPercent
                            ? current.Budget * (1m + pct)
                            : current.Budget * (1m - pct);
                        await _api.UpdateCampaignBudgetAsync(rule.AdvertiserId, campaignId, newBudget, token, ct);
                    }
                    break;
            }

            await _db.InsertAuditAsync(new AuditLogEntry
            {
                RuleId = null,
                AdvertiserId = rule.AdvertiserId,
                CampaignId = campaignId,
                Action = $"AutoRule:{rule.Action}",
                Status = AuditStatus.Success,
                Detail = detail
            });
        }
        catch (Exception ex)
        {
            await _db.InsertAuditAsync(new AuditLogEntry
            {
                RuleId = null,
                AdvertiserId = rule.AdvertiserId,
                CampaignId = campaignId,
                Action = $"AutoRule:{rule.Action}",
                Status = AuditStatus.Failed,
                Error = ex.Message
            });
            throw;
        }
    }
}
