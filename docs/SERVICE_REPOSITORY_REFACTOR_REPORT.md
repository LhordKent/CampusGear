# Service–Repository refactor report

## Baseline and delivery

- Branch: codex/service-repository-refactor. Main baseline: f5dc69b; clean checkout and 132 passing tests were recorded before extraction.
- Final implementation: the boundary-enforcement/handoff commit on this branch; its exact hash is pinned below by the following evidence-only documentation commit.
- Main is not merged or rewritten. No account recreation, credential change, schema migration, or live-database fixture operation was part of this refactor.
- Completed paths and code ownership are indexed by [FEATURE_TRACES](FEATURE_TRACES.md), [64 route/action entries](ROUTE_MIGRATION_MAP.md), [the beginner cheatsheet](CODEBASE_CHEATSHEET.md), and [the maintained source catalog](SOURCE_CATALOG.md).
- [baseline-http-contracts.json](baseline-http-contracts.json) freezes original MVC route spelling and role-attribute inventory from f5dc69b. HTTP tests separately enforce effective permissions and responses.

## Before and after

Before: controllers directly queried EF, coordinated Identity/SQL locks/transactions, allocated quantity bookings and handled image/database compensation. Reservation and challenge services also referenced EF. UI helpers queried equipment state/picker data, and the workspace layout injected UserManager.

After: browser → controller → feature service → focused repository → EF Core → SQL Server.

| Layer | Implemented ownership |
|---|---|
| WebApp | Original HTTP routes/binding/authorization/rate limits/antiforgery, field errors, views/JSON/status/redirects, session adapter, header component, formatting/geometry and static assets |
| Services | Eligibility/approval/state decisions, quantity coordination, Identity coordination, upload/email/audit orchestration and transaction/lock choices; plain commands/results/DTOs |
| Data | Focused interfaces/repositories, materialized queries and SQL filtering/order/paging, tracked persistence, rowversion/constraint translation, transaction and application-lock mechanics |
| Resources | Existing enum values, ItemStatus and shared quantity limit |

Controllers have no DbContext, repository or Identity-manager dependencies. Business services have no EF/SqlClient or MVC-result APIs. Razor receives scalar/display records, not mutable persistence entities. Data does not reference Services or WebApp.

There is no generic CRUD repository or AutoMapper. Existing entity namespaces and migration metadata remain stable. Some EF-free shared read DTOs retain CampusGear.Controllers / CampusGear.Models namespaces for Razor compatibility, although they are physically owned by Services. Browser form classes live in WebApp/Models and explicitly map to plain commands.

## Migration ledger

| Commit | Completed extraction / gate |
|---|---|
| 872b565 | Baseline characterization: stale category/equipment edits, repeated multi-unit ID reuse; three new HTTP checks |
| 3cefe09 | Data/Services registration, UnitOfWork/transactions/locks/results/context; category reference feature; 61 focused checks |
| 6a7d25d | Inventory and maintenance repositories/services, immutable display records and image compensation; 61 HTTP checks plus two SQL image checks |
| 6b15145 | ReservationService and EmailChallengeService persistence; original 13 reservation SQL and five registration/challenge checks |
| 8fbae29 | Borrower quantity/read orchestration, custodian queues/details/reports, directories, availability, dashboards; 137 full-suite tests |
| 2aca123 | User administration, account/profile/role/history/audit persistence and Identity constraint adapter; focused account/admin gate |
| 3e193ef | Account/session workflows, current-account header component, four stale/expired/unknown-flow checks; 26 account/password checks |
| Final stage | Boundary enforcement, frozen route inventory, remaining characterizations, plain borrower command mapping, formatting, corrected smoke/docs/catalog, final full gate |

Dependency ordering was adjusted where safe: core reservation/challenge persistence was extracted before remaining read models. Closely coupled inventory/maintenance scaffolding was committed together. Extra characterization was expanded after the initial frozen baseline while retaining all original assertions. Constructor wiring changed mechanically; two existing UI-status tests now call the extracted availability service with unchanged assertions.

## Action → service → repository

The [generated route migration map](ROUTE_MIGRATION_MAP.md) lists every live MVC route/action/input and its application/persistence path. Principal examples:

