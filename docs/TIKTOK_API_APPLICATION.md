# TTKManager — TikTok Marketing API Access Application

## 1. Application Overview
TTKManager is a portable Windows desktop application that helps TikTok advertisers manage their existing ad campaigns more efficiently through **scheduled budget control** and **time-based delivery rules** (dayparting). It is a self-contained client tool — no backend server, no shared infrastructure, no data leaves the advertiser's machine.

## 2. Target Users
- Small-to-medium businesses running their own TikTok ad campaigns
- In-house performance marketers managing multiple ad accounts
- Agencies operating campaigns on behalf of clients

## 3. Primary Use Case
TikTok advertisers frequently need to:
1. Allocate different budgets across different times of day to match peak audience activity
2. Pause campaigns outside business hours to avoid wasted spend
3. Adjust daily budgets dynamically based on observed performance
4. Operate multiple ad accounts from a single pane of glass without re-authenticating into each Business Center session

TTKManager solves these needs through a local scheduler that reads campaign state from the TikTok Marketing API and applies user-defined rules at scheduled times.

## 4. Requested Scopes and Endpoints

| Scope | Endpoints | Purpose |
|-------|-----------|---------|
| `ads.management` | `/campaign/get/`, `/campaign/update/`, `/campaign/status/update/` | Read campaigns; update budget and status per user-defined schedule |
| `ads.management` | `/adgroup/get/`, `/adgroup/update/`, `/adgroup/status/update/` | Enable/pause ad groups for dayparting |
| `advertiser.read` | `/advertiser/info/` | Display the advertiser account name and currency in the UI |
| `reports` | `/report/integrated/get/` | Pull spend, impressions, CPC/CPM so the user can evaluate pacing |

We do **not** request creative, audience, pixel, or identity-related scopes. The app is a management tool only.

## 5. Authentication Flow
- Standard OAuth 2.0 Authorization Code flow per TikTok for Business documentation
- User clicks "Connect Account" → system browser opens TikTok consent page → redirect to local loopback (`http://127.0.0.1:<port>/callback`) → app exchanges the code for an access token
- The user can revoke access at any time from the TikTok Business Center; the app handles `401`/refresh-token rotation gracefully

## 6. Data Handling and Privacy
- **All data is stored locally on the end-user's machine.** No remote backend exists.
- Access tokens and refresh tokens are encrypted at rest with **AES-256-GCM**, with the key bound to the Windows user via **DPAPI**
- Campaign metadata and report results are stored in a local **SQLite** file the user can inspect, export, or delete at any time
- **No telemetry, no analytics, no third-party data sharing**
- The application makes API calls only on behalf of the authenticated user, and only against ad accounts the user has explicitly granted access to

## 7. Rate-Limit and Policy Compliance
- All requests respect TikTok's published rate limits per advertiser and per app
- Exponential backoff with jitter on `429` and `5xx` responses
- Bulk reads are paginated; updates are serialized — no parallel flooding
- Polling intervals are configurable but enforced to a minimum of 60 seconds
- The user-agent identifies the application name and version

## 8. What This App Does NOT Do
- Does **not** scrape TikTok web properties or simulate browser activity
- Does **not** automate consumer-side TikTok behavior (likes, follows, comments, video posting)
- Does **not** create or modify ad creatives without an explicit user click in the UI
- Does **not** bypass Business Center permission boundaries — it inherits whatever access the OAuthing user already has
- Does **not** store user passwords; only OAuth tokens issued by TikTok

## 9. End-to-End User Flow
1. User runs the portable executable (no install required, no admin privileges)
2. User clicks **Add Account** → completes OAuth in their browser → app receives token
3. App fetches campaigns and ad groups via `/campaign/get/` and `/adgroup/get/`
4. User defines a rule, e.g.,
   *"Raise Campaign A daily budget to 5,000 THB from 18:00–22:00 ICT on weekdays."*
5. The local scheduler fires at 18:00 → calls `/campaign/update/` with the new budget → fires at 22:00 → restores the prior budget
6. Every action is recorded in a local audit log visible to the user

## 10. Compliance Statement
TTKManager is designed to comply with the **TikTok for Business API Terms of Service**, the **Marketing API Developer Policies**, and applicable data-protection regulations. It does not interact with the consumer-facing TikTok product, does not collect personal data beyond what is required for advertiser authentication, and exposes no functionality that could be used to circumvent TikTok's platform integrity controls.
