> Historical snapshot: this document describes the pre-refactor implementation/UI. For current code use [the code guide](CODEBASE_CHEATSHEET.md) and [feature traces](FEATURE_TRACES.md). Existing PDFs are preserved.

USER GUIDE  /  14 NOVEMBER 2026

# CampusGear - Administrator flow guide

A practical guide to using the Administrator workspace: signing in, managing accounts and equipment, checking activity, and finishing a session. Follow the steps in order when learning the system.

## What the administrator is responsible for

The administrator keeps access, inventory and records organized. You approve new user registrations, maintain accounts and roles, manage equipment and categories, control borrower eligibility, oversee repairs, and review borrowing activity.

**Role boundary:** borrowers submit their own reservations. Custodians approve or reject reservation requests, release equipment and record returns. Administrators can review those records, but cannot perform the custodian borrowing actions.

## 1. Open the system and sign in

- Open **http://localhost:5264/auth/login**. Enter your administrator email and password, then select Sign In.

- For the current local demo account, use **demo.admin@campusgear.local**. Use the demo password already provided to you.

- When the verification-code screen appears, open **http://localhost:5264/development/email** in a second tab. Find the latest message addressed to your administrator account.

- Return to the sign-in tab, enter the code and submit. The Administrator Dashboard should appear. Keep the system tab open while retrieving the code.

**Where is the email?** This local demo stores messages in its development mailbox. It does not send them to Gmail. The code is addressed to the email used for sign-in. Use the newest code; it expires after ten minutes. If needed, wait for the resend cooldown, request a fresh code and check the mailbox again.

## 2. Get familiar with the workspace

| Menu group | Where you go | Why you go there |
| --- | --- | --- |
| Overview | Dashboard | See the overall equipment and borrowing picture. |
| Inventory | Equipment Items, Equipment Categories, Maintenance | Keep the catalog and repair records accurate. |
| Reservations | Borrowing History, Availability Calendar | Monitor activity and scheduling. |
| People | Borrower Profiles, Users & Roles | Manage access and borrowing eligibility. |
| Accountability | Audit Log | Review the recorded actions and outcomes. |

## A useful daily order

**Dashboard → pending registrations → account/eligibility changes → inventory and maintenance → borrowing reports/calendar → audit review → sign out.** You do not need to visit every area if no action is needed.


<!-- pagebreak -->

ADMIN FLOW  /  ACCOUNTS, ROLES AND ELIGIBILITY

# 1. Manage people and access

## Approve or reject a new registration

- **1.** Open **Users & Roles** and choose the pending-registration view.

- **2.** Review the applicant’s name, email and borrower details. Confirm that the registration should be allowed.

- **3.** Approve an acceptable registration. Reject one that should not be admitted.

- **4.** Refresh or filter the list to confirm the new status. An approved account can sign in when its other access requirements are satisfied; a pending or rejected account cannot sign in.

**Practice:** look for demo.pending.approve@campusgear.local and demo.pending.reject@campusgear.local. If they are still pending, approve the first and reject the second. If someone already processed them, use another pending registration.

## Create an account directly

- **1.** In **Users & Roles**, open the create-account form.

- **2.** Enter the person’s name, email, password and intended role. Add borrower-only details when creating a borrower.

- **3.** Review the information, save, and confirm the account appears in the list.

- **4.** Use a separate browser session to verify the person sees the correct workspace. An account created directly by admin is approved for sign-in.

## Choose the right role

| Role | Use it for |
| --- | --- |
| Borrower | People who request equipment and review their own reservations. |
| Custodian | Staff handling reservation decisions, release and return at the equipment desk. |
| Administrator | People managing accounts, inventory, eligibility, maintenance and oversight. |

## Edit a role, pause borrowing, or deactivate access

Find the person in **Users & Roles**, open the edit screen, change the appropriate fields, save, and review the result. Borrower details and eligibility are also accessible through **Borrower Profiles**.

