using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class SchedulesViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly SchedulerService? _scheduler;
    private readonly ITikTokApiClient? _api;
    private readonly ITokenProtector? _tokens;

    public ObservableCollection<ScheduleRule> Rules { get; } = new();
    public ObservableCollection<TikTokAccount> Accounts { get; } = new();
    public ObservableCollection<Campaign> Campaigns { get; } = new();

    public IReadOnlyList<RuleAction> AvailableActions { get; } =
        Enum.GetValues<RuleAction>().ToList();

    public IReadOnlyList<CronPreset> CronPresets { get; } = new[]
    {
        new CronPreset("Every day at 18:00 (peak hours start)", "0 0 18 * * ?"),
        new CronPreset("Every day at 22:00 (peak hours end)", "0 0 22 * * ?"),
        new CronPreset("Every day at 09:00", "0 0 9 * * ?"),
        new CronPreset("Weekdays at 09:00", "0 0 9 ? * MON-FRI"),
        new CronPreset("Weekdays at 18:00", "0 0 18 ? * MON-FRI"),
        new CronPreset("Weekends at 12:00", "0 0 12 ? * SAT,SUN"),
        new CronPreset("Every hour on the hour", "0 0 * * * ?"),
        new CronPreset("Every 15 minutes (testing)", "0 0/15 * * * ?"),
        new CronPreset("Custom — edit cron manually", "")
    };

    private string _newRuleName = "";
    public string NewRuleName { get => _newRuleName; set => SetProperty(ref _newRuleName, value); }

    private TikTokAccount? _selectedAccount;
    public TikTokAccount? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
                _ = LoadCampaignsAsync();
        }
    }

    private Campaign? _selectedCampaign;
    public Campaign? SelectedCampaign { get => _selectedCampaign; set => SetProperty(ref _selectedCampaign, value); }

    private CronPreset? _selectedCronPreset;
    public CronPreset? SelectedCronPreset
    {
        get => _selectedCronPreset;
        set
        {
            if (SetProperty(ref _selectedCronPreset, value) && value is not null && !string.IsNullOrEmpty(value.Cron))
                NewRuleCron = value.Cron;
        }
    }

    private string _newRuleCron = "0 0 18 * * ?";
    public string NewRuleCron { get => _newRuleCron; set => SetProperty(ref _newRuleCron, value); }

    private decimal _newRuleBudget = 1000m;
    public decimal NewRuleBudget { get => _newRuleBudget; set => SetProperty(ref _newRuleBudget, value); }

    private RuleAction _newRuleAction = RuleAction.SetDailyBudget;
    public RuleAction NewRuleAction { get => _newRuleAction; set => SetProperty(ref _newRuleAction, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private ScheduleRule? _selectedRule;
    public ScheduleRule? SelectedRule
    {
        get => _selectedRule;
        set
        {
            SetProperty(ref _selectedRule, value);
            DeleteRuleCommand.NotifyCanExecuteChanged();
            ToggleRuleCommand.NotifyCanExecuteChanged();
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AddRuleCommand { get; }
    public IAsyncRelayCommand DeleteRuleCommand { get; }
    public IAsyncRelayCommand ToggleRuleCommand { get; }
    public IAsyncRelayCommand LoadCampaignsCommand { get; }

    public SchedulesViewModel(Database db, SchedulerService scheduler, ITikTokApiClient api, ITokenProtector tokens)
    {
        _db = db;
        _scheduler = scheduler;
        _api = api;
        _tokens = tokens;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddRuleCommand = new AsyncRelayCommand(AddRuleAsync);
        DeleteRuleCommand = new AsyncRelayCommand(DeleteRuleAsync, () => SelectedRule is not null);
        ToggleRuleCommand = new AsyncRelayCommand(ToggleRuleAsync, () => SelectedRule is not null);
        LoadCampaignsCommand = new AsyncRelayCommand(LoadCampaignsAsync);
        _ = RefreshAsync();
    }

    public SchedulesViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        AddRuleCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        DeleteRuleCommand = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
        ToggleRuleCommand = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
        LoadCampaignsCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        Rules.Clear();
        foreach (var r in await _db.ListRulesAsync())
            Rules.Add(r);
        Accounts.Clear();
        foreach (var a in await _db.ListAccountsAsync())
            Accounts.Add(a);
        StatusMessage = $"{Rules.Count} rule(s), {Accounts.Count} account(s)";
    }

    private async Task LoadCampaignsAsync()
    {
        if (_api is null || _tokens is null || SelectedAccount is null) return;
        try
        {
            var token = _tokens.Unprotect(SelectedAccount.EncryptedAccessToken);
            var campaigns = await _api.GetCampaignsAsync(SelectedAccount.AdvertiserId, token);
            Campaigns.Clear();
            foreach (var c in campaigns)
                Campaigns.Add(c);
            StatusMessage = $"{Campaigns.Count} campaign(s) loaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load campaigns failed: {ex.Message}";
        }
    }

    private async Task AddRuleAsync()
    {
        if (_db is null || _scheduler is null) return;
        if (string.IsNullOrWhiteSpace(NewRuleName) || SelectedAccount is null || SelectedCampaign is null)
        {
            StatusMessage = "Name, account, and campaign are required";
            return;
        }
        var rule = new ScheduleRule
        {
            AdvertiserId = SelectedAccount.AdvertiserId,
            CampaignId = SelectedCampaign.CampaignId,
            Name = NewRuleName.Trim(),
            Action = NewRuleAction,
            BudgetAmount = NewRuleAction == RuleAction.SetDailyBudget ? NewRuleBudget : null,
            CronExpression = NewRuleCron.Trim()
        };
        try
        {
            var id = await _db.InsertRuleAsync(rule);
            var saved = (await _db.ListRulesAsync()).First(r => r.Id == id);
            await _scheduler.ScheduleRuleAsync(saved);
            await RefreshAsync();
            NewRuleName = "";
            StatusMessage = $"Added rule #{id}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Add rule failed: {ex.Message}";
        }
    }

    private async Task DeleteRuleAsync()
    {
        if (_db is null || _scheduler is null || SelectedRule is null) return;
        await _scheduler.UnscheduleRuleAsync(SelectedRule.Id);
        await _db.DeleteRuleAsync(SelectedRule.Id);
        SelectedRule = null;
        await RefreshAsync();
    }

    private async Task ToggleRuleAsync()
    {
        if (_db is null || _scheduler is null || SelectedRule is null) return;
        var newState = !SelectedRule.Enabled;
        await _db.UpdateRuleEnabledAsync(SelectedRule.Id, newState);
        if (newState)
        {
            var rule = (await _db.ListRulesAsync()).First(r => r.Id == SelectedRule.Id);
            await _scheduler.ScheduleRuleAsync(rule);
        }
        else
        {
            await _scheduler.UnscheduleRuleAsync(SelectedRule.Id);
        }
        await RefreshAsync();
    }
}

public sealed record CronPreset(string Label, string Cron)
{
    public override string ToString() => Label;
}
