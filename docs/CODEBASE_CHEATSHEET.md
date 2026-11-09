# CampusGear developer cheatsheet

Start here when you do not know the code. This guide describes the implemented application, not the older signup-code prototype.

## How to follow a click

1. Open the screen's Razor file in WebApp/Views. Search its form action, link, or JavaScript fetch URL.
2. Search that URL in WebApp/Controllers. Read its HTTP verb, role attribute and binding model.
3. Follow the injected service interface into Services/Services. This is where the business decision belongs.
4. Follow the service's repository interface into Data/Repositories. This is where the SQL query/persistence belongs.
5. Check ApplicationDbContext for the table/relationship and the feature tests for expected behavior.
6. Follow the returned DTO/outcome back to the controller, then the view/JSON/redirect.

Use [feature traces](FEATURE_TRACES.md) for actual routes/methods and [the complete source catalog](SOURCE_CATALOG.md) for every maintained C#, Razor, JavaScript and CSS file, including callers, dependencies, resources and relevant tests.

## Folder ownership

| Folder | What to change here | Example |
|---|---|---|
| WebApp/Controllers | HTTP binding, authorization, antiforgery, responses | AdminCategoriesController.Save maps Input fields and field errors |
| WebApp/Models | Annotated forms and display wrappers | Accounts/AdministrationInputs.cs retains Required and StringLength |
| WebApp/Views | HTML, visible labels and accessible controls | Maintenance/Index.cshtml renders cases and dialogs |
| WebApp/wwwroot/js and css | Progressive interactions and presentation | modal-forms.js handles fetch feedback; CSS controls layout |
| WebApp/Infrastructure | HTTP transport adapters | AccountFlowStore reads/writes the existing session |
| WebApp/ViewComponents | Small server-rendered UI adapters | WorkspaceIdentity gets safe name/initial DTOs |
| Services/Interfaces | Application contracts | IInventoryService.SaveAsync accepts a command and upload stream descriptor |
| Services/Services | Business decisions and workflow coordination | InventoryService compensates failed saves by deleting the new photo |
| Services/ServiceModels | Commands, results and safe DTOs | OperationResult, ReservationCommand, AccountFlow |
| Data/Interfaces | Persistence contracts | IReservationRepository.FindByRequestIdAsync |
| Data/Repositories | EF queries, SQL filtering/order/paging, tracked writes | ReservationRepository.HasConflictAsync |
| Data/QueryModels | Data-owned projections/filter contracts | EquipmentRecord is immutable; internal tracked query results stay inside services |
| Data/Models | Tracked database entities | Reservation, BorrowerProfile, Loan |
| Data/ApplicationDbContext and Migrations | Schema configuration and generated history | Rowversions and unique indexes |
| Resources/Constants | Stable shared enums and limits | ReservationStatus and BorrowingLimits |
| IntegrationTests | Executable behavior and layer checks | ReservationServiceSqlTests races independent SQL scopes |

Physical project ownership determines the layer. Existing entity/enum namespaces and some EF-free read DTO namespaces (CampusGear.Controllers / CampusGear.Models) were retained for Razor/source compatibility. These names do not mean Services references the WebApp assembly. New plain commands use CampusGear.Services.ServiceModels. Data never references Services or WebApp.

## The reference feature: categories

Start with [AdminCategoriesController](../CampusGear/CampusGear.WebApp/Controllers/AdminCategoriesController.cs), [CategoryService](../CampusGear/CampusGear.Services/Services/CategoryService.cs), and [CategoryRepository](../CampusGear/CampusGear.Data/Repositories/CategoryRepository.cs).

The page binds Input.Id, Input.Name, Input.Description, Input.IsActive and Input.RowVersion. The controller validates the form and maps to CategoryCommand. SaveAsync normalizes/validates, acquires the existing category lock, checks the submitted version and active-item restriction, creates the audit content and chooses save/commit timing. Repository methods materialize queries and attach/remove entities without independently committing. OperationResult carries field errors back to the same HTML inputs. The controller selects the existing view or redirect.

To add a category business rule, edit the service and add a behavior test. To change search ordering, edit the repository. To move a button, edit Razor/CSS. Do not put SQL into the controller just because it can reference Data for startup wiring.

## Worked trace: register and approve a borrower

Screen: [Account/SignUp](../CampusGear/CampusGear.WebApp/Views/Account/SignUp.cshtml).
POST /auth/signup binds SignUpInput: FirstName, LastName, StudentIdNumber, Email, MobileNumber, Password, ConfirmPassword.

