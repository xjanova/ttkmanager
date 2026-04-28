using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class SchedulesViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly SchedulerService? _scheduler;

    public ObservableCollection<ScheduleRule> Rules { get; } = new();

    private string _newRuleName = "";
    public string NewRuleName { get => _newRuleName; set => SetProperty(ref _newRuleName, value); }

    private string _newRuleAdvertiserId = "";
    public string NewRuleAdvertiserId { get => _newRuleAdvertiserId; set => SetProperty(ref _newRuleAdvertiserId, value); }

    private string _newRuleCampaignId = "";
    public string NewRuleCampaignId { get => _newRuleCampaignId; set => SetProperty(ref _newRuleCampaignId, value); }

    private string _newRuleCron = "0 0 18 * * ? *";
    public string NewRuleCron { get => _newRuleCron; set => SetProperty(ref _newRuleCron, value); }

    private decimal _newRuleBudget = 1000m;
    public decimal NewRuleBudget { get => _newRuleBudget; set => SetProperty(ref _newRuleBudget, value); }

    private RuleAction _newRuleAction = RuleAction.SetDailyBudget;
    public RuleAction NewRuleAction { get => _newRuleAction; set => SetProperty(ref _newRuleAction, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IReadOnlyList<RuleAction> AvailableActions { get; } =
        Enum.GetValues<RuleAction>().ToList();

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AddRuleCommand { get; }

    public SchedulesViewModel(Database db, SchedulerService scheduler)
    {
        _db = db;
        _scheduler = scheduler;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddRuleCommand = new AsyncRelayCommand(AddRuleAsync);
        _ = RefreshAsync();
    }

    public SchedulesViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        AddRuleCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        Rules.Clear();
        foreach (var r in await _db.ListRulesAsync())
            Rules.Add(r);
        StatusMessage = $"{Rules.Count} rule(s)";
    }

    private async Task AddRuleAsync()
    {
        if (_db is null || _scheduler is null) return;
        if (string.IsNullOrWhiteSpace(NewRuleName) || string.IsNullOrWhiteSpace(NewRuleAdvertiserId) || string.IsNullOrWhiteSpace(NewRuleCampaignId))
        {
            StatusMessage = "Name, advertiser, and campaign are required";
            return;
        }
        var rule = new ScheduleRule
        {
            AdvertiserId = NewRuleAdvertiserId.Trim(),
            CampaignId = NewRuleCampaignId.Trim(),
            Name = NewRuleName.Trim(),
            Action = NewRuleAction,
            BudgetAmount = NewRuleAction == RuleAction.SetDailyBudget ? NewRuleBudget : null,
            CronExpression = NewRuleCron.Trim()
        };
        var id = await _db.InsertRuleAsync(rule);
        var saved = (await _db.ListRulesAsync()).First(r => r.Id == id);
        await _scheduler.ScheduleRuleAsync(saved);
        await RefreshAsync();
        StatusMessage = $"Added rule #{id}";
    }
}
