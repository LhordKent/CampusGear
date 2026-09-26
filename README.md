# CampusGear

.NET 8 ASP.NET Core MVC application with Entity Framework Core 8, ASP.NET Core Identity, and Microsoft SQL Server. The original 70-frame Figma UI demo remains available under `/demo`.

## Run locally

SQL Server Express is configured as `.\SQLEXPRESS` in `appsettings.Development.json`, using Windows authentication. The application creates/migrates only the `CampusGear` database in Development. A different instance can be configured with `ConnectionStrings__CampusGear`.

From `C:\CampusGear`:

```powershell
dotnet tool restore
dotnet restore .\CampusGear\CampusGear\CampusGear.csproj
dotnet run --project .\CampusGear\CampusGear\CampusGear.csproj --launch-profile http
```

Open `http://localhost:5264/auth/login`. Self-registration creates a Borrower account; after verifying any email, the borrower can reserve immediately. Passwords require at least 8 characters including upper/lower case, a number, and a non-alphanumeric character.

Local development uses an in-memory email inbox rather than sending mail. Open `/development/email` to read a signup, administrator login, or password recovery code. Refresh the inbox after requesting a code. It is available only in Development over a loopback connection. `/auth/development-inbox` exposes the same messages as JSON for smoke tests. Codes expire after 10 minutes, allow five attempts, and have a 60-second resend cooldown.

### Create the first administrator

Set these process environment variables before starting the app. Use an email you control and choose your own strong password:

```powershell
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_EMAIL = 'your-email@example.com'
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_NAME = 'Your Name'
$setupPassword = Read-Host 'Initial administrator password' -AsSecureString
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', $setupPassword).Password
dotnet run --project .\CampusGear\CampusGear\CampusGear.csproj --launch-profile http
```

The bootstrap runs only when no administrator account exists. The initial account is confirmed by this local operator setup and requires an email code at login. Remove the password environment variable afterward with `Remove-Item Env:CAMPUSGEAR_BOOTSTRAP_ADMIN_PASSWORD`. No default administrator password is checked into the project.

Custodians and additional administrators first register and verify their email. An administrator then assigns their role in User Management. Account history is retained when roles change or access is paused.

## Live workflows

| Role | Routes |
| --- | --- |
| Account | `/auth/login`, `/auth/signup`, `/auth/email-verification`, `/auth/two-factor`, password recovery screens |
| Borrower | `/borrower/dashboard`, `/borrower/reservation`, `/borrower/history`, `/borrower/calendar` |
| Custodian | `/custodian/dashboard`, `/custodian/approvals`, `/custodian/returns`, `/custodian/history`, `/custodian/calendar` |
| Administrator | `/admin/dashboard`, `/admin/reservations`, `/admin/equipment`, `/admin/categories`, `/admin/users`, `/admin/borrowers`, `/admin/maintenance`, `/admin/audit`, `/admin/approvals`, `/admin/returns`, `/admin/calendar`, `/admin/history` |

Times displayed/entered in borrowing forms use Asia/Manila; database times are UTC. Pending requests hold a slot for 24 hours or until the requested start, whichever comes first. Rejection, cancellation, and expiration free the slot. Approved reservations can be released during the approved interval. Returns record condition and timeliness; damaged/unserviceable returns automatically open maintenance. Closing a repair restores availability.

Reservations use a transaction-owned SQL Server application lock per item to serialize overlapping submissions and staff changes. Borrower access checks use a separate profile lock. Unique request IDs make retries idempotent. Row versions prevent stale inventory, maintenance, and borrower-profile edits. All mutations use role checks and anti-forgery validation; important account and equipment changes are audited.

## Database and deployment configuration

The repository includes the initial migration. Apply migrations explicitly outside Development:

```powershell
dotnet ef database update --project .\CampusGear\CampusGear\CampusGear.csproj
```

For a deployment, configure `ConnectionStrings__CampusGear` and these SMTP environment variables through the host's secret store:

- `Email__Smtp__Host`, `Email__Smtp__From`
- `Email__Smtp__Port` (default 587), `Email__Smtp__EnableSsl` (default true)
- `Email__Smtp__Username`, `Email__Smtp__Password` when required by the mail server

Use HTTPS and a valid SQL Server certificate in deployment. `TrustServerCertificate=True` is only in the local Development connection. The SMTP sender uses the configured server outside Development; real email delivery has not been configured or tested yet. Authentication flow sessions and the development inbox are in memory, so restarting clears them. A deployment across multiple app instances also needs shared session storage and persistent/shared Data Protection keys.

## Figma sources and UI reference

- `figma-reference/frames.json`: all 70 visible application frames with node IDs.
- `figma-reference/generated/`: generated React/Tailwind code returned by Figma for every frame, foundations, and component boards. These are source snapshots, with no runtime React or Tailwind dependency.
- `CampusGear/CampusGear/wwwroot/figma/catalog.json`: each frame's stable `/demo/{workspace}/{screen}?state=...` URL.
- `CampusGear/CampusGear/Pages/Figma/Frames/`: Razor partials converted from those snapshots.
- `CampusGear/CampusGear/wwwroot/figma/`: local SVG/images and self-hosted fonts. Temporary Figma URLs are retained only in offline source snapshots.

The live account views reuse Figma-generated decorative panels and local assets. The live borrowing/admin views use semantic MVC forms, data tables, and the shared design colors/fonts. The complete visual state catalog remains in the reference demo while these live workflows are integrated. `/demo/auth/login` retains optional empty-field sign-in and role selection as a sample-only interaction; it does not authenticate or write SQL data.

Desktop layouts support the Figma 1440×900 view and 1920×1080. Tablet/phone layouts scroll vertically rather than being trapped in a fixed 900-pixel canvas.

## Verification

```powershell
dotnet build .\CampusGear\CampusGear\CampusGear.csproj
node .\figma-reference\verify.cjs
dotnet test .\CampusGear\CampusGear.IntegrationTests\CampusGear.IntegrationTests.csproj
# Start the Development app before the HTTP check:
.\scripts\smoke-live.ps1 -BaseUrl http://localhost:5264
```

Stop the running app before building/testing to release its output files. The SQL tests create and remove a uniquely named test database; see `CampusGear/CampusGear.IntegrationTests/README.md`. The HTTP smoke script uses disposable accounts/items in the Development CampusGear database and removes them after the run. It supports `-KeepFixtures` for local browser inspection; that option retains synthetic credentials in a temporary file until the fixtures are removed.

The completed checks are recorded in `scripts/verification.md`: build with zero warnings, 13 SQL integration tests, 603 HTTP/data assertions, all 70 reference states and 127 local images, responsive layouts, and keyboard access. SMTP delivery remains untested until a real sender is configured.