AccountController.SignUp keeps form validation and maps to SignUpCommand. AccountService.SignUpAsync checks email and student-number duplication through Identity/AccountRepository, creates a PendingApproval user using Identity, assigns Borrower, and adds BorrowerProfile. Creation/role/profile commit together. The success audit happens after that commit, matching the original behavior.

Tables: AspNetUsers, AspNetUserRoles, AspNetRoles, BorrowerProfiles, then AuditEvents. Result: redirect to /auth/registration-submitted. No signup code is sent and there is no live email-verification route.

An administrator opens /admin/users?status=pending and posts /admin/users/{id}/approve. AdminUsersController → UserAdministrationService.DecideAsync → AccountRepository.FindAsync + Identity.UpdateAsync → Approved and EmailConfirmed=true → audit/save → safe local redirect. The borrower can now log in; pending/rejected status is disclosed only after a correct password.

Checks: AccountRegistrationTests, AccountRoleManagementTests, SignUpInputValidationTests, RolePermissionTests.

## Worked trace: request two units

Screen: [Borrower/Reservation](../CampusGear/CampusGear.WebApp/Views/Borrower/Reservation.cshtml); browser interaction: [borrower-catalog.js](../CampusGear/CampusGear.WebApp/wwwroot/js/borrower-catalog.js).
POST /borrower/reservation binds ReservationInputModel: RequestId, EquipmentItemId, Quantity, StartLocal, EndLocal, Purpose and Notes.

BorrowerController.CreateReservation maps to ReservationCommand → BorrowerService.CreateReservationAsync. The service loads the current borrower's own profile, groups catalog units by category/name, validates quantity/time/purpose/eligibility, converts Manila input to UTC, derives deterministic per-unit request IDs, and checks existing submissions. BorrowerReadRepository performs database filtering and candidate-conflict reads.

Each selected physical unit calls ReservationService.CreateAsync with the server actor. It locks/reloads the borrower and equipment, rechecks current eligibility/role/account/status and conflicts, adds one Reservation and audit, saves and commits its own transaction. This is deliberately not one new batch transaction.

A candidate taken after the read is retried. A later handled workflow failure compensates already-created units with CancelAsync. Once unit creation starts, CancellationToken.None preserves the existing behavior when a browser disconnects. Deterministic IDs keep retries idempotent. The outer workflow reloads saved request IDs for simultaneous duplicate forms.

Tables: BorrowerProfiles/AspNetUsers/roles, EquipmentItems/Categories, Reservations/Loans/MaintenanceCases for availability, then Reservations/AuditEvents. Success redirects with equipmentItemId, state=submitted and repeated id parameters; the GET re-queries owned IDs. Errors redisplay the dialog; availability JSON retains bookableUnits/freeUnits/maxQuantity.

Checks: ReservationSubmissionTests, ReservationServiceSqlTests, BorrowerOrchestrationTests and RefactorCharacterizationTests. SQL locks remain authoritative even if the browser previously displayed availability.

## Worked trace: approve, release and return

Screen: [Custodian/Details](../CampusGear/CampusGear.WebApp/Views/Custodian/Details.cshtml).
CustodianController's Approve, Reject, Release and RecordReturn actions retain their original POST routes/form fields and Custodian-only attributes. Read their exact routes in the [route inventory](FEATURE_TRACES.md).

The controller parses the posted condition/reason/notes and calls the existing IReservationService methods. ReservationService loads the reservation through ReservationRepository, takes the original locks, rechecks current state/eligibility and performs the transition. LoanRepository owns loan insertion/outstanding-loan queries; EquipmentRepository owns item writes; MaintenanceRepository opens a repair when necessary; AuditRepository records the action.

Release requires an approved request inside its interval and no outstanding previous loan. Return records condition/time/staff; Damaged or Unserviceable automatically sets a maintenance hold and opens a case. Repeated release/return keeps existing idempotent behavior. The controller maps ReservationWorkflowException codes to the existing 403/404 or TempData redirect; public reservation method signatures/error codes are unchanged.

Tables: Reservations, Loans, EquipmentItems, BorrowerProfiles, AspNetUsers/roles, MaintenanceCases and AuditEvents. CustodianService.GetDetailsAsync returns immutable nested display records. Administrators can read details/history/calendar but cannot perform these transitions.

