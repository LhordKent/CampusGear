# Feature tracing index

## Read a feature from the browser down

The controller binds and returns HTTP. The service chooses business outcomes. The repository executes SQL through EF. `IUnitOfWork` decides when the shared context saves/commits; repository `Add`/`Remove` methods do not commit.

## Categories

| Browser operation | Controller action | Service | Repository | Database / response |
|---|---|---|---|---|
| Open/search categories (`GET /admin/categories`) | `AdminCategoriesController.Index` | `CategoryService.GetEditAsync` / `GetPageAsync` | `CategoryRepository.FindAsync` / `ListAsync` | EquipmentCategories + item counts → Index view |
| Save category (`POST /admin/categories/save`) | `Save` | `CategoryService.SaveAsync` | Find, HasItems, Add + UnitOfWork + WorkflowLockManager + AuditRepository | Category and audit; redirect or same form errors |
| Delete category (`POST /admin/categories/{id}/delete`) | `Delete` | `CategoryService.DeleteAsync` | Find, HasItems, Remove | Delete empty category, otherwise deactivate; audit and redirect |

`CategoryInput` is the browser form model in WebApp/Models/Categories. `CategoryCommand` is the service input. `CategoryListRecord` is a materialized database projection. The controller maps these explicitly. Follow `CategoryService.SaveAsync` to see the transaction, category lock, stale rowversion comparison, active-item rule, audit, and commit in order.

### How to debug

Start with the URL, put a breakpoint in the action, step into the service, then the repository. A field validation error returns before persistence. A stale editor returns a service error. A duplicate SQL name becomes `PersistenceFailure.Duplicate` in Data, then the existing message in Services. A cancelled or failed transaction rolls back on disposal. Never remove the lock or version check to make a save succeed.
