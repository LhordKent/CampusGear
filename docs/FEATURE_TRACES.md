# Feature tracing index

## Read a feature from the browser down

The controller binds and returns HTTP. The service chooses business outcomes. The repository executes SQL through EF. `IUnitOfWork` decides when the shared context saves/commits; repository `Add`/`Remove` methods do not commit.

## Categories

| Browser operation | Controller action | Service | Repository | Database / response |
|---|---|---|---|---|
| Open/search categories (`GET /admin/categories`) | `AdminCategoriesController.Index` | `CategoryService.GetEditAsync` / `GetPageAsync` | `CategoryRepository.FindAsync` / `ListAsync` | EquipmentCategories + item counts → Index view |
| Save category (`POST /admin/categories/save`) | `Save` | `CategoryService.SaveAsync` | Find, HasItems, Add + UnitOfWork + WorkflowLockManager + AuditRepository | Category and audit; redirect or same form errors |
| Delete category (`POST /admin/categories/{id}/delete`) | `Delete` | `CategoryService.DeleteAsync` | Find, HasItems, Remove | Delete empty category, otherwise deactivate; audit and redirect |

`CategoryInput` is the browser form model in WebApp/Models/Categories. `CategoryCommand` is the service input. `CategoryListRecord` is a materialized database projection. The controller maps these explicitly. Follow `CategoryService.SaveAsync` to see the transaction, category lock, stale rowversion comparison, active-item rule, audit, and commit in order.

### How to debug

Start with the URL, put a breakpoint in the action, step into the service, then the repository. A field validation error returns before persistence. A stale editor returns a service error. A duplicate SQL name becomes `PersistenceFailure.Duplicate` in Data, then the existing message in Services. A cancelled or failed transaction rolls back on disposal. Never remove the lock or version check to make a save succeed.

## Equipment and images

`GET /admin/equipment` → `AdminInventoryController.Equipment` → `InventoryService.GetPageAsync` → `EquipmentRepository.InventoryAsync` → EquipmentItems + categories → Equipment view. `EquipmentEditInput` is the posted form; `EquipmentCommand` is the service input. `EquipmentRecord` is an immutable display projection, not a tracked EF entity.

`POST /admin/equipment/save` → controller opens the upload stream → `InventoryService.SaveAsync` → category/equipment/maintenance repositories + transaction/locks/audit → existing redirect or form errors. The service validates the category again after taking the item/category locks. A new photo is removed if persistence fails; the replaced photo is removed after commit. `InventoryCompensationTests` verifies these side effects using a fake store and disposable SQL database.

`POST /admin/equipment/{id}/delete` → `InventoryService.DeleteAsync`: equipment with borrowing/repair history is deactivated; unused equipment is deleted and its photo removed after saving.

## Maintenance

`GET /admin/maintenance` → `MaintenanceController.Index` → `MaintenanceService.GetPageAsync` → `MaintenanceRepository.ListAsync` → case rows and equipment choices. The view receives detached `MaintenanceRecord` projections.

`POST /admin/maintenance/open` and `/{id}/update` → controller binds form → `MaintenanceService.OpenAsync` / `UpdateAsync` → equipment/maintenance repositories under the same item lock → case, item hold, audit, commit. Services validate outstanding loans, duplicate open cases, stale rowversions, and usable repair conditions. The controller alone chooses fetch JSON (200/422/404) versus a plain-form redirect. `Round3BehaviourTests` freezes that transport contract.

## Reservation core and email challenges

`IReservationService` retains its original command/result/error contract. `ReservationService` decides transitions and owns transactions; ReservationRepository loads reservations/conflicts/expiry candidates, BorrowerProfileRepository reloads eligibility after the profile lock, AccountRepository checks current role membership, and Loan/Equipment/Maintenance/Audit repositories share the same context. The expiry worker still creates a scope per batch. `ReservationServiceSqlTests` verifies concurrency, duplicate IDs, half-open intervals, fresh eligibility, release/return and outstanding-loan restrictions.

`EmailChallengeService.IssueAsync` → EmailChallengeRepository.UnconsumedAsync → serializable transaction → invalidate previous codes/add challenge/save/commit → IEmailSender. A failed send invalidates the new code and permits immediate retry. `VerifyAsync` loads LatestValidAsync, verifies protected content and attempts, then saves through UnitOfWork; a stale write detaches pending challenge changes and refuses duplicate consumption. No verification code or credentials are logged.

## Borrower catalog, requests, history and calendar

`BorrowerController` maps HTTP to `IBorrowerService`. `BorrowerReadRepository` performs catalog projections, profile-scoped history with SQL paging, submitted-ID ownership checks, conflict reads and calendar ranges. `BorrowerService.CreateReservationAsync` validates the request, derives stable request IDs for each unit, selects free candidates, calls the existing reservation core for each unit, and compensates partial failures by cancellation. The controller only builds the existing submitted URL from the returned IDs. Free-now catalog counts and interval availability remain distinct calculations.

`EquipmentAvailabilityService` computes status priority from repository facts and supplies the calendar picker; WebApp's former `ItemStatuses.ForAllAsync` and `EquipmentPicker.LoadAsync` database helpers are removed. `ItemStatuses.Label`, `EquipmentIcon`, and `ChartGeometry` remain presentation helpers.

## Staff reads and dashboards

`CustodianController` → `CustodianService` → CustodianReadRepository for queues/reports/details, and BorrowerReadRepository for calendar facts. Details are mapped to detached `ReservationRecord` including safe borrower/staff display data; the view never receives a tracked reservation. Administrator aliases retain read-only permissions. Report date validation and query ordering retain their existing differences from borrower history.

`CustodianDirectoryController` → `DirectoryService` → DirectoryReadRepository; `WorkspaceController` → `DashboardService` → DashboardReadRepository. Dates, counts and chart series come from current database records. Axis geometry stays in WebApp's ChartGeometry. Shared, EF-free read DTOs retain their original namespaces for Razor/source compatibility and are grouped in Services/ServiceModels, while web-only form/display adapters remain under WebApp/Models.
