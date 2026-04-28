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

    public ConnectAccountViewModel(Database db, ITikTokApiClient api, ITokenProtector tokens, TikTokAppCredentials creds)
    {
        _db = db;
        _api = api;
        _tokens = tokens;
        var state = OAuthHelper.GenerateState();
        AuthorizationUrl = OAuthHelper.BuildAuthorizationUrl(creds, state);
        ExchangeCodeCommand = new AsyncRelayCommand(ExchangeCodeAsync);
    }

    public ConnectAccountViewModel()
    {
        ExchangeCodeCommand = new AsyncRelayCommand(() => Task.CompletedTask);
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
    }
}
