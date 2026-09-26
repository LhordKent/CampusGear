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

SMTP is the default email provider, including in Development. The startup log identifies the selected provider. An in-memory inbox requires explicitly selecting `Email:Provider=Development`; this provider does not send real email. With it selected, open `/development/email` to read a signup, administrator login, or password recovery code. Refresh the inbox after requesting a code. It is available only in Development over a loopback connection and only with the Development email provider. `/auth/development-inbox` exposes the same messages as JSON for smoke tests. Codes expire after 10 minutes, allow five attempts, and have a 60-second resend cooldown.

Development startup loads an optional shared fallback at `%USERPROFILE%\.campusgear\email-secrets.json`, then CampusGear's standard User Secrets. This handles packaged tools whose private AppData store is invisible to Visual Studio. `Email:DevelopmentSettingsFile` can override the fallback path. Keep this file outside the repository and restrict access to your Windows account. Standard User Secrets, environment variables, and command-line arguments take precedence in that order. Startup diagnostics report file presence and whether Host/From are configured, without showing their values. Production does not load this fallback or local User Secrets.

Retrying signup with an active, unconfirmed account and its original password resumes verification without replacing account details. Confirmed accounts still use sign-in. Delivery failures invalidate the unsent code and permit an immediate retry. Logs record the sender, challenge ID, purpose, exception type, and SMTP status without logging codes, credentials, or message bodies. SMTP acceptance does not confirm inbox receipt; check Spam as well as Inbox. Asynchronous SMTP sends have a 45-second timeout.

If a previously opened account form becomes invalid after a restart or sign-in change, the rejected submission returns to a fresh form with a retry message. Re-enter your details; the rejected request does not create an account or send a code.

### Send real email with Gmail locally

1. Enable [Google 2-Step Verification](https://myaccount.google.com/security).
2. Open [Google App Passwords](https://myaccount.google.com/apppasswords), create one named `CampusGear`, and keep the generated password on your computer. App Passwords may be unavailable for some managed accounts or Advanced Protection accounts; see [Google's instructions](https://support.google.com/accounts/answer/185833).
3. From `C:\CampusGear`, run the interactive setup. It prompts for your sender address and hides the App Password input:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-gmail.ps1
   ```

4. Stop the app if it is running, then restart it:

   ```powershell
   dotnet run --project .\CampusGear\CampusGear\CampusGear.csproj --launch-profile http
   ```

5. Open `http://localhost:5264/auth/signup` and register using an email you control. Check Inbox and Spam for the verification code, then enter it in CampusGear. Password recovery and administrator login codes use the same sender.

The script selects `Email:Provider=Smtp` and configures `smtp.gmail.com`, port `587`, and STARTTLS (`EnableSsl=true`), as documented in [Gmail's SMTP settings](https://support.google.com/mail/answer/7104828). It saves the App Password in [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-8.0), outside the repository. This development store is not encrypted. It passes the password through standard input, so it is not included in command arguments. No password belongs in `appsettings.json`, GitHub, or chat.

Real email delivery can run in Development without changing the local SQL Server or authentication setup. The development inbox is disabled when SMTP is selected. Test signup and password recovery with addresses you control; the automated live smoke script requires the Development provider and must not run against real email delivery. Local Gmail signup delivery was confirmed by the recipient on September 26, 2026.

To switch back to the local inbox for smoke tests, run this and restart the app:

```powershell
dotnet user-secrets set "Email:Provider" "Development" --project .\CampusGear\CampusGear\CampusGear.csproj
```

### Create the first administrator

Set these process environment variables before starting the app. Use an email you control and choose your own strong password:

```powershell
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_EMAIL = 'your-email@example.com'
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_NAME = 'Your Name'
$setupPassword = Read-Host 'Initial administrator password' -AsSecureString
$env:CAMPUSGEAR_BOOTSTRAP_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', $setupPassword).Password
dotnet run --project .\CampusGear\CampusGear\CampusGear.csproj --launch-profile http -- --initialize-only
```

The bootstrap runs only when no administrator account exists. `--initialize-only` creates the account and exits without starting the web server. Account creation and role assignment commit together. The initial account is confirmed by this local operator setup and requires an email code at login. Remove the password environment variable afterward with `Remove-Item Env:CAMPUSGEAR_BOOTSTRAP_ADMIN_PASSWORD`. No default administrator password is checked into the project.

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
- `Email__Provider=Smtp` (the default in every environment)

Use HTTPS and a valid SQL Server certificate in deployment. `TrustServerCertificate=True` is only in the local Development connection. User Secrets are loaded only in Development; deployed SMTP credentials must be configured through the host's secret store. Gmail SMTP is configured locally; STARTTLS, account authentication, and receipt of a real signup verification email were verified. Authentication flow sessions and the development inbox are in memory, so restarting clears them. A deployment across multiple app instances also needs shared session storage and persistent/shared Data Protection keys.

## Figma sources and UI reference

- `figma-reference/frames.json`: all 70 visible application frames with node IDs.
- `figma-reference/live-screen-manifest.json`: source frame mappings for the database-backed MVC screens and management forms.
- `figma-reference/generated/`: generated React/Tailwind code returned by Figma for every frame, foundations, and component boards. These are source snapshots, with no runtime React or Tailwind dependency.
- `CampusGear/CampusGear/wwwroot/figma/catalog.json`: each frame's stable `/demo/{workspace}/{screen}?state=...` URL.
- `CampusGear/CampusGear/Pages/Figma/Frames/`: Razor partials converted from those snapshots.
- `CampusGear/CampusGear/wwwroot/figma/`: local SVG/images and self-hosted fonts. Temporary Figma URLs are retained only in offline source snapshots.

The live MVC screens translate the saved Figma-generated markup into shared workspace shells, dashboard panels, rounded table rows, request decision panels, full-page add/edit forms, and summary/action strips. Original navigation icons, background graphics, and fonts are served locally. Dashboard totals and rows come from SQL Server, including empty states. Search, booking end time, maintenance information, and account eligibility controls accommodate the real backend. Borrower Profiles has its own live listing. The complete visual state catalog remains available in the reference demo. `/demo/auth/login` retains optional empty-field sign-in and role selection as a sample-only interaction; live sign-in verifies credentials and administrator email codes.

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

Previous full checks passed with zero build warnings, 13 SQL integration tests, 594 HTTP/data assertions, all 70 reference states and 127 local images, responsive layouts, and keyboard access. The signup email investigation passed focused email/configuration tests and real HTTP signups; the recipient confirmed receiving a verification email. The shared settings fix still requires a normal Visual Studio signup/resend check. Verification results are recorded in `scripts/verification.md`.
