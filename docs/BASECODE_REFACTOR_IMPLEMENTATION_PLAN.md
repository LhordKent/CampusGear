# CampusGear: detailed plan for a traceable ASI-style refactor

Prepared 14 November 2026. Status: application refactor implemented and verified locally on codex/basecode-traceability-refactor; see [the implementation report](BASECODE_REFACTOR_PROGRESS.md) for actual results, adaptations and remaining licensing/deployment/rehearsal limits. The user confirmed that the next defense is next week; the exact day and available team time are still unknown. The numbered phases below remain the original planning/acceptance record.

Revision: incorporated all five pages, including code screenshots, of `C:\Users\Admin\Downloads\Basecode_short_guide.pdf`. The whole application remains in scope. The guide's repository base, MVC base and central DI conventions are now explicit requirements rather than optional choices.

## 1. What this refactor must accomplish

The panel's complaint is about code that the developers cannot trace or explain. The goal is therefore a conventional, predictable application that follows the supplied ASI basecode and that each team member can defend from the actual source. Four project folders alone do not achieve that goal.

A student should be able to start with a button, find the HTTP action, identify the service method containing the rule, identify the repository query, and explain what is saved. They should also be able to explain invalid input, forbidden access, conflicting bookings and failed saves. A feature fails this refactor's acceptance criteria if only its original implementer or an AI can explain it.

The recommended approach is an incremental refactor of CampusGear. Do not transplant the application into an empty copy of the sample and re-create its behavior from memory. CampusGear already has useful tests, transaction protections and working workflows. Retain those as the safety net while making its implementation simpler.

The implementation must cover the whole maintained application: production C#, Razor views, application JavaScript/CSS, dependency injection, configuration, tests, seeds and current documentation. Covering everything does not mean rewriting everything. Well-scoped repositories and useful components can remain after their names, contracts and behavior are checked.

### Working assumptions

1. Use the supplied repository and supplemental guide together: all business features follow the Controller → Service → Repository path, with `BaseRepository`, public `I...Repository` / `I...Service` contracts, a custom `ControllerBase<T>`, ordinary constructor injection and central `Startup.DI.cs` registration. The guide's controller constructor also includes AutoMapper infrastructure. Authentication internals remain the existing Identity implementation.
2. Existing role permissions and current user flows are the behavior baseline. A deliberate bug fix is a separate, explicitly tested change.
3. Keep CampusGear's current .NET target and unrelated dependency versions during structural work. Add the guide's mapper dependency with compatibility checked against the current target. The reference targets .NET 9 while CampusGear targets .NET 8. A runtime upgrade needs its own compatibility decision and validation; do not combine it with this refactor.
4. Preserve existing users, password hashes, records, uploads, migration history and local development configuration.
5. Keep the current main-branch admin modal forms. Earlier full-page screenshots are historical documentation, not the implementation target.
6. Implementation should use a `codex/` feature branch. This planning session does not create an implementation branch, commit, push or deployment.

The exact defense day and available team time could change execution details. The user has confirmed next week, so Section 13 makes the defense deadline the scheduling constraint. The supplement resolves the previously open structural questions about base repositories, base controllers and DI placement. The small implementation adaptations below are recorded explicitly.

## 2. Evidence and starting baseline

### Inspected revisions

| Repository | Inspected revision | Meaning |
|---|---|---|
| CampusGear | `79baba6625a55990205a9d1169488ceac9c8ef79` | Local main at the start of planning; commit time 2026-11-14 13:39 +08:00. |
| ASIBasecodeCsharp | `2df3206f3a520bd55e759b1c3e70d1bbb608e6e5` on `master` | Reference snapshot inspected for actual implementation conventions. |
| Supplemental short guide | `C:\Users\Admin\Downloads\Basecode_short_guide.pdf`, five pages | Text extracted and every page rendered/visually inspected, including image-only constructor and registration examples. |

Supplemental file SHA-256: `95535122DA952B66BA2BB6B5F6926B2FA40CCA2B42240CF1842075C39066E2E5`.

