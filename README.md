# TTKManager

Portable Windows desktop tool for the TikTok Marketing API — scheduled budget control and dayparting (time-based delivery rules) for ad campaigns.

## Status

- **Phase 1 (current):** Self-use desktop app. TikTok Developer App pending review.
- **Phase 2 (future):** Multi-tenant SaaS, contingent on validated demand.

## Architecture

- .NET 8 + Avalonia (cross-platform UI)
- Quartz.NET (scheduler)
- SQLite (local storage)
- AES-GCM + DPAPI (token encryption at rest)
- Single-file self-contained publish — no install, no admin required

## OAuth callback

This repository's `callback.html` is hosted at `https://xjanova.github.io/ttkmanager/callback.html` via GitHub Pages and used as the Advertiser Redirect URL in the TikTok Marketing API app configuration.

The page reads the `auth_code` query parameter and presents it for the user to copy back into the desktop application (out-of-band OAuth flow — required because portable desktop apps cannot host an HTTPS server).