| Change | What it means |
| --- | --- |
| Approve registration | The person is admitted to the system. This is separate from approving an equipment reservation. |
| Pause borrowing eligibility | The borrower can still sign in to an active, approved account, but cannot make eligible borrowing requests. |
| Deactivate the account | The person cannot sign in; an existing signed-in session loses access on a later request. |
| Change the role | The person’s permitted workspace and actions change to match the assigned role. |

**Practice:** find demo.paused@campusgear.local, review the paused borrowing setting, enable eligibility and save. Use demo.custodian.backup@campusgear.local for spare-account role or deactivation practice.

**Account protections:** you cannot delete your own account. The system protects the last active qualifying administrator. Accounts with history may be deactivated rather than permanently removed, preserving their records.


<!-- pagebreak -->

ADMIN FLOW  /  CATALOG, PHYSICAL UNITS AND MAINTENANCE

# 2. Maintain equipment and repairs

## Start with a category

- **1.** Open **Equipment Categories**. Search or review the list before adding a duplicate category.

- **2.** Open the add/edit form and enter a clear category name and description.

- **3.** Save, then check that the category appears correctly. Use categories that help people find equipment, such as cameras, audio or laptops.

## Register or update a physical item

- **1.** Open **Equipment Items** and choose an existing item to edit or open the new-item form.

- **2.** Select the category. Enter the asset code, name/details, serial number, condition and storage location. Add or update a photo when useful.

- **3.** Review the details carefully, save, and confirm the item in the list.

- **4.** If you are changing an existing item, ensure the correct physical unit is selected. Two units can share the same name but have different asset codes.

**Example:** two identical projectors should have separate item records and asset codes. Borrowing one projector does not automatically mean the other physical unit is borrowed.

## Handle a repair through Maintenance

- **1.** Open **Maintenance** and find the item/case. If a repair case already exists, review it rather than creating a duplicate.

- **2.** For a suitable item needing repair, open a case and describe the problem. The system may prevent opening a case while the unit is on loan or already has a case.

- **3.** As work progresses, update the repair status, equipment condition and notes.

- **4.** When repair is complete, record the resolution and appropriate condition, close the case, and check the item’s resulting availability.

## Understand condition and availability

| Term | How to interpret it |
| --- | --- |
| Condition | The physical state, such as Good, Fair, Damaged or Unserviceable. |
| Borrowed | The unit has been released and its loan remains open. |
| Reserved | An approved booking has started and is currently holding the unit. |
| Maintenance / for repair | The unit is held for repair and should not be treated as freely borrowable. |
| Available | The unit is not blocked by the applicable loan, booking, repair or inactive state. |

Availability follows the borrowing and maintenance records. Do not try to fix an outstanding physical loan by changing an unrelated item field. The custodian records the return; maintenance is completed through the repair workflow.

## Practice with the prepared records

Search **DM-TRY** in Equipment Items. Review the **DAMAGED** example and its repair case. You can also create a clearly named practice category and item. Read deletion messages: historical records may cause deactivation or prevent removal.


<!-- pagebreak -->

ADMIN FLOW  /  HISTORY, CALENDAR AND AUDIT

# 3. Monitor activity and accountability

## Review the dashboard first

Use the **Dashboard** as your starting overview. Look for changes in inventory and borrowing activity, then open the relevant detailed screen. A dashboard summary helps you decide what to inspect; use the underlying records when investigating a specific item or person.

## Use Borrowing History to answer “what happened?”

- **1.** Open **Borrowing History**. Narrow the results using borrower, item, date range and status filters.

- **2.** Look at the request/loan status, scheduled time and relevant return information. Use late records/counts to identify missed due times.

- **3.** Open a reservation’s details when you need more context. Check who borrowed the unit and what stage it reached.

- **4.** Change or clear the filters before concluding that a record is missing.

**Administrator limit:** this is a read-only borrowing view. If a request needs approval, equipment needs releasing, or a return needs recording, the custodian must perform that action from the custodian workspace.

