# Teacher base-code alignment

Structural reference previously inspected: [ASIBasecodeCsharp](https://github.com/keder-asi/ASIBasecodeCsharp.git), commit 2df3206 (Init commit). CampusGear retains .NET 8 and its established Identity authentication rather than copying sample password/JWT logic.

| Reference project | CampusGear | Ownership |
|---|---|---|
| ASI.Basecode.WebApp | [CampusGear.WebApp](../CampusGear/CampusGear.WebApp) | HTTP, forms, views, browser assets and composition root |
| ASI.Basecode.Services | [CampusGear.Services](../CampusGear/CampusGear.Services) | Interfaces, business workflows and plain application models |
| ASI.Basecode.Data | [CampusGear.Data](../CampusGear/CampusGear.Data) | Interfaces, Repositories, QueryModels, EF entities, UnitOfWork, locks, migrations |
| ASI.Basecode.Resources | [CampusGear.Resources](../CampusGear/CampusGear.Resources) | Stable shared enums and limits |

Live dependency path: browser → controller → feature service → focused repository → EF Core → SQL Server. Controller constructors accept application/transport dependencies, not repositories, DbContext or Identity managers. Business services use repository/unit-of-work/locking abstractions and established Identity managers without EF/SQL/MVC APIs.

Repositories express operations such as HasOutstandingLoanAsync and FindByRequestIdAsync rather than a generic CRUD rename of DbSet. They materialize queries and keep search/order/paging in SQL. Services choose transaction boundaries and coordinate audits/files/email; repositories do not commit after each call.

The same scoped ApplicationDbContext is shared by repositories, UnitOfWork and Identity stores. AccountUserStore retains the built-in Identity EF behavior and only translates duplicate persistence failures. Data references neither Services nor WebApp. Existing entity namespaces and EF migration metadata stay unchanged; some read DTO namespaces remain compatible with existing Razor imports.

Minimal-host Program.cs registers both shared layer extensions. Database initialization, migrations, explicit Development demo operations, framework account/cookie middleware and Identity stores are documented infrastructure exceptions. They are not permission for live controllers/views/helpers to query SQL.

All live feature extraction is implemented; this document no longer recommends direct controller queries. [ArchitectureBoundaryTests](../CampusGear/CampusGear.IntegrationTests/ArchitectureBoundaryTests.cs) guard the boundaries. See the [refactor report](SERVICE_REPOSITORY_REFACTOR_REPORT.md), [beginner guide](CODEBASE_CHEATSHEET.md) and [source catalog](SOURCE_CATALOG.md).
