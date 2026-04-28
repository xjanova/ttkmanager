using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly Database? _db;

    public ObservableCollection<Alert> RecentAlerts { get; } = new();
    public ObservableCollection<ScheduleRule> NextRules { get; } = new();
    public ObservableCollection<AuditLogEntry> RecentActivity { get; } = new();

    private string _todaySpend = "—";
    public string TodaySpend { get => _todaySpend; set => SetProperty(ref _todaySpend, value); }

    private string _avgCpm = "—";
    public string AvgCpm { get => _avgCpm; set => SetProperty(ref _avgCpm, value); }

    private string _avgCpc = "—";
    public string AvgCpc { get => _avgCpc; set => SetProperty(ref _avgCpc, value); }

    private string _avgRoas = "—";
    public string AvgRoas { get => _avgRoas; set => SetProperty(ref _avgRoas, value); }

    private int _accountsCount;
    public int AccountsCount { get => _accountsCount; set => SetProperty(ref _accountsCount, value); }

    private int _activeRulesCount;
    public int ActiveRulesCount { get => _activeRulesCount; set => SetProperty(ref _activeRulesCount, value); }

    public IAsyncRelayCommand RefreshCommand { get; }

    public DashboardViewModel(Database db)
    {
        _db = db;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        _ = RefreshAsync();
    }

    public DashboardViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        var accounts = await _db.ListAccountsAsync();
        var rules = await _db.ListRulesAsync();
        var alerts = await _db.ListAlertsAsync(5);
        var audit = await _db.ListRecentAuditAsync(8);

        AccountsCount = accounts.Count;
        ActiveRulesCount = rules.Count(r => r.Enabled);

        var since = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        decimal totalSpend = 0m;
        foreach (var acct in accounts)
            totalSpend += await _db.SumSpendSinceAsync(acct.AdvertiserId, since);

        TodaySpend = totalSpend > 0 ? $"฿{totalSpend:N0}" : "—";

        var samplesSinceWeek = new List<MetricSample>();
        foreach (var acct in accounts)
        {
            for (int c = 1; c <= 4; c++)
            {
                var s = await _db.ListSamplesAsync($"campaign_{c:D3}", DateTimeOffset.UtcNow.AddDays(-7));
                samplesSinceWeek.AddRange(s);
            }
        }
        if (samplesSinceWeek.Count > 0)
        {
            var imp = samplesSinceWeek.Sum(s => s.Impressions);
            var clk = samplesSinceWeek.Sum(s => s.Clicks);
            var sp = samplesSinceWeek.Sum(s => s.Spend);
            var rev = samplesSinceWeek.Sum(s => s.Revenue);
            AvgCpm = imp > 0 ? $"฿{sp / imp * 1000m:F2}" : "—";
            AvgCpc = clk > 0 ? $"฿{sp / clk:F2}" : "—";
            AvgRoas = sp > 0 ? $"{rev / sp:F2}×" : "—";
        }

        RecentAlerts.Clear();
        foreach (var a in alerts) RecentAlerts.Add(a);

        NextRules.Clear();
        foreach (var r in rules.Where(r => r.Enabled).Take(5)) NextRules.Add(r);

        RecentActivity.Clear();
        foreach (var a in audit) RecentActivity.Add(a);
    }
}
