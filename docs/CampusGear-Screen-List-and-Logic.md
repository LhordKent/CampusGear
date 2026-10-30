# CampusGear: Screen List and Screen Logic

_Recovered from the Claude Code conversation of October 10, 2026._

## Part 1: Screen List

| **Screen** | **Description** | **REMARKS** |
|------------|-----------------|-------------|
| 1.0 Home / Login | The home screen contains the Login section, where registered users sign in with their email address and password. Each user is taken to the workspace for their role after signing in. | The root URL ( / ) redirects to /auth/login. |
| 2.0 Registration | This is the Registration Screen shown when the user clicks the "Create a New Account" link on the Home / Login Screen. The user enters their full name, email address, mobile number, password, and password confirmation. | Self-registration creates Borrower (Student/Faculty) accounts only. |
| 3.0 Email Verification | This screen is shown after registration, or when an unverified user tries to log in. The user enters the verification code sent to their email address and may request a new code. | |
| 4.0 Registration Success | This screen confirms that the email address has been verified and the Borrower account has been activated. It provides a link back to the Login Screen. | |
| 5.0 Administrator Two-Factor Verification | This screen is shown after an Administrator enters a valid email address and password. The Administrator enters the sign-in code sent by email to finish logging in. | Restricted to Administrator accounts. |
| 6.0 Password Reset Request | This screen is shown when the user clicks the "Forgot password?" link on the Home / Login Screen. The user enters their registered email address to receive a password reset code. | |
| 7.0 Password Reset Code Verification | This screen is shown after a reset request is submitted. The user enters the reset code sent to their email address and may request a new code. | |
| 8.0 New Password | This screen is shown after the reset code is verified. The user enters and confirms a new password. | |
| 9.0 Password Reset Success | This screen confirms that the password has been changed and directs the user back to the Login Screen. | |
| 10.0 Access Denied | This screen is shown when a signed-in user tries to open a page that their role is not allowed to access. | System page. |
| 11.0 Borrower Dashboard | This is the main screen shown after a Borrower logs in. It shows reservation summary figures, upcoming reservations, and the borrowing lifecycle. It also provides navigation to the Borrower functions. | Restricted to Borrower (Student/Faculty). |
| 12.0 Equipment Reservation | This screen is accessed through the "Equipment Reservation" menu. The Borrower selects available equipment, enters the request details and schedule, and submits a reservation request. The system checks for schedule conflicts before accepting the request. | Restricted to Borrower. Submission and conflict messages are shown within the screen. |
| 13.0 Borrower Borrowing History Report | This screen is accessed through the "Borrowing History" menu. It lists the Borrower's own reservations and borrowing records, with search and filter options. Pending requests can be cancelled from this screen. | Restricted to Borrower. |
| 14.0 Borrower Availability Calendar | This screen is accessed through the "Availability Calendar" menu. It shows a monthly calendar of the selected equipment's bookings, together with the reserved time slots for a chosen date. | Restricted to Borrower. |
| 15.0 Custodian Dashboard | This is the main screen shown after a Custodian logs in. It shows operational summary figures and the requests awaiting review. It also provides navigation to the Custodian functions. | Restricted to Custodian. |
| 16.0 Approval & Release Queue | This screen is accessed through the "Approval & Release" menu. It lists reservation requests awaiting approval, with search, status, and date filters. | Custodian and Administrator. Administrators open it at /admin/approvals. |
| 17.0 Approval & Release Details | This screen is shown when a request is selected from the Approval & Release Queue. The user reviews the request, then approves or rejects it with a reason. For approved requests, the user records the equipment's condition at release and any notes. | Custodian and Administrator. The rejection form expands within the screen. |
| 18.0 Return & Condition Check Queue | This screen is accessed through the "Return & Condition Check" menu. It lists the approved requests awaiting release and the active loans awaiting return. | Custodian and Administrator. Administrators open it at /admin/returns. |
| 19.0 Return & Condition Check Details | This screen is shown when an active loan is selected from the Return & Condition Check Queue. The user inspects the returned equipment and records its condition at return and any notes. | Custodian and Administrator. |
| 20.0 Staff Borrowing History Report | This screen is accessed through the "Borrowing History" menu. It lists all borrowing and reservation records in the system, with search, status, and date filters. | Custodian and Administrator. Custodians open it under "More operations." Administrators open it at /admin/history. |
| 21.0 Staff Availability Calendar | This screen is accessed through the "Availability Calendar" menu. It shows a monthly calendar of equipment bookings and maintenance status, together with the reserved time slots for a selected day. | Custodian and Administrator. Administrators open it at /admin/calendar. |
| 22.0 Equipment Maintenance | This screen is accessed through the "Equipment Maintenance" link under "More operations." The user opens maintenance cases for equipment items, updates their status, and returns repaired items to availability. | Custodian and Administrator. An open case blocks new reservations and release of the item. |
| 23.0 Administrator Dashboard | This is the main screen shown after an Administrator completes two-factor verification. It shows system-wide summary figures and live borrowing activity. It also provides navigation to the administration functions. | Restricted to Administrator. |
| 24.0 Equipment Categories | This screen is accessed through the "Equipment Categories" menu. It lists all equipment categories and lets the Administrator add a new category or select an existing one to edit. | Restricted to Administrator. |
| 25.0 Add / Edit Equipment Category | This screen is shown when the Administrator clicks the add button or selects a category on the Equipment Categories Screen. The Administrator enters or updates the category information. | Restricted to Administrator. Displayed as an editor panel on the Equipment Categories page. |
| 26.0 Equipment Items | This screen is accessed through the "Equipment Items" menu. It lists the equipment inventory, with search and paging, and lets the Administrator add a new item or select an existing one to edit. | Restricted to Administrator. |
| 27.0 Add / Edit Equipment Item | This screen is shown when the Administrator clicks the add button or selects an item on the Equipment Items Screen. The Administrator enters or updates the item's details, category, condition, and availability. | Restricted to Administrator. Displayed as an editor panel on the Equipment Items page. |
| 28.0 Reservation Management | This screen is accessed through the "Reservation Management" link under "More operations." It lists recent reservation requests and lets the Administrator cancel active reservations. | Restricted to Administrator. |
| 29.0 New Reservation (Administrator) | This screen is shown when the Administrator clicks the add button on the Reservation Management Screen. The Administrator creates a reservation request for an eligible Borrower. | Restricted to Administrator. Displayed as an editor panel on the Reservation Management page. |
| 30.0 Borrower Profiles | This screen is accessed through the "Borrower Profiles" menu. It lists the registered Borrower accounts, with search, status filters, and paging. | Restricted to Administrator. |
| 31.0 User & Role Management | This screen is accessed through the "User & Role Management" menu. It lists all user accounts, with search, role and status filters, and paging. | Restricted to Administrator. |
| 32.0 Edit Account & Role | This screen is shown when the Administrator selects an account on the User & Role Management or Borrower Profiles Screen. The Administrator updates the account information, role, status, and Borrower profile details. | Restricted to Administrator. |
| 33.0 System Audit Log | This screen is accessed through the "System Audit Log" menu. It shows the audit history of system and authentication events, with search and paging. | Restricted to Administrator. |
| 34.0 Development Email Inbox | This screen shows the system emails, such as verification and reset codes, captured in place of real email delivery during local development. | Development environment and local access only. Not available in production. |
| 35.0 Error | This screen is shown when an unexpected system error occurs while a request is being processed. | System page. |

