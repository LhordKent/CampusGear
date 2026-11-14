# CampusGear coding style

Applied 15 November 2026 on `codex/readability-refactor`, following the readable C# conventions inspected in [Apilan development at 6a70a76](https://github.com/Syynnc/Apilan/tree/6a70a764617cb44776cfb04f8695c3ecafce21f8).

## Conventions used

- Block namespaces, four-space indentation, braces on their own lines and ordinary constructors with private readonly fields.
- Separate intermediate results, readable query chains and explicit branches for multi-step decisions. Short forwarding helpers may use expression bodies.
- Comments explain timing, ownership or persistence reasons. They should not repeat the method name or describe an unrelated copied type.
- Related models share a feature file. Service and repository contracts have grouped overview files; implementations remain separate and easy to find by class name.
- Resources keys express intent, such as `CategoryMessages.StaleCategory` or `CustodianQueueMessages.ReleaseNotStarted`. Renaming the keys did not change any displayed message.
- Capability results use named arguments where adjacent booleans would be hard to distinguish.

The reference supplies coding conventions. CampusGear retains its actual domain names, asynchronous methods, immutable result records and existing Identity/transaction behavior. The supplemental guide's generic MVC base, repository base and central DI remain in use.

## Find the code

```text
CampusGear/
  CampusGear.Data/
    Interfaces/
      IRepositories.cs
      IUnitOfWork.cs
      IWorkflowLockManager.cs
    Models/
    QueryModels/
    Repositories/
      BaseRepository.cs
      CategoryRepository.cs
      ...
  CampusGear.Services/
    Interfaces/
      IServices.cs
      IAccountFlowStore.cs
      IEmailSender.cs
      IEquipmentImageStore.cs
    ServiceModels/
      AccountViewModels.cs
      BorrowerViewModels.cs
      CategoryViewModels.cs
      CommonModels.cs
      CustodianViewModels.cs
      DirectoryViewModels.cs
      InventoryViewModels.cs
      MaintenanceViewModels.cs
      ReportViewModels.cs
      ReservationModels.cs
    Services/
    Helpers/
    Mapping/
    Workers/
  CampusGear.Resources/
    Constants/
    Messages/
    Views/
  CampusGear.WebApp/
    Controllers/
    Models/
    Mvc/
    Infrastructure/
    Views/
    wwwroot/
    Startup.DI.cs
    Startup.AutoMapper.cs
    Startup.Email.cs
    Program.cs
  CampusGear.IntegrationTests/
```

The original 77 service-model files are now ten feature files. Public types and namespaces remain stable; the move changes where to read them, not persisted data or form fields.

## Keep the layout consistent

The repository pins CSharpier 0.30.6 as a development tool compatible with the installed .NET runtime. It is not an application dependency. `.editorconfig` sets namespace, indentation and unused-import conventions; `.csharpierrc.json` sets the formatter's layout. `.csharpierignore` excludes historical migrations and generated/vendor output.

From the repository root:

```powershell
dotnet tool restore
dotnet format style CampusGear/CampusGear.sln --no-restore --diagnostics IDE0005 IDE0003 IDE0160 --severity info
dotnet csharpier CampusGear
dotnet csharpier --check CampusGear
```

Use CSharpier as the final layout step after any IDE cleanup. Do not reformat historical migrations as part of ordinary edits. Long SQL/string literals should retain their value and parameterization; do not turn an interpolated SQL command into concatenated SQL to satisfy a width target.

## Verification

The initial comparison counted 153 lines over 160 characters in the controller/service/repository folders; the final style pass has **zero**. The full maintained production C# inventory also has zero lines over that threshold, excluding historical migrations and generated/vendor output. These are readability checks, not proof of correct behavior.

Current behavior/model/browser evidence is in [the refactor report](BASECODE_REFACTOR_PROGRESS.md). Source navigation is in [the cheatsheet](CODEBASE_CHEATSHEET.md), [route map](ROUTE_MIGRATION_MAP.md) and [source catalog](SOURCE_CATALOG.md).
