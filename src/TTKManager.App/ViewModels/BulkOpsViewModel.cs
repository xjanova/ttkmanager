using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class BulkOpsViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly ITikTokApiClient? _api;
    private readonly ITokenProtector? _tokens;

    public ObservableCollection<TikTokAccount> Accounts { get; } = new();
    public ObservableCollection<CampaignRow> Campaigns { get; } = new();

    private TikTokAccount? _selectedAccount;
    public TikTokAccount? SelectedAccount
    {
        get => _selectedAccount;
        set { if (SetProperty(ref _selectedAccount, value)) _ = LoadCampaignsAsync(); }
    }

    private decimal _adjustValue = 1000m;
    public decimal AdjustValue { get => _adjustValue; set => SetProperty(ref _adjustValue, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SelectAllCommand { get; }
    public IAsyncRelayCommand BulkPauseCommand { get; }
    public IAsyncRelayCommand BulkEnableCommand { get; }
    public IAsyncRelayCommand BulkSetBudgetCommand { get; }

    public BulkOpsViewModel(Database db, ITikTokApiClient api, ITokenProtector tokens)
    {
        _db = db; _api = api; _tokens = tokens;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SelectAllCommand = new AsyncRelayCommand(SelectAllAsync);
        BulkPauseCommand = new AsyncRelayCommand(() => RunOnSelectedAsync(CampaignStatus.Disable));
        BulkEnableCommand = new AsyncRelayCommand(() => RunOnSelectedAsync(CampaignStatus.Enable));
        BulkSetBudgetCommand = new AsyncRelayCommand(BulkSetBudgetAsync);
        _ = RefreshAsync();
    }

    public BulkOpsViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        SelectAllCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        BulkPauseCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        BulkEnableCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        BulkSetBudgetCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        Accounts.Clear();
        foreach (var a in await _db.ListAccountsAsync()) Accounts.Add(a);
        if (SelectedAccount is null && Accounts.Count > 0) SelectedAccount = Accounts[0];
    }

    private Task SelectAllAsync()
    {
        foreach (var c in Campaigns) c.IsSelected = true;
        StatusMessage = $"{Campaigns.Count} selected";
        return Task.CompletedTask;
    }

    private async Task LoadCampaignsAsync()
    {
        if (SelectedAccount is null || _api is null || _tokens is null) return;
        try
        {
            var token = _tokens.Unprotect(SelectedAccount.EncryptedAccessToken);
            var list = await _api.GetCampaignsAsync(SelectedAccount.AdvertiserId, token);
            Campaigns.Clear();
            foreach (var c in list) Campaigns.Add(new CampaignRow(c));
            StatusMessage = $"{Campaigns.Count} campaigns loaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }

    private async Task RunOnSelectedAsync(CampaignStatus status)
    {
        if (SelectedAccount is null || _api is null || _tokens is null) return;
        var selected = Campaigns.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0) { StatusMessage = "Select at least one campaign"; return; }
        var token = _tokens.Unprotect(SelectedAccount.EncryptedAccessToken);
        var ok = 0; var failed = 0;
        foreach (var row in selected)
        {
            try { await _api.UpdateCampaignStatusAsync(SelectedAccount.AdvertiserId, row.Inner.CampaignId, status, token); ok++; }
            catch { failed++; }
        }
        StatusMessage = $"{status} → {ok} ok · {failed} failed";
        await LoadCampaignsAsync();
    }

    private async Task BulkSetBudgetAsync()
    {
        if (SelectedAccount is null || _api is null || _tokens is null) return;
        var selected = Campaigns.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0) { StatusMessage = "Select at least one campaign"; return; }
        var token = _tokens.Unprotect(SelectedAccount.EncryptedAccessToken);
        var ok = 0; var failed = 0;
        foreach (var row in selected)
        {
            try { await _api.UpdateCampaignBudgetAsync(SelectedAccount.AdvertiserId, row.Inner.CampaignId, AdjustValue, token); ok++; }
            catch { failed++; }
        }
        StatusMessage = $"SetBudget {AdjustValue} → {ok} ok · {failed} failed";
        await LoadCampaignsAsync();
    }
}

public class CampaignRow : ViewModelBase
{
    public Campaign Inner { get; }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string CampaignId => Inner.CampaignId;
    public string Name => Inner.Name;
    public CampaignStatus Status => Inner.Status;
    public decimal Budget => Inner.Budget;
    public CampaignRow(Campaign c) { Inner = c; }
}
