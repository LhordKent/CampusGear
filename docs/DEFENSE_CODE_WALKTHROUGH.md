# CampusGear: defend the refactored code

Updated 14 November 2026 for `codex/basecode-traceability-refactor`. This walkthrough describes the implemented source. The earlier PDF study guides are historical snapshots; their old class names and line numbers should not be used to explain this branch.

## Start with the request

For any button, explain this sequence: the Razor form supplies a URL and field names; the controller binds/validates input and checks HTTP access; the service applies the business rule; the repository queries or stages data; the service saves the complete operation. The view displays the result. Dependency injection supplies these objects; it does not run the business process itself.

Keep [feature traces](FEATURE_TRACES.md), [route map](ROUTE_MIGRATION_MAP.md) and [source catalog](SOURCE_CATALOG.md) open while rehearsing. These link the real source rather than asking you to remember every filename.

## Basubas WBS 4: login/logout

Open [AuthenticationController](../CampusGear/CampusGear.WebApp/Controllers/AuthenticationController.cs), then [IAuthenticationService](../CampusGear/CampusGear.Services/Interfaces/IServices.cs) and [AuthenticationService](../CampusGear/CampusGear.Services/Services/AuthenticationService.cs). Existing `/auth/login`, `/auth/two-factor` and `/auth/logout` routes remain.

The controller binds `LoginInput`, checks ModelState, calls `LoginAsync` and chooses a view or redirect from `AuthenticationStatus`. The service finds the account, checks active/approval status, invokes Identity's password/lockout checks and records the audit. An administrator normally receives an email challenge before an authentication cookie is created:

```csharp
var passwordResult = await _signInManager.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
```

```csharp
_flow.PendingAdministrator = new(user.Id, user.SecurityStamp ?? string.Empty);
var issued = await _emailChallengeService.IssueAsync(user, EmailChallengePurpose.AdministratorLogin, ct);
```

After successful code verification the service reloads the user and rechecks the account/security stamp before signing in. Logout records the event, signs out through Identity and clears the flow session. Registration and password reset have their own services/controllers; authentication no longer routes every account screen through one general flow enum.

