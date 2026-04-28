namespace TTKManager.App.Models;

public sealed class Alert
{
    public long Id { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required AlertSeverity Severity { get; init; }
    public required AlertSource Source { get; init; }
    public required string Title { get; init; }
    public string? Body { get; init; }
    public string? AdvertiserId { get; init; }
    public string? CampaignId { get; init; }
    public bool Dismissed { get; init; }
}

public enum AlertSeverity { Info, Warning, Critical }
public enum AlertSource { Anomaly, BudgetCap, AutoRule, TokenExpiry, ApiError, System }
