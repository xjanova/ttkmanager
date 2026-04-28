namespace TTKManager.App.Models;

public sealed class BudgetCap
{
    public long Id { get; init; }
    public required string AdvertiserId { get; init; }
    public string? CampaignIdScope { get; init; }
    public required CapPeriod Period { get; init; }
    public required decimal CapAmount { get; init; }
    public string Currency { get; init; } = "THB";
    public bool AutoPauseOnCap { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum CapPeriod { Daily, Weekly, Monthly }
