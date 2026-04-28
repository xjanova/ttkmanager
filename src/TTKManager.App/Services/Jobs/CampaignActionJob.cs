using Microsoft.Extensions.Logging;
using Quartz;
using TTKManager.App.Models;

namespace TTKManager.App.Services.Jobs;

[DisallowConcurrentExecution]
public sealed class CampaignActionJob : IJob
{
    public const string RuleIdKey = "ruleId";

    private readonly Database _db;
    private readonly ITikTokApiClient _api;
    private readonly ITokenProtector _tokens;
    private readonly ILogger<CampaignActionJob> _log;

    public CampaignActionJob(Database db, ITikTokApiClient api, ITokenProtector tokens, ILogger<CampaignActionJob> log)
    {
        _db = db;
        _api = api;
        _tokens = tokens;
        _log = log;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ruleId = context.MergedJobDataMap.GetLong(RuleIdKey);
        var rules = await _db.ListRulesAsync();
        var rule = rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null || !rule.Enabled)
        {
            _log.LogInformation("Rule {RuleId} not found or disabled — skipping", ruleId);
            return;
        }

        var accounts = await _db.ListAccountsAsync();
        var account = accounts.FirstOrDefault(a => a.AdvertiserId == rule.AdvertiserId);
        if (account is null)
        {
            await _db.InsertAuditAsync(new AuditLogEntry
            {
                RuleId = rule.Id,
                AdvertiserId = rule.AdvertiserId,
                CampaignId = rule.CampaignId,
                Action = rule.Action.ToString(),
                Status = AuditStatus.Failed,
                Error = "Account not connected"
            });
            return;
        }

        var accessToken = _tokens.Unprotect(account.EncryptedAccessToken);
        try
        {
            switch (rule.Action)
            {
                case RuleAction.SetDailyBudget when rule.BudgetAmount.HasValue:
                    await _api.UpdateCampaignBudgetAsync(rule.AdvertiserId, rule.CampaignId, rule.BudgetAmount.Value, accessToken, context.CancellationToken);
                    break;
                case RuleAction.PauseCampaign:
                    await _api.UpdateCampaignStatusAsync(rule.AdvertiserId, rule.CampaignId, CampaignStatus.Disable, accessToken, context.CancellationToken);
                    break;
                case RuleAction.EnableCampaign:
                    await _api.UpdateCampaignStatusAsync(rule.AdvertiserId, rule.CampaignId, CampaignStatus.Enable, accessToken, context.CancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported action: {rule.Action}");
            }

            await _db.InsertAuditAsync(new AuditLogEntry
            {
                RuleId = rule.Id,
                AdvertiserId = rule.AdvertiserId,
                CampaignId = rule.CampaignId,
                Action = rule.Action.ToString(),
                Status = AuditStatus.Success,
                Detail = rule.BudgetAmount.HasValue ? $"budget={rule.BudgetAmount.Value}" : null
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Rule {RuleId} failed", ruleId);
            await _db.InsertAuditAsync(new AuditLogEntry
            {
                RuleId = rule.Id,
                AdvertiserId = rule.AdvertiserId,
                CampaignId = rule.CampaignId,
                Action = rule.Action.ToString(),
                Status = AuditStatus.Failed,
                Error = ex.Message
            });
            throw;
        }
    }
}
