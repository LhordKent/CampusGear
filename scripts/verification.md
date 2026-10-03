# Verification — September 26, 2026

## Teacher base-code structure migration — October 3, 2026

- Inspected `keder-asi/ASIBasecodeCsharp` at commit `2df3206` and adopted its WebApp, Services, Data, and Resources project boundaries.
- Moved EF Core entities, DbContext, initialization, and migrations into `CampusGear.Data`; email/reservation contracts and implementations into `CampusGear.Services`; domain enums into `CampusGear.Resources`; and retained HTTP/UI/configuration code in `CampusGear.WebApp`.
- Preserved existing namespaces, database schema, User Secrets ID, Identity implementation, and runtime behavior. Updated scripts and Figma tooling to the new WebApp path.
- Remaining architectural work is documented in `docs/TEACHER_BASECODE_ALIGNMENT.md`; administrator controller processing should be extracted incrementally rather than hidden behind unused pass-through repositories.
- The five-project solution built with **0 errors and 0 warnings**; all **25 integration tests passed**.
- EF Core discovered the existing `20260926010541_InitialCampusGear` migration from the Data project and reported the local database up to date.
- The renamed WebApp started from its new project path and served `/auth/login` with HTTP 200. The Figma verifier passed all **70 frames** and **127 local image files**.

## Build and database

- .NET 8 application build: **0 errors, 0 warnings**.
- SQL Server Express 2022 (`.\SQLEXPRESS`), Windows authentication, CampusGear migration applied.
- **13/13** SQL integration tests passed. The test fixture created, migrated, and removed its own `CampusGear_Integration_<guid>` database.
- Coverage includes concurrent overlapping requests, idempotent retries, adjacent time intervals, hold expiry, ownership/role restrictions, borrower access changes, release/return, late/damaged return, maintenance holds, and outstanding loans.

## Live MVC smoke

`scripts/smoke-live.ps1` completed **603 HTTP/data assertions**.

- Signup, email confirmation, role-based login, administrator verification, CSRF enforcement.
- Borrower reservation submission, overlap feedback, approval, premature-release rejection, release, invalid-condition rejection, return, repair, and cancellation.
- Administrator booking for a borrower, user role/access edits, immediate cookie revocation, stale edits, profile/history retention, own-admin protection, duplicate student number rejection.
- Pending administrator authentication and authorized password recovery reject stale security stamps.
- Protected routes reject anonymous or incorrect-role access.
- Generated pagination/calendar/user-edit links and post-submit redirects reach working destinations.
- All **70** Figma reference frame URLs rendered their expected node IDs. All **127** local image files were served successfully.
- Additional focused checks passed for equipment updates, stale inventory row versions, and category deactivation while active equipment exists.

Earlier account smoke also checked full password recovery, old/new passwords, administrator access only after a correct code, consumed-code replay, incorrect-attempt exhaustion, code expiry, and resend invalidation/cooldown.

## Browser checks

- Live borrower reservation/history/calendar and administrator inventory/users/approvals/calendar/maintenance/audit inspected at **1440×900**, **1920×1080**, **768×1024**, and **390×844**.
- No page-wide horizontal overflow or broken images on the checked screens. Long forms and records scroll vertically; wide tables scroll inside their containers.
- Local Inter font loaded. Phone navigation collapses into a native keyboard-accessible menu and expands for desktop.
- Keyboard Enter opens the phone menu; Tab reaches navigation links. Visible focus is a 3-pixel orange outline.
- The full Figma screen catalog remains the visual reference demo. Live MVC views use data-bound semantic controls and retain source references/design assets; they have not been exhaustively compared pixel for pixel with all 70 design states.

## Remaining configuration

Gmail SMTP is configured in local User Secrets. Hosting configuration, shared session storage/Data Protection for multiple instances, and production deployment verification are outside this local implementation check.

## Signup email investigation — September 26, 2026

