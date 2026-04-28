using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class ConnectAccountViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly ITikTokApiClient? _api;
    private readonly ITokenProtector? _tokens;

    private string _authorizationUrl = "";
    public string AuthorizationUrl { get => _authorizationUrl; set => SetProperty(ref _authorizationUrl, value); }

    private string _pastedCode = "";
    public string PastedCode { get => _pastedCode; set => SetProperty(ref _pastedCode, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand ExchangeCodeCommand { get; }
    public IRelayCommand OpenAuthorizationUrlCommand { get; }
    public IAsyncRelayCommand CopyUrlCommand { get; }

    public Action<bool>? RequestClose { get; set; }

    public ConnectAccountViewModel(Database db, ITikTokApiClient api, ITokenProtector tokens, TikTokAppCredentials creds)
    {
        _db = db;
        _api = api;
        _tokens = tokens;
        var state = OAuthHelper.GenerateState();
        AuthorizationUrl = OAuthHelper.BuildAuthorizationUrl(creds, state);
        ExchangeCodeCommand = new AsyncRelayCommand(ExchangeCodeAsync);
        OpenAuthorizationUrlCommand = new RelayCommand(OpenAuthorizationUrl);
        CopyUrlCommand = new AsyncRelayCommand(CopyUrlAsync);
    }

    public ConnectAccountViewModel()
    {
        ExchangeCodeCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        OpenAuthorizationUrlCommand = new RelayCommand(() => { });
        CopyUrlCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private void OpenAuthorizationUrl()
    {
        if (string.IsNullOrWhiteSpace(AuthorizationUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AuthorizationUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open browser: {ex.Message}";
        }
    }

    private async Task CopyUrlAsync()
    {
        try
        {
            var clipboard = GetClipboard();
            if (clipboard is null)
            {
                StatusMessage = "Clipboard unavailable";
                return;
            }
            await clipboard.SetTextAsync(AuthorizationUrl);
            StatusMessage = "URL copied to clipboard";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
        }
    }

    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow?.Clipboard;
        return null;
    }

    private async Task ExchangeCodeAsync()
    {
        if (_db is null || _api is null || _tokens is null)
        {
            StatusMessage = "Service container not available";
            return;
        }
        if (string.IsNullOrWhiteSpace(PastedCode))
        {
            StatusMessage = "Paste the auth code first";
            return;
        }
        try
        {
            StatusMessage = "Exchanging code…";
            var pair = await _api.ExchangeAuthCodeAsync(PastedCode.Trim());
            var advertisers = await _api.ListAuthorizedAdvertisersAsync(pair.AccessToken);
            foreach (var a in advertisers)
            {
                var account = new TikTokAccount
                {
                    AdvertiserId = a.AdvertiserId,
                    Name = a.Name,
                    Currency = a.Currency,
                    Country = a.Country,
                    EncryptedAccessToken = _tokens.Protect(pair.AccessToken),
                    EncryptedRefreshToken = _tokens.Protect(pair.RefreshToken),
                    AccessTokenExpiresAt = pair.AccessTokenExpiresAt
                };
                await _db.UpsertAccountAsync(account);
            }
            StatusMessage = $"Connected {advertisers.Count} advertiser(s)";
            PastedCode = "";
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
    }
}
