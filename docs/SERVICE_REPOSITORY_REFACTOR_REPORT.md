# Service–Repository refactor report

## Baseline

- Branch: `codex/service-repository-refactor`, based on `main` commit `f5dc69b`.
- Baseline: 132 passing tests. Routes and permissions follow `PERMISSIONS.md` and the current HTTP tests.
- Preserve behavior and schema. Tests use disposable SQL databases and the Development mailbox; never use existing accounts or uploads as fixtures.
- Historical README, cheatsheet, and smoke-script instructions about signup email verification and administrator borrowing actions are stale. Registration currently requires administrator approval; only custodians perform borrowing decisions.

## Verification ledger

Each implementation commit records its completed boundary and focused verification here. The final handoff also contains feature traces and a complete maintained-source catalog.

## Known risks to preserve

Identity stores and repositories must share one scoped context. Preserve per-item/profile/admin SQL locks, fresh post-lock reads, concurrency tokens, audit timing, image compensation, challenge send-after-commit, and multi-unit per-unit transactions with compensating cancellation. Unexpected pre-existing defects are documented separately instead of silently changed.
