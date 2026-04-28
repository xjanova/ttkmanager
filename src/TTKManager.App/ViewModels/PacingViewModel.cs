using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class PacingViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly BudgetPacer? _pacer;

    public ObservableCollection<PacingRow> Caps { get; } = new();
    public ObservableCollection<TikTokAccount> Accounts { get; } = new();
    public IReadOnlyList<CapPeriod> Periods { get; } = Enum.GetValues<CapPeriod>().ToList();

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private TikTokAccount? _newAccount;
    public TikTokAccount? NewAccount { get => _newAccount; set => SetProperty(ref _newAccount, value); }

    private CapPeriod _newPeriod = CapPeriod.Daily;
    public CapPeriod NewPeriod { get => _newPeriod; set => SetProperty(ref _newPeriod, value); }

    private decimal _newCap = 5000m;
    public decimal NewCap { get => _newCap; set => SetProperty(ref _newCap, value); }

    private bool _newAutoPause = true;
    public bool NewAutoPause { get => _newAutoPause; set => SetProperty(ref _newAutoPause, value); }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AddCapCommand { get; }
    public IAsyncRelayCommand CheckNowCommand { get; }
    public IAsyncRelayCommand<long?> DeleteCapCommand { get; }

    public PacingViewModel(Database db, BudgetPacer pacer)
    {
        _db = db; _pacer = pacer;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddCapCommand = new AsyncRelayCommand(AddCapAsync);
        CheckNowCommand = new AsyncRelayCommand(CheckNowAsync);
        DeleteCapCommand = new AsyncRelayCommand<long?>(DeleteCapAsync);
        _ = RefreshAsync();
    }

    public PacingViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        AddCapCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        CheckNowCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        DeleteCapCommand = new AsyncRelayCommand<long?>(_ => Task.CompletedTask);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        Caps.Clear();
        Accounts.Clear();
        foreach (var a in await _db.ListAccountsAsync()) Accounts.Add(a);
        var caps = await _db.ListBudgetCapsAsync();
        foreach (var cap in caps)
        {
            var since = StartOfPeriod(cap.Period);
            var spent = await _db.SumSpendSinceAsync(cap.AdvertiserId, since, cap.CampaignIdScope);
            Caps.Add(new PacingRow(cap, spent));
        }
        StatusMessage = $"{Caps.Count} cap(s)";
    }

    private async Task AddCapAsync()
    {
        if (_db is null || NewAccount is null) { StatusMessage = "Pick an account"; return; }
        await _db.InsertBudgetCapAsync(new BudgetCap
        {
            AdvertiserId = NewAccount.AdvertiserId,
            Period = NewPeriod,
            CapAmount = NewCap,
            Currency = NewAccount.Currency ?? "THB",
            AutoPauseOnCap = NewAutoPause
        });
        await RefreshAsync();
    }

    private async Task CheckNowAsync()
    {
        if (_pacer is null) return;
        var paused = await _pacer.CheckCapsAsync();
        StatusMessage = paused == 0 ? "All caps within bounds" : $"Auto-paused {paused} campaign(s)";
        await RefreshAsync();
    }

    private async Task DeleteCapAsync(long? id)
    {
        if (_db is null || !id.HasValue) return;
        await _db.DeleteBudgetCapAsync(id.Value);
        await RefreshAsync();
    }

    private static DateTimeOffset StartOfPeriod(CapPeriod period)
    {
        var now = DateTimeOffset.UtcNow;
        return period switch
        {
            CapPeriod.Daily => new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero),
            CapPeriod.Weekly => now.AddDays(-(int)now.DayOfWeek),
            CapPeriod.Monthly => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero),
            _ => now.AddDays(-1)
        };
    }
}

public class PacingRow : ViewModelBase
{
    public BudgetCap Cap { get; }
    public decimal Spent { get; }
    public string Description => $"{Cap.AdvertiserId} · {(Cap.CampaignIdScope ?? "all campaigns")} · {Cap.Period}";
    public string CapText => $"{Cap.CapAmount:N0} {Cap.Currency}";
    public string SpentText => $"{Spent:N0} {Cap.Currency}";
    public double FillPercent => (double)(Cap.CapAmount > 0 ? Math.Min(1m, Spent / Cap.CapAmount) : 0m) * 100;
    public string Status =>
        Spent >= Cap.CapAmount ? "OVER CAP" :
        Spent >= Cap.CapAmount * 0.9m ? "near cap" :
        "ok";
    public PacingRow(BudgetCap cap, decimal spent) { Cap = cap; Spent = spent; }
}
