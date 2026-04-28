using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class AnomalyViewModel : ViewModelBase
{
    private readonly Database? _db;
    private readonly AnomalyDetector? _detector;
    private readonly MockSamplerService? _sampler;

    public ObservableCollection<Alert> Alerts { get; } = new();

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ScanNowCommand { get; }
    public IAsyncRelayCommand SeedSamplesCommand { get; }
    public IAsyncRelayCommand<long?> DismissCommand { get; }

    public AnomalyViewModel(Database db, AnomalyDetector detector, MockSamplerService sampler)
    {
        _db = db; _detector = detector; _sampler = sampler;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ScanNowCommand = new AsyncRelayCommand(ScanAsync);
        SeedSamplesCommand = new AsyncRelayCommand(SeedAsync);
        DismissCommand = new AsyncRelayCommand<long?>(DismissAsync);
        _ = RefreshAsync();
    }

    public AnomalyViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        ScanNowCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        SeedSamplesCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        DismissCommand = new AsyncRelayCommand<long?>(_ => Task.CompletedTask);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        Alerts.Clear();
        foreach (var a in await _db.ListAlertsAsync(100)) Alerts.Add(a);
        StatusMessage = $"{Alerts.Count} active alert(s)";
    }

    private async Task ScanAsync()
    {
        if (_detector is null) return;
        StatusMessage = "Scanning…";
        var n = await _detector.ScanAsync();
        StatusMessage = $"Scan complete · {n} new alerts";
        await RefreshAsync();
    }

    private async Task SeedAsync()
    {
        if (_sampler is null) return;
        StatusMessage = "Seeding 14 days of demo metric samples…";
        await _sampler.SeedDemoSamplesAsync();
        StatusMessage = "Seeded — try Scan Now";
    }

    private async Task DismissAsync(long? id)
    {
        if (_db is null || !id.HasValue) return;
        await _db.DismissAlertAsync(id.Value);
        await RefreshAsync();
    }
}
