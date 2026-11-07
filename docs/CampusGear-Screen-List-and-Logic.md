# CampusGear live screens and logic

This inventory supersedes the earlier signup-email-verification screen specification. The 70 generated Figma states remain a separate reference catalog under /demo. For current rules use [permissions](PERMISSIONS.md), [feature traces](FEATURE_TRACES.md), [the beginner guide](CODEBASE_CHEATSHEET.md), and executable feature tests.

| Screen / action | Route | Role / implemented behavior |
|---|---|---|
| Sign in | /auth/login GET/POST | Password and account/approval/lockout decisions; staff/borrower workspace redirects |
| Sign up | /auth/signup GET/POST | Anonymous form; creates PendingApproval Borrower, sends no code |
| Registration submitted | /auth/registration-submitted GET | Wait for administrator approval |
| Administrator code | /auth/two-factor GET/POST; resend-two-factor POST | Pending administrator flow/security stamp; email challenge; existing local Development bypass remains configured behavior |
| Password reset request | /auth/password-reset-request GET/POST | Identical known/unknown-address response |
| Reset code | /auth/password-reset-code GET/POST; resend-password-reset-code POST | Attempts/expiry and account-stamp checks |
| New password | /auth/password-reset-new GET/POST | Valid ten-minute grant; Identity token reset |
| Reset success | /auth/success GET | Completed password recovery |
| Sign out | /auth/logout POST | Audit, sign out, clear flow session |
| Access/status/error | /auth/access-denied; /status/{code}; /Error | Existing status codes and navigation-specific body retained |
| Borrower home | /borrower/home; /borrower/dashboard | Own welcome/upcoming activity and catalog |
| Reservation/catalog | /borrower/reservation GET/POST | Own eligibility, quantity/time validation, deterministic IDs, per-unit workflows |
| Availability request | /borrower/reservation/availability GET | Existing bookableUnits/freeUnits/maxQuantity JSON |
| Cancel own request | /borrower/reservation/{id}/cancel POST | Own pending/approved record; privacy-preserving errors |
| Borrower history/calendar | /borrower/history; /borrower/calendar GET | Own record filtering; busy slots do not expose other borrower identities |
| Custodian dashboard | /custodian/dashboard GET | Desk counts, trends and activity |
| Approvals / return queue | /custodian/approvals; /custodian/returns GET | Custodian only, existing SQL filters/paging |
| Reservation details | /custodian/reservations/{id} GET | Custodian workflow; administrator read-only access retained |
| Approve/reject/release/return | Routes declared by CustodianController | Custodian-only mutations through IReservationService; administrators have no borrowing mutation permission |
| Staff reports/calendar | /custodian/history; /custodian/calendar | Read-only reports and availability |
| Admin reports/calendar | /admin/history; /admin/calendar | Administrator read-only reports and availability |
| Custodian directories | /custodian/categories; /custodian/borrowers | Read-only categories/borrower eligibility lists |
| Administrator dashboard | /admin/dashboard GET | System inventory/account/borrowing statistics |
| Equipment management | /admin/equipment GET; save and delete POST | Administrator only; versions, active-loan/category rules, images and history retention |
| Categories | /admin/categories GET; save/delete POST | Administrator only; active-item/version/unique-name rules |
| Maintenance | /admin/maintenance GET; open/update POST | Administrator and custodian; repair notes/usable condition required to close; other cases keep holds |
| Users/borrowers | /admin/users; /admin/borrowers GET | Administrator filters/paging/newest-first account review |
| Create/edit/delete account | /admin/users/new; /admin/users/{id}/edit; delete POST | Administrator role/access/profile workflows; self/last-admin/history protection |
| Registration decision | /admin/users/{id}/approve or reject POST | Administrator approval confirms email; audit retained |
| Audit | /admin/audit GET | Administrator read-only search/paging |
| Development mailbox | /development/email; /auth/development-inbox | Development + loopback only, no-store; Development sender or SMTP mirror |
| Figma reference | /demo/{workspace}/{screen}?state=... | Generated visual examples; not live backend forms |

Times are displayed/entered in Manila time and persisted in UTC. The source catalog links every live Razor view and custom browser asset. Do not edit generated reference frames to change live business behavior. The [route/action migration map](ROUTE_MIGRATION_MAP.md) lists actual HTTP methods and service/repository paths.