For the running local command, `Email:Provider=Development` stores messages in [the local mailbox](http://localhost:5264/development/email). It sends no Gmail. Real SMTP delivery is a separate integration that was not verified here. A retained Development-only fallback accepts the password when Smtp is selected without a configured host; it does not apply to the explicitly selected Development mailbox used during verification.

## Basubas WBS 5: lockout and session

Open [Program.cs](../CampusGear/CampusGear.WebApp/Program.cs), [IAccountFlowStore](../CampusGear/CampusGear.Services/Interfaces/IAccountFlowStore.cs) and [AccountFlowStore](../CampusGear/CampusGear.WebApp/Infrastructure/AccountFlowStore.cs).

```csharp
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
options.Lockout.MaxFailedAccessAttempts = 5;
options.Lockout.AllowedForNewUsers = true;
```

Identity records failed-password attempts and lockout. The controller's `auth-post` rate limit additionally allows 20 requests per minute per IP. They are separate controls.

The authentication cookie has an eight-hour lifetime. The account-flow session has a 15-minute idle timeout. That session holds pending administrator/reset state; it is not the login cookie. Password-reset authorization has its own ten-minute expiry. Do not explain these three values as one timeout.

The typed flow store preserves existing session keys and distinguishes an absent reset request from an empty unknown-account marker. Security stamps invalidate stale challenges. Identity stamp validation runs every request; the request middleware also rejects missing, inactive or unapproved users. Role/status changes require a new sign-in.

## Basubas WBS 25: schedule conflicts

Open [ReservationRepository.HasConflictAsync](../CampusGear/CampusGear.Data/Repositories/ReservationRepository.cs) and [ReservationService](../CampusGear/CampusGear.Services/Services/ReservationService.cs).

The overlap condition in the database query is:

```csharp
x.StartAtUtc < end &&
x.EndAtUtc > start &&
```

Bookings use a half-open interval: 09:00–10:00 and 10:00–11:00 do not overlap; 09:00–10:00 and 09:30–10:30 do. The query also checks the same equipment item, excludes the reservation being changed, and counts approved/released reservations plus pending reservations whose hold and start are still in the future.

The availability preview helps choose a unit; it is not the final guarantee. The transition service checks again inside the item transaction/lock. Two simultaneous requests therefore cannot rely on the same earlier free result. [WorkflowLockManager](../CampusGear/CampusGear.Data/WorkflowLockManager.cs) supplies SQL transaction-owned locks. The existing unique request identifier also protects repeat submission.

Quantity submission belongs to [ReservationRequestService](../CampusGear/CampusGear.Services/Services/ReservationRequestService.cs). Individual units commit through the core transition service. If a later unit fails, the orchestration attempts compensating cancellation. Do not call this one atomic SQL transaction for the entire quantity; an outage can prevent compensation.

## Basubas WBS 28: approval/rejection

Open [CustodianController](../CampusGear/CampusGear.WebApp/Controllers/CustodianController.cs), then `ApproveAsync`/`RejectAsync` in [ReservationService](../CampusGear/CampusGear.Services/Services/ReservationService.cs).

The approve endpoint is custodian-only and antiforgery-protected. The service independently requires a custodian, starts an item transaction, reloads the reservation, handles an already-approved or expired request, checks the pending state, locks/rechecks borrower eligibility, verifies item usability and checks conflicts. The successful write includes:

```csharp
reservation.Status = ReservationStatus.Approved;
reservation.HoldExpiresAtUtc = null;
reservation.DecidedByUserId = actor.UserId;
reservation.DecidedAtUtc = now;
```

The audit is staged in the same scope before save/commit. Rejection requires a reason and a pending request. Approval does not mean the equipment was physically issued: release creates the loan; return closes it and can create maintenance for a damaged item.

[CustodianQueueService](../CampusGear/CampusGear.Services/Services/CustodianQueueService.cs) reads queues, history and detail records. It does not approve reservations. Administrator reservation pages are read-only; administrators do not gain custodian decision rights just because they can inspect records.

## Basubas WBS 45: menu/access control

Open [WorkspaceNavigation](../CampusGear/CampusGear.WebApp/Models/WorkspaceNavigation.cs), [RoleNames](../CampusGear/CampusGear.Resources/Constants/RoleNames.cs), then a protected controller action.

`WorkspaceNavigation.ForRole` selects visible links and Resources labels. Unknown roles receive an empty menu. Menu visibility is a convenience; actual authorization comes from the endpoint's `[Authorize]` policies and service-side workflow checks. Typing a hidden URL does not grant access. A signed-in wrong-role request returns 403; an anonymous browser page visit redirects to login, while protected fetch/form requests return 401.

## Explain the ASI structure

Show [Startup.DI.cs](../CampusGear/CampusGear.WebApp/Startup.DI.cs): each interface explicitly maps to its implementation. Show [BaseRepository](../CampusGear/CampusGear.Data/Repositories/BaseRepository.cs): every application EF repository inherits it and uses the same scoped context/unit of work. Repositories stage changes; the service owns save/commit for the workflow.

Show [ControllerBase&lt;T&gt;](../CampusGear/CampusGear.WebApp/Mvc/ControllerBase.cs): business controllers inherit common HTTP/logging/configuration/mapper infrastructure. It contains no reservation or inventory rule. Show [ServiceMappingProfile](../CampusGear/CampusGear.Services/Mapping/ServiceMappingProfile.cs): the category read mapping is explicit and validated. Identity and reservation writes remain explicit so mapping cannot silently change roles or state.

The sample's reversible-password/dummy-login examples were not transplanted. Existing Identity and migration history remain. [Alignment notes](TEACHER_BASECODE_ALIGNMENT.md) document these deliberate adaptations; [dependency notes](DEPENDENCIES.md) explain the patched mapper version and its licensing limit.

## Rehearse with evidence

Practice one successful flow and one rejection per topic. Find the action/service/repository without search assistance; explain what would happen on invalid input, stale state or a failed save. Use [the verification report](BASECODE_REFACTOR_PROGRESS.md) for actual automated/browser results. Passing tests cannot establish that every team member can explain the implementation; that rehearsal remains yours to perform before defense.
