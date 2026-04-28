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

            CREATE TABLE IF NOT EXISTS auto_rules (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                advertiser_id TEXT NOT NULL,
                campaign_id_scope TEXT,
                metric TEXT NOT NULL,
                comparator TEXT NOT NULL,
                threshold TEXT NOT NULL,
                window_minutes INTEGER NOT NULL DEFAULT 60,
                action TEXT NOT NULL,
                action_amount TEXT,
                cooldown_minutes INTEGER NOT NULL DEFAULT 60,
                enabled INTEGER NOT NULL DEFAULT 1,
                last_fired_at INTEGER,
                created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS budget_caps (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                advertiser_id TEXT NOT NULL,
                campaign_id_scope TEXT,
                period TEXT NOT NULL,
                cap_amount TEXT NOT NULL,
                currency TEXT NOT NULL DEFAULT 'THB',
                auto_pause_on_cap INTEGER NOT NULL DEFAULT 1,
                created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS alerts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp INTEGER NOT NULL,
                severity TEXT NOT NULL,
                source TEXT NOT NULL,
                title TEXT NOT NULL,
                body TEXT,
                advertiser_id TEXT,
                campaign_id TEXT,
                dismissed INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_alerts_timestamp ON alerts(timestamp DESC);

            CREATE TABLE IF NOT EXISTS metric_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp INTEGER NOT NULL,
                advertiser_id TEXT NOT NULL,
                campaign_id TEXT NOT NULL,
                spend TEXT NOT NULL,
                impressions INTEGER NOT NULL,
                clicks INTEGER NOT NULL,
                conversions INTEGER NOT NULL,
                revenue TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_samples_campaign_time ON metric_samples(campaign_id, timestamp DESC);
        ");
    }

    public async Task<IReadOnlyList<AutoRule>> ListAutoRulesAsync()
    {
        using var conn = Open();
        var rows = await conn.QueryAsync<AutoRuleRow>(@"
            SELECT id AS Id, name AS Name, advertiser_id AS AdvertiserId,
                   campaign_id_scope AS CampaignIdScope, metric AS MetricText,
                   comparator AS ComparatorText, threshold AS ThresholdText,
                   window_minutes AS WindowMinutes, action AS ActionText,
                   action_amount AS ActionAmountText, cooldown_minutes AS CooldownMinutes,
                   enabled AS EnabledInt, last_fired_at AS LastFiredAtUnix,
                   created_at AS CreatedAtUnix
            FROM auto_rules ORDER BY id DESC");
        return rows.Select(r => new AutoRule
        {
            Id = r.Id,
            Name = r.Name,
            AdvertiserId = r.AdvertiserId,
            CampaignIdScope = r.CampaignIdScope,
            Metric = Enum.Parse<AutoMetric>(r.MetricText),
            Comparator = Enum.Parse<AutoComparator>(r.ComparatorText),
            Threshold = decimal.Parse(r.ThresholdText, System.Globalization.CultureInfo.InvariantCulture),
            WindowMinutes = r.WindowMinutes,
            Action = Enum.Parse<AutoAction>(r.ActionText),
            ActionAmount = decimal.TryParse(r.ActionAmountText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : null,
            CooldownMinutes = r.CooldownMinutes,
            Enabled = r.EnabledInt != 0,
            LastFiredAt = r.LastFiredAtUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(r.LastFiredAtUnix.Value) : null,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(r.CreatedAtUnix)
        }).ToList();
    }

    public async Task<long> InsertAutoRuleAsync(AutoRule r)
    {
        using var conn = Open();
        return await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO auto_rules (name, advertiser_id, campaign_id_scope, metric, comparator, threshold, window_minutes, action, action_amount, cooldown_minutes, enabled, created_at)
            VALUES (@Name, @AdvertiserId, @Scope, @Metric, @Comparator, @Threshold, @Window, @Action, @Amount, @Cooldown, @Enabled, @CreatedAt);
            SELECT last_insert_rowid();",
            new
            {
                r.Name, r.AdvertiserId,
                Scope = r.CampaignIdScope,
                Metric = r.Metric.ToString(),
                Comparator = r.Comparator.ToString(),
                Threshold = r.Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Window = r.WindowMinutes,
                Action = r.Action.ToString(),
                Amount = r.ActionAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Cooldown = r.CooldownMinutes,
                Enabled = r.Enabled ? 1 : 0,
                CreatedAt = r.CreatedAt.ToUnixTimeSeconds()
            });
    }

    public async Task DeleteAutoRuleAsync(long id)
    {
        using var conn = Open();
        await conn.ExecuteAsync("DELETE FROM auto_rules WHERE id = @id", new { id });
    }

    public async Task UpdateAutoRuleEnabledAsync(long id, bool enabled)
    {
        using var conn = Open();
        await conn.ExecuteAsync("UPDATE auto_rules SET enabled = @e WHERE id = @id",
            new { id, e = enabled ? 1 : 0 });
    }

    public async Task TouchAutoRuleAsync(long id)
    {
        using var conn = Open();
        await conn.ExecuteAsync("UPDATE auto_rules SET last_fired_at = @ts WHERE id = @id",
            new { id, ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
    }

    public async Task<IReadOnlyList<BudgetCap>> ListBudgetCapsAsync()
    {
        using var conn = Open();
        var rows = await conn.QueryAsync<BudgetCapRow>(@"
            SELECT id AS Id, advertiser_id AS AdvertiserId, campaign_id_scope AS CampaignIdScope,
                   period AS PeriodText, cap_amount AS CapAmountText, currency AS Currency,
                   auto_pause_on_cap AS AutoPauseInt, created_at AS CreatedAtUnix
            FROM budget_caps ORDER BY id DESC");
        return rows.Select(r => new BudgetCap
        {
            Id = r.Id,
            AdvertiserId = r.AdvertiserId,
            CampaignIdScope = r.CampaignIdScope,
            Period = Enum.Parse<CapPeriod>(r.PeriodText),
            CapAmount = decimal.Parse(r.CapAmountText, System.Globalization.CultureInfo.InvariantCulture),
            Currency = r.Currency,
            AutoPauseOnCap = r.AutoPauseInt != 0,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(r.CreatedAtUnix)
        }).ToList();
    }

    public async Task<long> InsertBudgetCapAsync(BudgetCap c)
    {
        using var conn = Open();
        return await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO budget_caps (advertiser_id, campaign_id_scope, period, cap_amount, currency, auto_pause_on_cap, created_at)
            VALUES (@AdvertiserId, @Scope, @Period, @Cap, @Currency, @AutoPause, @CreatedAt);
            SELECT last_insert_rowid();",
            new
            {
                c.AdvertiserId,
                Scope = c.CampaignIdScope,
                Period = c.Period.ToString(),
                Cap = c.CapAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                c.Currency,
                AutoPause = c.AutoPauseOnCap ? 1 : 0,
                CreatedAt = c.CreatedAt.ToUnixTimeSeconds()
            });
    }

    public async Task DeleteBudgetCapAsync(long id)
    {
        using var conn = Open();
        await conn.ExecuteAsync("DELETE FROM budget_caps WHERE id = @id", new { id });
    }

    public async Task<long> InsertAlertAsync(Alert a)
    {
        using var conn = Open();
        return await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO alerts (timestamp, severity, source, title, body, advertiser_id, campaign_id, dismissed)
            VALUES (@Timestamp, @Severity, @Source, @Title, @Body, @AdvertiserId, @CampaignId, @Dismissed);
            SELECT last_insert_rowid();",
            new
            {
                Timestamp = a.Timestamp.ToUnixTimeSeconds(),
                Severity = a.Severity.ToString(),
                Source = a.Source.ToString(),
                a.Title, a.Body, a.AdvertiserId, a.CampaignId,
                Dismissed = a.Dismissed ? 1 : 0
            });
    }

    public async Task<IReadOnlyList<Alert>> ListAlertsAsync(int limit = 200, bool includeDismissed = false)
    {
        using var conn = Open();
        var sql = "SELECT id AS Id, timestamp AS TimestampUnix, severity AS SeverityText, source AS SourceText, title AS Title, body AS Body, advertiser_id AS AdvertiserId, campaign_id AS CampaignId, dismissed AS DismissedInt FROM alerts";
        if (!includeDismissed) sql += " WHERE dismissed = 0";
        sql += " ORDER BY timestamp DESC LIMIT @Limit";
        var rows = await conn.QueryAsync<AlertRow>(sql, new { Limit = limit });
        return rows.Select(r => new Alert
        {
            Id = r.Id,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(r.TimestampUnix),
            Severity = Enum.Parse<AlertSeverity>(r.SeverityText),
            Source = Enum.Parse<AlertSource>(r.SourceText),
            Title = r.Title,
            Body = r.Body,
            AdvertiserId = r.AdvertiserId,
            CampaignId = r.CampaignId,
            Dismissed = r.DismissedInt != 0
        }).ToList();
    }

    public async Task DismissAlertAsync(long id)
    {
        using var conn = Open();
        await conn.ExecuteAsync("UPDATE alerts SET dismissed = 1 WHERE id = @id", new { id });
    }

    public async Task InsertMetricSampleAsync(MetricSample m)
    {
        using var conn = Open();
        await conn.ExecuteAsync(@"
            INSERT INTO metric_samples (timestamp, advertiser_id, campaign_id, spend, impressions, clicks, conversions, revenue)
            VALUES (@Timestamp, @AdvertiserId, @CampaignId, @Spend, @Impressions, @Clicks, @Conversions, @Revenue)",
            new
            {
                Timestamp = m.Timestamp.ToUnixTimeSeconds(),
                m.AdvertiserId, m.CampaignId,
                Spend = m.Spend.ToString(System.Globalization.CultureInfo.InvariantCulture),
                m.Impressions, m.Clicks, m.Conversions,
                Revenue = m.Revenue.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
    }

    public async Task<IReadOnlyList<MetricSample>> ListSamplesAsync(string campaignId, DateTimeOffset since)
    {
        using var conn = Open();
        var rows = await conn.QueryAsync<MetricSampleRow>(@"
            SELECT id AS Id, timestamp AS TimestampUnix, advertiser_id AS AdvertiserId, campaign_id AS CampaignId,
                   spend AS SpendText, impressions AS Impressions, clicks AS Clicks, conversions AS Conversions, revenue AS RevenueText
            FROM metric_samples WHERE campaign_id = @cid AND timestamp >= @ts ORDER BY timestamp",
            new { cid = campaignId, ts = since.ToUnixTimeSeconds() });
        return rows.Select(r => new MetricSample
        {
            Id = r.Id,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(r.TimestampUnix),
            AdvertiserId = r.AdvertiserId,
            CampaignId = r.CampaignId,
            Spend = decimal.Parse(r.SpendText, System.Globalization.CultureInfo.InvariantCulture),
            Impressions = r.Impressions,
            Clicks = r.Clicks,
            Conversions = r.Conversions,
            Revenue = decimal.Parse(r.RevenueText, System.Globalization.CultureInfo.InvariantCulture)
        }).ToList();
    }

    public async Task<decimal> SumSpendSinceAsync(string advertiserId, DateTimeOffset since, string? campaignId = null)
    {
        using var conn = Open();
        var sql = "SELECT COALESCE(SUM(CAST(spend AS REAL)), 0) FROM metric_samples WHERE advertiser_id = @adv AND timestamp >= @ts";
        if (campaignId is not null) sql += " AND campaign_id = @cid";
        return (decimal)await conn.ExecuteScalarAsync<double>(sql, new { adv = advertiserId, ts = since.ToUnixTimeSeconds(), cid = campaignId });
    }

    private sealed class AutoRuleRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string AdvertiserId { get; set; } = "";
        public string? CampaignIdScope { get; set; }
        public string MetricText { get; set; } = "";
        public string ComparatorText { get; set; } = "";
        public string ThresholdText { get; set; } = "0";
        public int WindowMinutes { get; set; }
        public string ActionText { get; set; } = "";
        public string? ActionAmountText { get; set; }
        public int CooldownMinutes { get; set; }
        public int EnabledInt { get; set; }
        public long? LastFiredAtUnix { get; set; }
        public long CreatedAtUnix { get; set; }
    }

    private sealed class BudgetCapRow
    {
        public long Id { get; set; }
        public string AdvertiserId { get; set; } = "";
        public string? CampaignIdScope { get; set; }
        public string PeriodText { get; set; } = "";
        public string CapAmountText { get; set; } = "0";
        public string Currency { get; set; } = "THB";
        public int AutoPauseInt { get; set; }
        public long CreatedAtUnix { get; set; }
    }

    private sealed class AlertRow
    {
        public long Id { get; set; }
        public long TimestampUnix { get; set; }
        public string SeverityText { get; set; } = "";
        public string SourceText { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Body { get; set; }
        public string? AdvertiserId { get; set; }
        public string? CampaignId { get; set; }
        public int DismissedInt { get; set; }
    }

    private sealed class MetricSampleRow
    {
        public long Id { get; set; }
        public long TimestampUnix { get; set; }
        public string AdvertiserId { get; set; } = "";
        public string CampaignId { get; set; } = "";
        public string SpendText { get; set; } = "0";
        public long Impressions { get; set; }
        public long Clicks { get; set; }
        public long Conversions { get; set; }
        public string RevenueText { get; set; } = "0";
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

    public async Task DeleteAccountAsync(string advertiserId)
    {
        using var conn = Open();
        await conn.ExecuteAsync("DELETE FROM accounts WHERE advertiser_id = @advertiserId", new { advertiserId });
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

    public async Task DeleteRuleAsync(long ruleId)
    {
        using var conn = Open();
        await conn.ExecuteAsync("DELETE FROM schedule_rules WHERE id = @ruleId", new { ruleId });
    }

    public async Task UpdateRuleEnabledAsync(long ruleId, bool enabled)
    {
        using var conn = Open();
        await conn.ExecuteAsync("UPDATE schedule_rules SET enabled = @enabled WHERE id = @ruleId",
            new { ruleId, enabled = enabled ? 1 : 0 });
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
