# TTKManager

Portable Windows desktop tool for the TikTok Marketing API — scheduled budget control and dayparting (time-based delivery rules) for ad campaigns.

## Status

- **Phase 1 (current):** Self-use desktop app. TikTok Developer App pending review.
- **Phase 2 (future):** Multi-tenant SaaS, contingent on validated demand.

## Features

- Connect one or more TikTok ad advertiser accounts via OAuth (out-of-band code paste).
- Browse campaigns per advertiser; schedule rules to:
  - Set daily budget at a specific time (dayparting)
  - Pause / enable a campaign on a schedule
- Cron-based scheduler (Quartz.NET) running locally in the background.
- Audit log of every scheduled action (success / failure / detail).
- Tokens encrypted at rest with **AES-GCM via Windows DPAPI** — bound to the user account.
- Single-file self-contained portable executable — no install, no admin required.

## Architecture

| Layer | Tech |
|-------|------|
| UI | Avalonia 12 (cross-platform XAML) |
| MVVM | CommunityToolkit.Mvvm.ObservableObject (manual properties) |
| Scheduler | Quartz.NET 3 (in-process, persisted via DB) |
| Storage | SQLite + Dapper |
| HTTP | HttpClient + Polly v8 retry/backoff |
| Token security | AES-GCM via DPAPI (Windows) |
| Target framework | .NET 9 |

## Repository layout

```
TTKManager/
├── TTKManager.sln                  Solution (VS / Rider compatible)
├── global.json                     SDK pin (.NET 9)
├── publish.cmd / publish.sh        Portable single-file release script
├── index.html / callback.html      GitHub Pages — OAuth callback host
├── docs/
│   └── TIKTOK_API_APPLICATION.md   Marketing API access application document
└── src/
    └── TTKManager.App/             Avalonia app (single project for now)
        ├── Models/                 Domain types
        ├── Services/               Database, API client, scheduler, token protector
        ├── ViewModels/             MVVM ViewModels
        ├── Views/                  AXAML views + windows
        ├── AppSettings.cs          Local config loader
        └── Bootstrapper.cs         DI container wiring
```

## Development

### Prerequisites

- **.NET 9 SDK** (`9.0.x`). Pinned by `global.json`.
- **Visual Studio 2026**, **JetBrains Rider 2025.1+**, or **VS Code with C# Dev Kit**.

### Open in Visual Studio 2026

1. Clone the repo.
2. Double-click `TTKManager.sln` (the standard `.sln` format — VS 2026 also opens `.slnx` but `.sln` is bundled here for broader tool compatibility).
3. Set `TTKManager.App` as the startup project.
4. Press F5 to debug. The Avalonia window should appear; the SQLite database will be created next to the executable, and the Quartz scheduler will start with 0 active rules.

### Open in Rider / VS Code

```
rider TTKManager.sln
# or
code .
```

The C# Dev Kit will pick up the `.sln`. Launch profiles are in `src/TTKManager.App/Properties/launchSettings.json`.

### Build from CLI

```
dotnet build TTKManager.sln
```

> ⚠️ **MSBuild SDK quirk:** if your machine has the `MSBuildSDKsPath` environment variable pointing to an older / preview .NET SDK, the build may fail with `MSB4062`. Either unset that variable or run with the SDK 9 path explicitly:
>
> ```
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

`appsettings.local.json` is gitignored. Until the file is present (or `TikTokAppId` is empty), the app falls back to a mock API client with seeded demo data — useful for UI work while the developer app is in review.

## Publishing a portable build

Single-file self-contained Windows x64 executable, no .NET runtime required on the target:

```
./publish.cmd          # Windows
./publish.sh           # Bash / WSL / Git Bash
```

Output: `publish/TTKManager.exe`. Place that one file (and optionally `appsettings.local.json` next to it) anywhere — USB stick, network share, user folder — and run.

## OAuth flow (out-of-band)

1. In the **Accounts** tab, click **Connect Account…** to open the dialog.
2. Click **Open in Browser** — TikTok consent screen opens.
3. After approving, TikTok redirects to `https://xjanova.github.io/ttkmanager/callback.html?auth_code=...`.
4. The callback page displays the `auth_code`; click **Copy code**.
5. Paste it into the **Paste authorization code** field in the dialog and click **Exchange Code**.
6. The dialog closes and the new advertiser account appears in the list.

Tokens are encrypted with DPAPI before being written to the local SQLite database.

## Scheduling rules

In the **Schedules** tab:

1. Pick the connected **Account**, then a **Campaign** (auto-loaded).
2. Choose an **Action**: set daily budget / pause / enable.
3. Pick a **When** preset (e.g. "Weekdays at 18:00") or write a custom Quartz cron expression.
4. Click **Add Rule**. The rule fires immediately according to its cron schedule.

All firings are recorded in the **Logs** tab.

## License

Personal / internal use during Phase 1. License will be set when Phase 2 begins.
