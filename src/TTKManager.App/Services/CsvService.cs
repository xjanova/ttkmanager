using System.Globalization;
using System.Text;
using TTKManager.App.Models;

namespace TTKManager.App.Services;

public sealed class CsvService
{
    private readonly Database _db;

    public CsvService(Database db) { _db = db; }

    public async Task ExportSchedulesAsync(string path)
    {
        var rules = await _db.ListRulesAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,AdvertiserId,CampaignId,Name,Action,BudgetAmount,Cron,TimeZoneId,Enabled,CreatedAt");
        foreach (var r in rules)
        {
            sb.AppendLine(string.Join(",",
                r.Id,
                Esc(r.AdvertiserId),
                Esc(r.CampaignId),
                Esc(r.Name),
                r.Action,
                r.BudgetAmount?.ToString(CultureInfo.InvariantCulture) ?? "",
                Esc(r.CronExpression),
                Esc(r.TimeZoneId),
                r.Enabled,
                r.CreatedAt.ToString("o")));
        }
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
    }

    public async Task ExportAuditAsync(string path)
    {
        var entries = await _db.ListRecentAuditAsync(10000);
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,RuleId,AdvertiserId,CampaignId,Action,Status,Detail,Error");
        foreach (var e in entries)
        {
            sb.AppendLine(string.Join(",",
                e.Timestamp.ToString("o"),
                e.RuleId?.ToString() ?? "",
                Esc(e.AdvertiserId),
                Esc(e.CampaignId),
                Esc(e.Action),
                e.Status,
                Esc(e.Detail ?? ""),
                Esc(e.Error ?? "")));
        }
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
    }

    public async Task ExportAccountsAsync(string path)
    {
        var accounts = await _db.ListAccountsAsync();
        var sb = new StringBuilder();
        sb.AppendLine("AdvertiserId,Name,Currency,Country,AccessTokenExpiresAt,CreatedAt");
        foreach (var a in accounts)
        {
            sb.AppendLine(string.Join(",",
                Esc(a.AdvertiserId), Esc(a.Name), Esc(a.Currency ?? ""), Esc(a.Country ?? ""),
                a.AccessTokenExpiresAt.ToString("o"), a.CreatedAt.ToString("o")));
        }
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
    }

    public async Task<int> ImportSchedulesAsync(string path)
    {
        var lines = await File.ReadAllLinesAsync(path);
        if (lines.Length < 2) return 0;
        var imported = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Length < 9) continue;
            try
            {
                var rule = new ScheduleRule
                {
                    AdvertiserId = fields[1],
                    CampaignId = fields[2],
                    Name = fields[3],
                    Action = Enum.Parse<RuleAction>(fields[4]),
                    BudgetAmount = string.IsNullOrEmpty(fields[5]) ? null : decimal.Parse(fields[5], CultureInfo.InvariantCulture),
                    CronExpression = fields[6],
                    TimeZoneId = string.IsNullOrEmpty(fields[7]) ? "Asia/Bangkok" : fields[7],
                    Enabled = bool.Parse(fields[8])
                };
                await _db.InsertRuleAsync(rule);
                imported++;
            }
            catch { }
        }
        return imported;
    }

    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"'); i++;
                }
                else if (c == '"') inQuotes = false;
                else sb.Append(c);
            }
            else
            {
                if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else if (c == '"') inQuotes = true;
                else sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }
}
