using System.IO.Compression;

namespace TTKManager.App.Services;

public sealed class BackupService
{
    private readonly AppSettings _settings;
    private readonly string _portableFolder;

    public BackupService(AppSettings settings)
    {
        _settings = settings;
        _portableFolder = AppContext.BaseDirectory;
    }

    public async Task<string> CreateBackupAsync(bool includeAuditLog = true, bool includeSettings = true)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(_portableFolder, $"ttkmanager-backup-{stamp}.ttkbak");
        await using var fs = File.Create(path);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true);

        var dbPath = Path.IsPathRooted(_settings.DatabasePath)
            ? _settings.DatabasePath
            : Path.Combine(_portableFolder, _settings.DatabasePath);
        if (File.Exists(dbPath))
        {
            var entry = archive.CreateEntry("db/ttkmanager.db", CompressionLevel.Optimal);
            await using var stream = entry.Open();
            await using var src = File.OpenRead(dbPath);
            await src.CopyToAsync(stream);
        }

        if (includeSettings)
        {
            var settingsPath = Path.Combine(_portableFolder, "appsettings.local.json");
            if (File.Exists(settingsPath))
            {
                var entry = archive.CreateEntry("config/appsettings.local.json", CompressionLevel.Optimal);
                await using var stream = entry.Open();
                await using var src = File.OpenRead(settingsPath);
                await src.CopyToAsync(stream);
            }
        }

        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using (var ms = manifestEntry.Open())
        {
            var json = $@"{{""version"":""0.1"",""created"":""{DateTimeOffset.UtcNow:o}"",""includesAudit"":{includeAuditLog.ToString().ToLowerInvariant()}}}";
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await ms.WriteAsync(bytes);
        }

        return path;
    }

    public async Task RestoreFromAsync(string backupPath)
    {
        using var archive = ZipFile.OpenRead(backupPath);
        var dbEntry = archive.GetEntry("db/ttkmanager.db");
        if (dbEntry is not null)
        {
            var dbPath = Path.IsPathRooted(_settings.DatabasePath)
                ? _settings.DatabasePath
                : Path.Combine(_portableFolder, _settings.DatabasePath);
            var snapshotPath = dbPath + ".pre-restore";
            if (File.Exists(dbPath)) File.Copy(dbPath, snapshotPath, overwrite: true);
            await using var stream = dbEntry.Open();
            await using var dst = File.Create(dbPath);
            await stream.CopyToAsync(dst);
        }
        var settingsEntry = archive.GetEntry("config/appsettings.local.json");
        if (settingsEntry is not null)
        {
            var settingsPath = Path.Combine(_portableFolder, "appsettings.local.json");
            if (File.Exists(settingsPath)) File.Copy(settingsPath, settingsPath + ".pre-restore", overwrite: true);
            await using var stream = settingsEntry.Open();
            await using var dst = File.Create(settingsPath);
            await stream.CopyToAsync(dst);
        }
    }
}