---

## Part 2: Screen Logic

### CampusGear: Screen Logic

These notes cover the same 35 screens as the Screen List, and every message and rule comes from the current code. Self-explanatory behaviour is left out. Rules that apply to every screen are listed once at the end.

### 1.0 Home / Login Logic
```text
Login
  Campus Email
    This is the registered email address of the user
  Password
    This is the password of the user
    This field is shown as masked characters
  Sign In
    Onclick
      Check if input fields are empty or email format is invalid
        Display field validation message
      If email is not registered, account is inactive, or password is incorrect
        Display error message "The email or password is incorrect."
      If 5 consecutive failed attempts
        Lock account for 15 minutes
        Display error message "This account is temporarily locked. Try again later."
      If email is not yet verified
        Send verification code to email
        Redirect to 3.0 Email Verification
      If role is Administrator
        Send sign-in code to email
        Redirect to 5.0 Administrator Two-Factor Verification
      If role is Custodian
        Redirect to 15.0 Custodian Dashboard
      If role is Borrower
        Redirect to 11.0 Borrower Dashboard
      Else
        Redirect to 10.0 Access Denied
  Forgot Password?
    Onclick
      Redirect to 6.0 Password Reset Request
  Create a New Account
    Onclick
      Redirect to 2.0 Registration
Special Cases
  User already signed in
    Redirect to role dashboard
```

