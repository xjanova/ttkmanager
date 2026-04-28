using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class BackupViewModel : ViewModelBase
{
    private readonly BackupService? _backup;

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand CreateBackupCommand { get; }
    public IAsyncRelayCommand RestoreCommand { get; }

    public BackupViewModel(BackupService backup)
    {
        _backup = backup;
        CreateBackupCommand = new AsyncRelayCommand(CreateAsync);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync);
    }

    public BackupViewModel()
    {
        CreateBackupCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        RestoreCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private async Task CreateAsync()
    {
        if (_backup is null) return;
        try
        {
            var path = await _backup.CreateBackupAsync();
            StatusMessage = $"Backup created: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup failed: {ex.Message}";
        }
    }

    private async Task RestoreAsync()
    {
        if (_backup is null) return;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        var top = desktop.MainWindow;
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("TTKManager backup") { Patterns = new[] { "*.ttkbak" } } }
        });
        if (files.Count == 0) return;
        try
        {
            await _backup.RestoreFromAsync(files[0].Path.LocalPath);
            StatusMessage = "Restore complete · restart app to apply";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore failed: {ex.Message}";
        }
    }
}