| Controller action | Application method | Repository operations / tables |
|---|---|---|
| Account.SignUp | AccountService.SignUpAsync | AccountRepository.StudentExistsAsync/AddProfile + Identity stores → users/roles/profile; audit after main commit |
| AdminUsers.Approve/Reject | UserAdministrationService.DecideAsync | AccountRepository.FindAsync + Identity.UpdateAsync + AuditRepository |
| AdminUsers.Index/Borrowers/Edit/Create/Delete/Audit | UserAdministrationService query/command methods | AccountRepository, Identity stores, AuditRepository; accounts/roles/profiles/history |
| AdminCategories.Index/Save/Delete | CategoryService query/command methods | CategoryRepository.ListAsync/FindAsync/HasItemsAsync/Add/Remove |
| AdminInventory.Equipment/Save/Delete/AddCategory | InventoryService / CategoryService | EquipmentRepository.InventoryAsync/FindAsync/history checks; Category/Maintenance/Audit |
| Maintenance.Index/Open/Update | MaintenanceService | MaintenanceRepository.ListAsync/GetItemIdAsync/FindWithItemAsync/HasOpenAsync/Add; Equipment/Audit |
| Borrower.Home/Reservation/Availability/History/Calendar | BorrowerService | BorrowerReadRepository own-profile/catalog/blocked/request/history/calendar operations |
| Borrower.CreateReservation | BorrowerService.CreateReservationAsync → ReservationService.CreateAsync | Deterministic per-unit requests, candidate reads and core write repositories |
| Borrower.Cancel; Custodian.Approve/Reject/Release/RecordReturn | Existing IReservationService transition methods | Reservation/Profile/Account/Loan/Equipment/Maintenance/Audit repositories |
| Custodian queue/details/history/calendar | CustodianService | CustodianReadRepository.ListAsync/DetailsAsync + borrower calendar reads |
| CustodianDirectory.Categories/Borrowers | DirectoryService | DirectoryReadRepository.CategoriesAsync/BorrowersAsync |
| Workspace.Administrator/Custodian | DashboardService | DashboardReadRepository independent counts/trends + availability + Identity read |
| Account login/admin/reset/logout | AccountService → EmailChallengeService | AccountRepository reloads + Identity stores + EmailChallengeRepository + AuditRepository |
| WorkspaceIdentity component | CurrentAccountService.GetAsync | Identity read → CurrentAccountDisplay name/initial strings |

## Transaction, lock and side-effect decisions

All repositories, UnitOfWork and Identity EF stores share the host's existing scoped ApplicationDbContext. Data registration does not add a second context. Services choose isolation and commit timing; repository Add/Remove do not save automatically. Identity retains its established internal saves inside the shared transaction.

| Workflow | Retained boundaries |
|---|---|
| Reservation transitions | ReadCommitted per unit; item lock before profile lock where applicable; post-lock fresh eligibility/role reads; original state/error codes and save/audit ordering |
| Inventory edit | New file before SQL; item then selected-category locks; category revalidation and rowversion/loan checks; save/audit/commit; replaced image removed afterward; handled failure removes new image |
| Maintenance | Item lock, case version and loan/open-case checks; case/item/audit together; other open cases retain hold |
| Categories | Existing category lock, rowversion/active-item checks, existing save/audit timing |
| User edits/deletes | Global administration lock then profile lock/reload for edits; concurrency/security stamps, last-admin/self protections, history retention |
| Signup | Identity user/role/profile transaction; success audit after commit; PendingApproval and no signup email |
| Email issuance | Serializable challenge persistence, commit before send; delivery failure invalidates unsent challenge for retry |
| Quantity booking | Original independent per-unit transactions, deterministic IDs, candidate retries, duplicate-form lookup and compensating cancellation |
| Expiry worker | Existing scope per batch, limits/cadence/retry/shutdown behavior |

SQL application lock names remain CampusGear:EquipmentItem:{id:N}, CampusGear:EquipmentCategory:{id:N}, CampusGear:BorrowerProfile:{id:N}, and CampusGear:UserAdministration. They are transaction-owned/exclusive with the original 10,000 ms acquisition timeout.

Data converts EF concurrency, unique/foreign-key/check constraints and other update failures into sanitized persistence failures. Services catch only the failure categories handled by the old workflow; unexpected constraints remain server failures rather than being silently changed into validation responses. Identity's built-in concurrency result behavior remains intact.

Audit contents retain actor, action, outcome, entity, before/after and server IP/correlation. The rollback check proves Identity, repository/profile and audit writes participate in the same UnitOfWork transaction. Original operations that audited after main commit still do so.

## Identity, session and HTTP compatibility

