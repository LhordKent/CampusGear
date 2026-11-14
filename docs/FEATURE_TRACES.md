# Current feature traces

Use this alongside the [route map](ROUTE_MIGRATION_MAP.md), [code guide](CODEBASE_CHEATSHEET.md) and [complete source catalog](SOURCE_CATALOG.md). Services below live in Services/Services; repositories live in Data/Repositories.

| User action | Controller -> business service | Persistence and important result |
|---|---|---|
| Login, administrator verification, logout | AuthenticationController -> AuthenticationService | AccountRepository, Identity, EmailChallengeService, AuditService; typed status/role; controller explicitly chooses screen/redirect. |
| Register borrower | RegistrationController -> RegistrationService | Unique email/student checks; shared transaction creates Identity account/role/profile; pending administrator approval; no signup email code. |
| Recover password | PasswordResetController -> PasswordResetService | Typed session pending/grant state, EmailChallengeService and Identity reset token; same unknown-email notice, stamps/expiry enforced. |
| Create/edit/approve/reject/delete users | AdminUsersController -> UserService | AccountRepository, profiles, Identity, locks/audit; self/last-admin, stale-edit, eligibility and historical-record safeguards. |
| Browse audit | AdminUsersController.Audit -> AuditService | AuditRepository.ListAsync; SQL filtering/paging, safe formatted rows. |
| Save/delete/quick-add category | AdminCategories/AdminInventory -> CategoryService | CategoryRepository, locks/unit of work/audit; validation, duplicate names, versions and history-sensitive deletion. |
| Save/delete equipment | AdminInventoryController -> InventoryService | Equipment/category/maintenance/audit repositories; file storage compensation, version and live-use restrictions. |
| Open/update/close maintenance | MaintenanceController -> MaintenanceService | Equipment/maintenance/audit repositories; condition/resolution, open-case and concurrency checks. |
| Browse equipment, home, reservation form | BorrowerController -> EquipmentCatalogService | BorrowerReadRepository and shared availability; product grouping, owned submitted links, safe DTOs. |
| Submit quantity | BorrowerController -> ReservationRequestService -> ReservationService | Deterministic unit request IDs, preferred free-unit candidates, retries, per-unit commits and compensating cancellation. |
| Cancel own reservation | BorrowerController -> ReservationService.CancelAsync | Actor ownership, allowed state, locked transition and audit. |
| Approve/reject/release/return | CustodianController -> ReservationService | Role/state/eligibility/conflict checks under transaction-owned locks; loan and maintenance changes commit with audit. |
| Approvals/returns/staff history/details | CustodianController -> CustodianQueueService | Named operations with CustodianQueueFilter; CustodianReadRepository; admin details remain read-only. |
| Own history | BorrowerController -> BorrowingHistoryService | Own-profile query scope, status/date/search/paging and early-return label. |
| Borrower/staff calendars | Borrower/Custodian controllers -> AvailabilityCalendarService | Separate public/borrower versus staff result shapes; neutral equipment/calendar reads; early-return actual end. |
| Borrower/category directories | CustodianDirectoryController -> DirectoryService | DirectoryReadRepository SQL projections. |
| Admin/custodian dashboards | WorkspaceController -> DashboardService | Independent DashboardReadRepository metrics, shared item availability, role-specific counts. |
| Background expiry | ReservationExpiryWorker -> ReservationService.ExpireDueAsync | Same transition owner/locks/audits; bounded scoped batches. |

## Worked approval trace

POST /custodian/reservations/{id}/approve enters [CustodianController](../CampusGear/CampusGear.WebApp/Controllers/CustodianController.cs). The endpoint is custodian-only and antiforgery-protected. [ReservationService.ApproveAsync](../CampusGear/CampusGear.Services/Services/ReservationService.cs) verifies the actor, acquires the item transaction lock, reloads the record, handles an already-approved result or expiry, locks/reloads eligibility, checks usability and conflicts, changes state and stages audit, then saves/commits. Repositories perform the actual queries; no second controller or queue service approves the record.

## Failure handling

Ordinary feature methods return OperationResult with field/general errors. Authentication/reset use specific business-status enums; no enum-to-view-name router remains. ReservationService uses typed workflow errors to leave failed transactions uncommitted; callers translate known codes deliberately. SQL errors are translated in Data. Image storage cleanup includes transaction-start/save failures and keeps the old image until a successful commit.

The Development mailbox is loopback/environment guarded. In-memory email is not real Gmail delivery. Identity and existing migration history are preserved; BaseRepository receives context plus unit of work explicitly so Services-facing contracts do not expose EF APIs.
