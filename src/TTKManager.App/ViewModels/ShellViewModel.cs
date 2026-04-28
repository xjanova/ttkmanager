using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TTKManager.App.Services;
using TTKManager.App.Views;

namespace TTKManager.App.ViewModels;

public sealed record NavSection(string Group, string Title, string Emoji, string Tag, string Phase);

public class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly DemoModeService? _demo;
    private readonly XmanLicenseService? _license;

    public ObservableCollection<NavSection> Items { get; } = new();

    private NavSection? _selected;
    public NavSection? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                Current = ResolveView(value?.Tag ?? "dashboard");
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    private object? _current;
    public object? Current { get => _current; set => SetProperty(ref _current, value); }

    public string Title => Selected is null ? "TTK Manager" : $"TTK Manager — {Selected.Title}";

    private bool _isDemoActive;
    public bool IsDemoActive
    {
        get => _isDemoActive;
        set
        {
            if (SetProperty(ref _isDemoActive, value))
            {
                OnPropertyChanged(nameof(DemoStatusLabel));
                OnPropertyChanged(nameof(DemoButtonLabel));
                OnPropertyChanged(nameof(StatusPillEmoji));
            }
        }
    }

    public string DemoStatusLabel => IsDemoActive ? "DEMO MODE — sample data shown" :
        (_license?.CachedStatus.IsActive == true
            ? $"Live · {_license.CachedStatus.DisplayType}"
            : "Live · Free / unlicensed");

    public string DemoButtonLabel => IsDemoActive ? "▶ Switch to Live" : "▶ Try demo data";

    public string LicenseLabel
    {
        get
        {
            var s = _license?.CachedStatus;
            return s?.IsActive == true ? $"🔑 {s.DisplayType}" : "🔑 Activate license";
        }
    }
    public string StatusPillEmoji => IsDemoActive ? "🟡" : (_license?.CachedStatus.IsActive == true ? "🟢" : "⚪");

    public IRelayCommand<string> NavigateCommand { get; }
    public IAsyncRelayCommand ToggleDemoCommand { get; }
    public IAsyncRelayCommand OpenLicenseCommand { get; }

    public ShellViewModel(IServiceProvider services, DemoModeService demo, XmanLicenseService license)
    {
        _services = services;
        _demo = demo;
        _license = license;
        NavigateCommand = new RelayCommand<string>(tag =>
        {
            var item = Items.FirstOrDefault(i => i.Tag == tag);
            if (item is not null) Selected = item;
        });
        ToggleDemoCommand = new AsyncRelayCommand(ToggleDemoAsync);
        OpenLicenseCommand = new AsyncRelayCommand(OpenLicenseAsync);
        license.StateChanged += _ =>
        {
            OnPropertyChanged(nameof(DemoStatusLabel));
            OnPropertyChanged(nameof(LicenseLabel));
            OnPropertyChanged(nameof(StatusPillEmoji));
        };
        BuildNav();
        _ = InitAsync();
    }

    public ShellViewModel()
    {
        _services = null!;
        NavigateCommand = new RelayCommand<string>(_ => { });
        ToggleDemoCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        OpenLicenseCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        BuildNav();
    }

    private async Task InitAsync()
    {
        if (_license is not null) await _license.LoadCachedAsync();
        if (_demo is not null) IsDemoActive = await _demo.IsActiveAsync();
        OnPropertyChanged(nameof(LicenseLabel));
        Selected = Items.FirstOrDefault();
    }

    private async Task ToggleDemoAsync()
    {
        if (_demo is null) return;

        if (IsDemoActive)
        {
            // Switching to LIVE — gate behind a license unless user dismisses
            if (_license is not null && !_license.CachedStatus.IsActive)
            {
                var owner = GetMainWindow();
                if (owner is not null && _services is not null)
                {
                    var vm = _services.GetRequiredService<LicenseViewModel>();
                    var dlg = new LicenseDialog(vm);
                    var result = await dlg.ShowDialog<bool?>(owner);
                    if (result != true && !_license.CachedStatus.IsActive)
                    {
                        // user declined to activate — stay in demo mode
                        return;
                    }
                }
            }

            // proceed: clear demo data
            await ShowProgressWhile(_demo.DisableAsync, "Removing demo data");
            IsDemoActive = await _demo.IsActiveAsync();
        }
        else
        {
            await ShowProgressWhile(_demo.EnableAsync, "Building demo data");
            IsDemoActive = await _demo.IsActiveAsync();
        }

        OnPropertyChanged(nameof(DemoStatusLabel));
        // Force the current view to rebuild against new data
        var current = Selected;
        Selected = null;
        Selected = current;
    }

    private async Task ShowProgressWhile(Func<Task> action, string title)
    {
        if (_demo is null) { await action(); return; }
        var owner = GetMainWindow();
        if (owner is null) { await action(); return; }

        var vm = new DemoProgressViewModel { Title = title };
        var dlg = new DemoProgressDialog { DataContext = vm };

        void Handler(DemoProgress p) => Dispatcher.UIThread.Post(() => vm.Apply(p));
        _demo.ProgressChanged += Handler;

        var task = Task.Run(action);
        var showDlg = dlg.ShowDialog(owner);
        try
        {
            await task;
        }
        finally
        {
            _demo.ProgressChanged -= Handler;
            await Dispatcher.UIThread.InvokeAsync(() => dlg.Close());
        }
    }

    private async Task OpenLicenseAsync()
    {
        if (_services is null || _license is null) return;
        var owner = GetMainWindow();
        if (owner is null) return;
        var vm = _services.GetRequiredService<LicenseViewModel>();
        var dlg = new LicenseDialog(vm);
        await dlg.ShowDialog(owner);
        OnPropertyChanged(nameof(LicenseLabel));
        OnPropertyChanged(nameof(DemoStatusLabel));
    }

    private static Avalonia.Controls.Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private void BuildNav()
    {
        Items.Clear();
        Items.Add(new NavSection("Foundation", "Dashboard", "📊", "dashboard", "P1"));
        Items.Add(new NavSection("Foundation", "Accounts", "🔑", "accounts", "P1"));
        Items.Add(new NavSection("Foundation", "Campaigns", "📦", "campaigns", "P1"));
        Items.Add(new NavSection("Foundation", "Schedules", "⏰", "schedules", "P1"));
        Items.Add(new NavSection("Foundation", "Activity Log", "📋", "logs", "P1"));
        Items.Add(new NavSection("Pro", "Performance Reports", "📈", "reports", "P2"));
        Items.Add(new NavSection("Pro", "Dayparting Heatmap", "🔥", "heatmap", "P2"));
        Items.Add(new NavSection("Pro", "Auto-Rules Engine", "🤖", "auto-rules", "P2"));
        Items.Add(new NavSection("Pro", "Bulk Operations", "📋", "bulk", "P2"));
        Items.Add(new NavSection("Pro", "CSV Import / Export", "📤", "csv", "P2"));
        Items.Add(new NavSection("Pro", "Anomaly Detector", "⚠️", "anomaly", "P2"));
        Items.Add(new NavSection("Pro", "Budget Pacing", "💰", "pacing", "P2"));
        Items.Add(new NavSection("Pro", "Creative Library", "🎨", "creatives", "P2"));
        Items.Add(new NavSection("Pro", "A/B Test Manager", "🅰️", "ab-test", "P2"));
        Items.Add(new NavSection("Pro", "Audience Manager", "👥", "audiences", "P2"));
        Items.Add(new NavSection("Advanced", "Pixel & Event Inspector", "🎯", "pixel", "P3"));
        Items.Add(new NavSection("Advanced", "Conversion Funnel", "🪃", "funnel", "P3"));
        Items.Add(new NavSection("Advanced", "Creative Fatigue", "😴", "fatigue", "P3"));
        Items.Add(new NavSection("Advanced", "Competitor Spy", "🕵️", "competitor", "P3"));
        Items.Add(new NavSection("Advanced", "Hashtag Tracker", "#️⃣", "hashtags", "P3"));
        Items.Add(new NavSection("Advanced", "Bid Strategy Tester", "💸", "bid-tester", "P3"));
        Items.Add(new NavSection("Advanced", "Multi-Currency", "💱", "currency", "P3"));
        Items.Add(new NavSection("Advanced", "Naming Enforcer", "🏷️", "naming", "P3"));
        Items.Add(new NavSection("Advanced", "Webhooks / Slack", "🔔", "alerts-channel", "P3"));
        Items.Add(new NavSection("Advanced", "Scheduled Reports", "📧", "scheduled-reports", "P3"));
        Items.Add(new NavSection("Advanced", "API Quota Monitor", "📡", "quota", "P3"));
        Items.Add(new NavSection("Operations", "Settings", "⚙️", "settings", "P4"));
        Items.Add(new NavSection("Operations", "Backup / Restore", "💾", "backup", "P4"));
        Items.Add(new NavSection("Operations", "Health Check", "🩺", "health", "P4"));
        Items.Add(new NavSection("Operations", "Keyboard Shortcuts", "⌨️", "shortcuts", "P4"));
    }

    private object ResolveView(string tag) => tag switch
    {
        "dashboard" => _services.GetRequiredService<DashboardViewModel>(),
        "accounts" => _services.GetRequiredService<AccountsViewModel>(),
        "campaigns" => _services.GetRequiredService<CampaignsViewModel>(),
        "schedules" => _services.GetRequiredService<SchedulesViewModel>(),
        "logs" => _services.GetRequiredService<LogsViewModel>(),
        "reports" => _services.GetRequiredService<ReportsViewModel>(),
        "heatmap" => _services.GetRequiredService<HeatmapViewModel>(),
        "auto-rules" => _services.GetRequiredService<AutoRulesViewModel>(),
        "bulk" => _services.GetRequiredService<BulkOpsViewModel>(),
        "csv" => _services.GetRequiredService<CsvViewModel>(),
        "anomaly" => _services.GetRequiredService<AnomalyViewModel>(),
        "pacing" => _services.GetRequiredService<PacingViewModel>(),
        "settings" => _services.GetRequiredService<SettingsViewModel>(),
        "backup" => _services.GetRequiredService<BackupViewModel>(),
        "health" => _services.GetRequiredService<HealthViewModel>(),
        "shortcuts" => _services.GetRequiredService<ShortcutsViewModel>(),
        _ => new ComingSoonViewModel(tag)
    };
}
