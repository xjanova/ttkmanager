using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class HealthViewModel : ViewModelBase
{
    private readonly HealthCheckService? _health;

    public ObservableCollection<HealthCheck> Checks { get; } = new();

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public string Version { get; } = "TTK Manager v0.1.0 · .NET 9 · Avalonia 12 · Quartz 3";

    public IAsyncRelayCommand RunCommand { get; }

    public HealthViewModel(HealthCheckService health)
    {
        _health = health;
        RunCommand = new AsyncRelayCommand(RunAsync);
        _ = RunAsync();
    }

    public HealthViewModel()
    {
        RunCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task RunAsync()
    {
        if (_health is null) return;
        Checks.Clear();
        var results = await _health.RunAsync();
        foreach (var r in results) Checks.Add(r);
        var failed = results.Count(r => r.Status == HealthStatus.Failed);
        StatusMessage = failed == 0 ? "All systems healthy" : $"{failed} check(s) failed";
    }
}
