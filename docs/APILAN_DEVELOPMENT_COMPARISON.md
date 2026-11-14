# Apilan development versus current CampusGear

**Historical comparison before the requested readability implementation.** The measurements and examples below describe the 14 November source. The subsequent pass reduced the 153 long lines in the three logic folders to zero, grouped the models/contracts and cleaned up the named examples. See [the current coding-style guide](CODE_STYLE.md) and [verification report](BASECODE_REFACTOR_PROGRESS.md). Source links below now resolve to the current files rather than the pre-style snapshot.

Reviewed 14 November 2026. This is a source comparison, not an application change or a proposal to replace CampusGear with Apilan.

## Assessment

**Apilan is generally easier for a student to read line by line. CampusGear now has a comparable four-layer organization and stricter database boundaries, but its readability cleanup remains incomplete.** The earlier CampusGear refactor improved ownership, naming and compatibility; passing its tests does not establish that its implementation is easy to explain during defense.

The useful target is Apilan's readable method bodies and explanations, while preserving CampusGear's transaction, Identity and verification behavior. Copying its folders or reducing the number of classes alone would not accomplish that.

## Exact comparison scope

- Apilan: [`development` at 6a70a764617cb44776cfb04f8695c3ecafce21f8](https://github.com/Syynnc/Apilan/tree/6a70a764617cb44776cfb04f8695c3ecafce21f8), the branch head observed during review. Commit time: 5 November 2026, 8:04 PM Manila. Did not use main or a personal development branch.
- CampusGear: current local, uncommitted `codex/basecode-traceability-refactor`, based on `79baba6`. Comparing remote CampusGear main would miss the refactor the user just requested.
- Enumerated Apilan's complete tree; inspected its non-generated C# formatting inventory and representative category/event/attendance/account/report/audit flows, SQL change scripts, models, startup, large views and modal code. Compared these with current CampusGear source.
- No Apilan code was executed and no database was imported. Its README says the full initial `Alliance.sql` database is supplied separately. No runtime, security-completeness or performance superiority is established by this review.
- CampusGear's earlier 221-test pass is existing verification evidence, not a newly executed test result for this comparison. No application tests were rerun because no application source changed.

## Folder structure

| Area | Apilan development | Current CampusGear | Assessment |
|---|---|---|---|
| Solution layers | ASI.Basecode.Data / Services / Resources / WebApp | CampusGear.Data / Services / Resources / WebApp, plus IntegrationTests | Same broad ASI organization. Retaining the ASI project name does not itself improve code quality. |
| Repositories | Six implementations plus BaseRepository | Fourteen implementations plus BaseRepository | Both follow the repository convention. The domains and responsibilities differ, so counts alone are not a quality score. |
| Service contracts | Seven interfaces grouped in IServices.cs | Separate interface files | Apilan offers fewer files to open; CampusGear makes individual contracts easy to locate. Avoid turning all contracts into a large omnibus file merely to reduce the count. |
| Feature models | Seven model files, commonly several related types per file | Seventy-seven type files grouped into seven feature folders | CampusGear's organization is explicit but requires more navigation. Some folder choices remain loose: custodian workflow/detail types are under Reports. |
| Startup | Explicit registrations in Program.cs | Explicit Startup.DI, Startup.AutoMapper and Startup.Email called by Program | Both are traceable. CampusGear's composition is separated from the HTTP pipeline. |
| MVC base | Non-generic BaseController with account/modal/audit helpers | Generic ControllerBase<T> with shared HTTP/logger/configuration/mapper helpers | Both have a shared base. CampusGear follows the supplemental guide more literally but pays constructor boilerplate for it. |
| Database setup | External initial database plus seven numbered SQL change scripts | Checked-in EF context and migration history | CampusGear is more self-contained for schema reconstruction. Apilan cannot be reproduced from its tracked change scripts alone according to its README. |
| Verification | No tracked test project/files found in the inspected tree | Dedicated integration/architecture/endpoint tests | CampusGear has more visible automated verification infrastructure. This says nothing about private/manual tests the other team may have. |

Sources: [Apilan tree](https://github.com/Syynnc/Apilan/tree/6a70a764617cb44776cfb04f8695c3ecafce21f8), [interfaces](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.Services/Interfaces/IServices.cs), [startup](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.WebApp/Program.cs), [database setup](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/README.md).

## Readability measurements

Counts are physical lines, including blanks/comments but excluding trailing empty lines. The full C# inventory excludes bin/obj, EF migrations and .Designer.cs files; Resources access wrappers and existing scaffold source remain included. Line width is a diagnostic for scanning effort, not a correctness or complexity metric.

| Measurement | Apilan | CampusGear |
|---|---:|---:|
| C# files under that inventory | 56 | 242 |
| Physical C# lines | 4,381 | 11,479 |
| C# lines longer than 160 characters | 0 | 340 |
| Long lines in repository folders only | 0 | 64 |
| Long lines in service implementation folders only | 0 | 70 |
| Long lines in controller folders only | 0 | 19 |
| Largest service file | ReportService, 310 lines | ReservationService, 755 lines |
| Largest tracked production Razor view | Reports/Index, 800 lines | Borrower/Reservation, 289 lines |

The three comparable logic folders have **153 long lines in CampusGear versus zero in Apilan**. That excludes the Resources wrappers and model-file proliferation, making it a useful concrete signal. The largest-file counts are navigation signals only: CampusGear has more workflows, while Apilan places substantial rules in SQL and more markup in individual views.

## What Apilan does more readably

### 1. Small methods communicate a single operation

[EventTypeService](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.Services/Services/EventTypeService.cs#L23) separates GetEventTypes, GetEventType and SaveEventType. The save branches are brief, with visible variables and one operation per step. A reader can follow the controller -> service -> repository relationship quickly.

[CampusGear CategoryService](../CampusGear/CampusGear.Services/Services/CategoryService.cs) also has a clear owner, but SaveAsync includes validation, locking, row versions, active-item restrictions, audit snapshots, persistence and error translation. Several are required safeguards; the improvement is to make those stages readable, not remove them to match a shorter example.

### 2. Queries are visually laid out

[Apilan EventService.GetEvents](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.Services/Services/EventService.cs#L29) uses separate filter blocks and line-broken LINQ chains. In CampusGear, [EquipmentAvailabilityRepository](../CampusGear/CampusGear.Data/Repositories/EquipmentAvailabilityRepository.cs:19) puts a whole blocking predicate and projection onto one line; CalendarBookingsAsync has a 454-character query line. [DashboardReadRepository](../CampusGear/CampusGear.Data/Repositories/DashboardReadRepository.cs) similarly compresses several awaited counts into one expression.

These are confirmed reading problems. They can be improved without changing query semantics or creating another abstraction layer.

### 3. Comments explain the rule and its reason

[AttendanceWindow](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.Services/ServiceModels/AttendanceWindow.cs#L7) explains when each attendance phase opens and where the same rule is enforced. AccountService explains why deactivation must check both the member and account rows. ReportService explains why upcoming events are excluded from participation rates.

CampusGear has useful comments around locks and compensation, but sparse explanations around calendar precedence and some presentation flags. [CustodianQueueService](../CampusGear/CampusGear.Services/Services/CustodianQueueService.cs:34) chooses several availability messages in one 469-character nested conditional. Its following record construction has consecutive boolean expressions whose meaning requires opening the model declaration.

### 4. Naming is more natural in several inspected methods

Apilan commonly names intermediate results after their purpose: query, counts, summary, window. CampusGear still has `x` for meaningful loaded objects, mechanically retained unused imports, and truncated sentence-like Resources keys such as `ThatCategoryNameAlreadyExistsOrThe`. These do not make the application incorrect, but they make the code feel mechanically transformed rather than deliberately edited for a human reader.

## Where a short Apilan C# file hides additional work

Apilan's [EventRepository](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.Data/Repositories/EventRepository.cs#L24) calls stored procedures. [SQL script 06](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/database/06_LateArrivalsAndYears.sql#L130) contains attendance validation, transaction handling, duplicate checks and capacity checks with UPDLOCK/HOLDLOCK.

The full trace is therefore controller -> service -> repository -> stored procedure -> tables, with related timing rules also represented in AttendanceWindow. CampusGear's core reservation rules are in [ReservationService](../CampusGear/CampusGear.Services/Services/ReservationService.cs), with SQL query/locking mechanics in Data. A fair comparison follows the complete behavior in both languages.

The domain also differs. CampusGear covers approval, holds, cancellation, release, returns, maintenance, quantity compensation, administrator challenges and password recovery. Apilan's event/attendance flows do not constitute equivalent implementations of those requirements. Their shorter C# is partly cleaner expression and partly different scope/placement of rules.

## Where CampusGear has useful advantages to retain

1. **Database ownership.** Apilan repository contracts expose IQueryable; service methods compose database queries. Its ServiceBase directly handles SqlException/DbUpdateException. CampusGear materializes queries in Data and translates persistence failures there; Services uses repository contracts and business outcomes. That makes it clearer where SQL behavior belongs. Sources: [Apilan repository interfaces](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.Data/Interfaces/IRepositories.cs), [Apilan ServiceBase](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.Services/ServiceBase.cs), [CampusGear boundaries](../CampusGear/CampusGear.IntegrationTests/ArchitectureBoundaryTests.cs).
2. **Explicit workflow commits.** Apilan EventTypeRepository saves inside each write; its controller calls Audit after a saved change. AuditService logs and swallows audit-write failure. CampusGear's transactional reservation/inventory workflows stage domain changes and audits together, then commit through the service. These are different guarantees, not just different syntax. Do not transplant independent repository saves into CampusGear. Sources: [Apilan repository](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.Data/Repositories/EventTypeRepository.cs), [audit implementation](https://github.com/Syynnc/Apilan/blob/6a70a764617cb44776cfb04f8695c3ecafce21f8/ASI.Basecode.Services/Services/AuditService.cs#L28), [CampusGear inventory](../CampusGear/CampusGear.Services/Services/InventoryService.cs).
3. **Smaller individual production views.** Apilan's Reports/Index.cshtml is 800 lines; Events/Details.cshtml is 440. CampusGear's largest production view is 289. Apilan itself therefore has large-file hotspots despite its compact C#. This does not establish which rendered UI is better.
4. **Existing verification.** CampusGear checks HTTP roles/routes, concurrency, persistence, stale state, uploads and account flows. The previous pass was 221 tests. Keep those while improving readability; [current verification evidence](BASECODE_REFACTOR_PROGRESS.md) states what was and was not tested.

## Recommended next changes, in priority order

These recommendations are not implemented by this comparison.

1. Expand long queries and nested decision expressions in EquipmentAvailabilityRepository, DashboardReadRepository, CustodianQueueService and AvailabilityCalendarService. Use named intermediate results and one clear decision per branch. This is the most direct improvement for reading during defense.
2. Give long service workflows a visible sequence: validate, load/recheck, change, audit, save/commit. Extract only meaningful named steps inside the existing owner; avoid a new generic workflow engine or scattering one transition across many classes.
3. Use named arguments or object initializers where records accept several booleans or same-type values. Keep immutable result types if they help safety; make their construction readable.
4. Remove unused imports and inconsistent mechanical constructor naming. Replace truncated Resources keys with stable intent names, retaining the actual messages and tests.
5. Review model navigation: put staff queue/detail/calendar types in a meaningful Custodian feature folder rather than the broad Reports bucket. Group tightly related tiny result types only where that reduces navigation; do not collapse all models/interfaces into omnibus files.
6. Keep the supplemental guide's required bases/DI. Apilan's simpler BaseController is a comparison point, not a reason to silently undo the teacher's generic-base requirement. Use concise constructor examples in the defense guide to explain the shared dependencies once.
7. Rehearse one complete feature using both a successful and rejected request. Evaluate whether the team can point to the rule and explain the save boundary. Neither file counts nor green tests substitute for this.

The recommendation is a focused readability pass over the current architecture. A second whole-system rewrite, wholesale move into stored procedures, or replacement of Identity would add risk without directly addressing the observed reading problems.
