using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TTKManager.App.Services;
using TTKManager.App.Services.Jobs;
using TTKManager.App.ViewModels;

namespace TTKManager.App;

public static class Bootstrapper
{
    public static IServiceProvider Build()
    {
        var portableFolder = AppContext.BaseDirectory;
        var settings = AppSettings.LoadOrDefault(portableFolder);
        var dbPath = Path.IsPathRooted(settings.DatabasePath)
            ? settings.DatabasePath
            : Path.Combine(portableFolder, settings.DatabasePath);

        var services = new ServiceCollection();

        services.AddLogging(b => b.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.IncludeScopes = false;
            o.TimestampFormat = "HH:mm:ss ";
        }).SetMinimumLevel(LogLevel.Information));

        services.AddSingleton(settings);
        services.AddSingleton(new TikTokAppCredentials(
            AppId: settings.TikTokAppId,
            Secret: settings.TikTokAppSecret,
            RedirectUri: settings.RedirectUri));

        services.AddSingleton(_ => new Database(dbPath));

        if (OperatingSystem.IsWindows())
            services.AddSingleton<ITokenProtector, WindowsTokenProtector>();
        else
            services.AddSingleton<ITokenProtector, NoOpTokenProtector>();

        if (settings.UseMockApi || string.IsNullOrEmpty(settings.TikTokAppId))
        {
            services.AddSingleton<ITikTokApiClient, MockTikTokApiClient>();
        }
        else
        {
            services.AddHttpClient<ITikTokApiClient, TikTokApiClient>();
        }

        services.AddTransient<CampaignActionJob>();
        services.AddSingleton<SchedulerService>();

        services.AddSingleton<AutoRulesEngine>();
        services.AddSingleton<AnomalyDetector>();
        services.AddSingleton<BudgetPacer>();
        services.AddSingleton<CsvService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<HealthCheckService>();
        services.AddSingleton<MockSamplerService>();

        services.AddTransient<ShellViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<AccountsViewModel>();
        services.AddTransient<CampaignsViewModel>();
        services.AddTransient<SchedulesViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<HeatmapViewModel>();
        services.AddTransient<AutoRulesViewModel>();
        services.AddTransient<BulkOpsViewModel>();
        services.AddTransient<CsvViewModel>();
        services.AddTransient<AnomalyViewModel>();
        services.AddTransient<PacingViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<HealthViewModel>();
        services.AddTransient<ShortcutsViewModel>();
        services.AddTransient<ConnectAccountViewModel>();

        return services.BuildServiceProvider();
    }
}

internal sealed class NoOpTokenProtector : ITokenProtector
{
    public byte[] Protect(string plaintext) => System.Text.Encoding.UTF8.GetBytes(plaintext);
    public string Unprotect(byte[] ciphertext) => System.Text.Encoding.UTF8.GetString(ciphertext);
}