Checks: ReservationServiceSqlTests, ReleaseAvailabilityTests and RolePermissionTests.

## Worked trace: close maintenance

Screen/button: Maintenance/Index repair dialog; modal-forms.js progressively enhances it.
POST /admin/maintenance/{id}/update binds MaintenanceUpdateInput: RowVersion, Status, Condition and Resolution.

MaintenanceController.Update → MaintenanceService.UpdateAsync → MaintenanceRepository.GetItemIdAsync/FindWithItemAsync and EquipmentRepository → equipment lock, rowversion check, transition validation. Closing needs repair notes and a usable condition. HasOpenAsync excluding the closing case decides whether another case keeps the hold active. The service records staff/time/item state and audit and commits together.

Tables: MaintenanceCases, EquipmentItems and AuditEvents; active-loan checks read Loans. Fetch returns the existing ok/message/field object: success 200, validation/stale 422, missing 404. Plain forms redirect to /admin/maintenance with TempData feedback.

Checks: Round3BehaviourTests and stale-maintenance RefactorCharacterizationTests.

## Worked trace: edit an equipment photo

Screen: [AdminInventory/Equipment](../CampusGear/CampusGear.WebApp/Views/AdminInventory/Equipment.cshtml).
POST /admin/equipment/save binds Input.* plus imageFile. The controller retains the 8 MiB request limit and opens/disposes IFormFile's stream. It maps scalar fields to EquipmentCommand and passes UploadContent(stream,length), not an HTTP file object.

InventoryService.SaveAsync validates category/size, asks IEquipmentImageStore to save the new image first, then opens the existing transaction and takes item/category locks in the original order. It revalidates category, loan restrictions and rowversion, changes the record, optionally creates maintenance, adds the audit, saves and commits. On handled persistence failure the new image is removed; after success the replaced image is removed. RemoveImage clears the saved path and removes the old image after commit.

EquipmentImageStore accepts supported leading bytes and at most 5 MiB, generates its own filename and only deletes trusted bare filenames under /uploads/equipment. Data stores the relative path, never image bytes. Tests use in-memory stores or a unique temporary directory, not existing uploads.

Tables: EquipmentItems, EquipmentCategories, MaintenanceCases and AuditEvents; restriction checks read Reservations/Loans. Invalid image/unique asset/serial/stale state returns the original field feedback. See the report's explicitly retained pre-existing failure limits.

Checks: InventoryCompensationTests, EquipmentImageStoreTests, RefactorCharacterizationTests.

## Worked trace: forgotten password

Screen: Account/PasswordResetRequest → POST /auth/password-reset-request, EmailInput.Email.
AccountController → AccountService.PasswordResetRequestAsync → Identity.FindByEmailAsync and IAccountFlowStore. A real active account stores pending ID and security stamp; an unknown/inactive account stores the empty marker. Missing marker and empty marker must remain distinct. Both responses redirect to the same code screen with the same anti-enumeration notice.

EmailChallengeService.IssueAsync → EmailChallengeRepository: serialize issuance, invalidate older challenges, save/commit, then send. Delivery failure invalidates the unsent challenge so it can be retried. Codes/credentials are never audit data.

POST /auth/password-reset-code, CodeInput.Code → PasswordResetCodeAsync → stamp check, VerifyAsync, account reload/stamp check → consumed challenge and ten-minute authorized session grant. POST /auth/password-reset-new, NewPasswordInput.Password/ConfirmPassword → PasswordResetNewAsync → Identity.GeneratePasswordResetTokenAsync/ResetPasswordAsync → audit, clear authorized keys, sign out, redirect /auth/success.

Tables: AspNetUsers, EmailChallenges, AuditEvents. Challenge state persists; the flow session is in memory and clears on restart/expiry. Changed security stamps revoke stale flows. Session keys are implemented by WebApp/Infrastructure/AccountFlowStore without changing their names.

Checks: PasswordResetTests, AccountFlowIsolationTests and AccountRegistrationTests.

## How reads and screens differ

- Borrower queries always scope owned records by profile ID. Calendar busy slots intentionally omit borrower identity.
- Staff reports use their existing status labels and date validation; dashboard/chart periods have their original meanings. Do not merge similarly named counts without comparing semantics.
- Equipment status precedence is inactive → borrowed → repair → reserved → available. EquipmentAvailabilityService calculates; ItemStatuses.Label and FigmaUi choose presentation.
- Repository listing methods apply search/filter/order/Skip/Take before materializing. Services map results; never fetch the whole table just to implement paging.
- Read-only DTOs are not tracked entities. ReservationRecord has immutable nested account/loan/equipment records so Razor cannot mutate persistent state.
- Geometry, icons, labels and DOM state stay in WebApp. Generated Figma frames are reference screens, not a second backend.

