namespace TTKManager.App.ViewModels;

public class ComingSoonViewModel : ViewModelBase
{
    public string Tag { get; }
    public string Title { get; }
    public string Description { get; }
    public string[] PlannedFeatures { get; }

    public ComingSoonViewModel() : this("dashboard") { }

    public ComingSoonViewModel(string tag)
    {
        Tag = tag;
        var info = Lookup(tag);
        Title = info.title;
        Description = info.desc;
        PlannedFeatures = info.features;
    }

    private static (string title, string desc, string[] features) Lookup(string tag) => tag switch
    {
        "campaigns" => ("Campaigns Browser",
            "Browse and inspect all campaigns / ad groups / ads across connected accounts.",
            new[] { "Multi-account view", "Filter by objective, status, optimization goal", "Customizable columns", "Inline trend sparklines", "Drill into ad groups → ads" }),
        "creatives" => ("Creative Library",
            "Centralize, tag, and reuse video / image creatives across campaigns.",
            new[] { "Local-first asset store", "Tag-based search + duplicate detection by hash", "Per-creative performance summary", "Auto-resize for placements", "One-click 'use in new ad'" }),
        "ab-test" => ("A/B Test Manager",
            "Run and pick winners across creative / hook / audience / CTA variants.",
            new[] { "Even-split ad-group cloning", "Bayesian + frequentist confidence", "Auto-stop at significance", "Auto-promote winner to scaling campaign", "Test templates" }),
        "audiences" => ("Audience Manager",
            "Saved audiences, custom + lookalike audiences in one view.",
            new[] { "Custom audience builder (event + window + filter)", "Lookalike configurator", "Overlap analyzer", "Refresh schedule per audience", "Usage map" }),
        "pixel" => ("Pixel & Event Inspector",
            "Verify pixel events fire correctly and match what your ads expect.",
            new[] { "Live event stream (last 60 min)", "Schema + value + match-key validation", "Per-URL event count + dedup check", "EMQ score breakdown", "Replay test events" }),
        "funnel" => ("Conversion Funnel",
            "Where users drop off between impression and purchase.",
            new[] { "Configurable steps from any pixel events", "Auto-detect biggest drop step", "Compare segments", "Cohort funnel by acquisition week", "Recommendations" }),
        "fatigue" => ("Creative Fatigue Detector",
            "Spot creatives whose CTR is declining and surface refresh suggestions.",
            new[] { "Per-creative CTR trend", "Frequency cap recommendations", "One-click pause + replace from library", "Pattern fatigue grouping", "Predicted days remaining" }),
        "competitor" => ("Competitor Ad Spy",
            "Browse public TikTok Ad Library entries for benchmark inspiration.",
            new[] { "Search by brand / domain / hashtag", "Filter by country, format, days active", "Tag hook style", "Watch a brand → notify on new creatives", "ToS-compliant read-only" }),
        "hashtags" => ("Hashtag Tracker",
            "Trend-watch hashtags relevant to your brand.",
            new[] { "Bookmark hashtags to monitor", "Weekly trend delta + sentiment proxy", "Cross-reference with your campaigns", "Suggest related rising hashtags", "Seasonality flags" }),
        "bid-tester" => ("Bid Strategy Tester",
            "Compare cost-cap / lowest-cost / max-conversion / max-value side by side.",
            new[] { "Even-spend split across strategies", "Statistical significance testing", "Volatility (CV%) tracking", "Auto-promote winner", "Per-objective presets" }),
        "currency" => ("Multi-Currency Dashboard",
            "Aggregate spend in your home currency across mixed-currency accounts.",
            new[] { "Auto FX conversion", "Pluggable rate providers (ECB / OXR / manual)", "Cached rates + offline fallback", "Consolidated dashboard", "Native + converted columns" }),
        "naming" => ("Naming Convention Enforcer",
            "Lint campaign / ad-group / ad names against your team pattern.",
            new[] { "Token-based pattern", "Lint + propose fixes", "Bulk rename with preview", "Apply on create", "Per-team pattern presets" }),
        "alerts-channel" => ("Webhooks · Slack · Discord · Email",
            "Pipe alerts and reports to wherever your team lives.",
            new[] { "Slack / Discord / Teams / generic webhook / SMTP", "Per-channel filter + severity", "Templated message body", "Retry + dead-letter queue", "Test-fire button" }),
        "scheduled-reports" => ("Scheduled Reports",
            "Daily / weekly / monthly digests delivered automatically.",
            new[] { "Templates: ops digest / client deck / exec summary", "Pluggable sections", "Markdown / PDF / XLSX export", "Per-recipient timezone", "One-click 'send now' preview" }),
        "quota" => ("API Quota Monitor",
            "Track TikTok Marketing API rate-limit usage.",
            new[] { "Per-endpoint counter (rolling minute / hour / day)", "Color-coded utilization bars", "429 history + backoff stats", "Predictive limit alerts", "Tunable polling cadence" }),
        _ => (tag, "Feature coming in a later release.", Array.Empty<string>())
    };
}
