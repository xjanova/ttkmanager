namespace TTKManager.App.Models;

public sealed class AuditLogEntry
{
    public long Id { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public long? RuleId { get; init; }
    public required string AdvertiserId { get; init; }
    public required string CampaignId { get; init; }
    public required string Action { get; init; }
    public required AuditStatus Status { get; init; }
    public string? Detail { get; init; }
    public string? Error { get; init; }
}

public enum AuditStatus
{
    Success,
    Failed,
    Skipped
}
