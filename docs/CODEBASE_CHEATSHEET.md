# CampusGear code guide

Current implementation and readability pass, 15 November 2026. Start here, then use [coding style and folder layout](CODE_STYLE.md), [feature traces](FEATURE_TRACES.md), the generated [route map](ROUTE_MIGRATION_MAP.md) and [source catalog](SOURCE_CATALOG.md).

## Follow one click

1. Read the form action/fetch URL in WebApp/Views or wwwroot/js.
2. Find that route in WebApp/Controllers. Check its HTTP method, role and antiforgery attributes.
3. Follow the injected I...Service into Services/Services. Read the business checks and transaction boundaries.
4. Follow I...Repository into Data/Repositories. Read the actual EF query and table mappings.
5. Follow the typed result back to an explicit controller view, partial, JSON response or redirect.

The guide's construction order is repository, service, controller. Runtime requests travel controller, service, repository.

## Where code belongs

| Location | Responsibility |
|---|---|
| Data/Models | Persisted entities; namespace CampusGear.Data.Models. |
| Data/Interfaces/IRepositories.cs and Repositories | Grouped public repository contracts; separate implementations inheriting BaseRepository. Queries materialize results; writes stage changes. |
| Data/QueryModels | Database projections, not MVC input models. |
| Data/UnitOfWork and WorkflowLockManager | Shared async transaction/save and SQL application locks. |
| Services/Interfaces/IServices.cs and Services | Grouped public service contracts; separate explicit-constructor business services. Infrastructure interfaces remain beside IServices.cs. |
| Services/ServiceModels/*ViewModels.cs and *Models.cs | Ten feature files group annotated inputs, typed results and display data. No controller namespaces or mutable entity form binding. |
| Services/Helpers | Shared Manila-time/display/audit construction; no general workflow engine. |
| Services/Workers | Background expiry, using the same ReservationService. |
| Resources/Constants, Messages, Views | Roles/enums/limits, feature messages and workspace labels. |
| WebApp/Controllers and Mvc | Explicit HTTP behavior; business controllers inherit ControllerBase<T>. |
| WebApp/Models | Web-only page composition, navigation and rendering helpers. |
| WebApp/Infrastructure | Typed session adapter and modal/auth-response transport helpers. |
| WebApp/Startup.DI.cs | Authoritative explicit application registrations; shared scoped context/unit of work. |
| WebApp/Startup.AutoMapper.cs | Explicit validated read-model mapping configuration. |

## Reference feature: categories

[AdminCategoriesController](../CampusGear/CampusGear.WebApp/Controllers/AdminCategoriesController.cs) binds [CategoryEditModel](../CampusGear/CampusGear.Services/ServiceModels/CategoryViewModels.cs) directly under the existing Input prefix. It calls [CategoryService](../CampusGear/CampusGear.Services/Services/CategoryService.cs), which validates, locks, checks row version and category rules, stages an audit and chooses save/commit. [CategoryRepository](../CampusGear/CampusGear.Data/Repositories/CategoryRepository.cs) performs focused database operations through BaseRepository. One declared read mapping creates CategoryListItem; Web reuses it without another row-copy layer.

Change a business rule in the service, ordering/filtering in the repository, form layout in Razor, and interaction in JavaScript. Keep those owners consistent.

## Important service boundaries

- AuthenticationService: credentials, lockout, administrator challenge and logout. RegistrationService: pending borrower creation. PasswordResetService: reset-code/grant/password lifecycle.
- UserService: administrator account/role/eligibility policy. AuditService: audit browsing and account audit writes.
- EquipmentCatalogService: catalog/form/home reads. ReservationRequestService: quantity, retries and compensating cancellation.
- ReservationService: the single authoritative create/approve/reject/cancel/release/return/expire implementation.
- CustodianQueueService: approval/return/history reads and details. BorrowingHistoryService: borrower-owned history. AvailabilityCalendarService: separate borrower/staff calendar projections.
- EquipmentAvailabilityService and its neutral repository: shared item states, picker and advisory free-unit selection. The core service still rechecks conflicts under lock.

## Rules to explain during defense

- Adjacent intervals are allowed; overlapping intervals conflict using start < other end and end > other start.
- Pending holds expire at the earlier of 24 hours and the requested start.
- Borrower eligibility is reloaded under the appropriate lock. Catalog availability cannot authorize a booking by itself.
- Approval and release differ: release creates the actual loan, during the approved interval.
- Early returns affect calendar display using actual return time. Damaged/unserviceable returns create maintenance.
- Quantity submission commits per unit and compensates failures; it is not one atomic batch transaction.
- Shared scoped context keeps workflow changes and audits atomic where the original workflow uses a transaction.
- Auth uses Identity hashes/stamps/cookies. Typed session state preserves missing versus empty unknown-email markers.
- Modal forms keep their normal-navigation fallback. The service returns business results; Web decides HTTP status and view.

## Checks

Run dotnet build/test on CampusGear/CampusGear.sln, the EF pending-model check and node figma-reference/verify.cjs. Tests use disposable SQL databases. EndpointCompatibilityTests freezes 63 original public contracts independently of filenames; BasecodeConformityTests checks inheritance, public contracts, scoped resolution and mapper configuration. See [current evidence](BASECODE_REFACTOR_PROGRESS.md) and [dependency notes](DEPENDENCIES.md).
