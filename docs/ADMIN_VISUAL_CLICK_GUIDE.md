> Historical snapshot: this document describes the pre-refactor implementation/UI. For current code use [the code guide](CODEBASE_CHEATSHEET.md) and [feature traces](FEATURE_TRACES.md). Existing PDFs are preserved.

# CampusGear admin visual click guide

Screens captured from the running local app on 2026-11-14. No source code in this guide.


## 1. Sign in with your admin account

1. **Enter your email:** Use demo.admin@campusgear.local for the local demonstration.

2. **Enter your password:** Use the demo password already provided. The field hides it.

3. **Click SIGN IN:** An administrator must complete the verification step next.

Expected / next: Next: the administrator verification screen. If sign-in fails, check the account and password.


## 2. Get the code, then verify

1. **Enter the newest 6-digit code:** Open localhost:5264/development/email in another tab and find your admin message.

2. **Click VERIFY AND CONTINUE:** Return to this tab to submit the code and enter the workspace.

3. **Use Resend only when needed:** The code expires after 10 minutes. Respect the resend cooldown; use the latest code.

Expected / next: Expected: Administrator Dashboard. This local setup stores the message in its mailbox, not Gmail.


## 3. Start from the dashboard

1. **Click Verify registrations:** This shortcut opens new registrations waiting for an admin decision.

2. **Use Users & Roles for accounts:** Use the left sidebar to switch tasks. The highlighted item shows your location.

3. **Remember where Log out is:** Use it after finishing the session; the final guide step returns here.

Expected / next: Flow: People first, then Inventory, then History/Calendar/Audit. Counts change as you use the demo.


## 4. Find an account or create one

1. **Search for the person:** Type a name or email. Wait briefly for the matching results to update.

2. **Click Create account for a new person:** Existing people should be edited from their own row instead.

3. **Filter account status:** Choose Pending approval, Active, Inactive or Rejected. Role is another nearby filter.

Expected / next: Choose the next task: pending decisions on the next page, or account creation on the page after that.


## 5. Approve or reject a registration

1. **Confirm Pending approval:** Review the person’s name and email before making a decision.

2. **Approve the correct person:** The marked example is Demo Pending Approve. Approval admits the account to the system.

3. **Reject only the intended person:** The marked example is Demo Pending Reject. Do not confuse registration with reservation approval.

Expected / next: After your decision, review the updated status/list. These are account decisions; custodians decide reservations.


## 6. Create an account: identity and role

1. **Choose the right role:** Borrower requests equipment; Custodian handles the desk; Administrator manages the system.

2. **Enter the person’s email:** Also complete First name, Last name, Temporary password and any contact details shown.

Expected / next: Follow the password requirements printed on the form. Then scroll down for borrower-only details and Save changes.


## 7. Create an account: finish and save

1. **Complete borrower-only details:** Use Student ID Number and Department for a borrower. Staff roles do not need those fields.

2. **Click Save changes when reviewed:** The new account is approved directly. Confirm it appears in Users & Roles after saving.

Expected / next: Expected: saved account in the account list, or a clear validation message describing what to correct.


## 8. Find a borrower to manage eligibility

1. **Search for the borrower:** From Borrower Profiles, type demo.paused to find the prepared practice record.

2. **Click that person’s Edit:** Check the name and email so you do not change a different person’s access.

Expected / next: Next: the account editor. The list shows this example as active with borrowing access paused.


## 9. Understand account role and access

1. **Choose Role only when necessary:** Changing it changes the person’s workspace and permitted actions.

2. **Account is active controls sign-in:** Clearing this deactivates the account. It is different from pausing borrowing eligibility.

Expected / next: For borrowing eligibility, scroll down to Borrower profile & borrowing access. Save only intentional changes.


## 10. Allow or pause borrowing, then save

1. **Set Eligible to reserve equipment:** Check it to permit eligible borrowing; clear it to pause borrowing. Add an explanatory note.

2. **Click Save changes:** Then confirm the borrower’s access label in the list. Cancel abandons the edits.

Expected / next: An approved, active borrower may still sign in while borrowing is paused. Account deactivation blocks sign-in.


## 11. Open equipment category management

1. **Click Equipment Categories:** Check the existing list before creating a duplicate grouping.