### 2.0 Registration Logic
```text
Registration
  Full Name
    This is the full name of the user
    2 to 160 characters
  Email Address
    This is the email address used for login and verification
  Mobile Number
    This is the contact number of the user
  Password
    This is the password of the user
    This field is shown as masked characters
    Minimum of 8 characters
  Confirm Password
    This field is shown as masked characters
    Must match Password
  Send Verification Code
    Onclick
      Check if input fields are empty or invalid
        Display field validation message
      If email is already registered
        If account is pending verification and password matches
          Resend verification code
          Redirect to 3.0 Email Verification
        Else
          Display error message "An account already uses this email address. Sign in to continue verification or reset your password."
      If password does not meet password policy
        Display password policy message
      Else
        Create account with Borrower role and borrower profile
        Send verification code
        Redirect to 3.0 Email Verification
  Already have an account? Return to sign in
    Onclick
      Redirect to 1.0 Home / Login
Special Cases
  Account created but email delivery fails
    Display error message "Your account was created, but we could not send the code. Try resending it."
```

### 3.0 Email Verification Logic
```text
Email Verification
  6-Digit Verification Code
    This is the one-time code sent to the user's email
    Valid for 10 minutes
  Verify Account
    Onclick
      If code is not 6 digits
        Display error message "Enter the six-digit code."
      If code is incorrect or already used
        Display error message "The code is invalid or has already been used."
      If code is expired
        Display error message "The code expired. Request a new one."
      If 5 incorrect attempts
        Display error message "Too many incorrect attempts. Request a new code."
      If account is Borrower
        Sign in user
        Redirect to 4.0 Registration Success
      If account is Custodian or Administrator
        Display message "Email verified. Sign in to continue."
        Redirect to 1.0 Home / Login
  Resend Code
    Onclick
      If requested within 60 seconds of last code
        Display message "Please wait before requesting another code."
      Else
        Send new code
        Display message "A new code was sent to your email address."
  Back to Sign In
    Onclick
      Redirect to 1.0 Home / Login
Special Cases
  Screen opened without a pending registration
    Redirect to 1.0 Home / Login
```

### 4.0 Registration Success Logic
```text
Registration Success
  Continue to Borrower Workspace
    Onclick
      Redirect to 11.0 Borrower Dashboard
```

### 5.0 Administrator Two-Factor Verification Logic
```text
Administrator Two-Factor Verification
  6-Digit Verification Code
    This is the sign-in code sent to the Administrator's email
    Valid for 10 minutes
  Verify and Continue
    Onclick
      Same code validation and messages as 3.0 Email Verification
      If code is valid
        Sign in Administrator
        Redirect to 23.0 Administrator Dashboard
  Resend Verification Code
    Onclick
      Same resend logic as 3.0 Email Verification
  Back to Sign In
    Onclick
      Redirect to 1.0 Home / Login
Special Cases
  Account deactivated, role removed, or credentials changed during sign-in
    Display error message "This sign-in request is no longer valid. Sign in again."
    Redirect to 1.0 Home / Login
```

