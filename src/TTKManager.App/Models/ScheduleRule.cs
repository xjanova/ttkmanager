namespace TTKManager.App.Models;

public sealed class ScheduleRule
{
    public long Id { get; init; }
    public required string AdvertiserId { get; init; }
    public required string CampaignId { get; init; }
    public required string Name { get; init; }
    public required RuleAction Action { get; init; }
    public decimal? BudgetAmount { get; init; }
    public required string CronExpression { get; init; }
    public string TimeZoneId { get; init; } = "Asia/Bangkok";
    public bool Enabled { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum RuleAction
{
    SetDailyBudget,
    PauseCampaign,
    EnableCampaign
}
