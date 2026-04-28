namespace TTKManager.App.Models;

public sealed class MetricSample
{
    public long Id { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required string AdvertiserId { get; init; }
    public required string CampaignId { get; init; }
    public decimal Spend { get; init; }
    public long Impressions { get; init; }
    public long Clicks { get; init; }
    public long Conversions { get; init; }
    public decimal Revenue { get; init; }

    public decimal Cpc => Clicks > 0 ? Spend / Clicks : 0m;
    public decimal Cpm => Impressions > 0 ? Spend / Impressions * 1000m : 0m;
    public decimal Ctr => Impressions > 0 ? (decimal)Clicks / Impressions : 0m;
    public decimal Roas => Spend > 0 ? Revenue / Spend : 0m;
}