### 6.0 Password Reset Request Logic
```text
Password Reset Request
  Email Address
    This is the registered email address of the user
  Send Verification Code
    Onclick
      Check if email is empty or invalid
        Display field validation message
      If email belongs to an active account
        Send password reset code
      Display message "If an account uses that email, a reset code has been sent."
      Redirect to 7.0 Password Reset Code Verification
  Back to Sign In
    Onclick
      Redirect to 1.0 Home / Login
Special Cases
  Unregistered or inactive email
    Same message shown (account existence is not revealed)
```

### 7.0 Password Reset Code Verification Logic
```text
Password Reset Code Verification
  Verification Code
    This is the reset code sent to the user's email
    Valid for 10 minutes
  Verify Code
    Onclick
      Same code validation and messages as 3.0 Email Verification
      If code is valid
        Redirect to 8.0 New Password
  Resend Code
    Onclick
      Display message "If an account uses that email, a reset code has been sent."
  Use a Different Email Address
    Onclick
      Redirect to 6.0 Password Reset Request
Special Cases
  Account changed or deactivated after the request
    Display error message "This reset request is no longer valid. Request a new code."
    Redirect to 6.0 Password Reset Request
  Screen opened without a pending request
    Redirect to 6.0 Password Reset Request
```

### 8.0 New Password Logic
```text
New Password
  New Password
    This is the new password of the user
    This field is shown as masked characters
    Minimum of 8 characters
  Confirm Password
    This field is shown as masked characters
    Must match New Password
  Update Password
    Onclick
      Check if input fields are empty, mismatched, or fail password policy
        Display field validation message
      Else
        Save new password
        Sign out any active session
        Redirect to 9.0 Password Reset Success
Special Cases
  More than 10 minutes after code verification
    Display error message "Your password reset permission expired. Request a new code."
    Redirect to 6.0 Password Reset Request
```

### 9.0 Password Reset Success Logic
```text
Password Reset Success
  Return to Sign In
    Onclick
      Redirect to 1.0 Home / Login
```

### 10.0 Access Denied Logic
```text
Access Denied
  Displayed when
    Signed-in user opens a page outside the assigned role
    Account has no assigned role
    Borrower account has no borrower profile
  Return to Sign In
    Onclick
      Redirect to 1.0 Home / Login
```

### 11.0 Borrower Dashboard Logic
```text
Borrower Dashboard
  Summary Cards
    Active Requests, Currently Borrowed, Completed Reservations, Overdue
  Upcoming Reservations
    Up to 3 pending, approved, or borrowed reservations
  Reserve Equipment
    Onclick
      Redirect to 12.0 Equipment Reservation
  Availability Calendar
    Onclick
      Redirect to 14.0 Borrower Availability Calendar
Special Cases
  Overdue
    Counts borrowed items not returned by the due time
```

### 12.0 Equipment Reservation Logic
```text
Equipment Reservation
  Equipment Item
    This is the equipment to reserve
    Lists only active items not under maintenance and not Damaged/Unserviceable
  Start Date and Time
    This is the start of the booking (Manila time)
    Defaults to the next hour
  End Date and Time
    This is the end of the booking (Manila time)
    Defaults to 2 hours after start
  Purpose
    This is the reason for the request
    Required; maximum of 500 characters
  Note
    Optional; maximum of 2000 characters
  Check Calendar
    Onclick
      Redirect to 14.0 Borrower Availability Calendar
  Submit Reservation
    Onclick
      If borrowing access is paused
        Display error message "Your borrowing access is currently paused. Contact an administrator."
      If no equipment selected
        Display error message "Choose available equipment."
      If purpose is empty
        Display error message "Enter the purpose of your request."
      If end time is not after start time
        Display error message "The end time must be after the start time."
      If start time is not in the future
        Display error message "Choose a future start time."
      If schedule overlaps an existing booking
        Display "Reservation conflict" message "This equipment is unavailable for the requested time."
      If form already submitted
        Display error message "This request was already submitted."
      Else
        Save reservation as Pending
        Display "Reservation request submitted" with booking details and hold expiry
Special Cases
  Borrower not eligible
    Submit button disabled; "Borrowing access is paused" panel shown
  No available equipment
    Display "No equipment is available for reservation"
  Pending request not approved within 24 hours or before start time (whichever is earlier)
    Automatically marked Expired; slot released
```

