# CampusGear permissions

CampusGear has exactly three roles: **Borrower** (students and faculty), **Custodian**, and **Admin** (the `Administrator` role in code). This table is the source of truth for who may use each function. Keep it in step with the code and with `CampusGear.IntegrationTests/RolePermissionTests.cs`, which proves every row over real HTTP.

✅ allowed · 👁 read-only · — forbidden (the server answers **403**)

| Function | Borrower | Custodian | Admin | Routes |
|---|---|---|---|---|
| Login, logout | ✅ | ✅ | ✅ (with emailed code) | `/auth/login`, `/auth/two-factor`, `/auth/logout` |
| Password reset request | ✅ | ✅ | ✅ | `/auth/password-reset-*` |
| Self-registration (approved by an admin) | ✅ (anonymous) | — | — | `/auth/signup` |
| Borrower dashboard (homepage) | ✅ | — | — | `/borrower/home` (`/borrower/dashboard` still works) |
| Equipment reservation: search, choose time, submit | ✅ own requests only | — | — | `GET/POST /borrower/reservation`, `/borrower/reservation/availability` |
| Cancel a pending or approved request | ✅ own requests only | — | — | `POST /borrower/reservation/{id}/cancel` |
| Own borrowing history | ✅ own records only | — | — | `/borrower/history` |
| Availability calendar | 👁 | 👁 | 👁 | `/borrower/calendar`, `/custodian/calendar`, `/admin/calendar` |
| Custodian dashboard | — | ✅ | — | `/custodian/dashboard` |
| Approval and release: approve, reject, record release (item → borrowed) | — | ✅ | — | `/custodian/approvals`, `POST /custodian/reservations/{id}/approve`, `…/reject`, `…/release` |
| Return and condition check: record return and condition, flag late (item → available or for repair) | — | ✅ | — | `/custodian/returns`, `POST /custodian/reservations/{id}/return` |
| Reservation record (details) | — | ✅ with actions | 👁 no actions | `/custodian/reservations/{id}` |
| Borrowing history report (all borrowers, filterable) | — | 👁 | 👁 | `/custodian/history`, `/admin/history` |
| Update item status/condition (maintenance cases) | — | ✅ | ✅ | `/admin/maintenance`, `POST /admin/maintenance/open`, `…/{id}/update` |
| Equipment categories | — | 👁 | ✅ create, edit, delete | `/custodian/categories` · `/admin/categories`, `POST …/save`, `POST …/{id}/delete` |
| Borrower profiles, including eligibility | — | 👁 | ✅ create, edit, delete | `/custodian/borrowers` · `/admin/borrowers`, `/admin/users/{id}/edit` |
| Equipment items | — | — | ✅ create, edit, delete | `/admin/equipment`, `POST …/save`, `POST …/{id}/delete` |
| Admin dashboard | — | — | ✅ | `/admin/dashboard` |
| User and role management: create accounts, assign roles, deactivate, approve registrations | — | — | ✅ | `/admin/users`, `/admin/users/new`, `POST /admin/users/{id}/edit`, `…/approve`, `…/reject`, `…/delete` |
| Audit log | — | — | 👁 | `/admin/audit` |

The admin takes no part in the borrowing flow: there is no admin route to reserve, approve, reject, release, or return, and the reservation service refuses those actions for any role other than the one listed.

## How the rules are enforced

1. **Every endpoint checks the role on the server.** Each controller or action carries `[Authorize(Roles = …)]` matching the table. Where a controller serves two roles (`CustodianController`), each action names its own roles, because stacked `[Authorize]` attributes must all pass. The reservation service checks the role again: only a borrower can create or cancel their own requests (`CreateAsync`, `CancelAsync`), and only a custodian can approve, reject, release, or return (`RequireCustodianAsync`). Hiding a menu item is never the only protection.
2. **Responses.** A signed-in user calling an endpoint outside their role gets **403**. Browsers also see the access-denied page, rendered by `StatusController` with the 403 status kept. Without a session, a browser opening a page is sent to the login page; background (fetch) requests and form posts get **401** (`Program.cs`, cookie events).
3. **Landing pages and menus.** After login each role is redirected to its own dashboard (`AccountController.WorkspaceForAsync`). The menu comes from `FigmaUi.Navigation(role)` and lists only pages that role may open; a test opens every menu link as that role.
4. **Borrowers only see their own data.** Every borrower query filters by the signed-in borrower's profile. Cancelling another borrower's request returns 404. The availability calendar and conflict checks read all bookings but show borrowers only the time slots, never who booked them.
5. **No conflicting reservations.** The same unit cannot be booked for overlapping times; the reservation service checks under a per-item lock.
6. **Item status flow.** Status is derived from the records (`Models/ItemStatus.cs`), so it can never disagree with them: available → reserved (an approved booking whose time has started) → borrowed (released) → available, or for repair when returned Damaged or Unserviceable. The service blocks other moves: only pending requests can be approved, only approved bookings inside their time can be released, an item already out or on hold cannot be released, and only an open loan can be returned.
7. **Deactivated users cannot sign in**, and an open session ends on the next request after deactivation (`Program.cs` middleware).

## Decisions behind the table

These were agreed when the role rules were adopted:

- Students may still register themselves; an admin approves each registration before it can sign in. Admins can also create accounts directly.
- Menus are fixed per role (no per-role menu editor).
- Item status is derived, not stored. An item counts as reserved once a booking is approved and its time has started.
- Both custodians and admins may open and close maintenance cases.
- "Delete" removes a category, item, or account that has no history; anything with borrowing or repair history is deactivated instead, so records stay complete.
