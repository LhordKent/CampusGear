# Verification — September 26, 2026

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

Real SMTP delivery is not configured/tested. Local verification emails use the Development-only loopback inbox. Hosting configuration, shared session storage/Data Protection for multiple instances, and production deployment verification are outside this local implementation check.