### 13.0 Borrower Borrowing History Report Logic
```text
Borrower Borrowing History Report
  From Date / To Date
    Filters records by booking start date
  Status
    Filters records by reservation status
  Apply Filter
    Onclick
      If date is invalid
        Display error message "Choose a valid from date." / "Choose a valid to date."
      If To Date is earlier than From Date
        Display error message "The to date must be on or after the from date."
  Borrowing Records Table
    Equipment, Reservation Time, Status, Return / Condition, Action
    12 records per page
  Cancel Request
    Shown only for unexpired Pending or Approved requests
    Onclick
      Cancel reservation
      Display message "Reservation cancelled. The equipment slot is available again."
Special Cases
  Hold expired before cancellation
    Display message "The pending hold had already expired."
  Equipment already released
    Display error message "This request can no longer be cancelled."
```

### 14.0 Borrower Availability Calendar Logic
```text
Borrower Availability Calendar
  Equipment Item
    This is the equipment whose schedule is displayed
  View Availability
    Onclick
      Display monthly calendar for selected equipment
  Previous / Next Month
    Onclick
      Display adjacent month
  Calendar Date
    Onclick
      Display reserved time slots for selected date
  Reserve This Equipment
    Onclick
      Redirect to 12.0 Equipment Reservation with item preselected
Special Cases
  No active equipment
    Display "No active equipment"
```

### 15.0 Custodian Dashboard Logic
```text
Custodian Dashboard
  Summary Cards
    Pending Requests, Currently Borrowed, Under Maintenance, Overdue
  Requests Awaiting Review
    Up to 3 pending or approved requests
    Onclick
      Redirect to 17.0 Approval & Release Details
```

### 16.0 Approval & Release Queue Logic
```text
Approval & Release Queue
  Search Borrower or Equipment
    Filters by borrower name or equipment
  Status
    Pending (default), Approved, or Pending and Approved
  Booking From / Booking Through
    Filters by booking start date
  Apply Filters
    Onclick
      If date is invalid
        Display error message "Use valid dates for the date filters."
      If date is outside 2000–2100
        Display error message "Choose filter dates between 2000 and 2100."
      If end date is earlier than start date
        Display error message "The end date must be on or after the start date."
  Reset
    Onclick
      Clear all filters
  Request Row Action
    Onclick
      Redirect to 17.0 Approval & Release Details
Special Cases
  Expired pending requests
    Not listed in the queue
```

### 17.0 Approval & Release Details Logic
```text
Approval & Release Details
  Request Snapshot
    Borrower, equipment, booking time, purpose, status
  Approve Request
    Enabled only for unexpired Pending requests with usable equipment
    Onclick
      If schedule now conflicts
        Display error message "The equipment is no longer available for this time."
      Else
        Set status to Approved
        Display message "Reservation approved. The booking is ready for release at its start time."
  Reject Request
    Onclick
      Expand Rejection Reason field
  Rejection Reason
    Required; 1 to 1000 characters
  Confirm Rejection
    Onclick
      If reason is empty
        Display error message "Enter a rejection reason using 1 to 1000 characters."
      Else
        Set status to Rejected
        Display message "Request rejected. Its equipment slot is available again."
  Condition at Release
    Excellent, Good, or Fair
  Release Notes
    Optional; maximum of 2000 characters
  Record Release
    Enabled only for Approved requests during the booking time
    Onclick
      If condition is Damaged or Unserviceable
        Display error message "Choose Excellent, Good, or Fair for an equipment release."
      Else
        Create loan; set status to Released
        Display message "Release recorded. The equipment is now on loan."
  Back to Requests
    Onclick
      Redirect to 16.0 Approval & Release Queue
Special Cases
  Before booking start
    Display "Release opens at the approved start time."
  After booking end
    Display "The booking time has ended. Equipment can no longer be released."
  Pending hold expired
    Display "The pending hold expired. This request cannot be approved or rejected."
  Equipment inactive or under maintenance
    Display "This item is inactive or on a maintenance hold. It cannot be approved or released."
  Record modified by another user
    Display error message "This record changed. Reload and retry."
```

