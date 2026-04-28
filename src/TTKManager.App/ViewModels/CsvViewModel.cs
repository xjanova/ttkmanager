using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class CsvViewModel : ViewModelBase
{
    private readonly CsvService? _csv;

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand ExportSchedulesCommand { get; }
    public IAsyncRelayCommand ExportAuditCommand { get; }
    public IAsyncRelayCommand ExportAccountsCommand { get; }
    public IAsyncRelayCommand ImportSchedulesCommand { get; }

    public CsvViewModel(CsvService csv)
    {
        _csv = csv;
        ExportSchedulesCommand = new AsyncRelayCommand(ExportSchedulesAsync);
        ExportAuditCommand = new AsyncRelayCommand(ExportAuditAsync);
        ExportAccountsCommand = new AsyncRelayCommand(ExportAccountsAsync);
        ImportSchedulesCommand = new AsyncRelayCommand(ImportSchedulesAsync);
    }

    public CsvViewModel()
    {
        ExportSchedulesCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        ExportAuditCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        ExportAccountsCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        ImportSchedulesCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task ExportSchedulesAsync()
    {
        if (_csv is null) return;
        var path = await PickSavePathAsync("schedules.csv");
        if (path is null) return;
        await _csv.ExportSchedulesAsync(path);
        StatusMessage = $"Schedules exported to {path}";
    }

    private async Task ExportAuditAsync()
    {
        if (_csv is null) return;
        var path = await PickSavePathAsync("audit_log.csv");
        if (path is null) return;
        await _csv.ExportAuditAsync(path);
        StatusMessage = $"Audit log exported to {path}";
    }

    private async Task ExportAccountsAsync()
    {
        if (_csv is null) return;
        var path = await PickSavePathAsync("accounts.csv");
        if (path is null) return;
        await _csv.ExportAccountsAsync(path);
        StatusMessage = $"Accounts exported to {path}";
    }

    private async Task ImportSchedulesAsync()
    {
        if (_csv is null) return;
        var path = await PickOpenPathAsync();
        if (path is null) return;
        try
        {
            var n = await _csv.ImportSchedulesAsync(path);
            StatusMessage = $"Imported {n} schedule rule(s) from {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    private static async Task<string?> PickSavePathAsync(string suggested)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return null;
        var top = desktop.MainWindow;
        if (top is null) return null;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggested,
            FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
        });
        return file?.Path.LocalPath;
    }

    private static async Task<string?> PickOpenPathAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return null;
        var top = desktop.MainWindow;
        if (top is null) return null;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
