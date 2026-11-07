# Service–Repository refactor report

## Baseline

- Branch: `codex/service-repository-refactor`, based on `main` commit `f5dc69b`.
- Baseline: 132 passing tests. Routes and permissions follow `PERMISSIONS.md` and the current HTTP tests.
- Preserve behavior and schema. Tests use disposable SQL databases and the Development mailbox; never use existing accounts or uploads as fixtures.
- Historical README, cheatsheet, and smoke-script instructions about signup email verification and administrator borrowing actions are stale. Registration currently requires administrator approval; only custodians perform borrowing decisions.

## Verification ledger

Each implementation commit records its completed boundary and focused verification here. The final handoff also contains feature traces and a complete maintained-source catalog.

- Baseline characterization: stale category/equipment writes preserve saved values and add no audit; repeated multi-unit forms retain the same reservation IDs (query-string ID order is not stable in the pre-existing implementation). Three new HTTP checks passed.
- Infrastructure/categories: shared scoped UnitOfWork, provider-independent persistence failures, transaction-owned workflow locks, and audit construction. Categories now use a thin controller → CategoryService → CategoryRepository. Build clean; 61 focused permission, characterization, and round-three HTTP tests passed. See `FEATURE_TRACES.md`.
- Inventory/maintenance: business rules, upload/database compensation, item/category locks, and audits now live in feature services; controllers retain binding and response mapping. Display models contain immutable query records rather than tracked equipment/maintenance entities. All 61 focused HTTP checks and two additional real-SQL image-compensation tests passed.
- Core persistence was extracted before the remaining read-model work because it is an independent dependency: ReservationService and EmailChallengeService no longer use EF/SqlClient. The original 13 reservation SQL checks and five account-registration/challenge checks passed with unchanged assertions; only constructor wiring changed.
- Borrower orchestration and staff reads: all 137 tests passed after extracting catalog/quantity/history/calendar, custodian queues/details, directories, dashboards, and equipment status queries. Query paging remains in SQL. Shared EF-free DTOs retain existing namespaces to keep Razor imports compatible; only presentation geometry/icons/labels remain in WebApp helpers. Two tests now call the extracted status service instead of the removed EF-bearing UI helper, with identical assertions.

## Known risks to preserve

User administration: queries, approval, Identity coordination, profile editing, account deletion/deactivation, and audits moved to UserAdministrationService. AccountRepository keeps post-lock reload and history queries in Data. Built-in Identity EF stores remain responsible for password/role/stamp persistence; AccountUserStore adds only constraint-error translation. The 19 focused account/admin tests passed; the integrated suite subsequently passed all 137 tests. Browser inputs remain annotated in WebApp and are explicitly mapped to plain commands.

Identity stores and repositories must share one scoped context. Preserve per-item/profile/admin SQL locks, fresh post-lock reads, concurrency tokens, audit timing, image compensation, challenge send-after-commit, and multi-unit per-unit transactions with compensating cancellation. Unexpected pre-existing defects are documented separately instead of silently changed.
