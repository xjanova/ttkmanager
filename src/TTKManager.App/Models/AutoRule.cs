namespace TTKManager.App.Models;

public sealed class AutoRule
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public required string AdvertiserId { get; init; }
    public string? CampaignIdScope { get; init; }
    public required AutoMetric Metric { get; init; }
    public required AutoComparator Comparator { get; init; }
    public required decimal Threshold { get; init; }
    public int WindowMinutes { get; init; } = 60;
    public required AutoAction Action { get; init; }
    public decimal? ActionAmount { get; init; }
    public int CooldownMinutes { get; init; } = 60;
    public bool Enabled { get; init; } = true;
    public DateTimeOffset? LastFiredAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum AutoMetric { Cpc, Cpm, Ctr, Roas, Spend, Impressions, Conversions, Frequency }
public enum AutoComparator { GreaterThan, LessThan, GreaterOrEqual, LessOrEqual }
public enum AutoAction { PauseCampaign, EnableCampaign, IncreaseBudgetPercent, DecreaseBudgetPercent, SetBudget, AlertOnly }