### 18.0 Return & Condition Check Queue Logic
```text
Return & Condition Check Queue
  Active Loans Tab (default)
    Lists released equipment not yet returned
  Approved Release Queue Tab
    Lists approved requests awaiting release
  Search / Booking From / Booking Through
    Same filter logic as 16.0 Approval & Release Queue
  Return Action
    Onclick
      Active loan: redirect to 19.0 Return & Condition Check Details
      Approved request: redirect to 17.0 Approval & Release Details
```

### 19.0 Return & Condition Check Details Logic
```text
Return & Condition Check Details
  Condition at Return
    Excellent, Good, Fair, Damaged, or Unserviceable
  Return Notes
    Optional; maximum of 2000 characters
  Record Return
    Onclick
      If condition is invalid
        Display error message "Choose a valid return condition."
      Else
        Record return; set status to Completed
        Update equipment condition
        Display message "Return recorded. The equipment condition and borrowing history are updated."
  Back to Return Queue
    Onclick
      Redirect to 18.0 Return & Condition Check Queue
Special Cases
  Returned as Damaged or Unserviceable
    Equipment placed on maintenance hold
    Maintenance case opened automatically in 22.0 Equipment Maintenance
  Loan already returned
    Display error message "Only an active loan can be returned."
```

### 20.0 Staff Borrowing History Report Logic
```text
Staff Borrowing History Report
  Search / Booking From / Booking Through
    Same filter logic as 16.0 Approval & Release Queue
  Status
    All, Pending, Approved, Borrowed, Returned, Rejected, Cancelled, Expired, Late
  Records Table
    All borrowers' reservations; 12 records per page
  Record Row
    Onclick
      Redirect to 17.0 or 19.0 details screen
Special Cases
  Late
    Unreturned past due time, or returned after due time
```

### 21.0 Staff Availability Calendar Logic
```text
Staff Availability Calendar
  Equipment
    This is the equipment whose schedule is displayed
    Items under maintenance are labeled "Maintenance"
  View Schedule
    Onclick
      Display monthly calendar for selected equipment
  Previous / Next Month (← / →)
    Onclick
      Display adjacent month
  Calendar Day
    Onclick
      Display booked time slots for selected day
  View Details
    Onclick
      Redirect to 17.0 Approval & Release Details
```

### 22.0 Equipment Maintenance Logic
```text
Equipment Maintenance
  Open Cases / Closed Cases
    Filters maintenance cases by status
  Open a Maintenance Case
    Equipment
      This is the item to place under maintenance
    Issue Description
      Required; maximum of 2000 characters
    Place in Maintenance
      Onclick
        If equipment or description is missing
          Display error message "Choose equipment and enter a description using 2000 characters or fewer."
        If item is currently on loan
          Display error message "Record the active loan's return before placing the item in maintenance."
        If item already has an open case
          Display error message "This item already has an open maintenance case."
        Else
          Open case; block new reservations and release of item
  Update Maintenance Case
    Status
      Open, In Progress, or Closed
    Condition After Repair
      This is the equipment condition after repair
    Repair Notes
      This is the resolution of the repair
    Save Maintenance
      Onclick
        If closing without repair notes or with Damaged/Unserviceable condition
          Display error message "Describe the repair and choose Excellent, Good, or Fair before closing maintenance."
        If Closed
          Display message "Repair recorded. Maintenance case closed."
          Return item to availability if no other open case
        Else
          Display message "Maintenance status updated."
Special Cases
  Case already closed
    Display error message "This maintenance case is already closed."
```

