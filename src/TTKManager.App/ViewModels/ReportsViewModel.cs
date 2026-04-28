using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class ReportsViewModel : ViewModelBase
{
    private readonly Database? _db;

    public ObservableCollection<TikTokAccount> Accounts { get; } = new();
    public ObservableCollection<ReportRow> Rows { get; } = new();
    public IReadOnlyList<int> WindowOptions { get; } = new[] { 1, 7, 14, 30 };

    private TikTokAccount? _selectedAccount;
    public TikTokAccount? SelectedAccount { get => _selectedAccount; set { if (SetProperty(ref _selectedAccount, value)) _ = LoadAsync(); } }

    private int _windowDays = 7;
    public int WindowDays { get => _windowDays; set { if (SetProperty(ref _windowDays, value)) _ = LoadAsync(); } }

    private string _totalSpend = "—";
    public string TotalSpend { get => _totalSpend; set => SetProperty(ref _totalSpend, value); }

    private string _totalImpressions = "—";
    public string TotalImpressions { get => _totalImpressions; set => SetProperty(ref _totalImpressions, value); }

    private string _avgCpm = "—";
    public string AvgCpm { get => _avgCpm; set => SetProperty(ref _avgCpm, value); }

    private string _avgRoas = "—";
    public string AvgRoas { get => _avgRoas; set => SetProperty(ref _avgRoas, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand RefreshCommand { get; }

    public ReportsViewModel(Database db)
    {
        _db = db;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        _ = InitAsync();
    }

    public ReportsViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task InitAsync()
    {
        if (_db is null) return;
        Accounts.Clear();
        foreach (var a in await _db.ListAccountsAsync()) Accounts.Add(a);
        SelectedAccount ??= Accounts.FirstOrDefault();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_db is null) return;
        Rows.Clear();
        var since = DateTimeOffset.UtcNow.AddDays(-WindowDays);
        var grouped = new Dictionary<string, (decimal spend, long imp, long clk, long conv, decimal rev)>();

        if (SelectedAccount is null)
        {
            foreach (var a in Accounts) await Aggregate(a.AdvertiserId, since, grouped);
        }
        else
        {
            await Aggregate(SelectedAccount.AdvertiserId, since, grouped);
        }

        decimal totalSpend = 0; long totalImp = 0; long totalClk = 0; decimal totalRev = 0;
        foreach (var (cid, vals) in grouped.OrderByDescending(g => g.Value.spend))
        {
            Rows.Add(new ReportRow
            {
                CampaignId = cid,
                Spend = vals.spend,
                Impressions = vals.imp,
                Clicks = vals.clk,
                Conversions = vals.conv,
                Revenue = vals.rev
            });
            totalSpend += vals.spend; totalImp += vals.imp; totalClk += vals.clk; totalRev += vals.rev;
        }

        TotalSpend = $"฿{totalSpend:N0}";
        TotalImpressions = totalImp.ToString("N0");
        AvgCpm = totalImp > 0 ? $"฿{totalSpend / totalImp * 1000m:F2}" : "—";
        AvgRoas = totalSpend > 0 ? $"{totalRev / totalSpend:F2}×" : "—";
        StatusMessage = $"{Rows.Count} campaigns · {WindowDays}-day window";
    }

    private async Task Aggregate(string advertiserId, DateTimeOffset since, Dictionary<string, (decimal, long, long, long, decimal)> grouped)
    {
        if (_db is null) return;
        for (int c = 1; c <= 4; c++)
        {
            var cid = $"campaign_{c:D3}";
            var samples = await _db.ListSamplesAsync(cid, since);
            if (samples.Count == 0) continue;
            grouped[cid] = (
                samples.Sum(s => s.Spend),
                samples.Sum(s => s.Impressions),
                samples.Sum(s => s.Clicks),
                samples.Sum(s => s.Conversions),
                samples.Sum(s => s.Revenue));
        }
    }
}

public class ReportRow
{
    public required string CampaignId { get; init; }
    public decimal Spend { get; init; }
    public long Impressions { get; init; }
    public long Clicks { get; init; }
    public long Conversions { get; init; }
    public decimal Revenue { get; init; }
    public decimal Cpc => Clicks > 0 ? Spend / Clicks : 0;
    public decimal Cpm => Impressions > 0 ? Spend / Impressions * 1000m : 0;
    public decimal Ctr => Impressions > 0 ? (decimal)Clicks / Impressions : 0;
    public decimal Roas => Spend > 0 ? Revenue / Spend : 0;
    public string SpendText => $"฿{Spend:N0}";
    public string CpmText => $"฿{Cpm:F2}";
    public string CpcText => $"฿{Cpc:F2}";
    public string CtrText => $"{Ctr * 100:F2}%";
    public string RoasText => $"{Roas:F2}×";
}
