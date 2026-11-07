# CampusGear integration and architecture checks

From the repository root:

    dotnet test CampusGear/CampusGear.IntegrationTests
    ./scripts/smoke-live.ps1

The wrapper also checks Figma reference assets and uses the current registration approval/permissions flow. It does not need a separately running app.

SQL fixtures create/migrate uniquely named CampusGear_Integration_<guid> databases and delete them after the run. They never insert test fixtures into the live CampusGear database. WebAppFixture forces Development email and DemoMode=false. Image tests use fake stores or unique temporary folders, not live uploads.

Default server: .\SQLEXPRESS with Windows authentication. Set CAMPUSGEAR_TEST_SQLSERVER to select another server; fixtures always replace the database name. The SQL login needs create/drop permission for those disposable databases.

Coverage:

| Area | Main test files |
|---|---|
| Concurrent overlap, duplicate IDs, locks, eligibility, release/return/expiry | ReservationServiceSqlTests, ReleaseAvailabilityTests |
| HTTP authorization, navigation, own-data privacy and reports | RolePermissionTests, HistoryReportTests, BorrowerProfilesTests |
| Registration approval, roles, password/status decisions and recovery | AccountRegistrationTests, AccountRoleManagementTests, PasswordResetTests, AccountFlowIsolationTests |
| Stale edits, deletion/history/audit retention and repeated/simultaneous quantity forms | RefactorCharacterizationTests |
| Candidate retries, compensating cancellation, disconnect semantics | BorrowerOrchestrationTests |
| Image replacement/removal/failure cleanup and real file validation | InventoryCompensationTests, EquipmentImageStoreTests |
| Maintenance fetch/plain forms and demo isolation | Round3BehaviourTests |
| Secrets/configuration precedence and signup annotations | DevelopmentConfigurationTests, SignUpInputValidationTests |
| Shared Identity/repository/audit transaction and sanitized SQL failures | PersistenceBoundaryTests |
| Layer boundaries and entity-free presentation/repository contracts | ArchitectureBoundaryTests |
| File-catalog coverage, links, trace symbols and frozen route/role inventory | DeveloperDocumentationTests |

Source-based checks require the checkout alongside the test output. Regenerate docs with python scripts/update-code-map.py after adding files/symbols. A failed documentation/architecture check is not permission to weaken its assertion; update the implementation or intentionally reviewed contract.

See [verification evidence](../../scripts/verification.md), [the refactor report](../../docs/SERVICE_REPOSITORY_REFACTOR_REPORT.md), and [the beginner guide](../../docs/CODEBASE_CHEATSHEET.md).
