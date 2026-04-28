using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class HeatmapViewModel : ViewModelBase
{
    private readonly Database? _db;

    public ObservableCollection<TikTokAccount> Accounts { get; } = new();
    public ObservableCollection<HeatmapCell> Cells { get; } = new();
    public IReadOnlyList<string> Metrics { get; } = new[] { "Roas", "Cpm", "Ctr", "Conversions" };

    private TikTokAccount? _selectedAccount;
    public TikTokAccount? SelectedAccount { get => _selectedAccount; set { if (SetProperty(ref _selectedAccount, value)) _ = LoadAsync(); } }

    private string _metric = "Roas";
    public string Metric { get => _metric; set { if (SetProperty(ref _metric, value)) _ = LoadAsync(); } }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand RefreshCommand { get; }

    public HeatmapViewModel(Database db)
    {
        _db = db;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        _ = InitAsync();
    }

    public HeatmapViewModel()
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
        if (_db is null || SelectedAccount is null) return;
        var since = DateTimeOffset.UtcNow.AddDays(-30);
        var totals = new Dictionary<(int day, int hour), (decimal spend, long imp, long clk, long conv, decimal rev)>();
        for (int c = 1; c <= 4; c++)
        {
            var samples = await _db.ListSamplesAsync($"campaign_{c:D3}", since);
            foreach (var s in samples)
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(s.Timestamp.UtcDateTime, TryTz("Asia/Bangkok"));
                var key = ((int)local.DayOfWeek, local.Hour);
                if (!totals.TryGetValue(key, out var t)) t = (0, 0, 0, 0, 0);
                totals[key] = (t.spend + s.Spend, t.imp + s.Impressions, t.clk + s.Clicks, t.conv + s.Conversions, t.rev + s.Revenue);
            }
        }

        Cells.Clear();
        var values = new double[7, 24];
        for (int d = 0; d < 7; d++)
            for (int h = 0; h < 24; h++)
            {
                if (totals.TryGetValue((d, h), out var v))
                {
                    values[d, h] = Metric switch
                    {
                        "Roas" => v.spend > 0 ? (double)(v.rev / v.spend) : 0,
                        "Cpm" => v.imp > 0 ? (double)(v.spend / v.imp * 1000m) : 0,
                        "Ctr" => v.imp > 0 ? (double)v.clk / v.imp : 0,
                        "Conversions" => v.conv,
                        _ => 0
                    };
                }
            }

        var max = 0.0001;
        for (int d = 0; d < 7; d++) for (int h = 0; h < 24; h++) if (values[d, h] > max) max = values[d, h];

        var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        for (int d = 0; d < 7; d++)
            for (int h = 0; h < 24; h++)
            {
                var pct = values[d, h] / max;
                Cells.Add(new HeatmapCell
                {
                    Day = dayNames[d],
                    Hour = h,
                    Value = values[d, h],
                    Intensity = pct,
                    DayIndex = d
                });
            }
        StatusMessage = $"30-day {Metric} heatmap · max={max:F2}";
    }

    private static TimeZoneInfo TryTz(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Local; }
    }
}

public class HeatmapCell
{
    public required string Day { get; init; }
    public int Hour { get; init; }
    public double Value { get; init; }
    public double Intensity { get; init; }
    public int DayIndex { get; init; }
    public string Display => Value > 0 ? Value.ToString(Value < 10 ? "F2" : "F0") : "";
}
