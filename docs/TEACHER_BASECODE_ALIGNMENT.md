# ASI basecode and supplemental-guide alignment

Updated for implementation on 14 November 2026. References: [ASIBasecodeCsharp at the inspected revision](https://github.com/keder-asi/ASIBasecodeCsharp/tree/2df3206f3a520bd55e759b1c3e70d1bbb608e6e5) and the user's five-page supplemental short guide. The guide constructs repository -> service -> controller; requests execute controller -> service -> repository.

| Convention | Implemented location |
|---|---|
| Four application layers | CampusGear.Data, Services, Resources and WebApp; IntegrationTests is separate verification. |
| Public I...Repository / ...Repository | Data/Interfaces and Data/Repositories; every application EF repository inherits BaseRepository. |
| Public I...Service / ...Service | Services/Interfaces and Services/Services, ordinary constructors/private readonly fields. |
| Generic MVC base | WebApp/Mvc/ControllerBase.cs; every business controller derives from its own closed ControllerBase<T>. |
| Central application DI | WebApp/Startup.DI.cs; modern Program calls it; no old Data/Services registration containers. |
| Mapping infrastructure | Startup.AutoMapper.cs and Services/Mapping/ServiceMappingProfile.cs; validated read mapping only. |
| Models in their owning layer | Data entities, Services annotated inputs/results, Web-only page composition; misleading controller/model namespaces removed. |
| Resources | Constants, feature Messages .resx and workspace-label Views .resx, actually consumed. |

## Explicit adaptations

1. BaseRepository receives the same scoped ApplicationDbContext plus IUnitOfWork, rather than leaking EF context APIs through the service-facing unit-of-work contract or downcasting it.
2. Repositories stage changes; services save/commit complete workflows so audits, loans and maintenance do not commit independently.
3. Existing Identity hashes/stamps/cookies/roles are retained. Sample dummy-login/reversible-password code is not used.
4. Existing model mappings/migrations are preserved. The short guide's force-scaffold example for a new database is not run over the working Identity context.
5. Framework persistence stores, expiry hosting, SMTP/image boundaries, status rendering and Development mailbox have their actual infrastructure responsibilities; no fake repository chain is created for code that does not query a database.

There is no generic CRUD engine, mediator layer, extra architecture project or reflection-based service registration. Calendar/queue reads do not own reservation mutations. Source-level boundaries, actual endpoint policies, inheritance and DI are enforced by tests.

See [current report/evidence](BASECODE_REFACTOR_PROGRESS.md), [feature traces](FEATURE_TRACES.md), [source catalog](SOURCE_CATALOG.md) and [dependency/licensing notes](DEPENDENCIES.md).
