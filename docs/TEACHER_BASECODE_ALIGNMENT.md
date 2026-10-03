# Teacher base-code alignment

Reference inspected: [keder-asi/ASIBasecodeCsharp](https://github.com/keder-asi/ASIBasecodeCsharp.git), commit `2df3206` (`Init commit`).

## Structure adopted

| Teacher project | CampusGear project | Responsibility |
| --- | --- | --- |
| `ASI.Basecode.WebApp` | `CampusGear.WebApp` | Executable ASP.NET Core project, controllers, views, host configuration, static files |
| `ASI.Basecode.Services` | `CampusGear.Services` | Interfaces and processing/business workflows before database operations |
| `ASI.Basecode.Data` | `CampusGear.Data` | EF Core DbContext, persisted models, migrations, initialization, database behavior |
| `ASI.Basecode.Resources` | `CampusGear.Resources` | Shared constants/enums and future centralized messages/labels |

The dependency direction is:

```text
CampusGear.WebApp ──> CampusGear.Services ──> CampusGear.Data ──> CampusGear.Resources
         │                     └───────────────────────────────────────>
         └──────────────────────────────> CampusGear.Data / Resources
```

`CampusGear.IntegrationTests` references each production layer directly for focused testing.

## Decisions made during migration

- Kept .NET 8, ASP.NET Core Identity, EF Core migrations, cookie authentication, and existing security behavior. The teacher repository is a structural reference, not a reason to replace CampusGear's working authentication with its sample JWT/password implementation.
- Kept existing namespaces while moving files between projects. This protects migration metadata, Razor imports, and existing feature code while assembly ownership becomes explicit.
- Put service contracts in `CampusGear.Services/Interfaces` and implementations in `CampusGear.Services/Services`, following the example's interface/service split.
- Put persisted entities under `CampusGear.Data/Models`; WebApp's `Models` folder now contains UI-only helpers.
- Put shared domain enums under `CampusGear.Resources/Constants`. Their existing namespace remains temporarily for source compatibility.
- Kept modern minimal hosting in `Program.cs` instead of copying the teacher repository's older partial `Startup` arrangement. The responsibilities and project boundaries match even though the .NET hosting style differs.
- Did not add unused generic repositories or Unit of Work wrappers. EF Core's DbContext already supplies those mechanics, and adding pass-through types would create ceremony without moving controller logic.

## Remaining cleanup

The physical/project layout now follows the teacher example. Reservation and email workflows already live in Services. Some administrator controllers still contain direct EF queries and transaction logic for inventory, users, categories, and maintenance. Move that processing incrementally into focused service interfaces before those areas grow further:

1. Extract inventory/category commands and validation.
2. Extract user/role/borrower-profile administration while preserving SQL locks and Identity security-stamp behavior.
3. Extract maintenance commands while sharing the existing item lock.
4. Centralize repeated user-facing labels/messages in Resources when localization or copy maintenance becomes necessary.

Each extraction should keep controllers responsible for HTTP concerns only: binding, authorization attributes, calling a service, and choosing a response/view. Preserve the current SQL locks, rowversion checks, audits, antiforgery validation, and tests.
