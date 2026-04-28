using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class CampaignsViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly ITikTokApiClient? _api;
    private readonly ITokenProtector? _tokens;

    public ObservableCollection<TikTokAccount> Accounts { get; } = new();
    public ObservableCollection<Campaign> Campaigns { get; } = new();

    private TikTokAccount? _selectedAccount;
    public TikTokAccount? SelectedAccount { get => _selectedAccount; set { if (SetProperty(ref _selectedAccount, value)) _ = LoadAsync(); } }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand RefreshCommand { get; }

    public CampaignsViewModel(Database db, ITikTokApiClient api, ITokenProtector tokens)
    {
        _db = db; _api = api; _tokens = tokens;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        _ = InitAsync();
    }

    public CampaignsViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task InitAsync()
    {
        if (_db is null) return;
        Accounts.Clear();
        foreach (var a in await _db.ListAccountsAsync()) Accounts.Add(a);
        SelectedAccount ??= Accounts.FirstOrDefault();
    }

    private async Task LoadAsync()
    {
        if (SelectedAccount is null || _api is null || _tokens is null) return;
        try
        {
            var token = _tokens.Unprotect(SelectedAccount.EncryptedAccessToken);
            var list = await _api.GetCampaignsAsync(SelectedAccount.AdvertiserId, token);
            Campaigns.Clear();
            foreach (var c in list) Campaigns.Add(c);
            StatusMessage = $"{Campaigns.Count} campaign(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }
}
