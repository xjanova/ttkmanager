using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class AutoRulesViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly AutoRulesEngine? _engine;

    public ObservableCollection<AutoRule> Rules { get; } = new();
    public ObservableCollection<TikTokAccount> Accounts { get; } = new();

    public IReadOnlyList<AutoMetric> Metrics { get; } = Enum.GetValues<AutoMetric>().ToList();
    public IReadOnlyList<AutoComparator> Comparators { get; } = Enum.GetValues<AutoComparator>().ToList();
    public IReadOnlyList<AutoAction> Actions { get; } = Enum.GetValues<AutoAction>().ToList();

    private string _newName = "";
    public string NewName { get => _newName; set => SetProperty(ref _newName, value); }

    private TikTokAccount? _newAccount;
    public TikTokAccount? NewAccount { get => _newAccount; set => SetProperty(ref _newAccount, value); }

    private AutoMetric _newMetric = AutoMetric.Cpc;
    public AutoMetric NewMetric { get => _newMetric; set => SetProperty(ref _newMetric, value); }

    private AutoComparator _newComparator = AutoComparator.GreaterThan;
    public AutoComparator NewComparator { get => _newComparator; set => SetProperty(ref _newComparator, value); }

    private decimal _newThreshold = 8m;
    public decimal NewThreshold { get => _newThreshold; set => SetProperty(ref _newThreshold, value); }

    private int _newWindow = 60;
    public int NewWindow { get => _newWindow; set => SetProperty(ref _newWindow, value); }

    private AutoAction _newAction = AutoAction.PauseCampaign;
    public AutoAction NewAction { get => _newAction; set => SetProperty(ref _newAction, value); }

    private decimal _newAmount = 20m;
    public decimal NewAmount { get => _newAmount; set => SetProperty(ref _newAmount, value); }

    private int _newCooldown = 60;
    public int NewCooldown { get => _newCooldown; set => SetProperty(ref _newCooldown, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private AutoRule? _selectedRule;
    public AutoRule? SelectedRule
    {
        get => _selectedRule;
        set
        {
            SetProperty(ref _selectedRule, value);
            DeleteCommand.NotifyCanExecuteChanged();
            ToggleCommand.NotifyCanExecuteChanged();
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AddCommand { get; }
    public IAsyncRelayCommand EvaluateNowCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand ToggleCommand { get; }

    public AutoRulesViewModel(Database db, AutoRulesEngine engine)
    {
        _db = db; _engine = engine;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EvaluateNowCommand = new AsyncRelayCommand(EvaluateAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedRule is not null);
        ToggleCommand = new AsyncRelayCommand(ToggleAsync, () => SelectedRule is not null);
        _ = RefreshAsync();
    }

    public AutoRulesViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        AddCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        EvaluateNowCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        DeleteCommand = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
        ToggleCommand = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        Rules.Clear();
        foreach (var r in await _db.ListAutoRulesAsync()) Rules.Add(r);
        Accounts.Clear();
        foreach (var a in await _db.ListAccountsAsync()) Accounts.Add(a);
        StatusMessage = $"{Rules.Count} rule(s)";
    }

    private async Task AddAsync()
    {
        if (_db is null) return;
        if (string.IsNullOrWhiteSpace(NewName) || NewAccount is null)
        {
            StatusMessage = "Name and account required";
            return;
        }
        var rule = new AutoRule
        {
            Name = NewName.Trim(),
            AdvertiserId = NewAccount.AdvertiserId,
            Metric = NewMetric,
            Comparator = NewComparator,
            Threshold = NewThreshold,
            WindowMinutes = NewWindow,
            Action = NewAction,
            ActionAmount = (NewAction is AutoAction.SetBudget or AutoAction.IncreaseBudgetPercent or AutoAction.DecreaseBudgetPercent) ? NewAmount : null,
            CooldownMinutes = NewCooldown
        };
        await _db.InsertAutoRuleAsync(rule);
        await RefreshAsync();
        NewName = "";
        StatusMessage = "Rule added";
    }

    private async Task EvaluateAsync()
    {
        if (_engine is null) return;
        StatusMessage = "Evaluating…";
        var n = await _engine.EvaluateAllAsync();
        StatusMessage = $"Evaluated · {n} rule(s) fired";
        await RefreshAsync();
    }

    private async Task DeleteAsync()
    {
        if (_db is null || SelectedRule is null) return;
        await _db.DeleteAutoRuleAsync(SelectedRule.Id);
        SelectedRule = null;
        await RefreshAsync();
    }

    private async Task ToggleAsync()
    {
        if (_db is null || SelectedRule is null) return;
        await _db.UpdateAutoRuleEnabledAsync(SelectedRule.Id, !SelectedRule.Enabled);
        await RefreshAsync();
    }
}
