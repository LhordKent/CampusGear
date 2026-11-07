# CampusGear

Campus equipment borrowing on .NET 8, ASP.NET Core MVC/Identity, EF Core, and SQL Server.

**Registration requires administrator approval. Custodians approve/reject reservations, release equipment, and record returns. Administrators have read-only borrowing reports/details.** Signup sends no verification code.

## Find the code

Browser → WebApp controller → Services workflow → Data repository → EF Core → SQL Server.

| Project | Owns |
|---|---|
| [WebApp](CampusGear/CampusGear.WebApp) | Routes, form binding, authorization, HTTP/views, session adapter, UI assets |
| [Services](CampusGear/CampusGear.Services) | Business rules, workflows, DTOs, transaction decisions, Identity coordination |
| [Data](CampusGear/CampusGear.Data) | Focused repository interfaces/implementations, SQL queries, unit of work, locks, entities, migrations |
| [Resources](CampusGear/CampusGear.Resources) | Shared enums and limits |
| [IntegrationTests](CampusGear/CampusGear.IntegrationTests) | Real HTTP/SQL behavior, concurrency, architecture, uploads, account flows |

Start with the [beginner cheatsheet](docs/CODEBASE_CHEATSHEET.md), [feature traces](docs/FEATURE_TRACES.md), [complete source catalog](docs/SOURCE_CATALOG.md), [permissions](docs/PERMISSIONS.md), and [refactor report](docs/SERVICE_REPOSITORY_REFACTOR_REPORT.md). The [teacher alignment report](docs/TEACHER_BASECODE_ALIGNMENT.md) explains the project structure.

## Run locally

Requires .NET 8 SDK and SQL Server Express. Development defaults to `.\SQLEXPRESS` / database `CampusGear` with Windows authentication. Starting Development applies existing migrations and seeds basic equipment; this is normal startup, **not a test command**.

```powershell
dotnet tool restore
dotnet restore .\CampusGear\CampusGear.sln
dotnet run --project .\CampusGear\CampusGear.WebApp --launch-profile http
```

Open `http://localhost:5264/auth/login`. Select the matching launch profile in Visual Studio. Configure another SQL server through `ConnectionStrings__CampusGear`. Never use the live database for automated fixtures.

## Initial administrator

There is no committed default password. This operator procedure creates the initial administrator only when none exists; it does not replace an existing account.

```powershell
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_EMAIL = 'your-email@example.com'
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_NAME = 'Your Name'
$setupPassword = Read-Host 'Initial administrator password' -AsSecureString
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', $setupPassword).Password
dotnet run --project .\CampusGear\CampusGear.WebApp --launch-profile http -- --initialize-only
Remove-Item Env:CAMPUSGEAR_BOOTSTRAP_ADMIN_PASSWORD
```

New borrowers register at `/auth/signup`, land on `/auth/registration-submitted`, and wait for approval in `/admin/users?status=pending`. An administrator can also create accounts at `/admin/users/new`, assign staff roles, or pause eligibility. Passwords use Identity's configured upper/lowercase, digit, symbol, and minimum-eight-character rules.

## Email and session configuration

SMTP is the default. Configure `Email:Smtp:Host`, `From`, `Port` (587), `EnableSsl` (true), `Username`, and `Password` in local User Secrets or the deployment secret store. For local Gmail setup, run [setup-gmail.ps1](scripts/setup-gmail.ps1); it prompts privately for an App Password. Email is used for administrator sign-in and password recovery, **not signup**.

Development configuration precedence is: optional `%USERPROFILE%\.campusgear\email-secrets.json` → project User Secrets → environment → command line. The fallback path can be changed with `Email:DevelopmentSettingsFile`. The User Secrets ID remains `CampusGear-d27882c0-7d71-4d0d-97da-548b14740355`. Production loads neither local fallback nor Development User Secrets.

Select the in-memory mailbox explicitly for local testing:

```powershell
dotnet user-secrets set "Email:Provider" "Development" --project .\CampusGear\CampusGear.WebApp
```

Restart after changing providers. `Development` never sends real mail and is forbidden outside Development. With SMTP in Development, `DevelopmentMirrorEmailSender` also copies messages into the local mailbox; a mirror entry is not proof of SMTP delivery. `/development/email` and `/auth/development-inbox` are Development-only, loopback-only, and no-store.

Administrator codes are required except for the existing local Development bypass when provider is SMTP and Host is blank. Codes expire after ten minutes, permit five attempts, and impose a sixty-second resend cooldown. Failed delivery invalidates that code and permits retry. Startup/log diagnostics omit passwords and codes.

Session/cookies remain `CampusGear.Flow` (15-minute idle flow) and `CampusGear.Auth` (8-hour cookie). A reset grant lasts ten minutes and is tied to the account security stamp. Multiple deployment instances need shared session storage and persistent/shared Data Protection keys.

## Workflows

- Borrower: `/borrower/home`, `/borrower/reservation`, own `/borrower/history`, `/borrower/calendar`.
- Custodian: dashboard, approvals/releases/returns, history/calendar, read-only category/borrower directories, and maintenance.
- Administrator: dashboard, equipment/categories, users/borrowers, maintenance/audit, read-only history/calendar/reservation details.

Manila input/display times map to UTC records. Each physical unit has its own reservation; a quantity form requests up to ten units. Pending holds expire after 24 hours or at the start, whichever comes first. Half-open adjacent slots are allowed. Release rechecks eligibility and outstanding loans. Damaged/unserviceable returns create a repair hold.

Category/item/account deletion retains history by deactivating records where required. SQL application locks, rowversion/concurrency-stamp checks, audits, upload compensation, and per-unit batch compensation are documented in the tracing guide.

## Verification

```powershell
dotnet build .\CampusGear\CampusGear.sln
.\scripts\smoke-live.ps1
# Or run the suite directly:
dotnet test .\CampusGear\CampusGear.IntegrationTests
node .\figma-reference\verify.cjs
dotnet ef migrations has-pending-model-changes --project .\CampusGear\CampusGear.Data --startup-project .\CampusGear\CampusGear.WebApp --no-build
```

The smoke wrapper runs the current approval/permissions flow through the integration suite, plus Figma reference checks. It does not attach to a running app. Fixtures create/drop uniquely named disposable databases, force Development email, and disable Demo Mode. `CAMPUSGEAR_TEST_SQLSERVER` selects a test server; fixtures always replace its database name. Stop a running app before building if it locks output files.

See [verification evidence](scripts/verification.md). This structural refactor adds no migration or schema change. Production migration application remains an explicit deployment operation.

## Demo and visual references

`DemoMode:Enabled` adds synthetic catalog/history only in Development. It creates/changes no accounts. Existing demo-domain accounts are required for synthetic borrowing records. `DM-` asset codes make seeding idempotent. To remove demo data, use `--remove-demo-data` deliberately against the intended Development database, then disable Demo Mode.

[figma-reference](figma-reference) contains saved design snapshots and live screen mappings. [catalog.json](CampusGear/CampusGear.WebApp/wwwroot/figma/catalog.json) indexes 70 generated Razor frames under `/demo`; these are references, not working account/booking endpoints. Local assets/fonts live under `wwwroot/figma`. Edit live MVC views for live behavior.