- Effective Development configuration selects `Smtp`, using `smtp.gmail.com:587` and STARTTLS. Sender and authenticated account match the configured Gmail address.
- The existing signup challenge was not invalidated; historical records cannot distinguish SMTP acceptance from the previous in-memory provider because sender diagnostics were absent.
- Gmail accepted a direct diagnostic message and a real HTTP signup verification email to a plus alias of the configured sender. The recipient initially reported that the diagnostic message had not arrived, then **confirmed receiving the real signup verification email**. The original missing-mail report could not be reproduced with the current SMTP configuration.
- SMTP is now the default in every environment. The in-memory provider requires explicit selection and emits a startup warning. Startup logs identify the provider.
- Challenge delivery logs record sender, purpose, challenge ID, exception type, and SMTP status without credentials, codes, message bodies, or server exception text. Async SMTP sends respect cancellation and a 45-second timeout.
- Retrying signup with an active, unconfirmed account and its correct original password resumes verification, preserving existing credentials and profile data. Failed attempts respect lockout.
- Build passed with zero warnings/errors. All **6 focused email/signup SQL tests passed**, covering delivery failure, immediate retry, cooldown, safe logging, password checks, confirmed/locked accounts, and account preservation.
- The real HTTP check used its own `CampusGear_EmailCheck_<guid>` database. The test app was stopped and both verification databases were removed. The existing administrator and pending signup account were preserved.

## Reproduced Visual Studio resend failure

- The running Visual Studio app created two invalidated signup challenges almost immediately. A targeted EventPipe trace confirmed `InvalidOperationException` with the exact missing `Email:Smtp:Host` / `Email:Smtp:From` configuration error. The same saved SMTP configuration worked in a diagnostic process, including to the actual pending signup recipient.
- Development startup now explicitly loads User Secrets using CampusGear's assembly rather than relying on the hosting application's automatic assembly selection. Environment and command-line overrides retain precedence; Production never loads the Development secrets.
- All **10 focused tests passed**: the original six email/signup tests plus four configuration regressions covering absent automatic project secrets, environment overrides, command-line overrides, and Production isolation.
- A fresh real signup check passed with Development selected through the environment, as in the normal launch profiles. Gmail accepted the verification message. The test app was stopped and its temporary database removed.

## Follow-up after pasted Visual Studio output

- The later Visual Studio output still showed a missing-settings `InvalidOperationException`; assembly-based loading alone did not resolve that launch. The saved secrets and direct SMTP delivery to the pending recipient remained valid.
- An intermediate fix tried an explicit file provider and direct SMTP configuration binding. It did not resolve the Visual Studio launch and was superseded by the shared fallback below.
- Intermediate diagnostics reported only Host/From presence and whether injected configuration matched the project. No values or credentials were logged.
- All ten focused tests passed again. A normal Development launch reported both fields present and Gmail accepted a real signup verification message. The test app/database were cleaned up. The revised Visual Studio launch remains to be verified.

## Denise signup recovery

- The next Visual Studio launch still reported Host/From absent. The issue remains reproducible in that launch and is not proven resolved by the earlier configuration changes.
- Issued a real signup challenge for the existing pending Denise account through the working SMTP configuration, without recreating the account or changing its credentials. The recipient entered that code in the running app. SQL confirmed `EmailConfirmed=1`, `IsActive=1`, and a consumed challenge.
- An intermediate diagnostic reloaded Development secrets after building and logged safe loading flags. That diagnostic was later removed after the file-location mismatch was confirmed.
- All ten focused tests passed from an isolated build while the user's verification session remained running. No verification code was printed or committed.

## Confirmed file-location mismatch

- A filesystem handle check showed Codex's User Secrets file resides in the packaged app's private `LocalCache\Roaming` store. The Visual Studio process looked in normal AppData, where the same apparent file did not exist.
- Development now loads a shared user-profile fallback before standard User Secrets and explicit overrides. Existing SMTP settings were copied to that local file outside the repository with access restricted to the current Windows account and SYSTEM. No credentials are part of this commit.
- Configuration regression coverage includes the shared fallback with absent normal User Secrets, normal User Secrets precedence, environment and command-line precedence, and Production isolation.
- Before committing, the current application and test project built successfully and all **12 focused email/signup/configuration tests passed**.
- Computer Use was stopped by the user before the revised Visual Studio signup/resend check. Inbox receipt and Denise's confirmed account do not prove that launch is fixed; the normal Visual Studio flow remains unverified.