### 23.0 Administrator Dashboard Logic
```text
Administrator Dashboard
  Summary Cards
    Total Equipment, Active Borrowings, Pending Approvals, Total Borrowers
  Live Borrowing Activity
    3 most recently updated reservations
```

### 24.0 Equipment Categories Logic
```text
Equipment Categories
  Category Records Table
    Name, Description, Items, Status, Action
  Add Category
    Onclick
      Redirect to 25.0 Add / Edit Equipment Category
  Edit
    Onclick
      Redirect to 25.0 Add / Edit Equipment Category with record loaded
```

### 25.0 Add / Edit Equipment Category Logic
```text
Add / Edit Equipment Category
  Category Name
    This is the name of the category
    Required; maximum of 120 characters; must be unique
  Description
    Optional; maximum of 1000 characters
  Active Category
    Indicates whether the category is available for use
  Save Changes
    Onclick
      If category name already exists
        Display error message "That category name already exists, or the record changed."
      Else
        Save category
        Display message "Category saved."
        Redirect to 24.0 Equipment Categories
  Cancel
    Onclick
      Redirect to 24.0 Equipment Categories
Special Cases
  Deactivating a category with active equipment
    Display error message "Move or deactivate this category's active equipment before deactivating the category."
  Record modified by another user
    Display error message "This category changed. Reload before saving."
```

### 26.0 Equipment Items Logic
```text
Equipment Items
  Search Name or Asset Code
    Filters the inventory list
  Search / Reset
    Onclick
      Apply or clear search
  Equipment Inventory Table
    Equipment, Category, Condition, Availability, Action
  Add Item
    Onclick
      Redirect to 27.0 Add / Edit Equipment Item
  Edit
    Onclick
      Redirect to 27.0 Add / Edit Equipment Item with record loaded
```

### 27.0 Add / Edit Equipment Item Logic
```text
Add / Edit Equipment Item
  Asset Code
    This is the unique inventory code of the item
    Required; maximum of 64 characters
  Equipment Name
    Required; maximum of 160 characters
  Category
    Lists active categories only
  Condition
    Excellent, Good, Fair, Damaged, or Unserviceable
  Serial Number
    Optional; must be unique
  Storage Location
    Optional; maximum of 160 characters
  Description
    Optional; maximum of 2000 characters
  Active Inventory Item
    Indicates whether the item can be reserved
  Save Changes
    Onclick
      If category is not active
        Display error message "Choose an active category."
      If asset code or serial number already exists
        Display error message "The asset code or serial number already exists, or the record changed. Reload and try again."
      Else
        Save item
        Display message "Equipment added." / "Equipment updated."
        Redirect to 26.0 Equipment Items
  Cancel
    Onclick
      Redirect to 26.0 Equipment Items
Special Cases
  Item currently on loan
    Display error message "Record the active loan's return before editing this equipment."
  Deactivating item with pending or approved reservations
    Display error message "Resolve this item's pending and approved reservations before deactivating it."
  Condition set to Damaged or Unserviceable
    Item placed on maintenance hold; maintenance case opened automatically
  Item has an open maintenance case
    Hold remains until case is closed in 22.0 Equipment Maintenance
```

### 28.0 Reservation Management Logic
```text
Reservation Management
  Recent Requests Table
    Borrower, Equipment, Manila Time, Status, Actions
  Create Reservation
    Onclick
      Redirect to 29.0 New Reservation (Administrator)
  Details
    Onclick
      Redirect to 17.0 Approval & Release Details
  Cancel
    Shown for Pending and Approved reservations
    Onclick
      Cancel reservation
      Display message "Reservation cancelled."
Special Cases
  Reservation already released or closed
    Display error message "This request can no longer be cancelled."
```