2. **Click Add category:** To change an existing category, use its own Edit button instead.

Expected / next: Next: the category form. Create the category before assigning a new item to it.


## 12. Fill the category form and save

1. **Enter the category name:** Add a helpful description. Choose a name borrowers and staff can understand.

2. **Review Active category:** A category with active equipment must remain active.

3. **Click Save changes:** Confirm the category appears in the list; Cancel returns without saving.

Expected / next: Next task: Equipment Items. You can now assign a physical unit to the category.


## 13. Find a unit or add equipment

1. **Search inventory for an existing unit:** Use its asset code or name. Edit item opens the selected unit’s own form.

2. **Click Add item for a new unit:** Each physical unit gets its own record, even if several units share a name.

Expected / next: Next: the equipment form. Use separate asset codes for separate physical units.


## 14. Enter the equipment identity

1. **Enter Equipment name:** Use the name people will recognize in the catalog.

2. **Enter a unique Asset code:** Identify this exact physical unit. Add the serial number when available.

3. **Choose its Category:** Review Condition too. Then scroll down to the storage, description and photo fields.

Expected / next: Continue down the form. Do not use another unit’s asset code.


## 15. Finish the equipment form and save

1. **Review Active inventory item:** Complete storage location, description and an optional photo before saving.

2. **Click Save changes:** Return to inventory and confirm the name, category, condition and location are correct.

Expected / next: Damaged/unserviceable units enter maintenance. An equipment unit with an active loan cannot be edited.


## 16. Find a repair or start a new one

1. **Click Open maintenance for a new case:** Use this only when the equipment needs a repair hold and has no existing case.

2. **Search cases to avoid duplicates:** You can also switch between the Open and Closed lists.

3. **Click Update on the correct case:** The marked example is the damaged DM-TRY practice item.

Expected / next: Choose your route: opening a new case is next; updating an existing case follows it.


## 17. Open a maintenance case

1. **Choose the equipment unit:** Units already on loan or in maintenance cannot be selected for a new case.

2. **Describe the issue:** Write what is wrong so the next person knows why the unit is held.

3. **Click Open maintenance case when ready:** The hold blocks new reservations/releases. Cancel closes this form without saving.

Expected / next: Expected: a new case in the Open list. If the item is on loan, the custodian must record its return first.


## 18. Update a repair and complete it

1. **Choose the repair Status:** Use Open or In progress while work continues; choose Closed - repair complete when finished.

2. **Choose Condition after repair:** This is applied when closing the case. Complete the Repair notes, required to close it.

3. **Click Save maintenance:** Confirm the case status afterwards. Use Cancel to leave it unchanged.

Expected / next: Closing a case records the repair; the unit’s actual availability still depends on other bookings/loans.


## 19. Filter borrowing history

1. **Search borrower or equipment:** Use a name, email, item name or asset code, such as DM-TRY.

2. **Choose a Status:** Booking from/through filters are nearby. Use Reset to clear filters that hide records.

3. **Click Review details:** Open the correct row to inspect dates, item and borrower information.

Expected / next: Administrator borrowing history is read-only. Reservation approval, release and return belong to the Custodian.


## 20. Read the reservation, then return

1. **Click Back to borrowing history when done:** Before leaving, read the borrower, equipment, booking dates and current status. Scroll for more detail.

Expected / next: This is an oversight record. Admin does not approve/reject equipment requests or record release/return here.


## 21. Check a unit’s calendar

1. **Search, then select the equipment:** Click a unit in the list below the search field to load its calendar.

2. **Move between months:** Use the left/right month arrows to inspect another period.

3. **Click the date you want:** Scroll down for that day’s details for the selected unit. Times are shown in Manila time.

Expected / next: This actual screen uses a month grid with day selection. It is for monitoring, not creating a borrower reservation.


## 22. Review the audit trail

1. **Search audit events:** Use a relevant action/person/record term and wait for the results. Clear removes the filter.

2. **Click View details:** Read the action, time, actor, result and additional recorded information.

Expected / next: Use audit to investigate a change. Make any new correction on the proper management screen, not in the audit history.


## 23. Finish the admin session

1. **Click Log out at the bottom left:** Finish any intended form save first. Log out ends the signed-in session.

Expected / next: Expected: the sign-in page. Reopening a protected admin page requires signing in again.
