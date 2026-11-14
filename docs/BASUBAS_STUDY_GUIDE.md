> Historical snapshot: this document describes the pre-refactor implementation/UI. For current code use [the code guide](CODEBASE_CHEATSHEET.md) and [feature traces](FEATURE_TRACES.md). Existing PDFs are preserved.

DEFENSE PREPARATION  /  14 NOVEMBER 2026

# CampusGear - Basubas study guide

A detailed guide to the five work packages assigned to **Lhord Kent Basubas**, with actual source excerpts, file paths, explanations, rehearsal steps and sample answers. Based on local main **5f1fffe**.

## Your assigned scope

| WBS | Responsibility | What you must explain |
| --- | --- | --- |
| 4.0 | Login / Logout | How Identity checks credentials and creates or clears a login. |
| 5.0 | Lockout & Session Handling | Failed-password limits, cookies, workflow session and access expiry. |
| 25.0 | Reservation Conflict Checking | Which bookings block a slot and how simultaneous requests are handled. |
| 28.0 | Request Approval & Rejection | Who can decide, what gets rechecked and what is recorded. |
| 45.0 | Role-Based Menu Access Control | How menus and server permissions work together. |

## A 30-second introduction

“My assigned areas protect access and reservation decisions. I use ASP.NET Identity for sign-in and lockout, server-side role checks for permissions, and transactional conflict checking to stop overlapping bookings for the same physical item. Custodian decisions are validated and recorded in an audit trail.”

## The application in one flow

**Browser → Controller → Service → Repository → EF Core → SQL Server.** The controller handles HTTP and form input. The service decides whether the action is allowed. The repository retrieves or changes database records. EF Core translates database operations for SQL Server.

## How to study this guide

- Pages 2-3: authentication and sessions. Page 4: conflict formula. Page 5: simultaneous requests.

- Page 6: approval/rejection. Page 7: menus and permissions. Page 8: hands-on demonstration.

- Page 9: defense questions. Page 10: file map and evidence. Page 11: admin flow and abilities.

**Source assignment:** WBS spreadsheet, Jumpstart Program Schedule, column C: rows 5, 6, 26, 29 and 46. This establishes assigned scope; it does not prove individual authorship of every current line after the shared refactor.


<!-- pagebreak -->

WBS 4.0  /  AUTHENTICATION

# 1. Login and logout

**Authentication** answers “Who is this user?” CampusGear accepts an email and password, uses ASP.NET Identity to validate them, and creates an authentication cookie after the required checks succeed.

## What happens when someone signs in

- AccountController receives the form and checks model validation. LoginAsync trims the email and finds the account.

- Unknown or inactive accounts receive a generic error. Pending/rejected registration status is disclosed only after the password is verified.

- Approved accounts go through Identity password and lockout checks. A correct password alone does not immediately sign in an administrator when an email code is required.

- Borrowers and custodians receive a login cookie and their role dashboard. Administrators complete the email-code step first.

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\AccountService.cs:72` (excerpt, lines 72-80)

```csharp
var passwordResult = await _signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
if (!passwordResult.Succeeded)
{
    await AuditAsync(actor, "Auth.LoginPassword", "Rejected", user.Id, ct);
    feedback.Error = passwordResult.IsLockedOut
        ? "This account is temporarily locked. Try again later."
        : "The email or password is incorrect.";
    return feedback.Result(AccountStep.Login);
}
```

**Read the code:** await waits for Identity to finish. lockoutOnFailure: true counts a bad password toward lockout for a lockout-enabled account. Succeeded controls whether the workflow continues; IsLockedOut selects the temporary-lock message.

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\AccountService.cs:103` (excerpt, lines 103-105)

```csharp
await AuditAsync(actor, "Auth.Login", "Succeeded", user.Id, ct);
await _signIn.SignInAsync(user, isPersistent: false);
return feedback.Result(AccountStep.Authenticated, path: await WorkspaceForAsync(user));
```

