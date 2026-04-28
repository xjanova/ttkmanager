using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using TTKManager.App.Models;
using TTKManager.App.Services.Jobs;

namespace TTKManager.App.Services;

public sealed class SchedulerService : IAsyncDisposable
{
    private readonly Database _db;
    private readonly ILogger<SchedulerService> _log;
    private readonly IServiceProvider _services;
    private IScheduler? _scheduler;

    public SchedulerService(Database db, ILogger<SchedulerService> log, IServiceProvider services)
    {
        _db = db;
        _log = log;
        _services = services;
    }

    public async Task StartAsync()
    {
        var factory = new StdSchedulerFactory();
        _scheduler = await factory.GetScheduler();
        _scheduler.JobFactory = new DiJobFactory(_services);
        await _scheduler.Start();

        var rules = await _db.ListRulesAsync();
        foreach (var rule in rules.Where(r => r.Enabled))
        {
            await ScheduleRuleAsync(rule);
        }
        _log.LogInformation("Scheduler started with {Count} active rules", rules.Count(r => r.Enabled));
    }

    public async Task ScheduleRuleAsync(ScheduleRule rule)
    {
        if (_scheduler is null) throw new InvalidOperationException("Scheduler not started");

        var jobKey = new JobKey($"rule-{rule.Id}");
        if (await _scheduler.CheckExists(jobKey))
            await _scheduler.DeleteJob(jobKey);

        var job = JobBuilder.Create<CampaignActionJob>()
            .WithIdentity(jobKey)
            .UsingJobData(CampaignActionJob.RuleIdKey, rule.Id)
            .Build();

        var tz = TryGetTimeZone(rule.TimeZoneId);
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger-{rule.Id}")
            .WithCronSchedule(rule.CronExpression, b => b.InTimeZone(tz))
            .Build();

        await _scheduler.ScheduleJob(job, trigger);
        _log.LogInformation("Scheduled rule {RuleId} ({Name}) — cron={Cron} tz={Tz}", rule.Id, rule.Name, rule.CronExpression, rule.TimeZoneId);
    }

    public async Task UnscheduleRuleAsync(long ruleId)
    {
        if (_scheduler is null) return;
        await _scheduler.DeleteJob(new JobKey($"rule-{ruleId}"));
    }

    private static TimeZoneInfo TryGetTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Local; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_scheduler is not null)
            await _scheduler.Shutdown(waitForJobsToComplete: true);
    }

    private sealed class DiJobFactory : IJobFactory
    {
        private readonly IServiceProvider _services;
        public DiJobFactory(IServiceProvider services) => _services = services;

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            var jobType = bundle.JobDetail.JobType;
            return (IJob)ActivatorUtilities.CreateInstance(_services, jobType);
        }

        public void ReturnJob(IJob job)
        {
            (job as IDisposable)?.Dispose();
        }
    }
}
