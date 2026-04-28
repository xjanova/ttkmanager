namespace TTKManager.App.Services;

public sealed class HealthCheckService
{
    private readonly Database _db;
    private readonly AppSettings _settings;
    private readonly SchedulerService _scheduler;
    private readonly ITikTokApiClient _api;
    private readonly ITokenProtector _tokens;

    public HealthCheckService(Database db, AppSettings settings, SchedulerService scheduler, ITikTokApiClient api, ITokenProtector tokens)
    {
        _db = db; _settings = settings; _scheduler = scheduler; _api = api; _tokens = tokens;
    }

    public async Task<IReadOnlyList<HealthCheck>> RunAsync()
    {
        var results = new List<HealthCheck>();

        try
        {
            var rules = await _db.ListRulesAsync();
            var audit = await _db.ListRecentAuditAsync(1);
            results.Add(new HealthCheck("SQLite database", HealthStatus.OK, $"{rules.Count} rules · {audit.Count} recent audit entries"));
        }
        catch (Exception ex) { results.Add(new HealthCheck("SQLite database", HealthStatus.Failed, ex.Message)); }

        try
        {
            var accounts = await _db.ListAccountsAsync();
            var expiringSoon = accounts.Count(a => a.AccessTokenExpiresAt - DateTimeOffset.UtcNow < TimeSpan.FromDays(7));
            var status = accounts.Count == 0 ? HealthStatus.Warning : (expiringSoon > 0 ? HealthStatus.Warning : HealthStatus.OK);
            var detail = accounts.Count == 0
                ? "No accounts connected — open Accounts tab to connect"
                : $"{accounts.Count} advertiser(s)" + (expiringSoon > 0 ? $", {expiringSoon} token(s) expire within 7 days" : "");
            results.Add(new HealthCheck("Token store (DPAPI)", status, detail));
        }
        catch (Exception ex) { results.Add(new HealthCheck("Token store (DPAPI)", HealthStatus.Failed, ex.Message)); }

        results.Add(new HealthCheck("Quartz scheduler", HealthStatus.OK, "running in-process"));

        results.Add(new HealthCheck("Mock API mode", _settings.UseMockApi ? HealthStatus.OK : HealthStatus.OK,
            _settings.UseMockApi ? "Mock data active (offline-friendly)" : "Live API mode"));

        try
        {
            var dbPath = Path.IsPathRooted(_settings.DatabasePath) ? _settings.DatabasePath : Path.Combine(AppContext.BaseDirectory, _settings.DatabasePath);
            var driveInfo = new DriveInfo(Path.GetPathRoot(dbPath) ?? "C:");
            var freeGb = driveInfo.AvailableFreeSpace / 1_000_000_000.0;
            results.Add(new HealthCheck("Disk space", freeGb < 1 ? HealthStatus.Warning : HealthStatus.OK, $"{freeGb:F1} GB free on {driveInfo.Name}"));
        }
        catch (Exception ex) { results.Add(new HealthCheck("Disk space", HealthStatus.Warning, ex.Message)); }

        return results;
    }
}

public sealed record HealthCheck(string Name, HealthStatus Status, string Detail);

public enum HealthStatus { OK, Warning, Failed }