**isPersistent: false** means the cookie is not deliberately made persistent across browser sessions. It does not replace the configured authentication-ticket lifetime. WorkspaceForAsync chooses /admin/dashboard, /custodian/dashboard or /borrower/home.

## What logout clears

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\AccountService.cs:364` (excerpt, lines 364-370)

```csharp
public async Task<OperationResult<AccountFlow>> LogoutAsync(RequestContext actor, CancellationToken ct = default)
{
    var feedback = new FlowFeedback();
    await AuditAsync(actor, "Auth.Logout", "Succeeded", actor.UserId, ct);
    await _signIn.SignOutAsync();
    flow.Clear();
    return feedback.Result(AccountStep.Login, transition: true);
```

SignOutAsync removes the authentication cookie through Identity. flow.Clear() clears the separate session-backed workflow values, including pending-code/reset state. The audit records that logout occurred.

## Administrator email code: where does it go?

It is addressed to the account used for sign-in. For demo.admin@campusgear.local, the current Development provider stores it locally at **http://localhost:5264/development/email**; it does not send Gmail. The code lasts 10 minutes, has five attempts and a 60-second resend cooldown. SMTP delivery is a different provider.

**Be accurate:** the WBS mentions email or student/faculty ID. This implementation finds accounts by email; do not claim student-ID login is implemented. Signup waits for administrator approval and does not send a signup verification code.


<!-- pagebreak -->

WBS 5.0  /  ACCESS PROTECTION

# 2. Lockout, cookies and sessions

An **account lockout** limits repeated bad passwords. A **login cookie** identifies an authenticated browser. The **workflow session** stores temporary steps such as a pending administrator code. They solve different problems.

**File:** `C:\CampusGear\CampusGear\CampusGear.WebApp\Program.cs:26` (excerpt, lines 26-34)

```csharp
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Password.RequiredLength = 8;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
```

Five failed password attempts can lock a lockout-enabled account for 15 minutes. Identity maintains failure counts and LockoutEnd. AllowedForNewUsers enables lockout for newly created accounts; avoid claiming that this setting retroactively changes every possible old account.

| Control | Current setting | Meaning |
| --- | --- | --- |
| Password lockout | 5 failures / 15 minutes | Account-level protection against repeated incorrect passwords. |
| CampusGear.Auth | 8-hour ticket lifetime | Authentication state; the app signs in with isPersistent: false. |
| CampusGear.Flow | 15-minute idle timeout | Temporary sign-in/reset workflow state, separate from login. |
| Email challenge | 10 minutes / 5 attempts | Administrator-code validity and attempt limit. |
| Auth POST limiter | 20 requests / minute / IP | Separate request-rate control; rejected excess requests get 429. |

**File:** `C:\CampusGear\CampusGear\CampusGear.WebApp\Program.cs:64` (excerpt, lines 64-75)

```csharp
});
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "CampusGear.Flow";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(15);
```

ValidationInterval = TimeSpan.Zero makes security-stamp validation happen without a waiting interval. This helps detect account/security changes on later authenticated requests. The session timeout does **not** mean every logged-in user is automatically logged out after 15 idle minutes.

## How deactivation affects an existing login

**File:** `C:\CampusGear\CampusGear\CampusGear.WebApp\Program.cs:154` (excerpt, lines 154-161)

```csharp
if (context.User.Identity?.IsAuthenticated == true)
{
    var users = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
    var user = await users.GetUserAsync(context.User);
    if (user is null || !user.IsActive || user.ApprovalStatus != AccountApprovalStatus.Approved)
    {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        context.User = new System.Security.Claims.ClaimsPrincipal();
```

On an authenticated request, the middleware re-reads the account. If it is missing, inactive or no longer approved, the app signs it out and clears the request user. Changing access is therefore enforced beyond just hiding a menu.


<!-- pagebreak -->

WBS 25.0  /  THE OVERLAP RULE

# 3. Reservation conflict checking

A conflict means **the same physical equipment unit** is already held or booked for an overlapping interval. Two laptops with the same display name can have different equipment IDs and asset codes, so reserving one does not automatically reserve every copy.

**File:** `C:\CampusGear\CampusGear\CampusGear.Data\Repositories\ReservationRepository.cs:14` (excerpt, lines 14-17)

```csharp
public Task<bool> HasConflictAsync(Guid itemId, DateTime start, DateTime end, DateTime now, Guid? excluding, CancellationToken ct) =>
    db.Reservations.AnyAsync(x => x.EquipmentItemId == itemId && (excluding == null || x.Id != excluding) &&
        x.StartAtUtc < end && x.EndAtUtc > start && (x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Released ||
        x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc != null && x.HoldExpiresAtUtc > now && x.StartAtUtc > now), ct);
```

**Read the query in order:** match EquipmentItemId; optionally exclude the record currently being checked; test overlap; then test whether the existing record is a blocking status. AnyAsync asks the database whether at least one matching record exists.

## The overlap formula

**existing.Start < requested.End AND existing.End > requested.Start**. Both sides must be true. The strict comparisons allow adjacent bookings. Think of a slot as [start, end): the end instant is available for the next booking.

| Existing booking | Requested booking | Result |
| --- | --- | --- |
| 10:00-11:00 | 10:30-11:30 | Conflict: shared time. |
| 10:00-11:00 | 09:30-10:30 | Conflict: shared time. |
| 10:00-11:00 | 10:00-11:00 | Conflict: identical slot. |
| 10:00-11:00 | 11:00-12:00 | Allowed by overlap rule: adjacent. |
| 10:00-11:00, unit A | 10:30-11:30, unit B | No same-unit conflict; other rules still apply. |

## Which statuses block the slot?

- **Approved and Released:** included in the overlap query.

- **Pending:** included only while the hold is unexpired and the reservation start is still in the future.

- **Rejected, Cancelled, Expired and Completed:** excluded by this conflict query. Other availability rules still apply, such as repair holds and outstanding physical loans at release.

## Why pending requests already hold equipment

Without a temporary hold, multiple users could request the exact same unit/time while all requests are still waiting for a custodian. CampusGear gives a pending request a hold until the earlier of 24 hours after creation or its start time.

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\Reservations\ReservationService.cs:85` (excerpt, lines 85-92)

```csharp
BorrowerProfileId = profile.Id,
EquipmentItemId = command.EquipmentItemId,
StartAtUtc = command.StartAtUtc,
EndAtUtc = command.EndAtUtc,
HoldExpiresAtUtc = Earlier(now.Add(PendingHold), command.StartAtUtc),
Status = ReservationStatus.Pending,
Purpose = purpose,
Notes = notes,
```

**Time rule:** the workflow uses UTC timestamps for comparisons; input/display uses Manila time. A request must have a future start and an end after its start. A visually “available” calendar is helpful, but the service must check again when saving.


<!-- pagebreak -->

WBS 25.0  /  TRANSACTIONS AND LOCKS

# 4. Preventing simultaneous double bookings

A correct overlap formula alone is insufficient. Two requests could both read “available” before either saves. This is a **race condition**: the result depends on timing between concurrent operations.

## The actual protection

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\Reservations\ReservationService.cs:387` (excerpt, lines 387-400)

```csharp
private async Task<IDataTransaction> BeginItemTransactionAsync(
    Guid itemId, CancellationToken cancellationToken)
{
    var transaction = await work.BeginAsync(IsolationLevel.ReadCommitted, cancellationToken);
    try
    {
        await AcquireTransactionLockAsync($"CampusGear:EquipmentItem:{itemId:N}", cancellationToken);
        return transaction;
    }
    catch
    {
        await transaction.DisposeAsync();
        throw;
    }
```

BeginItemTransactionAsync starts a database transaction and acquires a named lock for one equipment ID. The resource includes itemId, so competing operations for that item share the same lock. A failure disposes the transaction before rethrowing.

**File:** `C:\CampusGear\CampusGear\CampusGear.Data\WorkflowLockManager.cs:13` (excerpt, lines 13-18)

```csharp
var result = new SqlParameter("@lockResult", SqlDbType.Int) { Direction = ParameterDirection.Output };
var key = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
await db.Database.ExecuteSqlRawAsync(
    "EXEC @lockResult = sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000",
    new object[] { result, key }, cancellationToken);
if (result.Value is not int code || code < 0) throw new PersistenceException(PersistenceFailure.Busy);
```

**Exclusive:** one transaction owns the resource at a time. **Transaction ownership:** the lock lasts until that transaction finishes. **10000:** the SQL lock wait is bounded to ten seconds. Negative/invalid results become a Busy failure; the service tells the user to retry.

## Explain the two-request example

- Request A acquires the item lock. Request B waits for the same item.

- A checks the database under the lock, creates the pending reservation and commits.

- B then acquires the lock and rechecks. A’s reservation is now visible, so the overlap is refused.

- Different item IDs use different lock resources. A database-owned lock can coordinate requests across app processes using the same SQL database.

## Why the request is checked again

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\Reservations\ReservationService.cs:57` (excerpt, lines 57-61)

```csharp

// The second lookup is essential: another request may have committed while we waited for the item lock.
existing = await records.FindByRequestIdAsync(command.RequestId, cancellationToken);
if (existing is not null)
    return ExistingRequestResult(existing, borrower.Id, command, purpose, notes);
```

RequestId also handles duplicate submissions. A repeat of the same compatible request can reuse the existing result instead of adding another record. This is **idempotency**. It is different from rejecting a new overlapping booking, which has a different request ID.

The service also takes a borrower-profile lock and reads fresh eligibility after waiting. That matters if the admin pauses borrowing or changes the account’s role while a request is waiting.


<!-- pagebreak -->

WBS 28.0  /  CUSTODIAN DECISIONS

# 5. Request approval and rejection

A reservation decision belongs to the **Custodian**. Administrator approval of a new user registration is a separate feature. Do not mix up these two meanings of “approval.”

## Approval is a fresh decision, not just a status toggle

- Require an active, verified custodian; find and lock the item/reservation.

- If already approved, return the existing result. Otherwise expire an overdue pending hold, or reject a state that is no longer Pending.

- Lock the borrower profile, recheck time and eligibility, verify equipment usability, and recheck overlapping reservations.

- Store the decision, add an audit event, save and commit the transaction.

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\Reservations\ReservationService.cs:141` (excerpt, lines 141-149)

```csharp
var before = reservation.Status;
reservation.Status = ReservationStatus.Approved;
reservation.HoldExpiresAtUtc = null;
reservation.DecidedByUserId = actor.UserId;
reservation.DecidedAtUtc = now;
reservation.UpdatedAtUtc = now;
AddAudit(actor, "reservation.approved", reservation, before, now);
await SaveAsync(cancellationToken);
await transaction.CommitAsync(cancellationToken);
```

The hold is cleared because the request is now an approved booking rather than a temporary pending hold. The custodian ID and decision time make the decision traceable. The audit is saved with the operation.

## Rejection must include an explanation

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\Reservations\ReservationService.cs:155` (excerpt, lines 155-157)

```csharp
{
    await RequireCustodianAsync(actor, cancellationToken);
    var decisionReason = RequiredText(reason, 1000, "A rejection reason is required.");
```

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\Reservations\ReservationService.cs:170` (excerpt, lines 170-180)

```csharp
var before = reservation.Status;
reservation.Status = ReservationStatus.Rejected;
reservation.HoldExpiresAtUtc = null;
reservation.DecisionReason = decisionReason;
reservation.DecidedByUserId = actor.UserId;
reservation.DecidedAtUtc = now;
reservation.UpdatedAtUtc = now;
AddAudit(actor, "reservation.rejected", reservation, before, now);
await SaveAsync(cancellationToken);
await transaction.CommitAsync(cancellationToken);
return ToResult(reservation);
```

RequiredText rejects blank reasons and enforces the maximum length. Rejection records who decided, when, why and the resulting status, then saves and commits. A repeated rejection returns the existing rejected result after its initial input/actor checks.

## Useful state rules

| Action | Normal starting state | Result |
| --- | --- | --- |
| Approve | Pending, unexpired, still eligible/available | Approved; no loan is created yet. |
| Reject | Pending, unexpired; reason required | Rejected; hold is cleared. |
| Release | Approved, inside its booking window | Released; a Loan is recorded. |
| Return | Open released loan | Completed; damage may create a repair hold. |

**Boundary:** release and return are neighboring features, not the same action as approval. Approval does not prove the borrower has physically collected the item.


<!-- pagebreak -->

WBS 45.0  /  WHO MAY DO WHAT

# 6. Menus and server authorization

**Authorization** answers “What may this user do?” CampusGear shows a role-specific menu, then checks permissions on the server. A user can type a URL or submit a request without clicking the menu, so the menu cannot be the only guard.

## Presentation: selecting the menu

**File:** `C:\CampusGear\CampusGear\CampusGear.WebApp\Models\FigmaUi.cs:31` (excerpt, lines 31-36)

```csharp
    _ => [
        new("/borrower/home", "Home", "home"),
        new("/borrower/reservation", "Browse Equipment", "box"),
        new("/borrower/history", "My Reservations", "history"),
        new("/borrower/calendar", "Availability", "calendar")]
};
```

Navigation(role) is a switch with Administrator, Custodian and borrower menu entries. The borrower entries expose Home, Browse Equipment, My Reservations and Availability. Administrator and Custodian each have their own fixed entries; there is no per-role menu editor.

## HTTP layer: refusing the wrong role

**File:** `C:\CampusGear\CampusGear\CampusGear.WebApp\Controllers\CustodianController.cs:22` (excerpt, lines 22-24)

```csharp
[Authorize(Roles = "Custodian")]
[HttpGet("custodian/approvals")]
public async Task<IActionResult> Approvals(string? q, string? status, string? from, string? to,
```

Authorize(Roles = "Custodian") protects the endpoint. The reservation service additionally calls RequireCustodianAsync before sensitive workflow operations, so the business rule is not enforced only by the MVC controller.

**File:** `C:\CampusGear\CampusGear\CampusGear.Services\Services\Reservations\ReservationService.cs:453` (excerpt, lines 453-458)

```csharp
private async Task RequireCustodianAsync(ReservationActor actor, CancellationToken cancellationToken)
{
    var user = await RequireActiveVerifiedUserAsync(actor, cancellationToken);
    if (!await users.IsInRoleAsync(user, "Custodian"))
        throw Error(ReservationErrorCode.Forbidden, "Only a custodian can do this.");
}
```

| Role | Main allowed work | Important restriction |
| --- | --- | --- |
| Borrower | Search/request equipment, own history/calendar, cancel own requests. | Cannot approve or manage users; cannot see another borrower’s private history. |
| Custodian | Approve/reject, release, record returns, maintain equipment. | Cannot manage accounts or edit equipment/category master records. |
| Administrator | Accounts/roles, inventory/categories, eligibility, maintenance, audit. | Borrowing reports/details are read-only; cannot approve/release/return reservations. |

## Explain HTTP outcomes correctly

- **302 to login:** an anonymous browser navigating to a protected page.

- **401:** unauthenticated background requests or form posts.

- **403:** the user is signed in but lacks the required role.

- **404:** sometimes used for records outside the borrower’s own scope, avoiding disclosure that another user’s record exists.

**Defense answer:** “The menu improves usability; authorization enforces security. We test both visible navigation and direct endpoint access.”


<!-- pagebreak -->

PRACTICE  /  THREE ROLES, ONE SHARED DATABASE

# 7. Hands-on demonstration plan

App: **http://localhost:5264/auth/login**. Use separate browsers/profiles to keep role sessions apart, or sign out before switching. Open the local development mailbox for an administrator code; no message goes to Gmail in the current local launch.

| Role / account | Where to start | What to show |
| --- | --- | --- |
| demo.borrower@campusgear.local | My Reservations / Browse Equipment | Own requests, cancellation, conflict handling. |
| demo.custodian@campusgear.local | Approval & Release / Returns | Fresh decisions, release-ready item, open loans. |
| demo.admin@campusgear.local | Users & Roles / Borrower Profiles | Account approvals, roles, paused borrowing, reports. |

## A practical rehearsal sequence

- **1. Login and logout:** sign in as Borrower, note the correct landing page, sign out and try reopening a protected route.

- **2. Menu access:** as Borrower, open /admin/users directly. Expect 403. Show that the admin link is absent from the borrower menu.

- **3. Conflict:** request a suitable available unit tomorrow for a chosen interval. Submit a second request for that exact unit and an overlapping interval. Expect refusal. Then explain why a neighboring interval can pass the overlap rule.

- **4. Approve:** as Custodian, search DM-TRY in Approval & Release and approve PENDING-APPROVE. Refresh the borrower history to see the change.

- **5. Reject:** reject PENDING-REJECT with a reason. Show the decision and reason. Use PENDING-CANCEL for a borrower cancellation demonstration.

- **6. Release/return context:** READY was seeded as approved during its booking window. RETURN and OVERDUE are open loans; use them to practice an on-time and late return. These are related workflow examples.

- **7. Admin accounts:** Users & Roles → Pending contains demo.pending.approve and demo.pending.reject on @campusgear.local. Approve one, reject the other. This is account approval, not reservation approval.

- **8. Eligibility:** demo.paused@campusgear.local is approved but borrowing-ineligible. Edit its eligibility as Admin. Explain that account activity, registration approval and borrowing eligibility are separate controls.

## What was populated

At setup on 14 November, the main demo borrower had 42 reservations, including eight dedicated DM-TRY examples: three pending, one approved, two released and two completed. One completed damaged return has an open maintenance case. A spare demo.custodian.backup account is available for account/role tests.

**Time-sensitive data:** fresh pending examples had 23-hour holds and READY had an eight-hour release window from insertion. They expire naturally and actions change their state. If a prepared item is no longer actionable, create a new future reservation and repeat the workflow; do not treat expired sample data as a code failure.

Credentials remain in the ignored local file C:\CampusGear\.env.demo for the three original accounts. No passwords are printed in this guide. New practice accounts use the same user-requested demo password; pending accounts cannot sign in until approved.


<!-- pagebreak -->

REHEARSE  /  ANSWER THE REASON, THEN POINT TO CODE

# 8. Likely defense questions

## Why use ASP.NET Identity?

It handles password hashing/verification, users, roles, login cookies and lockout through a standard framework. We configure its rules and call its managers rather than comparing plaintext passwords.

## Authentication versus authorization?

Authentication establishes the user’s identity. Authorization checks whether that identity may perform an action. A valid Borrower login still cannot access administrator user management.

## Why do some accounts need an email code?

Administrator access has an extra sign-in step. A correct password is checked before a pending-code flow is created; the auth cookie is issued only after code verification when required. Development email appears in the local mailbox.

## Are the two 15-minute settings the same?

No. Password lockout blocks repeated failed sign-ins for 15 minutes. Workflow-session idle expiry removes temporary flow state after 15 idle minutes. The login ticket has a separate eight-hour configured lifetime.

## How do you prevent double booking?

We compare the same item’s blocking intervals using start < requestedEnd and end > requestedStart, while holding the shared per-item database lock inside a transaction.

## Why not check availability only in the calendar?

The calendar is a view of data at one moment. Another request can arrive before submission or approval, so the service rechecks the database under a lock when making the decision.

## What if two requests arrive at the same time?

One acquires the item lock first. The other waits, then checks again after the first transaction commits. Integration tests verify exactly one winner for simultaneous overlapping requests.

## Why permit 10-11 followed by 11-12?

The overlap comparisons are strict. The first booking excludes its end instant, so those intervals do not share time. Release still checks for an outstanding physical loan.

## Why check eligibility again at approval?

Eligibility or the account role may change after request submission. The service locks the borrower profile and fetches current data before allowing the decision.

## Can the administrator approve a reservation?

No. The Administrator manages account approvals, roles, inventory and reports. Reservation approval, release and return belong to the Custodian.

## Why store a rejection reason and audit?

The borrower needs an explanation and staff need accountability. The decision records actor, time, status and reason; the audit provides an additional trace of the change.

## Does hiding a link protect the feature?

No. The endpoint and service still validate the role. We test direct URL access and unauthorized form actions, not just which menu entries are displayed.


<!-- pagebreak -->

REFERENCE  /  OPEN THESE FILES DURING YOUR EXPLANATION

# 9. Code map, evidence and final rehearsal

All paths below are under **C:\CampusGear\CampusGear\**. The earlier code excerpts print full paths and verified line numbers. Line numbers describe main 5f1fffe and may move after later edits.

| File within that folder | Function or purpose |
| --- | --- |
| CampusGear.WebApp/Controllers/AccountController.cs | Login form endpoints, input validation and HTTP mapping. |
| CampusGear.Services/Services/AccountService.cs | LoginAsync:36; TwoFactorAsync:191; LogoutAsync:364; WorkspaceForAsync:372. |
| CampusGear.WebApp/Program.cs | Identity lockout:31; cookie lifetime:48; session idle:75; account-state middleware. |
| CampusGear.Services/Services/Reservations/ReservationService.cs | CreateAsync:23; ApproveAsync:112; RejectAsync:153; BeginItemTransactionAsync:387; RequireCustodianAsync:453. |
| CampusGear.Data/Repositories/ReservationRepository.cs | HasConflictAsync:14, the same-unit interval/status query. |
| CampusGear.Data/WorkflowLockManager.cs | AcquireAsync:11, SQL Server sp_getapplock. |
| CampusGear.WebApp/Models/FigmaUi.cs | Navigation:10, role-specific menu entries. |
| CampusGear.WebApp/Controllers/CustodianController.cs | Custodian authorization and reservation action endpoints. |

## Tests you can explain

- **CampusGear.IntegrationTests/ReservationServiceSqlTests.cs:** concurrent overlap, duplicate request IDs, adjacent slots, hold expiry, eligibility changes and release/return behavior.

- **CampusGear.IntegrationTests/RolePermissionTests.cs:** allowed pages/actions by role, menus, correct dashboard, own-record privacy and deactivated-session access.

- **CampusGear.IntegrationTests/AccountFlowIsolationTests.cs:** pending admin/reset flows invalidated when security stamps change. These tests use disposable SQL databases rather than the working demo database.

**Verification already performed:** full solution build: zero warnings/errors; 206 integration tests passed; later local population checks: 29 HTTP checks passed, including all three sign-ins and seeded-data visibility. These are recorded earlier session results, not a new test run performed while generating this PDF. Visual browser rendering and real SMTP delivery were not part of those checks.

## Your final 60-second explanation

“The controller receives a request, the service applies the rules, and repositories access SQL through EF Core. Identity validates login and lockout. Authentication cookies and workflow sessions are separate. Reservations use a same-item overlap query inside a transaction-owned SQL lock, so concurrent requests cannot both reserve the same slot. Custodian approval rechecks the current account, item and schedule, while rejection requires a reason. Menus are role-specific and the server enforces access independently.”

## Source and scope

Assignment source: WBS_GearIt_CampusGear_20260909, Jumpstart Program Schedule, C1 and C5/C6/C26/C29/C46 with F/G task labels. Function List describes the business functions. Source link: [Open the WBS spreadsheet](https://docs.google.com/spreadsheets/d/1dCRL3SSA4QlFVNnP0F0FTW7dlmqVIJE1P8PdNyN2x6Q/edit).

Code excerpts are taken directly from the checkout and may omit surrounding validation. Study what this implementation actually does; distinguish the WBS wording, your assigned scope and contributions made by the team.


<!-- pagebreak -->

SYSTEM WALKTHROUGH  /  ADMINISTRATOR ROLE

# 10. Administrator flow and abilities

**Purpose:** keep accounts, equipment and borrowing records organized. The admin manages access and resources; custodians handle the physical borrowing desk.

## Start here: sign in and orient yourself

Open **http://localhost:5264/auth/login** and sign in as demo.admin@campusgear.local. For the current Development setup, read the code at **http://localhost:5264/development/email**, then return to the sign-in tab and enter it. Nothing is sent to Gmail. Successful verification opens **/admin/dashboard**.

## What each admin menu does

| Menu / URL | Admin abilities and limits |
| --- | --- |
| Dashboard
/admin/dashboard | Review the equipment and borrowing overview before deciding where to investigate. |
| Users & Roles
/admin/users | Approve/reject pending registrations; create accounts; assign Borrower, Custodian or Administrator; edit/deactivate accounts. New-account form: /admin/users/new. |
| Borrower Profiles
/admin/borrowers | Review/edit borrower details and borrowing eligibility. Pausing eligibility restricts borrowing; deactivating the account blocks sign-in. |
| Equipment Categories
/admin/categories | Create/edit categories so items are grouped consistently. Review any deletion warning before confirming. |
| Equipment Items
/admin/equipment | Create/edit physical units, including asset code, category, details, serial, condition, location and photo. Each unit has its own identity. |
| Maintenance
/admin/maintenance | Open a repair case for suitable equipment, update case/status/condition, and close it after repair. Operational item availability follows the records. |
| Borrowing History
/admin/history | Read all borrowers’ history; filter by borrower, item, dates and status, including late records. Reservation details are read-only for admin. |
| Availability Calendar
/admin/calendar | Review daily/weekly availability to monitor free, reserved, borrowed and maintenance equipment. No reservation-processing actions here. |
| Audit Log
/admin/audit | Search/review who changed what, when, the outcome, and recorded before/after details. Audit is a read-only accountability view. |

## A complete practice flow

**1.** Check Dashboard. **2.** Users & Roles → Pending: approve demo.pending.approve and reject demo.pending.reject on @campusgear.local, if still pending. **3.** Borrower Profiles: find demo.paused@campusgear.local and enable borrowing eligibility. **4.** Add/edit a sample category and equipment unit. **5.** Search DM-TRY in inventory/history; review its damaged-return maintenance example. **6.** Compare History, Calendar and Audit Log, then sign out. Switch to a borrower/custodian account to observe the corresponding effects.

## Rules to remember while demonstrating

**Account approval is not reservation approval.** Admin cannot create a borrower reservation or approve/reject/release/return it; use Custodian for those decisions. The app protects your own account from deletion and requires another qualifying admin before removing the last active administrator. Records with history can be deactivated instead of deleted. Borrowed/reserved/available status is derived from workflow records rather than freely set to bypass a loan.

**Code to open:** C:\CampusGear\CampusGear\CampusGear.WebApp\Models\FigmaUi.cs:10 (menus); CampusGear.WebApp\Controllers\AdminUsersController.cs:8 (Administrator guard); CampusGear.Services\Services\UserAdministrationService.cs (account rules). Full project prefix is C:\CampusGear\CampusGear\.
