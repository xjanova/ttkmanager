using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class MockSamplerService
{
    private readonly Database _db;
    private readonly Random _rng = new(42);

    public MockSamplerService(Database db) { _db = db; }

    public async Task SeedDemoSamplesAsync()
    {
        var accounts = await _db.ListAccountsAsync();
        if (accounts.Count == 0) return;
        foreach (var acct in accounts)
        {
            for (int day = 14; day >= 0; day--)
            {
                for (int hour = 0; hour < 24; hour += 2)
                {
                    var ts = DateTimeOffset.UtcNow.AddDays(-day).AddHours(-hour);
                    for (int i = 1; i <= 4; i++)
                    {
                        var cid = $"campaign_{i:D3}";
                        var imp = (long)(_rng.Next(2000, 20000) * (1 + Math.Sin(hour / 24.0 * Math.PI * 2)));
                        var clk = (long)(imp * (0.01 + _rng.NextDouble() * 0.03));
                        var conv = (long)(clk * (0.02 + _rng.NextDouble() * 0.06));
                        var spend = (decimal)(imp * (0.04 + _rng.NextDouble() * 0.06) / 1000.0 * 1000);
                        var revenue = conv * (decimal)(280 + _rng.NextDouble() * 200);
                        await _db.InsertMetricSampleAsync(new MetricSample
                        {
                            Timestamp = ts,
                            AdvertiserId = acct.AdvertiserId,
                            CampaignId = cid,
                            Spend = Math.Round(spend, 2),
                            Impressions = imp,
                            Clicks = clk,
                            Conversions = conv,
                            Revenue = Math.Round(revenue, 2)
                        });
                    }
                }
            }
        }
    }
}
