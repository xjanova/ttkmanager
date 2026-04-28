<div align="center">

<img src="logottk.png" alt="TTK Manager" width="160">

# TTK Manager

**Run TikTok ads on autopilot.** A portable Windows desktop tool that schedules budgets, paces spend, fires auto-rules on metric thresholds, and detects performance anomalies — across one or many TikTok ad accounts.

[![.NET](https://img.shields.io/badge/.NET-9-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-185ABD?style=flat-square)](https://avaloniaui.net/)
[![SQLite](https://img.shields.io/badge/SQLite-DPAPI-003B57?style=flat-square&logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![License](https://img.shields.io/badge/License-MIT--planned-a3e635?style=flat-square)](#license)
[![Status](https://img.shields.io/badge/Status-v0.1%20alpha-ef4444?style=flat-square)](#)

[**Live landing page**](https://xjanova.github.io/ttkmanager/) · [**30-screen mockup**](https://xjanova.github.io/ttkmanager/mockups/) · [**Issues**](https://github.com/xjanova/ttkmanager/issues)

</div>

---

## What it is

A self-contained `.exe` you can run from a USB stick. No install, no .NET runtime required on the target machine, no backend, no telemetry, no third-party data sharing. Your TikTok OAuth tokens stay encrypted on your own machine via Windows DPAPI + AES-GCM.

## Try it in one click — Demo Mode

> Open the app → click **▶ Try demo data** in the top bar.
>
> The app fills with three sample TikTok accounts, seven campaigns, six schedule rules, five auto-rules, four budget caps, fourteen days of metric samples (~1.2k rows), seven alerts, and thirty-two audit entries — all flagged in SQLite as demo records. Click **✕ Exit demo** and every demo row is deleted; your real data is untouched.

## Features (v0.1 shipping today)

| Tier | Feature | Status |
|------|---------|--------|
| Foundation | Dashboard with KPI tiles + recent activity + active alerts | ✅ |
| Foundation | Multi-account OAuth (out-of-band code paste) | ✅ |
| Foundation | Campaign browser per account | ✅ |
| Foundation | Cron-based schedule rules + dayparting (9 presets) | ✅ |
| Foundation | Local audit log with full-text search | ✅ |
| Pro | Auto-Rules engine — if-then triggers on CPC/CPM/CTR/ROAS/spend/conv/freq | ✅ |
| Pro | Anomaly Detector — z-score CPM and CPC vs 14-day baseline | ✅ |
| Pro | Budget Pacing — daily / weekly / monthly caps with auto-pause | ✅ |
| Pro | Bulk Operations — multi-select pause / enable / set-budget | ✅ |
| Pro | CSV import / export — schedules, audit log, accounts | ✅ |
| Pro | Performance Reports — windowed rollups + derived metrics | ✅ |
| Pro | Dayparting Heatmap — 7×24 grid colored by chosen metric | ✅ |
| Operations | Settings, Backup / Restore, Health Check, Keyboard Shortcuts | ✅ |
| Advanced | Creatives, A/B Tests, Audiences, Pixel, Funnel, Fatigue, Competitor Spy, Hashtags, Bid Tester, Multi-Currency, Naming, Webhooks, Scheduled Reports, Quota Monitor | 🟡 stubs (post-API) |

The 14 stub screens are scaffolded as `ComingSoonView` panels so the app shape is complete from day one — they unlock incrementally as the Marketing API is approved.

## Architecture

```
TTKManager/
├── index.html                       Marketing landing page
├── callback.html                    OAuth out-of-band callback
├── mockups/index.html               30-screen god-tier vision
├── publish.cmd / publish.sh         Single-file portable build script
├── TTKManager.sln                   Solution (VS 2026 / Rider / VS Code)
├── global.json                      .NET 9 SDK pin
└── src/TTKManager.App/
    ├── Models/                      Domain types (Account, Rule, AutoRule, Cap, Alert, Sample, ...)
    ├── Services/
    │   ├── Database.cs              SQLite + Dapper, single ttkmanager.db
    │   ├── TikTokApiClient.cs       Real /open_api/v1.3 client + Polly retry
    │   ├── MockTikTokApiClient.cs   Offline-friendly mock
    │   ├── SchedulerService.cs      Quartz.NET in-process scheduler
    │   ├── AutoRulesEngine.cs       Metric eval + cooldown + action dispatch
    │   ├── AnomalyDetector.cs       Per-campaign z-score scanner
    │   ├── BudgetPacer.cs           Period cap + auto-pause
    │   ├── DemoModeService.cs       Toggle-on / toggle-off demo data lifecycle
    │   ├── CsvService.cs            Round-trip CSV
    │   ├── BackupService.cs         .ttkbak ZIP archive + restore w/ snapshot
    │   ├── HealthCheckService.cs    System self-test
    │   └── WindowsTokenProtector.cs DPAPI + AES-GCM token vault
    ├── ViewModels/                  MVVM (CommunityToolkit base, manual properties)
    ├── Views/                       Avalonia AXAML
    ├── Themes/Colors.axaml          Neon Lime palette
    ├── Themes/Controls.axaml        Custom button / tab / datagrid / card styles
    ├── Bootstrapper.cs              DI container wiring
    └── App.axaml                    Application root + theme merge
```

| Layer | Tech |
|-------|------|
| UI | Avalonia 12 (cross-platform XAML) |
| MVVM | CommunityToolkit.Mvvm.ObservableObject (manual properties) |
| Scheduler | Quartz.NET 3 (in-process) |
| Storage | SQLite + Dapper (single file) |
| HTTP | HttpClient + Polly v8 retry / backoff / jitter |
| Token security | AES-GCM via Windows DPAPI |
| Target framework | .NET 9 |

## Quick start

### Prerequisites

- **.NET 9 SDK** (pinned by `global.json`)
- **Visual Studio 2026**, **JetBrains Rider 2025.1+**, or **VS Code with C# Dev Kit** (any one)

### Open in IDE

```bash
git clone https://github.com/xjanova/ttkmanager
cd ttkmanager
```

Then double-click `TTKManager.sln` (Visual Studio) or `code .` (VS Code) and press F5.

### Build from CLI

```bash
dotnet build TTKManager.sln
dotnet run --project src/TTKManager.App/TTKManager.App.csproj
```

> ⚠️ If your machine has the `MSBuildSDKsPath` environment variable pinned to a preview .NET SDK and the build fails with `MSB4062`, run:
>
> ```bash
> MSBuildSDKsPath="C:/Program Files/dotnet/sdk/9.0.313/Sdks" dotnet build TTKManager.sln
> ```

### Local configuration

Copy `src/TTKManager.App/appsettings.local.json.example` to `src/TTKManager.App/appsettings.local.json` (or place the file next to the published `.exe`) and fill in your TikTok app credentials:

```json
{
  "TikTokAppId": "your_app_id_here",
  "TikTokAppSecret": "your_app_secret_here",
  "RedirectUri": "https://xjanova.github.io/ttkmanager/callback.html",
  "UseMockApi": false
}
```

`appsettings.local.json` is gitignored. Until it is present (or `TikTokAppId` is empty), the app falls back to a mock API client with the seeded demo data — handy while the developer app is still in TikTok review.

### Build a portable .exe

```bash
./publish.cmd          # Windows
./publish.sh           # Bash / WSL / Git Bash
```

Output: `publish/TTKManager.exe`. Drop on a USB stick — no install, no .NET runtime required on the target.

## OAuth flow (out-of-band)

1. **Accounts** tab → **Connect Account…**
2. Click **Open in Browser** — TikTok consent screen opens.
3. After approving, TikTok redirects to `https://xjanova.github.io/ttkmanager/callback.html?auth_code=…`
4. The callback page displays the `auth_code`; click **Copy code**.
5. Paste it into the dialog and click **Exchange Code**.
6. The dialog closes and the new advertiser account appears in the list.

Tokens are encrypted with DPAPI before being written to the local SQLite database.

## Roadmap

| Phase | Scope | When |
|-------|-------|------|
| **P1 Foundation** | Dashboard, Accounts, Campaigns, Schedules, Logs, Settings, Backup, Health | ✅ shipped |
| **P2 Pro** | Auto-Rules engine, Anomaly, Pacing, Reports, Heatmap, Bulk, CSV — all built and live in v0.1 with demo data; full live data once Marketing API approved | 🟢 ready, awaits API |
| **P2 Pro (more)** | Creatives, A/B Tests, Audiences | 🟡 stubs |
| **P3 Advanced** | Pixel + Funnel + Fatigue + Competitor + Hashtags + Bid Tester + Multi-currency + Naming + Webhooks + Scheduled reports + Quota | 🟡 stubs |
| **P4 SaaS** | Multi-tenant cloud version, web dashboard, team collaboration, Tech Partner status with TikTok | future |

## Contributing

This repo is currently a personal / internal tool while the Marketing API approval is in flight. Issues and PRs are welcome — especially around:

- Avalonia theming polish
- Real TikTok API responses parsing edge cases
- Test fixtures
- Translations (Thai is the primary locale; English is the default UI language)

## License

License will be set to MIT when Phase 2 begins. Until then the repo is source-available for reference and self-use.

---

<div align="center">

Made with 🤍 by **xman studio** · [GitHub](https://github.com/xjanova) · [TTK Manager](https://github.com/xjanova/ttkmanager)

</div>