### 29.0 New Reservation (Administrator) Logic
```text
New Reservation (Administrator)
  Borrower
    Lists only eligible, active, and verified Borrower accounts
  Equipment
    Lists only active items not under maintenance
  Start (Manila Time) / End (Manila Time)
    This is the booking period
  Purpose
    This is the reason for the request
  Notes
    Optional
  Submit Reservation
    Onclick
      If start is not in the future or end is not after start
        Display error message "Choose a future start time and a later end time."
      If schedule overlaps an existing booking
        Display error message "This equipment is unavailable for the requested time."
      If borrower is not eligible
        Display error message "The borrower account is not eligible to reserve equipment."
      Else
        Save reservation as Pending
        Display message "Request saved for the selected borrower. It is pending staff approval."
        Redirect to 16.0 Approval & Release Queue (Pending)
  Cancel
    Onclick
      Redirect to 28.0 Reservation Management
```

### 30.0 Borrower Profiles Logic
```text
Borrower Profiles
  Search Name, Email, Student Number, or Department
    Filters borrower records
  Account Status
    All Accounts, Active, Inactive, or Email Not Verified
  Search / Clear
    Onclick
      Apply or clear filters
  Borrower Records Table
    Borrower, ID / Email, Department, Status, Action
  Edit
    Onclick
      Redirect to 32.0 Edit Account & Role
```

### 31.0 User & Role Management Logic
```text
User & Role Management
  Search Name, Email, Student Number, or Department
    Filters account records
  Role
    All Roles, Borrower, Custodian, or Administrator
  Account Status
    All Accounts, Active, Inactive, or Email Not Verified
  Review Unverified Accounts
    Onclick
      Filter list to accounts with unverified email
  Account Records Table
    User, ID / Email, Role, Status, Action
  Edit
    Onclick
      Redirect to 32.0 Edit Account & Role
```

### 32.0 Edit Account & Role Logic
```text
Edit Account & Role
  Name
    Required; 2 to 160 characters
  Role
    Borrower, Custodian, or Administrator
    Staff roles disabled if email is not verified
  Account Is Active
    Indicates whether the user can sign in
  Student / Borrower Number
    Optional; must be unique
  Department
    Optional; maximum of 160 characters
  Eligible to Reserve Equipment
    Controls whether the borrower can submit reservations
  Eligibility Notes
    Optional; maximum of 1000 characters
  Save Changes
    Onclick
      If student number is already used
        Display error message "Another borrower already uses this student number."
      Else
        Save account
        Display message "Account details and access permissions updated."
        Redirect to 31.0 User & Role Management
  Cancel
    Onclick
      Redirect to 31.0 User & Role Management
Special Cases
  Administrator deactivates or demotes own account
    Display error message "You cannot deactivate your own account or remove your own Administrator role."
  Change would leave no active Administrator
    Display error message "Keep at least one active, verified Administrator account."
  Staff role assigned to unverified account
    Display error message "The account must verify its email before receiving a staff role."
  Record modified by another user
    Display error message "This account changed while the form was open. Review the latest details and try again."
  Role or active status changed
    User's existing sessions invalidated
```

### 33.0 System Audit Log Logic
```text
System Audit Log
  Search Action, Actor, Record, Result, or IP Address
    Filters audit entries
  Search / Clear
    Onclick
      Apply or clear search
  Audit History Table
    Time, Action / Actor, Record, Result, Details
    Read-only; paginated
```

### 34.0 Development Email Inbox Logic
```text
Development Email Inbox
  Recent Emails List
    Shows captured verification, sign-in, and reset code emails
Special Cases
  Not in Development environment or not accessed from local machine
    Return "Not Found" (404)
```

### 35.0 Error Logic
```text
Error
  Displayed when
    An unhandled system error occurs (non-development environment)
  Request ID
    Shown for troubleshooting reference
```

### Common Logic (All Workspace Screens, 11.0–33.0)
```text
Sidebar Menu
  Shows only the menu items allowed for the user's role
CampusGear Logo
  Onclick
    Redirect to role dashboard
Log Out
  Onclick
    End session
    Redirect to 1.0 Home / Login
Authentication Forms (1.0–8.0)
  More than 20 submissions per minute from the same IP address
    Request rejected (HTTP 429)
```