## Use the Availability Calendar to answer “when is it free?”

- **1.** Open **Availability Calendar** and choose the day or week you want to review.

- **2.** Inspect the equipment and occupied time slots. Compare the dates with the borrowing record when something looks unexpected.

- **3.** Remember that a future reservation and a unit currently out on loan are different situations. A return or repair update can change the unit’s availability.

- **4.** Use the calendar for oversight. Administrators do not submit borrower reservations from this view.

## Use the Audit Log to answer “who changed it?”

- **1.** Open **Audit Log** and search for the relevant action or record.

- **2.** Review the time, actor, action and outcome. Inspect available before/after details when tracing a change.

- **3.** Compare the audit entry with the current account, equipment or borrowing record.

- **4.** Treat this as a read-only history. Use the proper management screen for a new correction rather than trying to edit old audit entries.

## A simple investigation example

**Question:** “Why can this borrower not collect this item?” First confirm the person’s account and borrowing eligibility in **People**. Then check the booking in **Borrowing History**, the time slot in the **Calendar**, and any open **Maintenance** case. Review **Audit Log** if you need to understand an earlier change. The custodian handles the actual release decision.

## Use the right screen for the question

| Your question | Start here |
| --- | --- |
| Can this person access the system? | Users & Roles |
| Can this person borrow? | Borrower Profiles / eligibility |
| Which unit and time were requested? | Borrowing History and reservation details |
| What is the current schedule? | Availability Calendar |
| Is the unit being repaired? | Maintenance |
| Who changed the record? | Audit Log |


<!-- pagebreak -->

REHEARSAL  /  EXPECTED RESULTS AND COMMON QUESTIONS

# 4. Complete admin practice session

## Follow this end-to-end sequence

| Step | Action | What to confirm |
| --- | --- | --- |
| 1 | Sign in as the demo administrator and enter the local-mailbox code. | Administrator Dashboard opens. |
| 2 | Review the dashboard and open Users & Roles. | You can find the account list and pending registrations. |
| 3 | Approve one pending demo registration; reject the other. | Each registration’s status changes as intended. |
| 4 | Find the paused demo borrower and enable borrowing eligibility. | The saved borrower record shows borrowing is allowed. |
| 5 | Add or edit a clearly named practice category and item. | The item appears under the correct category with the correct physical-unit details. |
| 6 | Review the damaged DM-TRY item and its maintenance case. | You can explain the condition, repair progress and effect on availability. |
| 7 | Review the demo borrower’s history and the calendar. | You can distinguish pending, approved, borrowed, returned and late records. |
| 8 | Review Audit Log after the management actions. | You can trace the relevant recorded changes and outcomes. |
| 9 | Sign out when finished. | Reopening a protected page requires sign-in. |

## Common questions while using the system

**“The sign-in page says it sent email. Where is it?”** In this local demo, open http://localhost:5264/development/email. Use the latest message for your administrator email. This setup does not deliver to Gmail.

**“Why can I approve a person but not their reservation?”** You manage account registration. The custodian manages reservation approval, equipment release and returns.

**“Why did Delete deactivate the account instead?”** The account has history that the system preserves. Deactivation prevents access without removing the historical connection.

**“Why can’t I use a prepared demo action anymore?”** Another test may already have processed it, or its booking/hold time may have expired. Check its status and dates. Use a fresh pending registration or future reservation for the next test.

**“Why is an equipment record not available?”** Check whether it is out on loan, within a started approved booking, inactive or held for maintenance. Fix the underlying workflow record through the appropriate role.

**“I changed the role, but I still see an older screen.”** Refresh or sign out and back in, then confirm the role on the account record. Use separate browser sessions when comparing admin, custodian and borrower behavior.

## One sentence to remember

“The administrator manages people, resources and oversight; the custodian handles the equipment borrowing desk; the borrower makes and follows their own requests.”

Prepared for the local CampusGear system on 14 November 2026. Demo records change as they are used; follow the current on-screen status and validation messages.
