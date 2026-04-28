using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class DemoModeService
{
    private const string Key = "demo_mode_active";
    private readonly Database _db;
    private readonly Random _rng = new(2026);

    public event Action<bool>? StateChanged;

    public DemoModeService(Database db) { _db = db; }

    public async Task<bool> IsActiveAsync()
    {
        var v = await _db.GetStateAsync(Key);
        return v == "1";
    }

    public async Task EnableAsync()
    {
        if (await IsActiveAsync()) return;
        await _db.DeleteDemoDataAsync();
        await SeedAsync();
        await _db.SetStateAsync(Key, "1");
        StateChanged?.Invoke(true);
    }

    public async Task DisableAsync()
    {
        await _db.DeleteDemoDataAsync();
        await _db.SetStateAsync(Key, "0");
        StateChanged?.Invoke(false);
    }

    private async Task SeedAsync()
    {
        var accounts = new[]
        {
            new TikTokAccount {
                AdvertiserId = "demo_th_001",
                Name = "Songkran Brand TH (DEMO)",
                Currency = "THB", Country = "TH",
                EncryptedAccessToken = Array.Empty<byte>(),
                EncryptedRefreshToken = Array.Empty<byte>(),
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
            },
            new TikTokAccount {
                AdvertiserId = "demo_us_002",
                Name = "Beauty Co. — US (DEMO)",
                Currency = "USD", Country = "US",
                EncryptedAccessToken = Array.Empty<byte>(),
                EncryptedRefreshToken = Array.Empty<byte>(),
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(6)
            },
            new TikTokAccount {
                AdvertiserId = "demo_sandbox_003",
                Name = "Sandbox Test (DEMO)",
                Currency = "THB", Country = "TH",
                EncryptedAccessToken = Array.Empty<byte>(),
                EncryptedRefreshToken = Array.Empty<byte>(),
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(90)
            }
        };

        foreach (var a in accounts) await _db.InsertDemoAccountAsync(a);

        var campaigns = new (string adv, string cid, string name)[]
        {
            ("demo_th_001", "campaign_001", "Songkran Push 2569"),
            ("demo_th_001", "campaign_002", "Songkran TopView"),
            ("demo_th_001", "campaign_003", "Always-on Retargeting"),
            ("demo_th_001", "campaign_004", "Test — Lima Hook v3"),
            ("demo_us_002", "campaign_005", "US Spring Drop"),
            ("demo_us_002", "campaign_006", "US Lookalike Q2"),
            ("demo_sandbox_003", "campaign_007", "Sandbox A/B Test"),
        };

        var schedRules = new[]
        {
            new ScheduleRule { AdvertiserId = "demo_th_001", CampaignId = "campaign_001", Name = "Peak hours boost", Action = RuleAction.SetDailyBudget, BudgetAmount = 5000m, CronExpression = "0 0 18 ? * MON-FRI" },
            new ScheduleRule { AdvertiserId = "demo_th_001", CampaignId = "campaign_001", Name = "Off-hours pause", Action = RuleAction.PauseCampaign, CronExpression = "0 0 22 * * ?" },
            new ScheduleRule { AdvertiserId = "demo_th_001", CampaignId = "campaign_001", Name = "Morning enable", Action = RuleAction.EnableCampaign, CronExpression = "0 0 9 * * ?" },
            new ScheduleRule { AdvertiserId = "demo_th_001", CampaignId = "campaign_003", Name = "Always-on weekday lift", Action = RuleAction.SetDailyBudget, BudgetAmount = 2500m, CronExpression = "0 0 6 ? * MON-FRI" },
            new ScheduleRule { AdvertiserId = "demo_us_002", CampaignId = "campaign_005", Name = "US weekend boost", Action = RuleAction.SetDailyBudget, BudgetAmount = 200m, CronExpression = "0 0 12 ? * SAT,SUN" },
            new ScheduleRule { AdvertiserId = "demo_us_002", CampaignId = "campaign_006", Name = "US off-hours pause", Action = RuleAction.PauseCampaign, CronExpression = "0 0 23 * * ?" }
        };

        foreach (var r in schedRules) await _db.InsertDemoRuleAsync(r, DateTimeOffset.UtcNow.AddDays(-_rng.Next(2, 14)));

        var autoRules = new[]
        {
            new AutoRule { Name = "Pause if CPC explodes", AdvertiserId = "demo_th_001", Metric = AutoMetric.Cpc, Comparator = AutoComparator.GreaterThan, Threshold = 8m, WindowMinutes = 60, Action = AutoAction.PauseCampaign, CooldownMinutes = 180, LastFiredAt = DateTimeOffset.UtcNow.AddHours(-13) },
            new AutoRule { Name = "Scale ROAS winners", AdvertiserId = "demo_th_001", Metric = AutoMetric.Roas, Comparator = AutoComparator.GreaterOrEqual, Threshold = 4m, WindowMinutes = 1440, Action = AutoAction.IncreaseBudgetPercent, ActionAmount = 20m, CooldownMinutes = 1440, LastFiredAt = DateTimeOffset.UtcNow.AddHours(-3) },
            new AutoRule { Name = "Sleep dormant ad groups", AdvertiserId = "demo_th_001", Metric = AutoMetric.Spend, Comparator = AutoComparator.LessThan, Threshold = 200m, WindowMinutes = 2880, Action = AutoAction.PauseCampaign, CooldownMinutes = 2880 },
            new AutoRule { Name = "Frequency cap protector", AdvertiserId = "demo_us_002", Metric = AutoMetric.Frequency, Comparator = AutoComparator.GreaterThan, Threshold = 4m, WindowMinutes = 1440, Action = AutoAction.PauseCampaign, CooldownMinutes = 720 },
            new AutoRule { Name = "Alert on CPM spike", AdvertiserId = "demo_us_002", Metric = AutoMetric.Cpm, Comparator = AutoComparator.GreaterThan, Threshold = 120m, WindowMinutes = 60, Action = AutoAction.AlertOnly, CooldownMinutes = 60, LastFiredAt = DateTimeOffset.UtcNow.AddMinutes(-72) }
        };

        foreach (var ar in autoRules) await _db.InsertDemoAutoRuleAsync(ar);

        var caps = new[]
        {
            new BudgetCap { AdvertiserId = "demo_th_001", Period = CapPeriod.Daily, CapAmount = 8000m, Currency = "THB", AutoPauseOnCap = true },
            new BudgetCap { AdvertiserId = "demo_th_001", Period = CapPeriod.Monthly, CapAmount = 500000m, Currency = "THB", AutoPauseOnCap = true },
            new BudgetCap { AdvertiserId = "demo_us_002", Period = CapPeriod.Monthly, CapAmount = 30000m, Currency = "USD", AutoPauseOnCap = true },
            new BudgetCap { AdvertiserId = "demo_sandbox_003", CampaignIdScope = "campaign_007", Period = CapPeriod.Daily, CapAmount = 1000m, Currency = "THB", AutoPauseOnCap = true }
        };

        foreach (var c in caps) await _db.InsertDemoBudgetCapAsync(c);

        for (int day = 14; day >= 0; day--)
        {
            for (int h = 0; h < 24; h += 2)
            {
                var ts = DateTimeOffset.UtcNow.AddDays(-day).AddHours(-h);
                foreach (var (adv, cid, _) in campaigns)
                {
                    var hourMultiplier = 0.6 + 0.6 * Math.Sin((h - 6) / 24.0 * Math.PI);
                    var dayMultiplier = 1.0 - 0.05 * day;
                    var noise = 0.8 + _rng.NextDouble() * 0.4;
                    var imp = (long)(_rng.Next(2000, 14000) * hourMultiplier * dayMultiplier * noise);
                    if (cid == "campaign_004") imp = (long)(imp * 0.4);
                    var ctr = cid == "campaign_004" ? 0.005 : 0.018 + _rng.NextDouble() * 0.015;
                    var clk = (long)(imp * ctr);
                    var conv = (long)(clk * (0.02 + _rng.NextDouble() * 0.05));
                    var cpmBase = cid == "campaign_004" ? 0.10 : 0.06;
                    var spend = (decimal)Math.Round(imp / 1000.0 * (cpmBase * 1000.0 + _rng.NextDouble() * 30), 2);
                    var aov = adv == "demo_us_002" ? 12m : 320m;
                    var revenue = conv * (decimal)(0.7 + _rng.NextDouble() * 0.5) * aov;
                    await _db.InsertDemoSampleAsync(new MetricSample
                    {
                        Timestamp = ts,
                        AdvertiserId = adv,
                        CampaignId = cid,
                        Spend = spend,
                        Impressions = imp,
                        Clicks = clk,
                        Conversions = conv,
                        Revenue = Math.Round(revenue, 2)
                    });
                }
            }
        }

        var alerts = new[]
        {
            new Alert { Severity = AlertSeverity.Critical, Source = AlertSource.Anomaly, Title = "CPM anomaly on campaign_004", Body = "Latest CPM ฿128 is 3.2σ above 14-day baseline — auction crowding suspected", AdvertiserId = "demo_th_001", CampaignId = "campaign_004", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-22) },
            new Alert { Severity = AlertSeverity.Warning, Source = AlertSource.Anomaly, Title = "CPC anomaly on campaign_005", Body = "Latest CPC $0.42 is 2.4σ above baseline", AdvertiserId = "demo_us_002", CampaignId = "campaign_005", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-94) },
            new Alert { Severity = AlertSeverity.Warning, Source = AlertSource.BudgetCap, Title = "Daily cap nearly hit", Body = "Spent ฿7,240 / ฿8,000 (90.5%) on Songkran Brand TH", AdvertiserId = "demo_th_001", Timestamp = DateTimeOffset.UtcNow.AddHours(-1) },
            new Alert { Severity = AlertSeverity.Info, Source = AlertSource.AutoRule, Title = "Auto-rule fired: Scale ROAS winners", Body = "campaign_003 budget raised ฿2,500 → ฿3,000 · ROAS 4.1× sustained 24h", AdvertiserId = "demo_th_001", CampaignId = "campaign_003", Timestamp = DateTimeOffset.UtcNow.AddHours(-3) },
            new Alert { Severity = AlertSeverity.Warning, Source = AlertSource.AutoRule, Title = "Auto-rule fired: Pause if CPC explodes", Body = "campaign_004 paused — CPC ฿12.6 > ฿8 threshold for 60 min", AdvertiserId = "demo_th_001", CampaignId = "campaign_004", Timestamp = DateTimeOffset.UtcNow.AddHours(-13) },
            new Alert { Severity = AlertSeverity.Info, Source = AlertSource.TokenExpiry, Title = "Token expiring soon: Beauty Co.", Body = "Refresh token expires in 6 days — auto-refresh queued", AdvertiserId = "demo_us_002", Timestamp = DateTimeOffset.UtcNow.AddHours(-6) },
            new Alert { Severity = AlertSeverity.Info, Source = AlertSource.System, Title = "Backup created", Body = "ttkmanager-backup-20260428-090012.ttkbak (412 KB)", Timestamp = DateTimeOffset.UtcNow.AddHours(-9) }
        };

        foreach (var al in alerts) await _db.InsertDemoAlertAsync(al);

        var actions = new[] { "SetBudget", "Pause", "Enable", "AutoRule:PauseCampaign", "RefreshToken", "AutoRule:IncreaseBudgetPercent" };
        for (int i = 0; i < 32; i++)
        {
            var when = DateTimeOffset.UtcNow.AddMinutes(-i * 22 - _rng.Next(0, 10));
            var camp = campaigns[_rng.Next(campaigns.Length)];
            var status = _rng.Next(10) > 1 ? AuditStatus.Success : (_rng.Next(2) == 0 ? AuditStatus.Failed : AuditStatus.Skipped);
            await _db.InsertDemoAuditAsync(new AuditLogEntry
            {
                Timestamp = when,
                AdvertiserId = camp.adv,
                CampaignId = camp.cid,
                Action = actions[_rng.Next(actions.Length)],
                Status = status,
                Detail = status == AuditStatus.Success ? $"budget={(_rng.Next(1, 8) * 1000)}.00" : null,
                Error = status == AuditStatus.Failed ? "HTTP 429 — backed off" : null
            });
        }
    }
}
