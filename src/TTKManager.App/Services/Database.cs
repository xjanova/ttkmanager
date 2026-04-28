using Dapper;
using Microsoft.Data.Sqlite;
using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class Database : IDisposable
{
    private readonly string _connectionString;

    public Database(string dbFilePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbFilePath,
            ForeignKeys = true
        }.ToString();
        Initialize();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void Initialize()
    {
        using var conn = Open();
        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS accounts (
                advertiser_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                currency TEXT,
                country TEXT,
                encrypted_refresh_token BLOB NOT NULL,
                encrypted_access_token BLOB NOT NULL,
                access_token_expires_at INTEGER NOT NULL,
                created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS schedule_rules (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                advertiser_id TEXT NOT NULL,
                campaign_id TEXT NOT NULL,
                name TEXT NOT NULL,
                action TEXT NOT NULL,
                budget_amount TEXT,
                cron_expression TEXT NOT NULL,
                time_zone_id TEXT NOT NULL DEFAULT 'Asia/Bangkok',
                enabled INTEGER NOT NULL DEFAULT 1,
                created_at INTEGER NOT NULL,
                FOREIGN KEY (advertiser_id) REFERENCES accounts(advertiser_id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS audit_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp INTEGER NOT NULL,
                rule_id INTEGER,
                advertiser_id TEXT NOT NULL,
                campaign_id TEXT NOT NULL,
                action TEXT NOT NULL,
                status TEXT NOT NULL,
                detail TEXT,
                error TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_audit_timestamp ON audit_log(timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_rule_advertiser ON schedule_rules(advertiser_id);
        ");
    }

    public async Task<IReadOnlyList<TikTokAccount>> ListAccountsAsync()
    {
        using var conn = Open();
        var rows = await conn.QueryAsync<AccountRow>(@"
            SELECT advertiser_id AS AdvertiserId, name AS Name, currency AS Currency, country AS Country,
                   encrypted_refresh_token AS EncryptedRefreshToken, encrypted_access_token AS EncryptedAccessToken,
                   access_token_expires_at AS AccessTokenExpiresAtUnix, created_at AS CreatedAtUnix
            FROM accounts ORDER BY created_at DESC");
        return rows.Select(r => new TikTokAccount
        {
            AdvertiserId = r.AdvertiserId,
            Name = r.Name,
            Currency = r.Currency,
            Country = r.Country,
            EncryptedRefreshToken = r.EncryptedRefreshToken,
            EncryptedAccessToken = r.EncryptedAccessToken,
            AccessTokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(r.AccessTokenExpiresAtUnix),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(r.CreatedAtUnix)
        }).ToList();
    }

    public async Task UpsertAccountAsync(TikTokAccount account)
    {
        using var conn = Open();
        await conn.ExecuteAsync(@"
            INSERT INTO accounts (advertiser_id, name, currency, country, encrypted_refresh_token, encrypted_access_token, access_token_expires_at, created_at)
            VALUES (@AdvertiserId, @Name, @Currency, @Country, @EncryptedRefreshToken, @EncryptedAccessToken, @AccessExpires, @CreatedAt)
            ON CONFLICT(advertiser_id) DO UPDATE SET
                name = excluded.name,
                currency = excluded.currency,
                country = excluded.country,
                encrypted_refresh_token = excluded.encrypted_refresh_token,
                encrypted_access_token = excluded.encrypted_access_token,
                access_token_expires_at = excluded.access_token_expires_at",
            new
            {
                account.AdvertiserId,
                account.Name,
                account.Currency,
                account.Country,
                account.EncryptedRefreshToken,
                account.EncryptedAccessToken,
                AccessExpires = account.AccessTokenExpiresAt.ToUnixTimeSeconds(),
                CreatedAt = account.CreatedAt.ToUnixTimeSeconds()
            });
    }

    public async Task<IReadOnlyList<ScheduleRule>> ListRulesAsync()
    {
        using var conn = Open();
        var rows = await conn.QueryAsync<RuleRow>(@"
            SELECT id AS Id, advertiser_id AS AdvertiserId, campaign_id AS CampaignId, name AS Name,
                   action AS ActionText, budget_amount AS BudgetAmountText,
                   cron_expression AS CronExpression, time_zone_id AS TimeZoneId,
                   enabled AS EnabledInt, created_at AS CreatedAtUnix
            FROM schedule_rules ORDER BY id DESC");
        return rows.Select(r => new ScheduleRule
        {
            Id = r.Id,
            AdvertiserId = r.AdvertiserId,
            CampaignId = r.CampaignId,
            Name = r.Name,
            Action = Enum.Parse<RuleAction>(r.ActionText),
            BudgetAmount = decimal.TryParse(r.BudgetAmountText, out var b) ? b : null,
            CronExpression = r.CronExpression,
            TimeZoneId = r.TimeZoneId,
            Enabled = r.EnabledInt != 0,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(r.CreatedAtUnix)
        }).ToList();
    }

    public async Task<long> InsertRuleAsync(ScheduleRule rule)
    {
        using var conn = Open();
        return await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO schedule_rules (advertiser_id, campaign_id, name, action, budget_amount, cron_expression, time_zone_id, enabled, created_at)
            VALUES (@AdvertiserId, @CampaignId, @Name, @Action, @Budget, @CronExpression, @TimeZoneId, @Enabled, @CreatedAt);
            SELECT last_insert_rowid();",
            new
            {
                rule.AdvertiserId,
                rule.CampaignId,
                rule.Name,
                Action = rule.Action.ToString(),
                Budget = rule.BudgetAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rule.CronExpression,
                rule.TimeZoneId,
                Enabled = rule.Enabled ? 1 : 0,
                CreatedAt = rule.CreatedAt.ToUnixTimeSeconds()
            });
    }

    public async Task InsertAuditAsync(AuditLogEntry entry)
    {
        using var conn = Open();
        await conn.ExecuteAsync(@"
            INSERT INTO audit_log (timestamp, rule_id, advertiser_id, campaign_id, action, status, detail, error)
            VALUES (@Timestamp, @RuleId, @AdvertiserId, @CampaignId, @Action, @Status, @Detail, @Error)",
            new
            {
                Timestamp = entry.Timestamp.ToUnixTimeSeconds(),
                entry.RuleId,
                entry.AdvertiserId,
                entry.CampaignId,
                entry.Action,
                Status = entry.Status.ToString(),
                entry.Detail,
                entry.Error
            });
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListRecentAuditAsync(int limit = 200)
    {
        using var conn = Open();
        var rows = await conn.QueryAsync<AuditRow>(@"
            SELECT id AS Id, timestamp AS TimestampUnix, rule_id AS RuleId,
                   advertiser_id AS AdvertiserId, campaign_id AS CampaignId,
                   action AS Action, status AS StatusText, detail AS Detail, error AS Error
            FROM audit_log ORDER BY timestamp DESC LIMIT @Limit",
            new { Limit = limit });
        return rows.Select(r => new AuditLogEntry
        {
            Id = r.Id,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(r.TimestampUnix),
            RuleId = r.RuleId,
            AdvertiserId = r.AdvertiserId,
            CampaignId = r.CampaignId,
            Action = r.Action,
            Status = Enum.Parse<AuditStatus>(r.StatusText),
            Detail = r.Detail,
            Error = r.Error
        }).ToList();
    }

    public void Dispose() { }

    private sealed class AccountRow
    {
        public string AdvertiserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Currency { get; set; }
        public string? Country { get; set; }
        public byte[] EncryptedRefreshToken { get; set; } = Array.Empty<byte>();
        public byte[] EncryptedAccessToken { get; set; } = Array.Empty<byte>();
        public long AccessTokenExpiresAtUnix { get; set; }
        public long CreatedAtUnix { get; set; }
    }

    private sealed class RuleRow
    {
        public long Id { get; set; }
        public string AdvertiserId { get; set; } = "";
        public string CampaignId { get; set; } = "";
        public string Name { get; set; } = "";
        public string ActionText { get; set; } = "";
        public string? BudgetAmountText { get; set; }
        public string CronExpression { get; set; } = "";
        public string TimeZoneId { get; set; } = "Asia/Bangkok";
        public int EnabledInt { get; set; }
        public long CreatedAtUnix { get; set; }
    }

    private sealed class AuditRow
    {
        public long Id { get; set; }
        public long TimestampUnix { get; set; }
        public long? RuleId { get; set; }
        public string AdvertiserId { get; set; } = "";
        public string CampaignId { get; set; } = "";
        public string Action { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string? Detail { get; set; }
        public string? Error { get; set; }
    }
}
