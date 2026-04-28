using System.Text.Json;
using CommunityToolkit.Mvvm.Input;

namespace TTKManager.App.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings? _settings;
    private readonly string? _settingsPath;

    private string _appId = "";
    public string TikTokAppId { get => _appId; set => SetProperty(ref _appId, value); }

    private string _appSecret = "";
    public string TikTokAppSecret { get => _appSecret; set => SetProperty(ref _appSecret, value); }

    private string _redirectUri = "";
    public string RedirectUri { get => _redirectUri; set => SetProperty(ref _redirectUri, value); }

    private string _databasePath = "";
    public string DatabasePath { get => _databasePath; set => SetProperty(ref _databasePath, value); }

    private bool _useMockApi = true;
    public bool UseMockApi { get => _useMockApi; set => SetProperty(ref _useMockApi, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IRelayCommand SaveCommand { get; }

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        _settingsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
        TikTokAppId = settings.TikTokAppId;
        TikTokAppSecret = settings.TikTokAppSecret;
        RedirectUri = settings.RedirectUri;
        DatabasePath = settings.DatabasePath;
        UseMockApi = settings.UseMockApi;
        SaveCommand = new RelayCommand(Save);
    }

    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(() => { });
    }

    private void Save()
    {
        if (_settingsPath is null) return;
        try
        {
            var data = new
            {
                DatabasePath,
                TikTokAppId,
                TikTokAppSecret,
                RedirectUri,
                UseMockApi
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_settingsPath, json);
            StatusMessage = $"Saved · restart app to apply";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }
}
