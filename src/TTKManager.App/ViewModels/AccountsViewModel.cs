using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class AccountsViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly ITikTokApiClient? _api;
    private readonly ITokenProtector? _tokens;

    public ObservableCollection<TikTokAccount> Accounts { get; } = new();

    private TikTokAccount? _selectedAccount;
    public TikTokAccount? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AddDemoAccountCommand { get; }

    public AccountsViewModel(Database db, ITikTokApiClient api, ITokenProtector tokens)
    {
        _db = db;
        _api = api;
        _tokens = tokens;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddDemoAccountCommand = new AsyncRelayCommand(AddDemoAccountAsync);
        _ = RefreshAsync();
    }

    public AccountsViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        AddDemoAccountCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        Accounts.Clear();
        foreach (var a in await _db.ListAccountsAsync())
            Accounts.Add(a);
        StatusMessage = $"{Accounts.Count} account(s) connected";
    }

    private async Task AddDemoAccountAsync()
    {
        if (_db is null || _tokens is null || _api is null) return;
        var pair = await _api.ExchangeAuthCodeAsync("demo_auth_code");
        var account = new TikTokAccount
        {
            AdvertiserId = $"demo_{DateTime.UtcNow:yyyyMMddHHmmss}",
            Name = "Demo Advertiser",
            Currency = "THB",
            Country = "TH",
            EncryptedAccessToken = _tokens.Protect(pair.AccessToken),
            EncryptedRefreshToken = _tokens.Protect(pair.RefreshToken),
            AccessTokenExpiresAt = pair.AccessTokenExpiresAt
        };
        await _db.UpsertAccountAsync(account);
        await RefreshAsync();
    }
}