Reference sources: [project and README](https://github.com/keder-asi/ASIBasecodeCsharp/tree/2df3206f3a520bd55e759b1c3e70d1bbb608e6e5), [UserService](https://github.com/keder-asi/ASIBasecodeCsharp/blob/2df3206f3a520bd55e759b1c3e70d1bbb608e6e5/ASI.Basecode.Services/Services/UserService.cs), [UserRepository](https://github.com/keder-asi/ASIBasecodeCsharp/blob/2df3206f3a520bd55e759b1c3e70d1bbb608e6e5/ASI.Basecode.Data/Repositories/UserRepository.cs), [dependency registration](https://github.com/keder-asi/ASIBasecodeCsharp/blob/2df3206f3a520bd55e759b1c3e70d1bbb608e6e5/ASI.Basecode.WebApp/Startup.DI.cs).

### Fresh checks during this planning session

The latest tracked revision was tested in a detached temporary worktree so that rebuilding did not disturb the normal working directory or the running demo process.

| Check | Result | What it establishes |
|---|---|---|
| Solution test run, including build | 215 passed; zero warnings reported | Current tracked source builds and passes its existing automated suite. |
| Figma reference verifier | 70 frames; 127 referenced local image files; zero failures | Saved reference assets are internally consistent. |
| EF pending-model check | No model changes since last migration | Current tracked model and migration snapshot agree. |
| Latest app in a browser | Not exercised during this planning audit | Do not treat the automated checks as a fresh visual or live-login verification. |
| Real SMTP delivery | Not tested | The local Development mailbox is not proof of Gmail delivery. |

The normal process on port 5264 was not restarted for this audit and can still serve an older build. Its screenshots cannot be used to claim the latest modal implementation was visually tested.

### What is already good

- The four application projects already exist, with sensible project-reference directions.
- Controllers have already been separated from direct EF database access.
- Data repositories contain materialized query operations instead of exposing arbitrary query providers to controllers.
- Reservation changes have transaction-owned SQL application locks and idempotency handling.
- Identity already handles password hashes, sign-in, lockout and security stamps.
- The integration suite covers several important permission, race, persistence and rollback behaviors.
- Existing route maps and source catalogs provide useful raw material for new human-readable traces.

These should be improved and retained. A rewrite that loses them would make the system harder to defend.

### Confirmed structural problems

Paths in this document are relative to `C:\CampusGear` unless an absolute path is shown.

| Evidence in current source | Why it obstructs understanding | Planned treatment |
|---|---|---|
| Data entities under `CampusGear/CampusGear.Data/Models` declare `CampusGear.Models`. | The namespace does not identify the owning layer. | Align with `CampusGear.Data.Models`; check EF snapshot effects carefully. |
| Several `Services/ServiceModels/*Models.cs` declare `CampusGear.Controllers`. | A service contract looks like web-controller code even though it is compiled into Services. | Move to truthful `CampusGear.Services.ServiceModels` namespaces and feature-specific model files. |
| Service interfaces and implementations commonly share `CampusGear.Services` despite separate physical folders. | Looking at an import gives little indication of the dependency type. | Use `.Services.Interfaces` and `.Services.Services`, matching the reference's convention. |
| `AccountService.cs` is about 501 lines and handles login, registration, two-factor, password reset and logout. | Several distinct lifecycles and session states are intertwined. | Split by authentication, registration and password reset; retain shared challenge mechanics. |
| `BorrowerService.cs` is about 501 lines and handles catalog, request orchestration, history and calendars. | Page construction and write orchestration share one large entry point. | Extract cohesive catalog, request, history and calendar services. |
| `UserAdministrationService.cs` is about 421 lines and also reads audit pages. | Account management and audit browsing have different responsibilities. | Keep cohesive account management; extract audit reading. |
| `ReservationService.cs` is about 577 lines. | Critical rules need clearer organization and names; dependency count warrants review. | Keep a single owner for state transitions. Improve its internal readability before considering further splits. Length alone is not a reason to fragment it. |
| Category editing has a Web input, service command, Data list record, service page, Web row and Web page. | Several shapes copy the same fields without adding meaning. | Use one allowlisted service edit model; one Data projection → service list mapping; reuse service list items in Web. |
| `AccountFlow` carries general step/path/transition information and `AccountController.Map` chooses screens generically. | A reader must understand an extra routing protocol before understanding login. | Return typed business outcomes and write explicit web responses in each action. |
| Ordinary workflows use `OperationResult`, reservations use typed workflow exceptions, and some expected failures use `InvalidOperationException` or private control-flow exceptions. | A caller must discover a different failure convention in each feature. | Make the few remaining conventions intentional, documented and consistent. |
| `CustodianService.GetListAsync` accepts action/title strings and several filter arguments. | Presentation navigation leaks into application queries. | Named approval/return/history methods with typed filters. |
| `CustodianTime` combines time conversion, status text and reservation-related calculations. | A harmless-looking formatter becomes a hidden rule owner. | Separate time display from actual reservation policy; keep rules with the responsible service. |
| Resources currently has only two small constants files. | The Resources project exists but is not used like the reference's messages/labels layer. | Introduce purposeful messages and labels as features are migrated. |
| Existing architecture tests search specific old namespaces; HTTP inventory tests are keyed by controller filename. | A legitimate rename/split could silently reduce test coverage or falsely look like a route change. | Update selectors and test route behavior independently of implementation filenames. |
| `FigmaUi` mixes working navigation with reference-frame metadata; several CSS files overlap. | Product behavior and reference/demo artifacts are hard to distinguish. | Separate live navigation from reference metadata; audit actual asset usage before cleanup. |

Line counts are a triage aid, not an acceptance metric. A short expression containing six decisions can be harder to maintain than a longer explicit method.

## 3. What “follow the basecode” should mean

The reference README assigns database operations to Data, application processing to Services, messages/labels to Resources, and MVC controllers/views to WebApp. Its user feature is easy to locate: `IUserService → UserService → IUserRepository → UserRepository`.

| Reference convention | CampusGear decision | Reason |
|---|---|---|
| Four named projects | Adopt the same four layer responsibilities. | Familiar to the panel and already compatible with this solution. |
| `Services/Interfaces`, `Services/Services`, `Services/ServiceModels` | Adopt truthful namespaces and predictable names. | Makes code location match the reference. |
| `Data/Interfaces`, `Data/Repositories`, `Data/Models` | Adopt. | Makes persistence ownership explicit. |
| Ordinary constructors and private readonly fields | Adopt for changed maintained code, then normalize remaining source in a dedicated pass. | Easier for students to read than compressed constructor/statement syntax. |
| Explicit DI registration | Require one authoritative application registration file, `CampusGear.WebApp/Startup.DI.cs`, called by Program. | Matches the guide's registration location; no service scanning or hidden runtime wiring. |
| Service models with validation attributes | Adopt for simple allowlisted input models. | Avoid a second identical Web input/command layer. |
| AutoMapper | Include the guide's `IMapper` infrastructure and explicit `Startup.AutoMapper.cs` configuration; use allowlisted mappings where a mapping is actually needed. | Matches the shown controller base constructor. Critical identity/role/state assignments remain explicit; do not create duplicate DTOs just to use a mapper. |
| `BaseRepository` | Required for every application-owned EF repository under Data/Repositories. Inject the shared unit of work and DbContext through its constructor. | Matches the guide's inheritance convention while preserving current async transactions. No generic CRUD engine or context cast. |
| `ControllerBase<TController>` | Required for business MVC controllers, in WebApp/Mvc, inheriting MVC Controller. Use the shown context/logger/configuration/mapper dependencies and shared current-actor support. | Matches page 5 and gives shared infrastructure a visible home. Do not copy unrelated claims or logging of raw input. |
| `ServiceBase` | Do not require it for ordinary services. | The guide makes BookService implement IBookService; it does not require a service base. |
| Repository calls `SaveChanges` | Adapt: repositories stage changes; service-owned unit of work saves/commits the whole workflow. | A booking, loan, maintenance change and audit entry must not commit separately. |
| `IQueryable<User>` returned by repository | Adapt: keep materialized focused queries. | Prevent business queries from drifting back into controllers/services. |
| Sample authentication and encryption | Do not transplant. Keep Identity and existing role/session controls. | The inspected sample login constructs a dummy user; its password helper encrypts/decrypts passwords reversibly. |
| Sample framework target | Decide separately. | Avoid mixing structural changes with a framework migration. |

The authentication observations are specific to the pinned reference: [AccountController](https://github.com/keder-asi/ASIBasecodeCsharp/blob/2df3206f3a520bd55e759b1c3e70d1bbb608e6e5/ASI.Basecode.WebApp/Controllers/AccountController.cs), [PasswordManager](https://github.com/keder-asi/ASIBasecodeCsharp/blob/2df3206f3a520bd55e759b1c3e70d1bbb608e6e5/ASI.Basecode.Services/Manager/PasswordManager.cs). They explain narrow implementation differences; they are not a reason to disregard the reference structure.

Use this table and the per-feature checklist in Section 17 as the conformity sheet: requirement, corresponding CampusGear file, and reason for any adaptation. Preserve Identity and atomic database workflows when following the sample's structure.

### Supplemental guide: exact requirements and implementation adaptations

The guide's construction order is Repository → Service → Controller. Runtime request direction is Controller → Service → Repository. Keep both explanations in the defense materials.

| Guide location | What it demonstrates | Required CampusGear treatment |
|---|---|---|
| Page 1 | SQL Server/table setup and rebuilding before adding the feature. | Verify existing SQL setup, mapped tables and clean build; existing data stays intact. |
| Page 2 | Scaffolding into Data/Models and using the correct context name throughout the solution. | Verify model/table/context consistency. If reverse engineering is needed, use a restored disposable database and scratch output, then compare. Do not overwrite the working Identity context or current models with the example's force-scaffold command. |
| Page 3 | Public `IBookRepository` in Data/Interfaces and `BookRepository : BaseRepository, IBookRepository` in Data/Repositories, using a unit of work. | Apply the equivalent pattern to all thirteen current EF repository classes and any genuine new repository. |
| Page 4 | Scoped repository registration and public `IBookService` / `BookService`, with the repository injected into the service. | Central explicit registration; services use repository interfaces and explicit private readonly fields. |
| Page 5 | Scoped service registration and `BookController : ControllerBase<BookController>`, injecting `IBookService` plus shared base dependencies. | Apply the equivalent custom base/controller-service pattern to every business MVC controller, including split authentication controllers. |

The page-2 context replacement wording is ambiguous for an existing application. Resolve it by preserving one authoritative `ApplicationDbContext` and updating deliberate references, never by deleting the working context. The PDF is reference material for the plan; its scaffold commands have not been executed.

Three narrow adaptations are intentional: (1) repositories stage changes and services commit the shared workflow; (2) `BaseRepository` receives the scoped `ApplicationDbContext` explicitly alongside `IUnitOfWork`, rather than exposing EF through the service-facing unit-of-work interface; (3) existing migrations/Identity are preserved instead of rebuilding the application by force-scaffolding. These do not change the guide's layer chain, inheritance conventions or DI location.

## 4. Target architecture and file structure

The normal feature path must stay visible:

```text
Razor form / JavaScript request
    → WebApp controller action
    → Services interface + concrete service method
    → Data repository interface + concrete query/change
    → shared ApplicationDbContext / SQL Server

Controller chooses HTTP response and view.
Service makes business decisions and controls the transaction.
Repository executes database operations.
Resources supplies shared messages, labels and role constants.
```

### Target folders

The following is the intended organization, not an instruction to create every proposed file before it has a real responsibility. Existing scoped repositories should normally be retained and renamed in place where appropriate.

```text
CampusGear/
  CampusGear.Data/
    Models/                         persisted entities
    Interfaces/                     repository, unit-of-work and lock contracts
    Repositories/                   focused EF queries and staged changes
      BaseRepository.cs             shared Data-only context + unit-of-work access
    QueryModels/                    materialized database projections
    Migrations/                     existing migration history
    ApplicationDbContext.cs
    UnitOfWork.cs
    WorkflowLockManager.cs
    AccountUserStore.cs
    DbInitializer.cs
    DemoData.cs

  CampusGear.Resources/
    Constants/                      roles, enums, real shared limits
    Messages/                       feature error/success resources
    Views/                          shared field/menu labels where useful

  CampusGear.Services/
    Interfaces/
      IAuthenticationService.cs
      IRegistrationService.cs
      IPasswordResetService.cs
      IEmailChallengeService.cs
      IUserService.cs
      IAuditService.cs
      ICategoryService.cs
      IInventoryService.cs
      IMaintenanceService.cs
      IReservationService.cs
      IReservationRequestService.cs
      IEquipmentCatalogService.cs
      IEquipmentAvailabilityService.cs
      IBorrowingHistoryService.cs
      IAvailabilityCalendarService.cs
      ICustodianQueueService.cs
      IDirectoryService.cs
      IDashboardService.cs
      ICurrentAccountService.cs
      ...                           existing email/image/flow-store contracts
    Services/
      AuthenticationService.cs
      RegistrationService.cs
      PasswordResetService.cs
      UserService.cs
      AuditService.cs
      CategoryService.cs
      InventoryService.cs
      MaintenanceService.cs
      ReservationService.cs
      ReservationRequestService.cs
      EquipmentCatalogService.cs
      EquipmentAvailabilityService.cs
      BorrowingHistoryService.cs
      AvailabilityCalendarService.cs
      CustodianQueueService.cs
      DirectoryService.cs
      DashboardService.cs
      ...                           retained focused implementations
    ServiceModels/
      Accounts/                     typed input/output models
      Categories/
      Inventory/
      Reservations/
      Reports/
      Common/                       operation result + request context only
    Helpers/                        a small number of genuinely shared pure helpers
    Workers/
      ReservationExpiryWorker.cs
    Mapping/
      ServiceMappingProfile.cs      explicit allowlisted mappings where needed

  CampusGear.WebApp/
    Controllers/                    explicit MVC actions and authorization
    Models/                         Web-only page composition, not duplicate commands
    Views/                          current feature views and partials
    Infrastructure/                 session adapter, HTTP/modal helpers, current actor
    Mvc/ControllerBase.cs            required common base for business controllers
    Configuration/                  explicit startup/Identity/email configuration
    ViewComponents/                 current user header, navigation if needed
    wwwroot/js/                     feature interaction only
    wwwroot/css/                    audited live styles
    Program.cs
    Startup.DI.cs                   authoritative explicit app DI registration
    Startup.AutoMapper.cs           mapping configuration/composition

  CampusGear.IntegrationTests/       retained and strengthened behavior checks
```

Use `CampusGear.Data.Models`, `.Data.Interfaces`, `.Data.Repositories`, `.Data.QueryModels`, `CampusGear.Services.Interfaces`, `.Services.Services`, `.Services.ServiceModels`, `.Resources.Constants`, and `CampusGear.WebApp.Controllers/Models/Infrastructure` as the corresponding namespace roots. Feature subfolders can use matching subnamespaces where they improve discovery; do not nest a simple feature through five folders.

Do not add Domain, Application, Infrastructure and Contracts projects to imitate another architecture. Do not add MediatR, CQRS handlers per action, a service locator, reflection-based DI, a generic “execute workflow” framework or a universal page router. The supplied four-project model is enough for this application.

Retire the current Data/Services `DependencyInjection.cs` registration containers when the central file takes over; do not leave duplicate authoritative lists. The modern Program entry point stays. A file named `Startup.DI.cs` does not require a framework upgrade or a migration to the older Startup hosting model.

### Ownership rules

| Location | Owns | Must not own |
|---|---|---|
| Controller | Routing, authorization attributes, binding, ModelState, HTTP status, view/partial/redirect choice. | EF queries, eligibility decisions, conflict rules, password checking. |
| Service | Workflow validation, role/ownership checks for direct service calls, business rules, orchestration, transaction boundaries, audit intent. | View names, TempData, ModelState, `IActionResult`, raw SQL/EF queries. |
| Repository | EF queries, tracked entity loading, projection, staging adds/removes. | Screen flow, role policy, starting independent commits for a multi-step workflow. |
| Unit of work | Shared persistence save, transaction lifecycle and safe provider-error translation. | Deciding whether an action should be allowed. |
| Resource | Stable labels, messages, role names and actual shared constants. | Database access or runtime workflow state. |
| Razor/JavaScript | Rendering and interaction, client convenience validation, modal state. | Authoritative access control, booking conflict decisions or persisted status transitions. |

Services may use `UserManager`/`SignInManager` for Identity. Controllers may not. Do not introduce an `IdentityFacade` that simply renames every Identity method without adding a real boundary.

## 5. Coding conventions that make the source explainable

1. Use explicit constructors and named private readonly fields for services, repositories and controllers. Use meaningful parameter names, not `x`, `work` and `reads` across complex methods.
2. Prefer one top-level model or class per file when it has its own purpose. A small tightly related enum can stay with its result type. Do not create dozens of tiny record files mechanically.
3. Use braces and one meaningful statement per line. Expand compressed `try { ... } catch { ... }` blocks and ternaries containing business decisions.
4. Name service methods after the user action: `ApproveAsync`, `ReturnAsync`, `GetApprovalQueueAsync`. Avoid strings such as `action="returns"` selecting hidden behavior.
5. Use guard clauses for invalid/forbidden states, followed by a visible successful path. Do not wrap every method in `try/catch(Exception)`.
6. Helpers must name a real concept. `RequireEligibleBorrowerAsync` is useful. `ProcessAsync`, `ExecuteOperation` and `BuildEverything` hide responsibilities.
7. Comments explain constraints and reasons: lock ordering, half-open intervals, security stamp changes and compensation. They should not narrate obvious assignments.
8. Use async database operations and pass cancellation tokens deliberately. Do not copy the sample's synchronous calls into working async workflows.
9. Map allowlisted fields only. Critical identity/role/state writes remain explicit; declared AutoMapper profiles handle straightforward mappings that are actually needed. Client input never chooses audit actor, approval status, password hash, equipment loan status or an account role outside the permitted operation.
10. Avoid arbitrary maximum-line rules. Review a method when it mixes responsibilities, requires several unrelated helpers to understand, or cannot be explained by its owner.

### Reduce redundant models deliberately

For each model, ask whether it represents persistence, a service input/output, or Web page composition. If two adjacent models have the same fields and lifetime, keep one. If their security or lifecycle differs, keep both and document the reason.

Category example:

```text
Current:
CategoryInput → CategoryCommand → EquipmentCategory
CategoryListRecord → CategoryPage → CategoryRow → CategoriesPageModel

Proposed:
CategoryEditModel → EquipmentCategory
CategoryListRecord → CategoryListItem → Web page composition
```

`CategoryEditModel` is an allowlisted service model with validation annotations, matching the reference's pattern. The MVC action binds it directly under the existing `Input` prefix. The service independently enforces the relevant input and business constraints. The Web page can wrap the edit model, list result and feedback; it does not copy every list row again.

Keep database entities out of rendered form models. Keep a separate user-create input from user-edit input: password, role assignment and account approval are not interchangeable operations.

### Result and error conventions

- Keep `OperationResult<T>` for expected ordinary workflow failures. Use field errors consistently and translate to ModelState in Web only.
- Use typed authentication outcomes such as `SignedIn` or `RequiresTwoFactor`; Web chooses the next action explicitly. A business outcome must not contain a Razor view name.
- Retain `ReservationWorkflowException` and its specific codes during the high-risk reservation cleanup. It is a documented internal workflow protocol with one consistent transport translation, not a replacement for ordinary CRUD validation. Changing it is optional later work, justified only if callers become simpler.
- Replace expected-business `InvalidOperationException` usage and private exception-based form validation with explicit results as those features are migrated. Unexpected faults still propagate to normal logging/error handling.
- Translate persistence failures once at the Data boundary. Services interpret duplicate/stale/busy cases in their feature context; controllers never inspect SQL exception numbers.
- A rejected modal must display both field errors and a general message when appropriate. Do not lose `result.Message` because the service supplied no field list.

Document the small number of intentional conventions. Do not build a generic mapper that derives HTTP responses or view names from arbitrary enum names.

## 6. The pilot: refactor categories completely before copying the pattern

Category management is a good first vertical slice: it includes a list, form, modal, validation, repository queries, row versions, transactions and audit records, without the full authentication/reservation complexity.

### Pilot files

Existing paths:

- `CampusGear/CampusGear.WebApp/Controllers/AdminCategoriesController.cs`
- `CampusGear/CampusGear.WebApp/Models/Categories/CategoryModels.cs`
- `CampusGear/CampusGear.WebApp/Views/AdminCategories/Index.cshtml`
- `CampusGear/CampusGear.WebApp/Views/AdminCategories/_CategoryEditor.cshtml`
- `CampusGear/CampusGear.Services/Interfaces/IServices.cs`
- `CampusGear/CampusGear.Services/Services/CategoryService.cs`
- `CampusGear/CampusGear.Services/ServiceModels/Categories.cs`
- `CampusGear/CampusGear.Data/Interfaces/IRepositories.cs`
- `CampusGear/CampusGear.Data/Repositories/CategoryRepository.cs`
- `CampusGear/CampusGear.Data/QueryModels/CategoryRecords.cs`

### Exact pilot work

1. Record all current category GET/POST routes, form field names, feedback behavior and response statuses. Establish the guide's common `BaseRepository`, `ControllerBase<T>` and central `Startup.DI.cs` foundation before completing the pilot.
2. Introduce `CategoryEditModel` and list models under truthful Services namespaces. Preserve `Input.Name`, `Input.Id` and row-version binding.
3. Bind the service edit model in Web and delete the now-redundant input/command conversion. Keep Web-only page composition where necessary.
4. Make CategoryRepository inherit BaseRepository, make AdminCategoriesController inherit ControllerBase<AdminCategoriesController>, and expand constructor syntax/decisions. Keep business checks in the service and register both interface/implementation pairs centrally.
5. Make validation errors explicit: name length/required, description length, stale version, active items preventing deactivation, duplicate name and busy record.
6. Move category messages to a feature resource. Do not invent a generic localization subsystem.
7. Retain the transaction around category save and audit. Inspect delete/quick-add separately: delete currently has a different locking path from save. Add a race test before changing that behavior.
8. Preserve `_CategoryEditor`, normal navigation fallback, fetch errors, successful modal closure and list refresh.
9. Update DI, boundary-test namespace selectors, source catalog and the category trace.
10. Have a student explain every method and show the database table and audit entry without consulting generated prose.

### Illustrative target style

This sketch demonstrates naming and dependency visibility. It is not a replacement for the existing concurrency/audit implementation.

```csharp
namespace CampusGear.Services.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkflowLockManager _lockManager;
    private readonly IAuditRepository _auditRepository;
    private readonly TimeProvider _timeProvider;

    public CategoryService(
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        IWorkflowLockManager lockManager,
        IAuditRepository auditRepository,
        TimeProvider timeProvider)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _lockManager = lockManager;
        _auditRepository = auditRepository;
        _timeProvider = timeProvider;
    }
}
```

The save method should read in this order:

```text
Validate allowed input.
Begin transaction.
Acquire the existing category lock.
Load or create category.
Check row version and deactivation rules.
Capture old values; assign allowed new values.
Stage category change and audit event.
Save shared unit of work; commit.
Return saved ID/message.
Translate known persistence conflicts; let unexpected faults propagate.
```

Every return before commit must leave no persisted partial change; transaction disposal/rollback behavior must remain explicit and tested. Do not extract each line into a separate service just to shorten the method.

### Pilot acceptance gate

- Existing category/item list/editor tests pass.
- Valid create/edit, invalid fields, duplicate name, stale row version and deactivate-with-active-items behave correctly.
- Delete/deactivate history behavior is characterized before any fix.
- Modal and full-page fallback produce the expected response and preserve entered values after errors.
- No EF/Identity manager enters the controller; no MVC type enters Services.
- The category repository/controller inherit the required custom bases, and their interface mappings are visible in Startup.DI.cs.
- A second student can trace the feature in approximately three minutes and explain its failure cases in five minutes. These are rehearsal targets, not automated timing tests.
- The team agrees this is simpler than the current slice. If it is harder to trace, revise the pattern before applying it elsewhere.

## 7. Feature-by-feature decomposition

### 7.1 Authentication, registration and password reset

Current entry points: `AccountController`, `IAccountService`, `AccountService`, `AccountFlowStore`, `EmailChallengeService`, and Identity registration in `Program.cs`.

Proposed split:

| Existing methods/responsibility | Target owner |
|---|---|
| `GetLoginAsync`, `LoginAsync`, `GetTwoFactorAsync`, `TwoFactorAsync`, `ResendTwoFactorAsync`, `LogoutAsync` | `IAuthenticationService` / `AuthenticationService` |
| `SignUpAsync` and creating a pending borrower registration/profile | `IRegistrationService` / `RegistrationService` |
| `PasswordResetRequestAsync`, reset-code get/verify/resend, new-password get/save | `IPasswordResetService` / `PasswordResetService` |
| Protected challenge storage, expiry, attempt count, resend cooldown | Existing `IEmailChallengeService` / `EmailChallengeService` |
| Typed session flow access with current keys/lifetimes | Existing flow-store boundary, made explicit and type-safe |
| HTTP action routing and view choice | Authentication/registration/password-reset controllers, retaining existing `/auth/...` URLs |
| Loopback-only Development inbox | Development controller, retaining needed existing URL aliases |

Remove the general `Map(AccountFlow)` protocol after each route is migrated. Do not keep a permanent old `AccountService` that forwards thirteen methods to new services. A temporary adapter, if needed to keep a commit compiling, must have named remaining callers and be removed by the end of the authentication phase.

Keep `UserManager` and `SignInManager`. Preserve password hashes, token-provider configuration, Identity tables and security stamps. There is no reason to migrate existing accounts to the sample's `User` table.

Required behavior checks:

- Approved, active, confirmed accounts can sign in; rejected, pending and inactive accounts cannot.
- Lockout remains five failed attempts for fifteen minutes unless a separately approved requirement changes it.
- Administrator two-factor challenge behavior, ten-minute challenge validity, five attempts and sixty-second resend cooldown remain covered.
- Authentication cookie lifetime remains eight hours; flow session idle limit remains fifteen minutes.
- Two-factor and password-reset flows cannot overwrite each other's state or be replayed after completion/expiry.
- Reset requests avoid revealing whether an unknown email exists.
- New passwords use Identity hashing; role/account changes invalidate stale sessions as intended.
- POST antiforgery, auth rate limits, anonymous fetch/POST 401 behavior and wrong-role 403 behavior remain intact.
- Development-only challenge shortcuts and mailbox visibility remain explicitly environment-limited; tests cover their configuration boundaries.
- Development email and SMTP remain distinct. No test should claim Gmail delivery from an in-memory message.

### 7.2 User administration and directories

Rename `UserAdministrationService` to the reference-familiar `UserService` if there is no naming collision. Keep account creation/editing/approval/deletion together because they share account policy. Extract `GetAuditAsync` into `AuditService`; do not create one service per button.

Retain `AccountRepository`, `BorrowerProfileRepository` and Identity's store for their actual persistence responsibilities. Avoid a new generic repository that re-implements password hashing or role APIs.

Protect self-management restrictions, last-administrator protection, pending registration decisions, role changes, borrower eligibility pauses, concurrent edits, historical records and audit metadata. Review those rules together: a role change and a booking may race even though they come from different screens.

Keep directory reads separate from account mutation. `DirectoryService` can remain if its queries and access constraints are coherent. Use typed filters, readable role constants and distinct create/edit input models.

### 7.3 Inventory and categories

Apply the accepted category pattern to `InventoryService` and `AdminInventoryController`. Keep inventory business rules in the service and EF filters/projections in `EquipmentRepository`.

Preserve category activity checks, item uniqueness, row versions, active-loan restrictions, maintenance restrictions and audit records. Do not let clients set a computed availability label.

Keep `EquipmentImageStore` as the file-storage boundary. The current report identifies an image-write-before-transaction cleanup gap: a new file may be written before transaction creation enters the compensation scope. Handle that as a separate defect fix with a failure test, not a hidden side effect of moving namespaces.

For that fix, make the compensating cleanup cover failures occurring after a new file is created, including transaction-start failure. Preserve the old file until the database change succeeds. Delete only application-owned paths. Test write failure, validation failure, database failure, successful replacement and attempted untrusted paths. Keep current upload size/type restrictions unless requirements change.

### 7.4 Maintenance

Keep `MaintenanceService` cohesive. Normalize its contracts, names and errors rather than splitting each state change into a new class.

Maintain row-version checks, item locks, open-case restrictions, resolution details on closure, permitted returned conditions and the effect of other open maintenance cases. Verify that closing one repair cannot make an item available while another blocking case remains.

### 7.5 Reservation state changes: one authoritative owner

`ReservationService` must remain the authoritative owner of create, approve, reject, cancel, release, return and expiry transitions. Keeping these rules together helps the team explain the lifecycle and prevents different screens from enforcing different policies.

Organize it in lifecycle order, expand construction and conditions, use descriptive local names, and extract only genuine shared concepts. Reasonable helpers include loading an eligible borrower under the required lock, checking a row version and building a safe audit event. An `ApproveHandler`, `ReleaseHandler` and `ReturnHandler` hierarchy is unnecessary here.

Protect these invariants:

| Invariant | Explanation to retain in code/tests |
|---|---|
| Half-open overlap | A request ending at the next request's start does not conflict: existing start `<` new end and existing end `>` new start. |
| Pending hold | A pending reservation blocks only while its hold is valid; expiry is the earlier of now + 24 hours and the requested start. |
| Authoritative recheck | Catalog availability is advisory. The core service rechecks while holding the item transaction lock. |
| Lock ordering | Preserve existing item/profile/account lock behavior and ordering across booking and account-management paths. Inventory all callers before changing it. |
| Fresh eligibility | Reload eligibility after acquiring the relevant borrower lock; do not trust a page loaded before an admin pause. |
| Ownership and roles | Borrowers request for themselves; custodians perform the operational decisions; admins have the existing reporting/management permissions. |
| Idempotency | Repeated create/release/return requests cannot produce duplicate reservations or loans. |
| Release | An approval is not a loan. Release enforces its time window and checks outstanding use. |
| Return | Exactly one return closes the loan; damaged/unserviceable returns produce the required maintenance state. |
| Early return | Latest-main display/calendar behavior uses actual return time where appropriate. Do not confuse historical display with blocking availability queries. |
| Atomic audit | Reservation/loan/maintenance changes and their audit entry commit together where the workflow requires it. |
| Expiry worker | Uses the same transition rules and correct scoped dependencies; it is not a second reservation engine. |

Create a lock inventory table during implementation: operation, resource key, acquisition order, isolation level, tracked reload, save and commit point. Preserve the current behavior first. Any redesign of the locking strategy requires its own concurrency evidence.

The inspected lifecycle gives this starting transition table:

| Current state | Action / actor | Result | Important guard |
|---|---|---|---|
| No reservation | Create / eligible borrower for self | Pending | Future valid interval, usable equipment, no locked conflict, request ID. |
| Pending | Approve / custodian | Approved | Still unexpired; fresh borrower eligibility and equipment/conflict recheck. |
| Pending | Reject / custodian | Rejected | Still unexpired; valid reason. |
| Pending or Approved | Cancel / owning borrower | Cancelled | Ownership and allowed current state; an expired pending request becomes Expired. |
| Pending | Expiry worker or due-check during an action | Expired | Hold missing/elapsed or requested start reached. |
| Approved | Release / custodian | Released plus open loan | Within booking interval; eligible borrower; valid release condition; equipment usable and no outstanding conflict. |
| Released with open loan | Return / custodian | Completed plus closed loan | Valid return condition/time; damaged or unserviceable return also opens maintenance. |
| Already at successful target state | Repeated supported action | Existing result | Preserve existing idempotency checks and actor validation; do not generalize every invalid transition into success. |

Approved reservations are not automatically expired by the current pending-expiry helper. Do not accidentally extend expiry to another state because a generic state handler makes that convenient.

### 7.6 Borrower pages and multi-unit requests

Split the current `BorrowerService` as follows:

| Current method or cluster | Target |
|---|---|
| `HomeAsync`, catalog/filter/picker construction, reservation form reads and availability lookup | `EquipmentCatalogService` |
| `CreateReservationAsync`, unit selection, deterministic request IDs, retries and compensation | `ReservationRequestService` |
| `HistoryAsync`, borrower-owned history projection and filters | `BorrowingHistoryService` |
| `CalendarAsync`, month/day construction and role-scoped calendar projection | `AvailabilityCalendarService` |
| Per-item eligibility/conflict/state changes | Existing `ReservationService`, called by the request service |

Controllers keep the existing routes and use the shared ControllerBase<T>. They should take only the business service dependencies their actions actually use, alongside the shared base-constructor dependencies. Do not inject every new service into every controller.

Multi-unit booking needs special care. Current orchestration uses per-unit operations, deterministic request IDs, retries and compensating cancellation. It is not one database transaction for the entire requested quantity. Keep that distinction honest in comments and the defense explanation.

Preserve quantity limit ten, required purpose/length restrictions, valid future UTC intervals, candidate ordering, duplicate submissions, retries, compensation and current disconnect/cancellation handling. There is a deliberate point where work stops using the disconnected HTTP request's cancellation token; understand it before changing it.

A truly atomic batch workflow could be a later requirement, but it must not be smuggled into a readability refactor. First test and document existing partial-failure/outage semantics; decide separately whether they meet the requirement.

### 7.7 Custodian queues and record details

Replace the action/title string switch with `GetApprovalQueueAsync`, `GetReturnQueueAsync` and any distinct history query actually required. Use a named filter model for search, status, date range and page.

Use `CustodianQueueService` for queue reads and operational details. Use `ReservationService` for actions. Do not duplicate state transition rules in queue view-model builders. Server-side permissions remain authoritative even when a button is hidden.

Admin record details remain read-only according to the current role model. Details can expose capability flags for display, but those flags never grant permission to POST an operational action.

### 7.8 Availability, history, calendar and dashboard

Keep query concepts separate even where they use the same reservations:

- Blocking availability: can this item be promised during an interval?
- Operational queue: which request or loan needs a custodian action?
- Historical/calendar display: what was booked or actually used, including completed early returns?
- Dashboard/reporting: what do the requested metrics count, and over which interval?

The current `EquipmentAvailabilityService` depends on a repository named for borrowers even though availability serves multiple roles. Move the neutral query responsibility to an appropriately named repository contract or a focused existing equipment query. Do not copy the same query into three role repositories.

Retain the current availability priority and its tests: inactive, borrowed, repair, reserved, available. Verify each page's meaning before making a global status helper. A historical “returned early” label is not a live availability status.

Share month-grid/time conversion mechanics only where they are truly identical. Apply actor scope before building the result so a borrower cannot receive another borrower's private data through a shared calendar service. Avoid reading all records and filtering private information only in Razor/JavaScript.

Keep `DashboardService` unless inspection finds genuinely separate workflows. Distinguish service chart data from Web chart rendering; similarly named helpers are not automatically duplicates.

### 7.9 Startup, background work and external boundaries

Keep `Program.cs` readable as the composition root: configuration, the Startup.DI registration call, mapping configuration, pipeline, startup initialization and run. Group coherent framework authentication/email setup where that shortens the main script without hiding execution order. All application repository/service pairs have one authoritative registration in Startup.DI.cs.

- Put reservation and other application repository/service registrations in Startup.DI.cs instead of scattered or duplicate registration containers.
- Preserve scoped DbContext, repositories, unit of work and Identity store consistency.
- Keep singleton email/image components free of captured scoped dependencies.
- Preserve session-before-auth-flow needs and middleware ordering for authentication, authorization and rate limiting.
- Keep Development secret-loading precedence and environment restrictions explicit; never write secret values into guides or logs.
- Keep initializer/demo data behavior separate: no sample data outside Development and no destructive reset during startup.
- Move the expiry worker to a clearly named Workers folder without changing hosted-service scope behavior.
- Remove service-location from normal controller workflows; retain legitimate root/scoped construction in startup/worker boundaries.

### 7.10 Razor, JavaScript, CSS and reference/demo assets

Keep current admin partials such as `_CategoryEditor`, `_EquipmentEditor`, `_CreateAccount` and `_EditAccount`. The latest main supports modals; restoring old full-page layouts would be a product change.

Preserve modal request headers, 422 invalid-form handling, successful save payload, field names, feedback, focus/escape behavior and normal browser fallback. Keep one form markup source per editor where practical. A shared modal helper should handle transport, not business policy.

Move `ModalForms` and `AuthResponses` to a truthful Web infrastructure namespace. Put repeated trusted current-actor construction in the guide-aligned custom ControllerBase<T>; it must read server claims, not submitted user IDs. Retain the guide's shared constructor dependency pattern, while excluding unrelated sample claims/session properties and general screen-routing behavior from the base.

Separate live workspace navigation from saved Figma frame metadata. Inventory layout/script/style references before removing assets. Consolidate proven duplicate CSS and unused application JS only after visual comparison; retain fonts/images/reference frames still used. Vendor/generated code is not a student rewrite target.

Unknown roles should receive no privileged navigation; server authorization must still deny protected actions. Document menu visibility separately from actual permission enforcement.

## 8. Data, transactions and migration safeguards

### One shared persistence scope

`ApplicationDbContext`, repositories, Identity store and unit of work must use the same request scope. A workflow may stage an entity change, a loan or maintenance change and an audit entry, then save/commit as one unit. Do not copy the sample repository's immediate save into each repository method.

Not every read needs a transaction. Add transaction boundaries because a workflow needs atomicity or concurrency coordination, not as a blanket wrapper around every method.

### Namespace changes are not schema changes

Moving `EquipmentItem` to `CampusGear.Data.Models` should not rename the equipment table, recreate Identity accounts or reset migrations. However, EF snapshots may contain CLR type names, so this cannot be treated as an unchecked text replacement.

For entity namespace work:

1. Record current migration list and pending-model result.
2. Update entity source, imports, current model configuration and relevant current snapshot metadata deliberately.
3. Preserve table/column/index names, foreign keys, row-version configuration, Identity mappings and migration IDs.
4. Run the model check. If a throwaway inspection migration is needed, inspect its generated operations and remove it cleanly before proceeding.
5. Validate both a new disposable database from the existing migration chain and an existing-baseline disposable database opened by the refactored application.
6. A namespace-only change should produce no destructive SQL. Any drop/create/rename or data transformation requires a separate migration decision.
7. Do not mass-edit every historical migration or delete/recreate the database to make the check pass.

Before final local cutover, take a verified backup of the intended development database and application-owned uploads. Record restore steps and test the restore to a separate database. Keep credentials outside source and documentation. Database restore is not a substitute for preserving migration compatibility.

### Known edge cases to track separately

- Image cleanup when a transaction cannot begin after the image has been written.
- Multi-unit request partial failure/outage semantics and compensating cancellation.
- Category delete/quick-add concurrency relative to normal save and inventory changes.
- Real SMTP delivery and deployment configuration, not established by local mailbox tests.

These are investigation/fix tickets with explicit expectations. Do not declare them all solved by rearranging files.

## 9. Ordered implementation phases

Use small reviewable changes. Each phase ends with compiling code and the relevant checks. Keep route compatibility throughout; avoid a branch that only becomes runnable after every feature has been rewritten.

### Phase 0 — Protect the baseline and define conformity

**Actions**

- Recheck branch/HEAD/remote when implementation begins; main may have advanced since this plan.
- Preserve existing untracked guides, `output`, ignored local settings and demo credentials.
- Create `codex/basecode-traceability-refactor` from the chosen main revision.
- Record baseline routes/methods/roles, tests, model check and current browser smoke results for all three roles.
- Pin the reference revision, supplemental five-page guide and adopt/adapt table; use Section 17's inheritance/DI checklist as the explicit baseline.
- Inventory all maintained files and label each retain/rename/split/simplify/remove/generated/vendor.
- Prepare disposable database fixtures and a backup/restore procedure for final cutover.

**Deliverables:** conformity checklist, feature inventory, behavior ledger, before/after route inventory, risk register.

**Gate:** existing tests pass on the actual implementation base; no missing critical workflow baseline. No application behavior change.

**Rollback:** documentation/test-harness changes only; running app and live database remain untouched.

### Phase 1 — Category pilot and conventions

**Actions:** establish the required common bases and central DI/mapping files, then implement Section 6 end to end; use it to settle model naming, error handling, constructors, resources and Web modal response conventions. Update relevant architecture-test selectors immediately when namespaces change.

**Deliverables:** one fully migrated feature and a short code-conventions page illustrated by actual source.

**Gate:** category acceptance tests plus the student trace rehearsal. Do not scale a pattern that the team cannot explain.

**Rollback:** revert the isolated pilot change; no schema changes.

### Phase 2 — Truthful namespaces and dependency registration

**Actions**

- Normalize entity, enum, interface, service, DTO and Web namespaces in controlled batches.
- Update Razor imports, DI, tests, helpers, seed code and reflection selectors in the same batch.
- Use compiler errors as the dependency inventory; avoid global find/replace over migrations or generated files.
- Consolidate explicit service registration; preserve lifetimes and shared context.
- Move application registration authority to Startup.DI.cs, remove superseded registration containers, and migrate all existing EF repositories/business controllers to the required bases in compiling batches.
- Check current EF snapshot behavior and both database creation/upgrade paths.

**Deliverables:** consistent folder/namespace ownership, required inheritance across features, central DI map and explicit AutoMapper configuration.

**Gate:** full suite, no pending model changes, migration compatibility, Razor compilation and nonempty architecture-test discovery.

**Rollback:** namespace/registration change without SQL/data changes.

### Phase 3 — Inventory, images and maintenance

**Actions:** apply pilot conventions; remove duplicate form/command mappings; normalize expected error handling; preserve image and maintenance boundaries. Fix the image cleanup gap in a separate change with a regression test. Review category delete concurrency separately.

**Deliverables:** traceable admin equipment/category/repair flows; explicit image-failure behavior.

**Gate:** category/editor, image store/compensation, return-condition, stale-write and maintenance tests; browser form/modal smoke.

**Rollback:** revert the feature change; retain uploads needed by existing data. Do not delete all uploaded files as cleanup.

### Phase 4 — Reservation core readability and invariant ledger

**Actions:** rewrite compressed construction/conditions into explicit code; order lifecycle methods; document lock/save/commit paths; extract only shared pure calculations; retain authoritative transitions and current error codes.

**Deliverables:** readable lifecycle implementation, transition table and lock inventory, with concrete method references.

**Gate:** reservation SQL, role, overlap, release, return, expiry, idempotency, stale-write, audit-rollback and concurrency tests.

**Rollback:** revert the refactor without schema changes. Never leave old and new transition engines active together.

### Phase 5 — Borrower catalog and request orchestration

**Actions:** extract catalog, multi-unit request and borrower-history responsibilities; preserve routes/binding and deterministic IDs; remove `BorrowerService` once callers have moved.

**Deliverables:** a visible request-service → reservation-service chain, documented retry/compensation behavior.

**Gate:** submission/orchestration tests, quantity limit, duplicate request, partial-failure compensation, disconnect behavior, ownership and picker/deep-link behavior.

**Rollback:** revert the cohesive extraction; no new batch schema or transaction model introduced.

### Phase 6 — Custodian queues, calendars, history and dashboard

**Actions:** introduce named queue methods and typed filters; extract shared calendar mechanics; separate live availability from historical display; remove time/status policy from misleading DTO helper classes; retain role-scoped reporting.

**Deliverables:** role-specific trace sheets with shared query responsibilities clearly named.

**Gate:** queue/calendar/history/dashboard tests, early-return regression, privacy, filtering/paging and date-boundary checks.

**Rollback:** return to prior read projections; no business state transition change.

### Phase 7 — Authentication and reset lifecycles

**Actions:** split registration/reset/authentication; replace generic screen routing with explicit actions and typed outcomes; preserve session keys until a compatible deliberate migration; move Development mailbox access out of normal account orchestration. Preserve Razor view lookup deliberately when a controller is split: keep explicit existing view paths or move views/imports/tag-helper/controller references together. Check every RedirectToAction/asp-controller reference affected by a controller rename.

**Deliverables:** separate lifecycle services/controllers, authentication state diagram expressed as a simple transition table, documented Identity/configuration boundary.

**Gate:** registration, flow isolation, reset, lockout, role permission, anti-enumeration, session/stamp, rate-limit/antiforgery and Development configuration checks; real browser login for all roles on an isolated host.

**Rollback:** revert authentication extraction; keep existing Identity tables/hashes and cookie configuration. A planned final restart may require re-login; it must not require recreating accounts.

### Phase 8 — User management, audit and directories

**Actions:** migrate user CRUD/approval/eligibility flows; extract audit reads; normalize role constants, create/edit input models and predictable result handling. Retain Identity APIs for account operations.

**Deliverables:** traceable admin user flow and audit flow, documented self/last-admin restrictions.

**Gate:** account-role/registration/borrower-profile tests; races between eligibility/role edits and reservation actions; stale writes and failed audit rollback.

**Rollback:** revert service/controller changes; no password or user-table migration.

### Phase 9 — Web helpers, resources and live asset cleanup

**Actions:** finish Web namespace/helpers, separate workspace navigation from Figma metadata, remove demonstrated duplicate/dead application assets, consolidate relevant styles, finish messages/labels for migrated features.

**Deliverables:** maintained live asset map; clearly isolated saved reference/demo surface; readable menu/permission ownership.

**Gate:** modal/full-page behavior, keyboard/focus checks, representative narrow/wide screenshots, no broken assets or new console failures, reference verifier.

**Rollback:** restore isolated asset/helper changes; no product redesign bundled into cleanup.

### Phase 10 — Whole-system verification and documentation

**Actions:** run full gates once on the completed changes; regenerate the source catalog/route map; rewrite current developer documentation around the final implementation; mark historical refactor reports as historical instead of deleting evidence. Update study guides after final method names stabilize.

**Deliverables:** final conformity table, source map, feature traces, test evidence and known-limitations list. New screenshots/PDFs should follow the final UI if guide regeneration is included in the later implementation scope.

**Gate:** all feature acceptance criteria in Section 10; no silent test-scope reductions, stale references or unexplained compatibility shims.

**Rollback:** source changes remain on the feature branch; main and deployment are not replaced until the final source is ready.

### Phase 11 — Team rehearsal and controlled local cutover

**Actions:** each owner traces real source and performs their demo; another member repeats the trace. Back up/verify the intended local database/uploads, build final source, stop only the identified old app process and start the final build. Verify login, role workflows, existing data, mail provider and recent early-return behavior.

**Deliverables:** running refactored local build, rehearsed ownership, current local status and vault notes. Git publishing/merging/deployment only occurs within the user's later implementation authorization.

**Gate:** the team can explain normal and failed flows; final running code matches the verified revision; retained data is present.

**Rollback:** restart the previous known-good build if source-only cutover fails. Restore data only when an actual data change requires it, using the verified procedure; do not restore over newer records casually.

## 10. Verification matrix

Retain the existing assertions. Add tests where a missing behavior or defect fix justifies one. Do not create a test merely to assert a helper calls another helper or to enforce an arbitrary line count.

| Area | Required behavior evidence | Existing test starting points |
|---|---|---|
| Project boundaries | No EF/Identity managers in Web feature code; no MVC/provider leakage in business services; no mutable entities in rendered models. | `ArchitectureBoundaryTests`, `PersistenceBoundaryTests` |
| Guide conformity | Every EF repository inherits BaseRepository; every business controller inherits its correctly closed ControllerBase<T>; public service/repository contracts resolve to the intended scoped implementation from central DI. | Extend architecture checks with nonempty type inventories and real DI resolution/shared-scope checks. |
| Mapping configuration | Declared profiles validate; sensitive/server-owned fields cannot be overposted through mappings. | Focused profile validation and relevant account/reservation behavior tests. |
| Namespace migration | Reflection selectors discover actual contracts/models after renames; assert nonempty intended sets. | `ArchitectureBoundaryTests` |
| Route compatibility | Same HTTP method, path, auth/role semantics, key binding and response behavior after controller splits. | `DeveloperDocumentationTests`, `RolePermissionTests`, HTTP fixture |
| Authentication | Approved/active/confirmed restrictions, lockout, logout, stamp invalidation and two-factor flow. | `AccountRegistrationTests`, `AccountFlowIsolationTests`, `RolePermissionTests` |
| Password reset | Known/unknown email behavior, expiry/attempts/resend, new password, flow isolation. | `PasswordResetTests`, `AccountFlowIsolationTests` |
| User management | Approve/reject, create/edit, role changes, self/last-admin protections, stale version and profile eligibility. | `AccountRoleManagementTests`, `BorrowerProfilesTests` |
| Category/inventory | Lists/filters/counts, invalid input, duplicate, active category/item rules, stale write, modal/full-page contract. | `CategoryItemListTests`, `CategoryItemEditorTests`, characterization tests |
| Images | Validation, trusted paths, replacement success, write/save/begin-transaction failure compensation. | `EquipmentImageStoreTests`, `InventoryCompensationTests`; add missing defect case |
| Reservation overlap | Adjacent nonoverlap, actual conflict, expired pending, future-start rules, maintenance and unusable item. | `ReservationServiceSqlTests`, `ReleaseAvailabilityTests` |
| Concurrent reservation/account edits | Same-item contention, profile pause/role change races, lock timeout and authoritative recheck. | Reservation SQL and account-role tests; extend actual missing races |
| Submission orchestration | Quantity 1/10/invalid, deterministic retries, duplicate submission, compensation and cancellation. | `ReservationSubmissionTests`, `BorrowerOrchestrationTests` |
| Lifecycle | Approve/reject/cancel/release/return/expire, duplicate action, forbidden action and correct audit. | `ReservationServiceSqlTests`, `ReturnConditionCheckTests`, `Round3BehaviourTests` |
| Maintenance | Return-induced case, closure resolution/condition, another open case still blocks item. | Return-condition and relevant behavior tests |
| Calendar/history/dashboard | Role scope, filters/pages, month/time boundaries, completed/early-return display and correct counts. | `ApprovalQueueCalendarTests`, `HistoryReportTests`, `DashboardTests` |
| Configuration | Development secrets/provider restrictions, no demo seeding outside Development, local mailbox limits. | `DevelopmentConfigurationTests` |
| Docs/assets | Resolvable links, real method names, complete source catalog, required reference assets. | `DeveloperDocumentationTests`, Figma verifier |

### Strengthen existing architecture/documentation tests without freezing the wrong design

`ArchitectureBoundaryTests.Presentation_models_never_contain_mutable_database_entities` currently selects specific old namespaces. Update it with each namespace change and assert that expected model families were found. A test that discovers zero models must fail.

`DeveloperDocumentationTests.Route_spellings_and_role_attribute_inventory_match_frozen_main_baseline` currently associates routes with source filenames. Replace or supplement that with runtime endpoint metadata and real HTTP behavior, keyed by HTTP method/path/policy rather than old filenames. A controller split is allowed; silently losing an authorization requirement is not.

Source-catalog/link tests should continue to find missing documentation and broken links. Do not delete them to permit renamed files. Regenerate/update catalogs in the same feature change and keep links resolvable. Historical reports can identify the revision they describe instead of pretending to be current.

### Suggested commands

Run from `C:\CampusGear` on the chosen implementation branch. Prefer RTK wrappers where supported; native commands remain appropriate for tools without a wrapper.

```powershell
& 'C:\Users\Admin\.local\bin\rtk.exe' git status --short
& 'C:\Users\Admin\.local\bin\rtk.exe' dotnet build CampusGear/CampusGear.sln
& 'C:\Users\Admin\.local\bin\rtk.exe' dotnet test CampusGear/CampusGear.sln
node figma-reference/verify.cjs
dotnet ef migrations has-pending-model-changes `
  --project CampusGear/CampusGear.Data `
  --startup-project CampusGear/CampusGear.WebApp --no-build
```

Use narrow relevant test filters during implementation. Run the whole suite after broad changes and at the final gate. Inspect actual pass/fail counts and meaningful output. Do not repeat the full suite without a new change or unresolved concern.

Browser smoke should use an isolated host/database with synthetic credentials, Development email and DemoMode disabled. Exercise the real UI after the final relevant Web change. Do not use the normal demo database as a disposable test database.

## 11. File-family migration ledger

This ledger ensures “all code” has a disposition. During Phase 0 expand it into an inventory of actual maintained paths; each entry receives an owner and final disposition.

| Current family | Planned disposition | Completion proof |
|---|---|---|
| `.sln`, application `.csproj`, package references | Keep four projects and test project; remove unused dependencies only after usage verification. | Clean restore/build and correct project graph. |
| `Data/Models/*` | Truthful namespace, clear property/state ownership; preserve persistence mapping. | No accidental schema change; entity/model tests. |
| `ApplicationDbContext`, migrations/snapshot | Retain mapping and history; deliberate current snapshot updates if needed. | Existing database compatibility and new disposable database creation. |
| `Data/Interfaces/*`, `Repositories/*`, `QueryModels/*` | Retain focused queries; all thirteen current EF repositories use BaseRepository with public contracts and explicit constructors; rename misleading shared read responsibilities. | Query behavior, required inheritance and shared scope, no provider leakage. |
| `UnitOfWork`, `PersistenceErrors`, `WorkflowLockManager` | Preserve atomicity and lock behavior; improve names/comments only where justified. | Rollback, persistence-translation and concurrency tests. |
| `AccountUserStore` | Keep Identity persistence/error translation in Data. | Duplicate/stale/rollback account tests. |
| `DbInitializer`, `DemoData` | Keep Development guards; readable responsibilities and updated namespaces. | No production demo data or destructive startup behavior. |
| `Services/Interfaces/*` | Truthful namespaces, cohesive per-service operations and typed filters. | Every contract has a real caller and explicit DI registration. |
| `Services/ServiceModels/*` | Remove misleading namespaces and duplicate input/command/row copies; separate form inputs by permitted fields. | Binding/validation/privacy tests; no mutable entity display models. |
| Account/borrower/user administration services | Cohesive split described above; delete replaced containers once callers move. | No permanent forwarding shell; clear feature traces. |
| Reservation service/expiry worker | One state-transition owner; readable lifecycle; clear worker scope. | Lifecycle/concurrency/idempotency/expiry tests. |
| Inventory/category/maintenance services | Pilot conventions; purposeful resources and explicit expected errors. | Feature tests and failure-path evidence. |
| Email sender/challenge/image store/flow adapter | Retain actual external boundaries; no empty wrappers around wrappers. | Provider, challenge, session and file-failure checks. |
| Directory/dashboard/custodian/availability reads | Named responsibilities, typed filters, privacy before rendering. | Role-scoped report and calendar tests. |
| `Resources/Constants`, proposed Messages/Views resources | Role/enum ownership and useful labels/messages, not business logic. | No duplicated role strings in migrated code; resource values used. |
| Web controllers | Business controllers use ControllerBase<T>, MVC transport and explicit actions; no generic screen routing. Status/mailbox transport endpoints have documented exceptions. | Endpoint/auth/binding/modal compatibility and inheritance inventory. |
| Web Startup.DI/Startup.AutoMapper and Services mapping profiles | Central authoritative app registration and explicit allowlisted mappings; replace old distributed registration containers. | Scope, resolution, mapper configuration and overposting checks. |
| Web page models/helpers/components | Web-only composition; truthful namespace; current actor/transport helpers only where repeated. | No direct database access; correct navigation/header identity. |
| Razor views/partials | Retain current forms/layouts, simplify duplicate markup, correct imports/model names. | Compiled views plus real role/browser smoke. |
| Application JS | Modal/catalog interaction only; remove dead files after reference audit. | Real fetch/error/fallback interaction and no new console failures. |
| Application CSS | Consolidate only demonstrated duplicates with representative visual comparison. | No layout regression on key pages and widths. |
| Vendor/generated/Figma saved frames | Inventory and isolate; preserve needed assets; do not hand-rewrite vendor code. | Asset/reference verifier and live page asset checks. |
| Tests/fixtures | Preserve behavior assertions; update legitimate namespace/route discovery; add missing risk tests. | Fresh suite with meaningful discovery/counts. |
| README, route/feature/source guides, prior refactor docs | Publish one current architecture map; mark historical evidence; refresh final study references. | Real paths/methods and peer trace rehearsal. |
| Local guides/output/ignored settings | Preserve user artifacts; later mark/regenerate guides for changed UI if requested. | No unrequested deletion, secret exposure or stale-current claims. |

## 12. Team ownership and defense preparation

Refactoring must transfer understanding to the team. Replacing confusing generated code with new generated code that nobody reviews repeats the same problem.

For each feature, assign one owner and one reviewer. They must read the service and repository together, predict one failure case, run the relevant demo and approve the trace sheet. Do not assign “all backend” to one person while everybody else learns only screenshots.

For Basubas, the previously supplied WBS assigns:

| WBS responsibility | Source path to master after refactor | Required explanation/demo |
|---|---|---|
| 4 — Login/logout | Authentication controller/service, Identity configuration, flow store | Password sign-in, admin challenge, failed login, logout and cookie/session distinction. |
| 5 — Lockout/session | Authentication/challenge service and startup configuration | Five failures, lockout duration, cookie lifetime, flow expiry and security-stamp effects. |
| 25 — Conflict checking | Reservation service and reservation/equipment repositories | Half-open overlap example, authoritative locked recheck and expired pending holds. |
| 28 — Approval/rejection | Custodian action → reservation transition → audit/save | Correct actor, allowed state, conflict/expiry recheck and resulting state/audit. |
| 45 — Menu access control | Workspace navigation, role constants, controller/service authorization | Hidden menu versus real permission; direct forbidden URL/POST behavior. |

The rest of the team should confirm ownership from the full WBS. This plan does not invent missing member assignments.

### One-page trace sheet per feature

Each sheet should contain:

1. User action and exact route/method.
2. Controller action and input model.
3. Service interface and implementation method.
4. Business checks in execution order.
5. Repository methods and actual tables/entities.
6. Lock/transaction/save/commit locations if any.
7. Success response and two important failure responses.
8. Test names proving those cases.
9. A small real code excerpt, not pseudocode substituted for source.

Rehearsal questions should include “Where exactly is this checked?”, “What if two people do this together?”, “What is rolled back?”, “Can a borrower submit a custodian URL?”, and “Why is this helper necessary?” A student should answer by navigating source, not repeating architectural labels.

## 13. Scheduling and priority under deadline pressure

**The defense is next week.** Today is Saturday, 14 November; next week can leave only a few days, depending on the actual defense day. Do not assume that “next week” means seven full implementation days. The exact day and available daily team time remain unconfirmed.

The complete scope has the following initial effort range for one primary implementer with an available reviewer. This estimates work, not elapsed calendar time or a promise to fit everything before defense:

| Work group | Initial focused effort range |
|---|---|
| Baseline, conformity, pilot | 1–2 working days |
| Namespaces, DI, model cleanup | 1–2 days |
| Inventory/maintenance and separate failure fixes | 1–2 days |
| Reservation core plus borrower orchestration | 2–4 days |
| Queues/reports/calendars and early-return preservation | 1–2 days |
| Authentication and account management | 2–3 days |
| Web/resource cleanup, full verification, documentation and rehearsal | 2–4 days |

This is roughly 10–19 focused working days, depending on discovered behavior gaps, required literal conformity and team review. Re-estimate after the category pilot. Available team members can help by reviewing separate features and rehearsing traces, but shared namespace changes, transaction changes and integration still need one coordinated sequence.

### Deadline priorities

| Priority | Work | Deadline decision |
|---|---|---|
| P0 — system must keep working | Existing data/authentication, all three roles, reservation lifecycle, permissions, tests and runnable build. | Never sacrifice these to make a folder tree look finished. |
| P1 — panel must be able to trace it | Category reference slice, truthful namespaces, explicit DI, visible business-rule ownership, simplified high-confusion flows, five Basubas traces. | Main implementation focus before code freeze. |
| P2 — finish wider readability | Remaining service/model cleanup, messages/labels, account/report simplification. | Include only as complete verified slices; otherwise retain a precise follow-up ledger. |
| P3 — lower defense value | Broad CSS consolidation, demo-asset reorganization, optional additional abstractions and runtime upgrades. | Schedule after the defense; the guide's required base classes, DI file and mapper infrastructure are already P1 work. |

No feature is silently disabled to reduce the workload. If the panel requires complete conformity by the defense, surface the actual remaining list early and allocate team time against it. Do not label partial completion as a finished whole-system refactor.

### If there are six or seven usable days

The final two days are for verification/rehearsal, so there are approximately four or five days available for structural changes. This schedule selects coherent portions of the full phases; it does not claim ten to nineteen days of work can be compressed without limits.

| Day | Implementation focus | Required output / stop rule |
|---|---|---|
| 1 | Phase 0 plus complete category pilot. | Running baseline, conformity table, accepted category pattern, fresh category tests and a successful peer trace. If the pattern is still confusing, revise it here. |
| 2 | Namespace/DI cleanup in compiling batches; stable role constants/imports; current route/model checks. | Full suite and unchanged schema mapping. Finish shared names before separate feature edits. |
| 3 | Reservation lifecycle readability and Basubas conflict/approval traces; named queue boundaries where small enough. | Existing SQL/concurrency/lifecycle tests pass; another member can explain locks, overlap and decisions. Avoid a new workflow engine. |
| 4 | Highest-confusion authentication routing and lifecycle split, sized against remaining time. | Real login/two-factor/reset behavior remains covered. If extraction cannot be completed and verified that day, retain the tested original lifecycle and make its explicit trace/readability improvements instead. |
| 5, only if available before the freeze | One additional complete slice: borrower request orchestration or inventory/admin model simplification, selected from the actual panel feedback. | Narrow tests plus full integration green; no half-migrated service or temporary forwarding adapter left over. |
| Final 2 days | Structural code freeze. Isolated browser checks for all roles, final full suite/model/assets, update source traces and rehearse. | Fix only demonstrated blockers. Pin a working build, back up intended local data/uploads and practice the exact demo. |

Do not start a new authentication or transaction extraction on the night before defense. A rollback to the last verified change is preferable to defending an untested mixed implementation.

### If there are only three to five usable days

1. Complete baseline plus the category pilot first.
2. Apply truthful namespace/DI changes only in batches that can finish and pass gates. If too large, restrict the batch and record what remains.
3. Prioritize Basubas's login/lockout/conflict/approval/menu traces, expand compressed code in those paths, and rehearse direct source navigation.
4. Take only one additional structural extraction that fits the verified window. Defer broad auth-plus-borrower rewrites happening together.
5. Reserve at least the final full day for browser/system verification and rehearsal. Freeze structural changes before it.

### If the defense is Monday or Tuesday

Treat this as a stabilization and traceability milestone. Use the existing passing architecture, finish the small pilot if feasible, prepare accurate source/role/lifecycle maps and rehearse actual failures. A whole-system rewrite is not a credible safe commitment in that window.

### Work remaining after the defense milestone

Retain Phases 0–11 as the full completion plan. Mark each phase complete, partial or not started with its remaining files and failed/unrun checks. Complete the remaining service decomposition, resource adoption and asset cleanup after the defense rather than leaving two architectures indefinitely.

The exact-day answer should select the schedule above and determine the freeze date. It does not change the full definition of done in Section 15.

## 14. Risks and decisions to record

| Risk | Response |
|---|---|
| A literal convention from the supplement is missed | Audit Section 17 per feature and enforce required inheritance/registration. Record the narrow constructor/transaction/scaffolding adaptations without replacing Identity. |
| A new abstraction makes tracing worse | Require the pilot/peer explanation gate; delete unnecessary wrappers before propagating them. |
| Namespace rename hides models from tests | Assert nonempty discovery and update selectors in the same change. |
| Controller split looks like changed routes | Verify endpoints/HTTP behavior independently of filenames. |
| Transaction/lock extraction changes ordering or context scope | Preserve existing scope/ordering, document the inventory, test concurrent operations on SQL Server. |
| EF generates destructive SQL from a cosmetic change | Stop; inspect mapping/snapshot and reject unintended schema operations. |
| Batch compensation is described as full atomicity | Keep the distinction explicit; any redesign is a separate requirement. |
| Latest early-return behavior regresses | Specific current-main return/history/calendar tests and a browser example. |
| Stale local process or PDF is used as evidence | Record tested revision/build; restart deliberately at cutover and regenerate guides only after names/UI stabilize. |
| New main changes land during refactor | Integrate in controlled checkpoints, compare behavior ledger and re-run affected gates. |
| Tests pass but students still cannot explain code | Human trace rehearsal is required to finish, alongside automated checks. |

## 15. Definition of done

The full refactor is finished only when all of the following are true:

- The reference conformity table points to real final files and explains narrow adaptations.
- Every existing EF repository inherits BaseRepository; every business MVC controller inherits ControllerBase<T>; public I...Repository/I...Service contracts, central Startup.DI registration and mapper configuration are verified. Whole-application coverage is checked feature by feature, not inferred from the category pilot.
- Every maintained file family has a recorded disposition; no old service container or compatibility adapter remains without a real documented reason.
- Namespaces match owning layers; service models no longer impersonate controllers.
- The same responsibility has one authoritative owner; no duplicated reservation rules or parallel authentication engines exist.
- Public routes, role permissions, modal/full-page contracts and retained business behavior are verified.
- Identity accounts/password hashes, application data, uploads and migration compatibility are preserved.
- Fresh full-suite, model and reference checks pass on final source; actual browser role flows were checked on the final relevant build.
- Known behavior gaps and untested external delivery are stated accurately.
- Current source/route/feature documentation matches final code; old reports are clearly historical.
- Each member can trace and explain their owned feature and at least one failure case without relying on an AI response.
- The final running local build matches the tested revision, with current local status and shared project memory updated.

The first implementation change should be the protected baseline and category pilot. Only propagate the pattern after the team can understand and defend that complete slice.

## 16. Route migration starting map

The tracked `docs/baseline-http-contracts.json` provides the following starting inventory. It is useful evidence, not the whole compatibility specification: the generic status route and Razor reference/demo pages also need inventory. Binding, validation, role policies and response semantics must be verified from actual endpoint/action metadata and HTTP behavior.

All paths below remain stable during the refactor. The target column describes responsibility, not a requirement to add a controller for every row.

| HTTP | Existing route(s) | Target responsibility / compatibility requirement |
|---|---|---|
| GET, POST | `/auth/login` | Authentication controller → AuthenticationService; preserve email binding and invalid-login feedback. |
| POST | `/auth/logout` | Authentication controller/service; sign out and clear the appropriate flow state. |
| GET, POST | `/auth/two-factor` | Authentication controller/service plus EmailChallengeService; preserve pre-auth challenge state. |
| POST | `/auth/resend-two-factor` | Same authentication lifecycle; cooldown and delivery outcomes remain explicit. |
| GET, POST | `/auth/signup` | Registration controller → RegistrationService; borrower registration and pending approval. |
| GET | `/auth/registration-submitted` | Registration confirmation view. |
| GET, POST | `/auth/password-reset-request` | PasswordReset controller/service; preserve unknown-email behavior. |
| GET, POST | `/auth/password-reset-code` | PasswordReset controller/service plus challenge mechanics. |
| POST | `/auth/resend-password-reset-code` | Reset lifecycle; preserve cooldown and state separation. |
| GET, POST | `/auth/password-reset-new` | Reset lifecycle; verify authorized reset state before changing a password. |
| GET | `/auth/success`, `/auth/access-denied` | Explicit Web responses; keep compatibility and correct status behavior. |
| GET | `/auth/development-inbox`, `/development/email` | Development mailbox boundary; preserve loopback/environment guards and no-store behavior. |
| GET | `/admin/categories` | AdminCategories controller → CategoryService; list/editor/modal handling. |
| POST | `/admin/categories/save`, `/admin/categories/{id:guid}/delete` | CategoryService; preserve row version, active-item checks and history behavior. |
| POST | `/admin/categories` | Quick category creation from inventory; same category business ownership. |
| GET | `/admin/equipment` | AdminInventory controller → InventoryService; list/editor/modal handling. |
| POST | `/admin/equipment/save`, `/admin/equipment/{id:guid}/delete` | InventoryService; image boundary, category rules, concurrency and history. |
| GET | `/admin/users`, `/admin/borrowers` | UserService; borrower route is a filtered admin view, not a second account engine. |
| GET, POST | `/admin/users/new` | UserService; distinct create model, permitted roles, validation and modal contract. |
| GET, POST | `/admin/users/{id}/edit` | UserService; distinct edit model, self/last-admin/profile/concurrency protections. |
| POST | `/admin/users/{id}/approve`, `/admin/users/{id}/reject` | UserService registration decision; retain local return-URL validation. |
| POST | `/admin/users/{id}/delete` | UserService; protect retained history and account restrictions. |
| GET | `/admin/audit` | AuditService; admin-only reads and current filters/paging. |
| GET | `/borrower/home`, `/borrower/dashboard` | EquipmentCatalogService; preserve the current alias. |
| GET | `/borrower/reservation` | EquipmentCatalogService; preserve equipment selection and owner-checked submitted-ID links. |
| GET | `/borrower/reservation/availability` | Catalog/availability read; preserve JSON keys and invalid/not-found/forbidden behavior. |
| POST | `/borrower/reservation` | ReservationRequestService → ReservationService; quantity, deterministic IDs, retries and compensation. |
| POST | `/borrower/reservation/{id:guid}/cancel` | ReservationService; owning borrower only, current not-found/privacy behavior and feedback. |
| GET | `/borrower/history` | BorrowingHistoryService; borrower-owned records only. |
| GET | `/borrower/calendar` | AvailabilityCalendarService; borrower scope and month/day filters. |
| GET | `/custodian/approvals`, `/custodian/returns` | CustodianQueueService; named queries and typed filters. Preserve the old `queue=release` redirect. |
| GET | `/custodian/history`, `/admin/history` | Role-scoped history service; retain separate custodian/admin route authorization. |
| GET | `/custodian/calendar`, `/admin/calendar` | Calendar service; retain separate route policies and current completed/early-return display. |
| GET | `/custodian/reservations/{id:guid}` | CustodianQueueService detail read; admin can read without gaining operational actions. |
| POST | `/custodian/reservations/{id:guid}/approve`, `/custodian/reservations/{id:guid}/reject` | ReservationService; custodian-only transition and rejection reason. |
| POST | `/custodian/reservations/{id:guid}/release`, `/custodian/reservations/{id:guid}/return` | ReservationService; custodian-only condition/time checks and loan/maintenance effects. |
| GET | `/custodian/borrowers`, `/custodian/categories` | DirectoryService; custodian-only lookup views. |
| GET | `/admin/maintenance` | MaintenanceService; current Administrator/Custodian policy despite the URL prefix. |
| POST | `/admin/maintenance/open`, `/admin/maintenance/{id:guid}/update` | MaintenanceService; preserve actual current roles and concurrency rules. |
| GET | `/admin/dashboard`, `/custodian/dashboard` | DashboardService; action-specific role authorization and correct metrics. |
| Generic status route | `/status/{code:int}` | Web-only StatusController; preserve original response code and browser/fetch distinction. |

When splitting controllers, preserve the difference between authorization attributes applied by MVC at the endpoint and a direct C# call to another controller method. Move shared work to a service/private helper rather than relying on attributes being reapplied during an internal method call. Endpoint authorization tests must exercise the actual public route.

## 17. Whole-codebase conformity checklist from the supplemental guide

This section makes the user's scope explicit: the final architecture must cover every maintained business feature. Categories is the first pilot, not the final extent of the work. A next-week milestone can be partial, but full completion requires every feature below and the final definition of done.

### 17.1 Common foundation files

| Target file | Required responsibility | Acceptance evidence |
|---|---|---|
| `CampusGear.Data/Repositories/BaseRepository.cs` | Shared protected context/unit-of-work access; no domain policy, implicit save or generic CRUD framework. | All thirteen current application EF repository types inherit it; no repository creates a second context. |
| `CampusGear.WebApp/Mvc/ControllerBase.cs` | Generic MVC base with the guide's context/logger/configuration/mapper constructor pattern, trusted current actor and limited shared Web infrastructure. | Every business controller closes the generic type with its own controller type; no inherited arbitrary view router or business rules. |
| `CampusGear.WebApp/Startup.DI.cs` | Explicit scoped service/repository registrations plus correctly scoped unit of work and supporting boundaries. | Application pairs have one authoritative registration list; scope validation and resolution pass. |
| `CampusGear.WebApp/Startup.AutoMapper.cs` | Explicit mapper composition/registration. | Every included profile is known and configuration validation passes. |
| `CampusGear.Services/Mapping/ServiceMappingProfile.cs` | Allowlisted needed model mappings; no uncontrolled role/password/state mapping. | Profile validation plus sensitive-field behavior checks; remove meaningless duplicate mappings/models. |
| Existing `ApplicationDbContext`, `UnitOfWork` and interfaces | Authoritative model and shared async transaction scope. | Unchanged intended schema, Identity compatibility and shared rollback behavior. |

The current Data/Services registration extensions are replaced by the central application registration file. Framework option configuration can remain in coherent Configuration files; application service/repository mappings must be visible in Startup.DI.cs.

### 17.2 Required repository coverage

Each row means public `I...Repository` in Data/Interfaces, public implementation in Data/Repositories inheriting BaseRepository, ordinary constructor injection, and central DI registration. Renaming or merging a repository is allowed only when the resulting owner is recorded and the old duplicate is removed.

| Current repository | Main consumers / work to preserve |
|---|---|
| `AccountRepository` | Authentication, registration/reset account reads and admin account policy support. |
| `AuditRepository` | Workflow audit staging plus AuditService read queries. |
| `BorrowerProfileRepository` | Eligibility/profile loading and fresh locked checks in account/reservation workflows. |
| `BorrowerReadRepository` | Catalog, availability and borrower reads; rename/extract neutral shared equipment queries where appropriate. |
| `CategoryRepository` | Category list/edit/active selection and inventory quick-add support. |
| `CustodianReadRepository` | Named queues, role-appropriate reservation details and operational history. |
| `DashboardReadRepository` | Dashboard metrics and materialized chart data. |
| `DirectoryReadRepository` | Borrower/category directory projections with current access scope. |
| `EmailChallengeRepository` | Protected challenge persistence, expiry/attempt/cooldown support. |
| `EquipmentRepository` | Equipment mutation/loading, usable-item queries and inventory projections. |
| `LoanRepository` | Loan staging and outstanding-loan checks used by reservation transitions. |
| `MaintenanceRepository` | Maintenance case staging/queries and open-hold consistency. |
| `ReservationRepository` | Request-ID lookup, conflicts, reservation state persistence and expiry candidates. |

`AccountUserStore`, `ApplicationDbContext`, `WorkflowLockManager`, UnitOfWork and its transaction wrapper are persistence infrastructure, not additional business repositories. Preserve their framework/transaction responsibilities instead of making them inherit BaseRepository for appearance.

### 17.3 Required feature coverage

For every row, complete the controller/base, service interface/implementation, repository/base, DI and tests columns in the implementation ledger. Shared services/repositories are recorded once and referenced by their actual consumers; do not duplicate them per role.

| Feature | Controller/service target | Persistence owner |
|---|---|---|
| Login/logout and administrator code | AuthenticationController → IAuthenticationService / AuthenticationService; shared EmailChallengeService. | AccountRepository, EmailChallengeRepository, AuditRepository and Identity store. |
| Borrower registration | RegistrationController → IRegistrationService / RegistrationService. | AccountRepository, BorrowerProfileRepository, AuditRepository and Identity store. |
| Password reset | PasswordResetController → IPasswordResetService / PasswordResetService; shared challenge mechanics. | AccountRepository, EmailChallengeRepository, AuditRepository and Identity store. |
| Admin user create/edit/approve/reject/delete | AdminUsersController → IUserService / UserService. | AccountRepository, BorrowerProfileRepository, AuditRepository and Identity store. |
| Audit browsing | Existing admin action or a cohesive AuditController → IAuditService / AuditService. | AuditRepository. |
| Category management and inventory quick-add | AdminCategories/AdminInventory actions → ICategoryService / CategoryService. | CategoryRepository and AuditRepository. |
| Equipment management | AdminInventoryController → IInventoryService / InventoryService; image-store boundary. | EquipmentRepository, CategoryRepository, related restriction queries and AuditRepository. |
| Maintenance management | MaintenanceController → IMaintenanceService / MaintenanceService. | MaintenanceRepository, EquipmentRepository and AuditRepository. |
| Borrower catalog/form/availability | Borrower actions → IEquipmentCatalogService / EquipmentCatalogService and shared availability service. | Materialized catalog/equipment availability queries. |
| Quantity reservation submission | Borrower action → IReservationRequestService / ReservationRequestService → IReservationService / ReservationService. | Authoritative reservation/equipment/profile/account/loan/maintenance/audit repositories. |
| Borrower cancellation | Borrower action → IReservationService / ReservationService. | ReservationRepository, related locked entity reads and AuditRepository. |
| Approve/reject/release/return | Custodian actions → IReservationService / ReservationService. | Reservation/loan/equipment/profile/account/maintenance/audit repositories as required by each transition. |
| Custodian queues/details | CustodianController → ICustodianQueueService / CustodianQueueService. | CustodianReadRepository and focused shared availability reads. |
| Borrower/admin/custodian history | Role-scoped actions → IBorrowingHistoryService / BorrowingHistoryService; retain explicit scope differences. | Appropriate borrower/custodian materialized history queries. |
| Borrower/admin/custodian calendars | Role-scoped actions → IAvailabilityCalendarService / AvailabilityCalendarService. | Appropriate role-scoped calendar/equipment queries. |
| Borrower/category directories | CustodianDirectoryController → IDirectoryService / DirectoryService. | DirectoryReadRepository. |
| Admin/custodian dashboards | WorkspaceController → IDashboardService / DashboardService. | DashboardReadRepository. |
| Expiry background processing | ReservationExpiryWorker → IReservationService / ReservationService. | Same authoritative reservation transition repositories and locks. |

Current-account/header services, email senders, image storage and session-flow adapters retain interfaces where they are real existing boundaries. Their implementation need not invent an SQL repository when it performs no database operation. Pure status/error rendering and the Development mailbox are transport-only endpoints; record their exception from the business-controller chain instead of creating fake services/repositories. Vendor/generated assets and Razor reference pages also retain their appropriate roles.

### 17.4 Guide-aligned constructor sketches

The following illustrates the intended shape; it is planning code, not application code already implemented. Transaction/audit/validation logic remains in the actual category service.

```csharp
// Data only: keep EF out of the service-facing IUnitOfWork contract.
public abstract class BaseRepository
{
    protected readonly ApplicationDbContext Context;
    protected readonly IUnitOfWork UnitOfWork;

    protected BaseRepository(
        IUnitOfWork unitOfWork,
        ApplicationDbContext context)
    {
        UnitOfWork = unitOfWork;
        Context = context;
    }
}

public sealed class CategoryRepository : BaseRepository, ICategoryRepository
{
    public CategoryRepository(
        IUnitOfWork unitOfWork,
        ApplicationDbContext context)
        : base(unitOfWork, context)
    {
    }

    // Existing focused queries and staged changes use Context.
    // No independent SaveChanges/commit here.
}
```

The explicit context argument is the documented constructor adaptation from the guide's unit-of-work-only example. It avoids downcasting the unit of work or adding EF APIs to the Services-facing interface. DI supplies the same scoped context to both the unit of work and repositories. Keep that arrangement simple and verify rollback across repositories.

```csharp
public sealed class AdminCategoriesController
    : ControllerBase<AdminCategoriesController>
{
    private readonly ICategoryService _categoryService;

    public AdminCategoriesController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        ICategoryService categoryService)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _categoryService = categoryService;
    }

    // Existing routes, role/antiforgery attributes and actions remain.
}
```

Here `ControllerBase<T>` is the custom `CampusGear.WebApp.Mvc` type, which derives from MVC Controller. It is not the non-generic API ControllerBase. Its body supports shared actor/logging infrastructure without selecting workflow screens or querying the database.

```csharp
// Startup.DI.cs, inside the one application registration method.
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddScoped<ICategoryRepository, CategoryRepository>();
services.AddScoped<ICategoryService, CategoryService>();
// All other feature pairs are listed explicitly in the same file.
```

Do not start implementing a new feature in its controller and later sprinkle database calls wherever compilation succeeds. For each slice, check the table/model and build, define the repository interface and implementation/base, register it, define the service interface and implementation, register it, then wire the derived MVC controller and view. Run the feature checks before moving to the next slice.

### 17.5 Final conformity audit

- List every concrete application EF repository and assert BaseRepository inheritance; the starting inventory has thirteen. Assert that discovery is nonempty and that renamed owners are accounted for.
- List every business MVC controller and assert the correct ControllerBase<TController> inheritance. Maintain explicit, small transport-only exceptions.
- Resolve every required public repository/service interface within a scope, validate lifetimes and verify intended implementation types. Do not require singleton email/image boundaries to become scoped just because business pairs are scoped.
- Verify that all application registration pairs are maintained centrally and no superseded Data/Services registration list remains active.
- Validate mapper configuration and the sensitive-field allowlist. Preserve straightforward explicit assignments where they express business decisions.
- Check that MVC responses/views stay in Web, business decisions stay in Services, and EF/SQL operations stay in Data. Required base classes are not permission to bypass the layer boundary.
- Confirm existing migrations, Identity credentials, current role rules and transactional invariants still pass their checks.
- Review every feature row with a human owner and source trace. The whole-codebase checkbox cannot be marked complete from one passing example.