Registration is administrator-approved; custodians alone mutate borrowing; administrators have read-only borrowing reports/details. Old documentation claiming signup codes or administrator borrowing actions was replaced.

AccountFlowStore preserves all pending-administrator, pending-reset and authorized-reset session keys, missing-versus-empty markers, fifteen-minute idle session and ten-minute reset permission. Accounts/security stamps are checked/reloaded around verification. Identity retains password hashing, lockout, role stores, security stamps, tokens and cookies. Sender selection, Development SMTP mirror, secrets precedence and User Secrets ID are unchanged.

Controllers retain local-return-URL validation, rate-limiting attributes, antiforgery recovery, Development mailbox restrictions and endpoint-specific outcomes. Maintenance still returns ok/message/field with 200/422/404 for fetch and TempData redirects for plain forms. Borrower availability keeps bookableUnits/freeUnits/maxQuantity. Manila boundaries, status precedence, report-specific labels, chart periods, catalog grouping/preferred-unit selection and SQL paging remain separate where their semantics differ.

## Tests and verification

Final gate: build passed with zero warnings/errors; all 164 tests passed (zero failed/skipped), including the original 132 and 32 added checks. Figma reference verification passed all 70 frames/127 images. EF reported no pending model changes. The full smoke wrapper exited successfully, and the final diff passed whitespace checks.

The final recorded commands/results are in [scripts/verification.md](../scripts/verification.md). Automated coverage includes the original 132 tests and added checks for:

- stale category, equipment, user and maintenance edits without unwanted saves/audits;
- delete versus deactivate for categories/items/accounts, challenge/profile deletion and retained audit history;
- image validation/trusted deletion, successful replacement/removal and failed-persistence compensation;
- repeated and simultaneous multi-unit forms, candidate retry, compensating cancellation and disconnect semantics;
- unknown/missing reset markers, stale administrator/reset stamps and expired reset grants;
- server audit actor/IP/correlation, sanitized relational failures and shared Identity/repository/audit rollback;
- controller/view/helper/service boundaries, entity-free presentation contracts, repository API boundaries, route/role baseline, source-catalog coverage, documentation links and named service methods.

Existing SQL concurrency checks still cover item/profile locks, duplicate request IDs, eligibility changes while waiting, half-open slots, hold expiry, outstanding loans and release/return behavior. All tests run against uniquely named disposable SQL databases; WebAppFixture forces Development email and disables Demo Mode.

Figma validation checks the 70 saved reference frames and 127 local images. A focused real-browser run on a separate disposable host verified administrator password/code sign-in, dashboard/header, inventory, users and maintenance. Checked pages had no observed console errors, broken images or page-wide horizontal overflow at the default browser viewport. The temporary host/database were removed; the model-check database was not created. Existing accounts/uploads were not fixtures.

No migration files changed. EF's has-pending-model-changes command reported no changes after the final relevant build.

## Retained pre-existing gaps and verification limits

- Baseline repeated multi-unit redirect parameter ordering is unstable; tests compare ID sets. During simultaneous duplicate forms the existing per-unit workflow may return an already-saved subset while the other request finishes. This was characterized rather than replaced with batch atomicity.
- Inventory writes the new photo before starting its transaction. In the baseline, transaction acquisition itself is outside the compensation try block; a failure at that exact point can leave the new file. This source-inspected outage path remains a separate defect, not a silent behavior change. Handled duplicate/concurrency save failures are covered.
- Multi-unit compensation retains the original handled-workflow-exception scope and disconnect behavior; this refactor does not promise rollback for every unexpected process/database outage.
- Real SMTP inbox delivery, production deployment and exhaustive pixel comparisons of every generated Figma state were not retested. The existing sender/configuration code is retained; Development mailbox success is not real inbox proof.
- The source catalog is generated navigation metadata, not a semantic proof. Read the method and its linked feature tests before changing a rule.
- Startup initialization, existing migrations, explicit Development demo-data maintenance, framework account/cookie middleware and Identity EF stores are intentional infrastructure exceptions. They do not permit database access in live controllers/views/helpers.

## Recovery and handoff

There is no schema rollback for this structural refactor. Use dependency-ordered revert commits if rollback becomes necessary; do not hard-reset shared history. The branch is reviewable and main remains unchanged.

For a new developer: read the category reference example, then a worked feature trace, then the catalog entry for the file being changed. Run the relevant tests and regenerate the catalog with python scripts/update-code-map.py. Keep its feature documentation in the same commit as subsequent code changes.
