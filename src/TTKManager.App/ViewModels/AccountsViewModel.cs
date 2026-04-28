using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TTKManager.App.Models;
using TTKManager.App.Services;
using TTKManager.App.Views;

namespace TTKManager.App.ViewModels;

public class AccountsViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly ITikTokApiClient? _api;
    private readonly ITokenProtector? _tokens;
    private readonly IServiceProvider? _services;

    public ObservableCollection<TikTokAccount> Accounts { get; } = new();

    private TikTokAccount? _selectedAccount;
    public TikTokAccount? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            SetProperty(ref _selectedAccount, value);
            RemoveAccountCommand.NotifyCanExecuteChanged();
        }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ConnectAccountCommand { get; }
    public IAsyncRelayCommand RemoveAccountCommand { get; }

    public AccountsViewModel(Database db, ITikTokApiClient api, ITokenProtector tokens, IServiceProvider services)
    {
        _db = db;
        _api = api;
        _tokens = tokens;
        _services = services;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ConnectAccountCommand = new AsyncRelayCommand(ConnectAccountAsync);
        RemoveAccountCommand = new AsyncRelayCommand(RemoveAccountAsync, () => SelectedAccount is not null);
        _ = RefreshAsync();
    }

    public AccountsViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        ConnectAccountCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        RemoveAccountCommand = new AsyncRelayCommand(() => Task.CompletedTask, () => false);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        Accounts.Clear();
        foreach (var a in await _db.ListAccountsAsync())
            Accounts.Add(a);
        StatusMessage = $"{Accounts.Count} account(s) connected";
    }

    private async Task ConnectAccountAsync()
    {
        if (_services is null) return;
        var vm = _services.GetRequiredService<ConnectAccountViewModel>();
        var window = new ConnectAccountWindow(vm);
        var owner = GetMainWindow();
        bool? result = null;
        if (owner is not null)
            result = await window.ShowDialog<bool?>(owner);
        else
            window.Show();
        if (result == true)
            await RefreshAsync();
    }

    private async Task RemoveAccountAsync()
    {
        if (_db is null || SelectedAccount is null) return;
        await _db.DeleteAccountAsync(SelectedAccount.AdvertiserId);
        SelectedAccount = null;
        await RefreshAsync();
    }

    private static Avalonia.Controls.Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
