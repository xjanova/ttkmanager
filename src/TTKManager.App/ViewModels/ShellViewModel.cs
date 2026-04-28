using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace TTKManager.App.ViewModels;

public sealed record NavSection(string Group, string Title, string Emoji, string Tag, string Phase);

public class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

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

    public IRelayCommand<string> NavigateCommand { get; }

    public ShellViewModel(IServiceProvider services)
    {
        _services = services;
        NavigateCommand = new RelayCommand<string>(tag =>
        {
            var item = Items.FirstOrDefault(i => i.Tag == tag);
            if (item is not null) Selected = item;
        });
        BuildNav();
        Selected = Items.FirstOrDefault();
    }

    public ShellViewModel()
    {
        _services = null!;
        NavigateCommand = new RelayCommand<string>(_ => { });
        BuildNav();
    }

    private void BuildNav()
    {
        Items.Clear();
        // P1 Foundation
        Items.Add(new NavSection("Foundation", "Dashboard", "📊", "dashboard", "P1"));
        Items.Add(new NavSection("Foundation", "Accounts", "🔑", "accounts", "P1"));
        Items.Add(new NavSection("Foundation", "Campaigns", "📦", "campaigns", "P1"));
        Items.Add(new NavSection("Foundation", "Schedules", "⏰", "schedules", "P1"));
        Items.Add(new NavSection("Foundation", "Activity Log", "📋", "logs", "P1"));
        // P2 Pro
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
        // P3 Advanced
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
        // P4 Operations
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
