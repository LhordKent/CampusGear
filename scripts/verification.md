# Refactor verification — November 7, 2026 (Asia/Manila)

Branch: codex/service-repository-refactor. Baseline main: f5dc69b, 132 tests. Final implementation reference is recorded in the [refactor report](../docs/SERVICE_REPOSITORY_REFACTOR_REPORT.md).

## Current automated gate

- Solution build: passed, zero warnings and zero errors.
- Full isolated suite: 164 passed, zero failed, zero skipped (25 seconds); baseline was 132, with 32 added checks.
- Figma verifier: 70 frames, 127 referenced local images, zero failures.
- EF model check: no changes since the last migration; no migration files changed.
- Source catalog: 239 maintained files; route migration map: 64 MVC route/action entries.
- Source boundary/documentation checks cover controllers, helpers, views, services, DTOs, repository contracts, baseline routes/role attributes, local links and named service methods.

Commands from the repository root:

    dotnet build CampusGear/CampusGear.sln --no-restore --verbosity quiet
    ./scripts/smoke-live.ps1
    dotnet ef migrations has-pending-model-changes --project CampusGear/CampusGear.Data --startup-project CampusGear/CampusGear.WebApp --no-build

The smoke wrapper invokes the actual integration suite and the Figma verifier. It no longer connects to a normal running host, creates fixtures in CampusGear, expects signup email verification, or allows keeping fixtures/credentials. SQL fixtures choose unique disposable database names, Development email and DemoMode=false. CAMPUSGEAR_TEST_SQLSERVER can select a server, but its database name is replaced.

Added coverage includes stale writes, deletion/history retention, image validation/compensation, quantity retries/duplicates/compensation/disconnects, stale/expired authentication flow state, audit metadata/shared transaction rollback, persistence error translation, and architecture/documentation checks. Original assertions were retained.

## Focused browser gate

A separate host at loopback port 5089 used a disposable CampusGear_Integration_Browser_* database, Development mailbox, DemoMode=false and synthetic test-only bootstrap credentials.

Verified through the real browser:

- Administrator password login → emailed Development challenge → successful verification → administrator dashboard.
- Extracted name/initial header component displays the correct account.
- Dashboard charts and inventory, user management and maintenance pages render.
- No observed browser console errors; inventory/maintenance had zero broken images and no page-wide horizontal overflow at the default viewport.

The host/tabs were closed and the disposable database removed. The separate model-check database was absent. No normal app launch against the live database was used as a test shortcut.

## Limits and retained defects

This run does not establish real SMTP receipt, production deployment behavior or exhaustive pixel parity across all 70 saved design states. Development mailbox success is not inbox-delivery proof. The [report](../docs/SERVICE_REPOSITORY_REFACTOR_REPORT.md) records retained baseline edge cases, including pre-transaction image-write cleanup and per-unit duplicate/batch outage semantics.

Earlier signup-code/administrator-booking smoke results described older behavior and are superseded by this evidence. Their history remains in Git. Current registration is administrator-approved and sends no signup code; custodians alone perform borrowing decisions.