## Vocabulary using this project

| Concept | CampusGear meaning |
|---|---|
| DTO | A data transfer object: CurrentAccountDisplay exposes name/initial strings instead of handing Razor an ApplicationUser |
| Command | Plain input to a service, e.g. EquipmentCommand; no HTTP result or DbSet inside |
| Interface | A promise of operations, e.g. ICategoryService; callers do not need its implementation details |
| Dependency injection | Program registers AddCampusGearData/AddCampusGearServices; constructor parameters receive scoped implementations |
| Scoped | One instance per request/scope; repositories, UnitOfWork and Identity stores share the same ApplicationDbContext |
| async / await | Wait for database/email/file I/O without blocking a worker thread; callers still wait for the operation's result |
| CancellationToken | Signals stopped work; reads/worker honor it, established multi-unit creation/compensation deliberately uses None after allocation begins |
| EF tracking | The shared context remembers changes to entities returned for writes; save persists them. Read DTOs/AsNoTracking do not participate |
| Repository | Focused SQL/persistence operations such as HasOutstandingLoanAsync; does not own whether borrowing is allowed |
| Service | Decides whether an action is allowed and coordinates repositories, locks, transactions, audits and side effects |
| Unit of Work | Save/clear tracking/transaction adapter around the shared context; repository Add is not an implicit commit |
| Transaction | A database all-or-nothing boundary chosen by the service. A file or sent email is outside the SQL transaction |
| Compensation | Undo an external/previous effect: delete a new photo after failed save, cancel earlier units after a handled batch failure |
| Rowversion | SQL-generated binary edit token on equipment/categories/profiles/maintenance; base64 is posted to detect stale forms |
| Concurrency stamp | Identity's account edit token, distinct from the security stamp |
| Security stamp | Identity credential/access revocation marker; cookies and pending reset/admin flows recheck it |
| SQL application lock | A named transaction-owned lock coordinates workflows, e.g. CampusGear:EquipmentItem:{id:N}; ten-second timeout |
| Isolation | Services choose ReadCommitted or Serializable; Data implements the provider transaction |
| Identity | Existing hashing, roles, lockout, token generation and EF stores; never replace these with manual password SQL |
| ModelState | Web binding/annotation errors and service field errors shown against the original form field names |
| TempData | Short-lived HTTP feedback across a redirect; not the service's state store |
| OperationResult | Outcome, value, field errors/message and reload flag; each controller maps to its existing HTTP behavior |
| RequestContext | Actor ID, IP and correlation ID created from the server request; actor identity is never posted by the form |
| Audit | Action/outcome/entity plus actor/IP/correlation and relevant before/after values; preserve existing transaction association |

## Changing something safely

Find a feature trace and read the current test first. Change one layer's responsibility at a time. Update its service/DTO/repository contract explicitly and keep form names/routes/status codes/messages stable. Run the focused feature tests, then the complete suite when the change crosses features. Regenerate SOURCE_CATALOG with python scripts/update-code-map.py. Keep documentation in the same commit.

Program/DbInitializer, migrations, explicit DemoData and the built-in Identity EF store are infrastructure exceptions. There is no exception allowing a controller/view/helper to query the database. AccountService's Identity/session adapters are application abstractions; services do not use MVC or EF APIs.

## Verification and troubleshooting

Run scripts/smoke-live.ps1 from the repository root; it runs Figma checks and the current integration suite with disposable SQL databases. It does not require a normal host or real SMTP. Set CAMPUSGEAR_TEST_SQLSERVER only to select a test server; fixtures replace its database name.

If a controller cannot resolve a service, check both registration extensions and its interface. If a stale form returns, reload its current version rather than discarding the server check. If an item seems unavailable, trace status precedence, active loan, open repair, approved interval and pending hold expiry separately. If a reset fails, compare flow presence/expiry/security stamps before touching email settings.

See [README](../README.md) for startup, administrator bootstrap, secrets/email and deployment limitations. See [refactor report](SERVICE_REPOSITORY_REFACTOR_REPORT.md) for evidence, exceptions and retained risks.
