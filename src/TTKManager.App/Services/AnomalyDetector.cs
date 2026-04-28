using Microsoft.Extensions.Logging;
using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class AnomalyDetector
{
    private readonly Database _db;
    private readonly ILogger<AnomalyDetector> _log;

    public double SigmaThreshold { get; set; } = 2.0;
    public int MinSamples { get; set; } = 10;

    public AnomalyDetector(Database db, ILogger<AnomalyDetector> log)
    {
        _db = db; _log = log;
    }

    public async Task<int> ScanAsync(CancellationToken ct = default)
    {
        var accounts = await _db.ListAccountsAsync();
        var raised = 0;
        foreach (var account in accounts)
        {
            var since = DateTimeOffset.UtcNow.AddDays(-14);
            var allSamples = await _db.ListSamplesAsync(account.AdvertiserId, since);
            var byCampaign = allSamples.GroupBy(s => s.CampaignId);
            foreach (var group in byCampaign)
            {
                if (group.Count() < MinSamples) continue;
                var ordered = group.OrderBy(s => s.Timestamp).ToList();
                var latest = ordered.Last();
                var prior = ordered.Take(ordered.Count - 1).ToList();

                if (CheckAnomaly(prior.Select(s => (double)s.Cpm).ToList(), (double)latest.Cpm, out var z))
                {
                    await _db.InsertAlertAsync(new Alert
                    {
                        Severity = z >= 3 ? AlertSeverity.Critical : AlertSeverity.Warning,
                        Source = AlertSource.Anomaly,
                        Title = $"CPM anomaly on {group.Key}",
                        Body = $"Latest CPM {latest.Cpm:F2} is {z:F1}σ above 14-day baseline",
                        AdvertiserId = account.AdvertiserId,
                        CampaignId = group.Key
                    });
                    raised++;
                }
                if (CheckAnomaly(prior.Select(s => (double)s.Cpc).ToList(), (double)latest.Cpc, out var zc))
                {
                    await _db.InsertAlertAsync(new Alert
                    {
                        Severity = zc >= 3 ? AlertSeverity.Critical : AlertSeverity.Warning,
                        Source = AlertSource.Anomaly,
                        Title = $"CPC anomaly on {group.Key}",
                        Body = $"Latest CPC {latest.Cpc:F2} is {zc:F1}σ above 14-day baseline",
                        AdvertiserId = account.AdvertiserId,
                        CampaignId = group.Key
                    });
                    raised++;
                }
            }
        }
        if (raised > 0) _log.LogInformation("AnomalyDetector raised {N} alerts", raised);
        return raised;
    }

    private bool CheckAnomaly(List<double> baseline, double latest, out double z)
    {
        z = 0;
        if (baseline.Count < MinSamples) return false;
        var mean = baseline.Average();
        var variance = baseline.Sum(x => (x - mean) * (x - mean)) / baseline.Count;
        var stdev = Math.Sqrt(variance);
        if (stdev <= 0.0001) return false;
        z = (latest - mean) / stdev;
        return Math.Abs(z) >= SigmaThreshold;
    }
}
