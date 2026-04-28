using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Models;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class LogsViewModel : ViewModelBase
{
    private readonly Database? _db;

    public ObservableCollection<AuditLogEntry> Entries { get; } = new();

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand RefreshCommand { get; }

    public LogsViewModel(Database db)
    {
        _db = db;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        _ = RefreshAsync();
    }

    public LogsViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task RefreshAsync()
    {
        if (_db is null) return;
        Entries.Clear();
        foreach (var e in await _db.ListRecentAuditAsync())
            Entries.Add(e);
        StatusMessage = $"{Entries.Count} entries";
    }
}
