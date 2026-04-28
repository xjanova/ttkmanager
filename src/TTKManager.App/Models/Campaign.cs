namespace TTKManager.App.Models;

public sealed class Campaign
{
    public required string CampaignId { get; init; }
    public required string AdvertiserId { get; init; }
    public required string Name { get; init; }
    public CampaignStatus Status { get; init; }
    public BudgetMode BudgetMode { get; init; }
    public decimal Budget { get; init; }
    public string? Objective { get; init; }
}

public enum CampaignStatus
{
    Unknown,
    Enable,
    Disable,
    Delete
}

public enum BudgetMode
{
    Unknown,
    Infinite,
    Day,
    Total
}
