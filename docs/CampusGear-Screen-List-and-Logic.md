# CampusGear: Screen List and Screen Logic

_Updated November 5, 2026 to match the current system: admin-approved registration, the borrower equipment catalog, chart dashboards, custodian read-only directory screens, account creation, and registration verification._

## Part 1: Screen List

| **Screen** | **Description** | **REMARKS** |
|------------|-----------------|-------------|
| 1.0 Home / Login | The home screen contains the Account Sign In section, where registered users sign in with their campus email and password. Each user is taken to the workspace for their role after signing in. | The root URL ( / ) redirects to /auth/login. |
| 2.0 Registration | This is the Registration Screen shown when the user clicks the "Create a New Account" link on the Home / Login Screen. The user enters their first name, last name, Student ID Number, mobile number, email address, password, and password confirmation. | Self-registration creates Borrower (Student/Faculty) accounts only. An Administrator must approve the account before it can sign in. |
| 3.0 Registration Submitted | This screen confirms that the registration was saved and is pending administrator approval. It provides a link back to the Login Screen. | Replaces the former Email Verification and Registration Success screens. |
| 4.0 Administrator Two-Factor Verification | This screen is shown after an Administrator enters a valid email address and password. The Administrator enters the sign-in code sent by email to finish logging in. | Restricted to Administrator accounts. |
| 5.0 Password Reset Request | This screen is shown when the user clicks the "Forgot password?" link on the Home / Login Screen. The user enters their registered email address to receive a password reset code. | Step 01 of 03. |
| 6.0 Password Reset Code Verification | This screen is shown after a reset request is submitted. The user enters the reset code sent to their email address and may request a new code. | Step 02 of 03. |
| 7.0 New Password | This screen is shown after the reset code is verified. The user enters and confirms a new password. | Step 03 of 03. |
| 8.0 Password Reset Success | This screen confirms that the password has been changed and directs the user back to the Login Screen. | |
| 9.0 Access Denied | This screen is shown when a signed-in user tries to open a page that their role is not allowed to access. | System page. |
| 10.0 Borrower Home | This is the main screen shown after a Borrower logs in. It shows a welcome banner with equipment search, the Borrower's reservation counts and next reservations, categories to browse, recently added equipment, equipment ready to borrow now, and the steps of the borrowing process. | Restricted to Borrower (Student/Faculty). Menu label "Home". |
| 11.0 Browse Equipment | This screen is accessed through the "Browse Equipment" menu. It shows the equipment catalog as cards, with search, category filters, and an available-only option. Selecting an item opens the Reservation Request dialog. | Restricted to Borrower. The submission confirmation is shown on this screen. |
| 12.0 Reservation Request | This dialog is shown when the Borrower opens an item in the catalog. It shows the equipment details. The Borrower enters the quantity, borrowing and return date and time, purpose, and notes, then submits the request. The system checks for schedule conflicts before accepting the request. | Restricted to Borrower. Displayed as a dialog on the Browse Equipment page. Conflict messages are shown within the dialog. |
| 13.0 My Reservations | This screen is accessed through the "My Reservations" menu. It lists the Borrower's own reservations and borrowing records, with search, date, and status filters. Pending and approved requests can be cancelled from this screen. | Restricted to Borrower. |
| 14.0 Borrower Availability Calendar | This screen is accessed through the "Availability" menu. It shows a monthly calendar of the selected equipment's bookings, together with the reserved time slots for a chosen date. | Restricted to Borrower. |
| 15.0 Custodian Dashboard | This is the main screen shown after a Custodian logs in. It shows charts of desk activity, equipment status, and equipment condition, together with the requests awaiting review, overdue and due-today loans, and recent releases and returns. | Restricted to Custodian. |
| 16.0 Approval & Release Queue | This screen is accessed through the "Approval & Release" menu. It lists reservation requests awaiting approval, with search, status, and date filters. | Restricted to Custodian. |
| 17.0 Approval & Release Details | This screen is shown when a request is selected from the Approval & Release Queue. It shows the reservation as a borrowing ID card. The user approves or rejects the request with a reason. For approved requests, the user records the equipment's condition at release and any notes. | Custodian; Administrators can open the record read-only, without actions ("Reservation Record"). |
| 18.0 Return & Condition Check Queue | This screen is accessed through the "Return & Condition Check" menu. It lists the active loans awaiting return and the approved requests awaiting release. | Restricted to Custodian. |
| 19.0 Return & Condition Check Details | This screen is shown when an active loan is selected from the Return & Condition Check Queue. The user inspects the returned equipment and records its condition at return and any inspection notes. | Custodian; Administrators can open the record read-only, without actions. |
| 20.0 Staff Borrowing History Report | This screen is accessed through the "Borrowing History" menu. It lists all borrowing and reservation records in the system, with search, status, and date filters. | Custodian and Administrator. Read-only. Administrators open it at /admin/history. |
| 21.0 Staff Availability Calendar | This screen is accessed through the "Availability Calendar" menu. It shows a monthly calendar of equipment bookings and maintenance status, together with the booked time slots and borrowers for a selected day. | Custodian and Administrator. Administrators open it at /admin/calendar. |
| 22.0 Equipment Maintenance | This screen is accessed through the "Maintenance" menu. The user opens maintenance cases for equipment items, updates their status, and returns repaired items to service. | Custodian and Administrator. Opening and updating a case use dialogs on this page. An open case blocks new reservations and release of the item. |
| 23.0 Custodian Equipment Categories | This screen is accessed through the "Equipment Categories" menu. It lists every category with its equipment units, and each unit's condition, location, and current status. | Restricted to Custodian. Read-only. |
| 24.0 Custodian Borrower Profiles | This screen is accessed through the "Borrower Profiles" menu. It lists Borrower profiles with search, an eligibility filter, and paging, so the Custodian can check a borrower before approving or releasing. | Restricted to Custodian. Read-only. |
| 25.0 Administrator Dashboard | This is the main screen shown after an Administrator completes two-factor verification. It shows charts of borrowing activity, equipment status, reservation requests, and borrower registrations, together with the latest transactions and the registrations to verify. | Restricted to Administrator. |
| 26.0 Equipment Categories | This screen is accessed through the "Equipment Categories" menu. It lists all equipment categories, with search, and lets the Administrator add, edit, or delete a category. | Restricted to Administrator. |
| 27.0 Add / Edit Equipment Category | This screen is shown when the Administrator clicks the "Add category" button or the "Edit" link on the Equipment Categories Screen. The Administrator enters or updates the category information. | Restricted to Administrator. Displayed as an editor on the Equipment Categories page. |
| 28.0 Equipment Management | This screen is accessed through the "Equipment Items" menu. It lists the equipment inventory as cards with photos, with search and paging, and lets the Administrator add, edit, or delete an item. | Restricted to Administrator. |
| 29.0 Add / Edit Equipment Item | This screen is shown when the Administrator clicks the "Add item" button or the "Edit item" link on the Equipment Management Screen. The Administrator enters or updates the item's details, category, condition, photo, and availability. | Restricted to Administrator. Displayed as an editor on the Equipment Management page. |
| 30.0 Borrower Profiles | This screen is accessed through the "Borrower Profiles" menu. It lists the registered Borrower accounts, with search, status filters, and paging. | Restricted to Administrator. |
| 31.0 User & Role Management | This screen is accessed through the "Users & Roles" menu. It lists all user accounts, with search, role and status filters, and paging. Accounts can be edited or deleted from this screen. | Restricted to Administrator. |
| 32.0 Registration Verification | This screen is shown when the Administrator clicks "Verify registrations". It lists the self-registered accounts waiting for approval, which the Administrator approves or rejects. | Restricted to Administrator. Opens at /admin/users?status=pending. |
| 33.0 Create Account | This screen is shown when the Administrator clicks the create-account button on the User & Role Management Screen. The Administrator enters the new user's name, email, role, temporary password, and Borrower profile details. | Restricted to Administrator. Opens at /admin/users/new. |
| 34.0 Edit Account & Role | This screen is shown when the Administrator selects an account on the User & Role Management, Borrower Profiles, or Registration Verification Screen. The Administrator updates the account information, role, status, and Borrower profile details. | Restricted to Administrator. |
| 35.0 System Audit Log | This screen is accessed through the "Audit Log" menu. It shows the audit history of system and authentication events, with before and after details, search, and paging. | Restricted to Administrator. |
| 36.0 Development Email Inbox | This screen shows the system emails, such as password reset and administrator sign-in codes, captured during local development. | Development environment and local access only. Not available in production. Opens at /development/email. |
| 37.0 Error | This screen is shown when an unexpected system error occurs while a request is being processed. | System page. |

---

## Part 2: Screen Logic

### CampusGear: Screen Logic

These notes cover the same 37 screens as the Screen List, and every message and rule comes from the current code. Screens are referenced as [screen number - screen name]. Conditions are written under the event that triggers them (Onload, Onclick, Onchange, Oninput). Rules that apply to every screen are listed once at the end.

### 1.0 Home / Login Logic
```text
Login
  Onload
    Check if user is already signed in and the account is active
      If role is Administrator
        Redirect to [25.0 - Administrator Dashboard]
      If role is Custodian
        Redirect to [15.0 - Custodian Dashboard]
      If role is Borrower
        Redirect to [10.0 - Borrower Home]
      Else
        Redirect to [9.0 - Access Denied]
    Check if user is signed in but the account is inactive
      Sign out the user
      Proceed with Login page
    Else
      Proceed with Login page
Account Sign In
  Campus Email
    This is the registered email address of the user
  Password
    This is the password of the user
    This field is shown as masked characters, with a show/hide control
  Sign In
    Onclick
      Check if Campus Email is empty
        Display error message "The Email field is required."
      Check if Campus Email format is invalid
        Display error message "The Email field is not a valid e-mail address."
      Check if Password is empty
        Display error message "The Password field is required."
      If email is not registered or account is inactive
        Display error message "The email or password is incorrect."
      If password is incorrect
        Add 1 to the failed attempt count
        Display error message "The email or password is incorrect."
      If 5 consecutive failed attempts
        Lock account for 15 minutes
        Display error message "This account is temporarily locked. Try again later."
      If password is correct and registration is pending administrator approval
        Display error message "Your account is pending administrator approval."
      If password is correct and registration was rejected
        Display error message "Your registration was not approved. Please contact the administrator."
      If role is Administrator
        Check if the system is in the Development environment with no email server configured
          Sign in Administrator without a code
          Redirect to [25.0 - Administrator Dashboard]
        Else
          Send sign-in code to email
          If the code cannot be sent
            Display error message "We could not send the administrator code. Please try again."
          If a code was requested within the last 60 seconds
            Display message "A code was requested recently. Please wait before requesting a new one."
            Redirect to [4.0 - Administrator Two-Factor Verification]
          Else
            Display message "We sent an administrator sign-in code to your email."
            Redirect to [4.0 - Administrator Two-Factor Verification]
      If role is Custodian
        Redirect to [15.0 - Custodian Dashboard]
      If role is Borrower
        Redirect to [10.0 - Borrower Home]
      Else
        Redirect to [9.0 - Access Denied]
  Forgot Password?
    Onclick
      Redirect to [5.0 - Password Reset Request]
  Create a New Account
    Onclick
      Redirect to [2.0 - Registration]
```

### 2.0 Registration Logic
```text
Create Your Campus Account
  First Name / Last Name
    Required; maximum of 100 characters each
  Student ID Number
    Required; maximum of 64 characters; must be unique
  Mobile Number
    Required; must be a valid phone number
  Email Address
    Required; used for login
  Password
    This field is shown as masked characters
    Minimum of 8 characters
    Must contain an uppercase letter, a lowercase letter, a digit, and a symbol
  Confirm Password
    This field is shown as masked characters
    Must match Password
  Create Account
    Onclick
      Check if First Name is empty
        Display error message "The First name field is required."
      Check if Last Name is empty
        Display error message "The Last name field is required."
      Check if Student ID Number is empty
        Display error message "The Student ID Number field is required."
      Check if Mobile Number is empty
        Display error message "The Mobile number field is required."
      Check if Mobile Number is not a valid phone number
        Display error message "Enter a valid mobile number."
      Check if Email Address is empty
        Display error message "The Email field is required."
      Check if Email Address format is invalid
        Display error message "Enter a valid email address."
      Check if Password is empty
        Display error message "The Password field is required."
      Check if Password is shorter than 8 characters
        Display error message "Use at least 8 characters."
      Check if Password has no symbol
        Display error message "Passwords must have at least one non alphanumeric character."
      Check if Password has no digit
        Display error message "Passwords must have at least one digit ('0'-'9')."
      Check if Password has no lowercase letter
        Display error message "Passwords must have at least one lowercase ('a'-'z')."
      Check if Password has no uppercase letter
        Display error message "Passwords must have at least one uppercase ('A'-'Z')."
      Check if Confirm Password is empty
        Display error message "The Confirm password field is required."
      Check if Confirm Password does not match Password
        Display error message "Passwords do not match."
      If email is already registered
        Display error message "An account already uses this email address."
      If Student ID Number is already registered
        Display error message "This Student ID Number is already registered."
      If the account cannot be saved
        Display error message "Account creation is unavailable. Please try again later."
      Else
        Create account with Borrower role and borrower profile
        Set account to Pending Approval
        Redirect to [3.0 - Registration Submitted]
  Already have an account? Return to sign in
    Onclick
      Redirect to [1.0 - Home / Login]
```

### 3.0 Registration Submitted Logic
```text
Registration Submitted
  Onload
    Display "Pending approval"
    Display message "Your registration has been submitted. You can sign in once an administrator approves your account."
  Return to Sign In
    Onclick
      Redirect to [1.0 - Home / Login]
```

### 4.0 Administrator Two-Factor Verification Logic
```text
Verify Administrator Access
  Onload
    Check if there is no pending Administrator sign-in
      Redirect to [1.0 - Home / Login]
    Check if the account was deactivated, its Administrator role was removed, or its credentials changed after the password step
      Display error message "This sign-in request is no longer valid. Sign in again."
      Redirect to [1.0 - Home / Login]
    Else
      Proceed with Verify Administrator Access page
  6-Digit Verification Code
    This is the sign-in code sent to the Administrator's email
    Valid for 10 minutes
  Verify and Continue
    Onclick
      Check if code is empty or not 6 digits
        Display error message "Enter the six-digit code."
      If code is expired
        Display error message "The code expired. Request a new one."
      If 5 incorrect attempts
        Display error message "Too many incorrect attempts. Request a new code."
      If code is incorrect or already used
        Display error message "The code is invalid or has already been used."
      If the account was deactivated, its role was removed, or its credentials changed
        Display error message "This sign-in request is no longer valid. Sign in again."
        Redirect to [1.0 - Home / Login]
      Else
        Sign in Administrator
        Redirect to [25.0 - Administrator Dashboard]
  Resend Verification Code
    Onclick
      If requested within 60 seconds of the last code
        Display message "Please wait before requesting another code."
      If the code cannot be sent
        Display error message "We could not send a code. Please try again."
      Else
        Send new code
        Display message "A new code was sent to your email address."
  Back to Sign In
    Onclick
      Redirect to [1.0 - Home / Login]
```

### 5.0 Password Reset Request Logic
```text
Reset Your Password
  Email Address
    This is the registered email address of the user
  Send Reset Code
    Onclick
      Check if Email Address is empty
        Display error message "The Email field is required."
      Check if Email Address format is invalid
        Display error message "The Email field is not a valid e-mail address."
      If email belongs to an active account
        Send password reset code
        Display message "If an account uses that email, a reset code has been sent."
        Redirect to [6.0 - Password Reset Code Verification]
      Else
        Do not send a code (account existence is not revealed)
        Display message "If an account uses that email, a reset code has been sent."
        Redirect to [6.0 - Password Reset Code Verification]
  Back to Sign In
    Onclick
      Redirect to [1.0 - Home / Login]
```

### 6.0 Password Reset Code Verification Logic
```text
Enter Your Reset Code
  Onload
    Check if there is no pending reset request
      Redirect to [5.0 - Password Reset Request]
    Else
      Proceed with Enter Your Reset Code page
  Reset Code
    This is the 6-digit reset code sent to the user's email
    Valid for 10 minutes; only the newest code works if several were requested
  Verify Code
    Onclick
      Check if code is empty or not 6 digits
        Display error message "Enter the six-digit code."
      If the email entered in [5.0 - Password Reset Request] has no active account
        Display error message "The code is invalid or has expired."
      If the account was changed or deactivated after the request
        Display error message "This reset request is no longer valid. Request a new code."
        Redirect to [5.0 - Password Reset Request]
      If code is expired
        Display error message "The code expired. Request a new one."
      If 5 incorrect attempts
        Display error message "Too many incorrect attempts. Request a new code."
      If code is incorrect or already used
        Display error message "The code is invalid or has already been used."
      Else
        Allow password change for 10 minutes
        Redirect to [7.0 - New Password]
  Resend Code
    Onclick
      If the account was changed or deactivated after the request
        Redirect to [5.0 - Password Reset Request]
      Else
        Send new code (only when the email belongs to an active account)
        Display message "If an account uses that email, a reset code has been sent."
  Use a Different Email Address
    Onclick
      Redirect to [5.0 - Password Reset Request]
```

### 7.0 New Password Logic
```text
Set Your New Password
  Onload
    Check if code verification was not completed
      Redirect to [5.0 - Password Reset Request]
    Check if more than 10 minutes have passed since code verification
      Display error message "Your password reset permission expired. Request a new code."
      Redirect to [5.0 - Password Reset Request]
    Check if the account was changed or deactivated after code verification
      Display error message "This reset request is no longer valid. Request a new code."
      Redirect to [5.0 - Password Reset Request]
    Else
      Proceed with Set Your New Password page
  New Password
    This field is shown as masked characters
    Minimum of 8 characters
    Must contain an uppercase letter, a lowercase letter, a digit, and a symbol
  Confirm Password
    This field is shown as masked characters
    Must match New Password
  Update Password
    Onclick
      Check if New Password is empty
        Display error message "The Password field is required."
      Check if New Password is shorter than 8 characters
        Display error message "The field Password must be a string with a minimum length of 8 and a maximum length of 100."
      Check if Confirm Password is empty
        Display error message "The ConfirmPassword field is required."
      Check if Confirm Password does not match New Password
        Display error message "'ConfirmPassword' and 'Password' do not match."
      Check if New Password has no symbol
        Display error message "Passwords must have at least one non alphanumeric character."
      Check if New Password has no digit
        Display error message "Passwords must have at least one digit ('0'-'9')."
      Check if New Password has no lowercase letter
        Display error message "Passwords must have at least one lowercase ('a'-'z')."
      Check if New Password has no uppercase letter
        Display error message "Passwords must have at least one uppercase ('A'-'Z')."
      If more than 10 minutes have passed since code verification
        Display error message "Your password reset permission expired. Request a new code."
        Redirect to [5.0 - Password Reset Request]
      Else
        Save new password
        Sign out any active session
        Redirect to [8.0 - Password Reset Success]
```

### 8.0 Password Reset Success Logic
```text
Password Updated
  Onload
    Display message "Sign in with your new password."
  Return to Sign In
    Onclick
      Redirect to [1.0 - Home / Login]
```

### 9.0 Access Denied Logic
```text
Access Denied
  Onload
    Shown when a signed-in user
      Opens a page outside the assigned role
      Has no assigned role
      Is a Borrower with no borrower profile
  Go to My Workspace
    Onclick
      Redirect to [1.0 - Home / Login]
        Onload of [1.0 - Home / Login] sends the signed-in user to the role dashboard
```

### 10.0 Borrower Home Logic
```text
Borrower Home
  Onload
    Check if the Borrower is not eligible to borrow
      Display panel "Borrowing access is paused"
      Display message "You can browse equipment, but contact an administrator before making another reservation."
    Check if a borrowed item is past its return time
      Display overdue warning in My Reservations
    Else
      Proceed with Borrower Home page
  Search Equipment
    Onclick Search
      Redirect to [11.0 - Browse Equipment] with search applied
  Catalog Figures
    Equipment count, units ready now, number of categories
  My Reservations
    Counts: Pending, Approved, Borrowed, Completed
    Coming up next: up to 4 pending, approved, or borrowed reservations
    View All
      Onclick
        Redirect to [13.0 - My Reservations]
  Browse by Category
    Onclick
      Redirect to [11.0 - Browse Equipment] filtered by category
  Recently Added
    The 4 newest equipment items
    Onclick
      Redirect to [11.0 - Browse Equipment] and open [12.0 - Reservation Request] for the item
  Ready to Borrow Now
    Equipment with units on the shelf now
    Show Only Available
      Onclick
        Redirect to [11.0 - Browse Equipment] showing available items only
  How Borrowing Works
    Find equipment, Request a time, Collect it, Return on time
    Check the Availability Calendar
      Onclick
        Redirect to [14.0 - Borrower Availability Calendar]
```

### 11.0 Browse Equipment Logic
```text
Browse Equipment
  Onload
    Check if a reservation request was just submitted
      Display "Reservation request submitted" with each unit's booking time, status, and hold expiry
      Display View Borrowing History and Browse More Equipment links
    Check if the Borrower is not eligible to borrow
      Display panel "Borrowing access is paused"
      Display message "You can browse equipment, but contact an administrator before making another reservation."
      Browsing is still allowed
    Check if no equipment is available for reservation
      Display message "No equipment is available for reservation"
    Else
      Display equipment catalog
  Search Equipment
    Oninput
      Filter the catalog as the user types
      If no equipment matches the filters
        Display message "No equipment matches your filters"
  Category Buttons
    Onclick
      Filter by All or the selected category
      If no equipment matches the filters
        Display message "No equipment matches your filters"
  Available Only
    Onchange
      Show only equipment with units free now
  Equipment Cards
    Photo, name, category, units free now
    Onclick
      Open [12.0 - Reservation Request] dialog
  Clear Filters
    Onclick
      Reset search and category
  View Borrowing History / My Reservations
    Onclick
      Redirect to [13.0 - My Reservations]
  Browse More Equipment
    Onclick
      Hide the submission confirmation and display the catalog
```

### 12.0 Reservation Request Logic
```text
Reservation Request
  Onload
    Check if the Borrower is not eligible to borrow
      Display panel "Borrowing access is paused"
      Display message "You can browse equipment, but contact an administrator before making another reservation."
    Else
      Display equipment details and the request form
  Equipment Details
    Name, photo, category, condition, description, units free
  Quantity
    1 to the number of free units (maximum of 10)
    − / + buttons adjust the quantity
  Borrowing Date and Time
    This is the start of the booking (Manila time)
    Defaults to the next hour
    Onchange
      Recheck the number of free units
      If date or time is invalid
        Display error message "Choose a valid borrowing and return date and time."
      If return time is not after borrowing time
        Display error message "The return time must be after the borrowing time."
      If borrowing time is not in the future
        Display error message "Choose a future borrowing time."
  Return Date and Time
    This is the end of the booking (Manila time)
    Defaults to 2 hours after start
    Onchange
      Recheck the number of free units
      If date or time is invalid
        Display error message "Choose a valid borrowing and return date and time."
      If return time is not after borrowing time
        Display error message "The return time must be after the borrowing time."
      If borrowing time is not in the future
        Display error message "Choose a future borrowing time."
  Purpose
    Required; maximum of 500 characters
  Notes
    Optional; maximum of 2000 characters
  Submit Request
    Onclick
      If borrowing access is paused
        Display error message "Your borrowing access is currently paused. Contact an administrator."
      If the form has lost its request ID
        Display error message "Reload the form and try again."
      If equipment cannot be requested
        Display error message "Choose available equipment."
      If only 1 unit exists and quantity is not 1
        Display error message "Only 1 unit of this equipment can be requested."
      If quantity is outside 1 to N free units
        Display error message "Request between 1 and N units."
      If purpose is empty
        Display error message "Enter the purpose of your request."
      If purpose is longer than 500 characters
        Display error message "Use 500 characters or fewer."
      If notes are longer than 2000 characters
        Display error message "Use 2000 characters or fewer."
      If a date or time cannot be read
        Display error message "Choose a valid date and time in Manila time."
      If end time is not after start time
        Display error message "The end time must be after the start time."
      If start time is not in the future
        Display error message "Choose a future start time."
      If the same form was already submitted and its reservations are still active
        Do not save duplicates
        Redirect to [11.0 - Browse Equipment] with the confirmation of the reservations already created
      If the same form was already submitted and its reservations were cancelled
        Display error message "This request form was already used. Review the details and submit again."
      If no unit is free for the requested time
        Display "Reservation conflict" error message "This equipment is unavailable for the requested time."
      If fewer units are free than requested
        Display error message "Only X of the Y units you requested are free for that time. Lower the quantity or choose another time."
      If another borrower reserves the units during submission
        Reserve no units (all-or-nothing)
        Display error message "Another borrower just reserved this equipment for the requested time. Lower the quantity or choose another time."
      Else
        Save one Pending reservation per unit
        Set each hold to expire 24 hours after submission or at the start time, whichever is earlier
        Redirect to [11.0 - Browse Equipment] with the submission confirmation
  Review the Availability Calendar
    Onclick
      Redirect to [14.0 - Borrower Availability Calendar] for the item
  Cancel / Close
    Onclick
      Close dialog
```

### 13.0 My Reservations Logic
```text
My Reservations
  Onload
    Check if a Pending request is past its hold expiry (24 hours after submission or the start time, whichever is earlier)
      Display status Expired; the equipment slot is released
    Check if the Borrower has no reservations
      Display message "No reservations yet" with a Reserve Equipment link
    Else
      Display Borrowing Records Table
  Search
    Equipment, asset code, or purpose
    Oninput
      Apply filters
  From Date / To Date
    Filters records by booking start date
    Onchange
      If From Date is invalid
        Display error message "Choose a valid from date."
      If To Date is invalid
        Display error message "Choose a valid to date."
      If To Date is earlier than From Date
        Display error message "The to date must be on or after the from date."
      Else
        Apply filters
  Status
    All, Pending, Approved, Borrowed, Returned, Late, Cancelled
    Onchange
      Apply filters
  Clear
    Onclick
      Clear all filters
  Borrowing Records Table
    Equipment, Reservation Time, Status, Return / Condition, Action
    12 records per page, newest first
  Reserve Equipment
    Onclick
      Redirect to [11.0 - Browse Equipment]
  Cancel Request
    Shown only for unexpired Pending or Approved requests
    Onclick
      If the pending hold had already expired
        Display message "The pending hold had already expired."
      If the equipment was already released
        Display error message "This request can no longer be cancelled."
      Else
        Cancel reservation
        Display message "Reservation cancelled. The equipment slot is available again."
```

### 14.0 Borrower Availability Calendar Logic
```text
Availability Calendar
  Onload
    Check if there is no active equipment
      Display message "No active equipment"
    Check if the selected item is under maintenance
      Display message "This item is under maintenance and cannot be reserved."
    Else
      Display the current month for the selected equipment
  Equipment
    This is the equipment whose schedule is displayed
    Onchange
      Display the selected equipment's schedule
  Previous / Next Month (← / →)
    Onclick
      Display adjacent month
  Calendar Date
    Onclick
      If there are no reservations on the selected date
        Display message "No scheduled reservations on this date."
      Else
        Display reserved time slots for the selected date (no borrower names)
  Reserve This Equipment
    Onclick
      Redirect to [11.0 - Browse Equipment] and open [12.0 - Reservation Request] for the item
```

### 15.0 Custodian Dashboard Logic
```text
Custodian Dashboard
  Review Requests
    Onclick
      Redirect to [16.0 - Approval & Release Queue]
  Release Desk
    Onclick
      Redirect to [18.0 - Return & Condition Check Queue] on the Approved Release Queue tab
  Availability Calendar
    Onclick
      Redirect to [21.0 - Staff Availability Calendar]
  Charts
    Releases and Returns: daily desk activity for the last 14 days
    Equipment Status: Available, Reserved, Borrowed, For repair
    Equipment Condition: active units by last recorded condition
    Equipment Status segment
      Onclick
        Available: Redirect to [21.0 - Staff Availability Calendar]
        Reserved: Redirect to [18.0 - Return & Condition Check Queue] on the Approved Release Queue tab
        Borrowed: Redirect to [18.0 - Return & Condition Check Queue] on the Active Loans tab
        For repair: Redirect to [22.0 - Equipment Maintenance]
  Requests Awaiting Review
    Up to 5 newest pending requests
    Onclick
      Redirect to [17.0 - Approval & Release Details]
  Overdue and Due Today
    Up to 5 loans due back by tonight
    Onclick
      Redirect to [19.0 - Return & Condition Check Details]
  Recent Releases and Returns
    Latest desk transactions
    Onclick
      Redirect to [17.0 - Approval & Release Details]
  Manage Maintenance
    Onclick
      Redirect to [22.0 - Equipment Maintenance]
```

### 16.0 Approval & Release Queue Logic
```text
Approval & Release Queue
  Onload
    Hide Pending requests whose hold has expired
    Display Pending requests (default status filter)
  Search Borrower or Equipment
    Name, email, or asset code
    Oninput
      Apply filters
  Status
    Pending (default), Approved, or Pending and Approved
    Onchange
      Apply filters
  Booking From / Booking Through
    Filters by booking start date
    Onchange
      If a date is invalid
        Display error message "Use valid dates for the date filters."
      If a date is outside 2000–2100
        Display error message "Choose filter dates between 2000 and 2100."
      If Booking Through is earlier than Booking From
        Display error message "The end date must be on or after the start date."
      Else
        Apply filters
  Reset
    Onclick
      Clear all filters
  Request Table
    Borrower, Equipment, Booking Time, Status, Return, Action; 12 per page
  Review Details
    Onclick
      Redirect to [17.0 - Approval & Release Details]
```

### 17.0 Approval & Release Details Logic
```text
Approval & Release Details
  Onload
    Check if the user is an Administrator
      Display title "Reservation Record"
      Hide all actions
      Display Back to Borrowing History link
    Check if the equipment is inactive, under maintenance, Damaged, or Unserviceable
      Display message "This item is inactive or on a maintenance hold. It cannot be approved or released."
      Disable Approve Request and Record Release
    Check if the request is Approved and the booking has not started
      Display message "Release opens at the approved start time."
      Disable Record Release
    Check if the request is Approved and the booking time has ended
      Display message "The booking time has ended. Equipment can no longer be released."
      Disable Record Release
    Check if the request is Pending and its hold expired
      Display message "The pending hold expired. This request cannot be approved or rejected."
      Disable Approve Request and Confirm Rejection
    Else
      Proceed with Approval & Release Details page
  Borrowing ID Card
    Reservation ID, borrower name, Student ID, department, contact
    Equipment, condition, location
    Reservation date, booking start, expected return, actual return
  Approve Request
    Enabled only for unexpired Pending requests with usable equipment
    Onclick
      If the request is no longer Pending
        Display error message "Only pending requests can be approved."
      If the hold expired before the action
        Display message "The pending hold expired before this action. Its equipment slot was released."
      If the schedule now conflicts
        Display error message "The equipment is no longer available for this time."
      If the record was changed by another user
        Display error message "This record changed. Reload and retry."
      Else
        Set status to Approved
        Display message "Reservation approved. The booking is ready for release at its start time."
  Rejection Reason
    Required; 1 to 1000 characters
  Confirm Rejection
    Onclick
      If reason is empty or longer than 1000 characters
        Display error message "Enter a rejection reason using 1 to 1000 characters."
      If the request is no longer Pending
        Display error message "Only pending requests can be rejected."
      If the hold expired before the action
        Display message "The pending hold expired before this action. Its equipment slot was released."
      If the record was changed by another user
        Display error message "This record changed. Reload and retry."
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
      If condition is not Excellent, Good, or Fair
        Display error message "Choose Excellent, Good, or Fair for an equipment release."
      If release notes are longer than 2000 characters
        Display error message "Release notes must use 2000 characters or fewer."
      If the request is not Approved
        Display error message "Only approved requests can be released."
      If the current time is outside the booking time
        Display error message "Release must occur during the approved booking time."
      If the record was changed by another user
        Display error message "This record changed. Reload and retry."
      Else
        Create loan; set status to Borrowed
        Display message "Release recorded. The equipment is now on loan."
  Back to Requests
    Onclick
      Redirect to [16.0 - Approval & Release Queue]
  Back to Release Queue
    Onclick
      Redirect to [18.0 - Return & Condition Check Queue] on the Approved Release Queue tab
  Back to Borrowing History (Administrator)
    Onclick
      Redirect to [20.0 - Staff Borrowing History Report]
```

### 18.0 Return & Condition Check Queue Logic
```text
Return & Condition Check Queue
  Onload
    Display Active Loans tab (default)
    Check if a loan is past its return time
      Display status Overdue
  Active Loans Tab
    Onclick
      List released equipment not yet returned, newest release first
  Approved Release Queue Tab
    Onclick
      List approved requests awaiting release
  Search
    Name, email, or asset code
    Oninput
      Apply filters
  Booking From / Booking Through
    Onchange
      If a date is invalid
        Display error message "Use valid dates for the date filters."
      If a date is outside 2000–2100
        Display error message "Choose filter dates between 2000 and 2100."
      If Booking Through is earlier than Booking From
        Display error message "The end date must be on or after the start date."
      Else
        Apply filters
  Record Return (Active Loans row)
    Onclick
      Redirect to [19.0 - Return & Condition Check Details]
  Release Details (Approved Release Queue row)
    Onclick
      Redirect to [17.0 - Approval & Release Details]
```

### 19.0 Return & Condition Check Details Logic
```text
Return & Condition Check Details
  Onload
    Check if the user is an Administrator
      Hide all actions
    Else
      Proceed with Return & Condition Check Details page
  Borrowing ID Card
    Reservation ID, borrower name, Student ID, department, contact
    Equipment, condition, location
    Reservation date, booking start, expected return, actual return
    Condition at release and release notes
  Condition at Return
    Excellent, Good, Fair, Damaged, or Unserviceable
  Damage or Inspection Notes
    Optional; maximum of 2000 characters
  Record Return
    Onclick
      If condition is not one of the listed values
        Display error message "Choose a valid return condition."
      If notes are longer than 2000 characters
        Display error message "Return notes must use 2000 characters or fewer."
      If the loan was already returned
        Display error message "Only an active loan can be returned."
      If the record was changed by another user
        Display error message "This record changed. Reload and retry."
      If condition is Damaged or Unserviceable
        Record return; set status to Returned
        Place equipment on maintenance hold
        Open a maintenance case in [22.0 - Equipment Maintenance]
        Display message "Return recorded. A maintenance hold was created for this equipment."
      Else
        Record return; set status to Returned
        Update equipment condition
        Display message "Return recorded. The equipment condition and borrowing history are updated."
      If returned after the expected return time
        Mark the record "Returned late"
  Back to Return Queue
    Onclick
      Redirect to [18.0 - Return & Condition Check Queue]
```

### 20.0 Staff Borrowing History Report Logic
```text
Staff Borrowing History Report
  Onload
    Display all borrowers' reservations; 12 records per page, newest first
    Check if a record is unreturned past its due time or was returned after its due time
      Display status Late
  Search
    Borrower name, email, or asset code
    Oninput
      Apply filters
  Status
    All, Pending, Approved, Borrowed, Returned, Rejected, Cancelled, Expired, Late
    Onchange
      Apply filters
  Booking From / Booking Through
    Onchange
      If a date is invalid
        Display error message "Use valid dates for the date filters."
      If a date is outside 2000–2100
        Display error message "Choose filter dates between 2000 and 2100."
      If Booking Through is earlier than Booking From
        Display error message "The end date must be on or after the start date."
      Else
        Apply filters
  Record Row
    Onclick
      If the record is an active or returned loan
        Redirect to [19.0 - Return & Condition Check Details]
      Else
        Redirect to [17.0 - Approval & Release Details]
```

### 21.0 Staff Availability Calendar Logic
```text
Staff Availability Calendar
  Onload
    Display the current month for the selected equipment
  Equipment
    This is the unit whose schedule is displayed
    Items under maintenance are labeled "Maintenance"
    Onchange
      Display the selected unit's schedule
  Previous / Next Month (← / →)
    Onclick
      Display adjacent month
  Calendar Day
    Shows Available, Reserved (with booking count), or Maintenance
    Onclick
      If no reservations overlap the selected day
        Display message "No reservations overlap this day for the selected equipment."
      Else
        Display booked time slots and borrower names for the selected day
  View Details
    Onclick
      Redirect to [17.0 - Approval & Release Details]
```

### 22.0 Equipment Maintenance Logic
```text
Equipment Maintenance
  Onload
    Display Open tab (default)
  Open / Closed Tabs
    Onclick
      Filter maintenance cases by status
  Search Cases
    Equipment, asset code, issue, or repair notes
    Oninput
      Apply search
  Maintenance Cases Table
    Equipment, Issue, Status, Opened / Closed, Action; paginated
  Open Maintenance
    Onclick
      Open the Open Maintenance dialog
  Open Maintenance (dialog)
    Equipment
      Items on loan or with an open case are listed but disabled
    Issue Description
      Required; maximum of 2000 characters
    Open Maintenance Case
      Onclick
        If no equipment chosen
          Display error message "Choose the equipment that needs maintenance."
        If description is empty
          Display error message "Describe the problem so the repair can be tracked."
        If description is longer than 2000 characters
          Display error message "Keep the description to 2000 characters or fewer."
        If the equipment no longer exists or is inactive
          Display error message "That equipment no longer exists or is inactive. Reload and choose another item."
        If item is currently on loan
          Display error message "This item is on loan. Record its return before placing it in maintenance."
        If item already has an open case
          Display error message "This item already has an open maintenance case. Update that case instead."
        Else
          Open case; block new reservations and release of the item
          Display message "<Item> (<Asset code>) is now in maintenance. New reservations and releases are blocked until the case is closed."
  Update
    Onclick
      Open the Update dialog
  Update (dialog)
    Status
      Open, In Progress, or Closed · repair complete
    Condition After Repair
      This is the equipment condition after repair
    Repair Notes
      This is the resolution of the repair; maximum of 2000 characters
    Save Maintenance
      Onclick
        If status or condition is invalid, or notes are longer than 2000 characters
          Display error message "Choose a valid status and equipment condition, and keep the notes to 2000 characters."
        If the case no longer exists
          Display error message "This maintenance case no longer exists. Reload the list."
        If the case is already closed
          Display error message "This maintenance case is already closed."
        If the case was changed by another user
          Display error message "Someone else changed this case while you were editing. Close this dialog to see the latest details, then try again."
        If closing without repair notes
          Display error message "Describe the repair before closing the case."
        If closing with Damaged or Unserviceable condition
          Display error message "Choose Excellent, Good, or Fair as the condition after repair before closing the case."
        If status is Closed
          Return the item to availability if it has no other open case
          Display message "Repair recorded. <Item> (<Asset code>) is back in service in <Condition> condition."
        Else
          Display message "Maintenance saved for <Item> (<Asset code>). Status: Open / In progress."
  View (Closed tab)
    Onclick
      Display the closed case read-only
```

### 23.0 Custodian Equipment Categories Logic
```text
Custodian Equipment Categories
  Onload
    Check if no categories exist
      Display message "No equipment categories have been created yet."
    Else
      Display one read-only section per category; inactive categories are labeled
      Categories and items are managed by an Administrator in [26.0 - Equipment Categories] and [28.0 - Equipment Management]
  Units Table
    Equipment, Asset Code, Condition, Location, Status
```

### 24.0 Custodian Borrower Profiles Logic
```text
Custodian Borrower Profiles
  Onload
    Display all borrowers, read-only; paginated, newest first
    Profiles are edited by an Administrator in [34.0 - Edit Account & Role]
  Search Borrowers
    Name, email, Student ID, or department
    Oninput
      Apply filters
      If no borrower matches
        Display message "No matching borrowers"
  Eligibility
    All borrowers, Eligible to borrow, or Borrowing paused
    Onchange
      Apply filters
      If no borrower matches
        Display message "No matching borrowers"
  Clear
    Onclick
      Clear all filters
  Borrower Table
    Name, Student ID Number, Department, Contact, Eligibility
```

### 25.0 Administrator Dashboard Logic
```text
Administrator Dashboard
  Verify Registrations
    Shows the number of pending registrations
    Onclick
      Redirect to [32.0 - Registration Verification]
  Charts
    Borrowing Activity: weekly requests submitted and equipment released, last 12 weeks
    Equipment Status: Available, Reserved, Borrowed, For repair
    Reservation Requests: all requests by current status
    Borrower Registrations: new sign-ups per month, last 6 months
    Equipment Status segment
      Onclick
        Available: Redirect to [28.0 - Equipment Management]
        Reserved: Redirect to [20.0 - Staff Borrowing History Report] filtered to Approved
        Borrowed: Redirect to [20.0 - Staff Borrowing History Report] filtered to Borrowed
        For repair: Redirect to [22.0 - Equipment Maintenance]
    Reservation Requests segment
      Onclick
        Redirect to [20.0 - Staff Borrowing History Report] filtered to the selected status
  Latest Transactions
    7 most recently updated reservations
    Onclick
      Redirect to [17.0 - Approval & Release Details] (read-only)
  Registrations to Verify
    Up to 5 newest pending registrations
    Onclick
      Redirect to [34.0 - Edit Account & Role]
```

### 26.0 Equipment Categories Logic
```text
Equipment Categories
  Search Categories
    Name or description
    Oninput
      Apply search
  Category Records Table
    Name, Description, Items, Status, Action
  Add Category
    Onclick
      Open [27.0 - Add / Edit Equipment Category]
  Edit
    Onclick
      Open [27.0 - Add / Edit Equipment Category] with the record loaded
  Delete
    Onclick
      Display confirmation "Delete this category? A category that still holds equipment is deactivated instead."
      If confirmed and the category has no equipment
        Delete category
        Display message "<Name> was deleted."
      If confirmed and the category holds equipment
        Deactivate category
        Display message "<Name> still holds equipment, so it was deactivated instead of deleted."
      Else
        Close confirmation
```

### 27.0 Add / Edit Equipment Category Logic
```text
Add / Edit Equipment Category
  Onload
    Check if opened from Edit
      Load the category's details
    Else
      Display an empty form
  Category Name
    Required; maximum of 120 characters; must be unique
  Description
    Optional; maximum of 1000 characters
  Active Category
    Indicates whether the category is available for use
  Save Changes
    Onclick
      Check if Category Name is empty
        Display error message "The Name field is required."
      Check if Category Name is longer than 120 characters
        Display error message "The field Name must be a string with a maximum length of 120."
      Check if Description is longer than 1000 characters
        Display error message "The field Description must be a string with a maximum length of 1000."
      If Active Category is unchecked and the category still has active equipment
        Display error message "Move or deactivate this category's active equipment before deactivating the category."
      If the category was changed by another user
        Display error message "This category changed. Reload before saving."
      If category name already exists
        Display error message "That category name already exists, or the record changed."
      Else
        Save category
        Display message "Category saved."
        Redirect to [26.0 - Equipment Categories]
  Cancel
    Onclick
      Redirect to [26.0 - Equipment Categories]
```

### 28.0 Equipment Management Logic
```text
Equipment Management
  Search Inventory
    Name or asset code
    Oninput
      Apply search
  Equipment Cards
    Photo, name, asset code, category, condition, availability
    Maintenance Hold badge
      Onclick
        Redirect to [22.0 - Equipment Maintenance]
  Add Item
    Onclick
      Open [29.0 - Add / Edit Equipment Item]
  Edit Item
    Onclick
      Open [29.0 - Add / Edit Equipment Item] with the record loaded
  Delete
    Onclick
      Display confirmation "Delete this item? Items with borrowing or repair history are deactivated instead."
      If confirmed and the item has no history
        Delete item and its photo
        Display message "<Asset code> was deleted."
      If confirmed and the item has borrowing or repair history
        Deactivate item
        Display message "<Asset code> has borrowing or repair history, so it was deactivated instead of deleted."
      Else
        Close confirmation
  Previous / Next
    Onclick
      Display adjacent page
```

### 29.0 Add / Edit Equipment Item Logic
```text
Add / Edit Equipment Item
  Onload
    Check if opened from Edit Item
      Load the item's details
      Check if the item has an open maintenance case
        Display Maintenance Hold; the hold remains until the case is closed in [22.0 - Equipment Maintenance]
    Else
      Display an empty form
  Equipment Name
    Required; maximum of 160 characters
  Asset Code
    This is the unique inventory code of the item
    Required; maximum of 64 characters
  Category
    Lists active categories only
  Condition
    Excellent, Good, Fair, Damaged, or Unserviceable
  Serial Number
    Optional; maximum of 120 characters; must be unique
  Storage Location
    Optional; maximum of 160 characters
  Description
    Optional; maximum of 2000 characters
  Equipment Photo
    Optional; JPEG, PNG, WebP, or GIF; maximum of 5 MB
    Existing photo can be removed
  Active Inventory Item
    Indicates whether the item can be reserved
  Save Changes
    Onclick
      Check if Equipment Name is empty
        Display error message "The Name field is required."
      Check if Asset Code is empty
        Display error message "The AssetCode field is required."
      Check if a field is longer than its maximum
        Display error message "The field <Field> must be a string with a maximum length of <N>."
      If condition is not one of the listed values
        Display error message "Choose a valid condition."
      If category is not active
        Display error message "Choose an active category."
      If photo is larger than 5 MB
        Display error message "Use an image of 5 MB or smaller."
      If photo is not a JPEG, PNG, WebP, or GIF image
        Display error message "Upload a JPEG, PNG, WebP or GIF image."
      If the item is currently on loan
        Display error message "Record the active loan's return before editing this equipment."
      If Active Inventory Item is unchecked and the item has pending or approved reservations
        Display error message "Resolve this item's pending and approved reservations before deactivating it."
      If the item was changed by another user
        Display error message "This equipment changed. Reload it before saving."
      If asset code or serial number already exists
        Display error message "The asset code or serial number already exists, or the record changed. Reload and try again."
      If condition is set to Damaged or Unserviceable
        Save item
        Place item on maintenance hold and open a maintenance case in [22.0 - Equipment Maintenance]
        Redirect to [28.0 - Equipment Management]
      Else
        Save item
        Display message "Equipment added." / "Equipment updated."
        Redirect to [28.0 - Equipment Management]
  Cancel
    Onclick
      Redirect to [28.0 - Equipment Management]
```

### 30.0 Borrower Profiles Logic
```text
Borrower Profiles
  Search Accounts
    Name, email, Student ID, or department
    Oninput
      Apply filters
  Account Status
    All Accounts, Pending Approval, Active, Inactive, or Rejected
    Onchange
      Apply filters
  Clear
    Onclick
      Clear filters
  Borrower Records Table
    Name, Student ID Number, Email, Department, Status, Registered, Action
  Edit
    Onclick
      Redirect to [34.0 - Edit Account & Role]
  Delete
    Onclick
      Display confirmation "Delete this account? Accounts with borrowing history are deactivated instead."
      If confirmed and the account has no borrowing history
        Delete account
        Display message "<Name>'s account was deleted."
      If confirmed and the account has borrowing history
        Deactivate account
        Display message "<Name> has borrowing history, so the account was deactivated instead of deleted. They can no longer sign in."
      If the account cannot be deleted
        Display error message "The account could not be deleted. Please try again."
      Else
        Close confirmation
```

### 31.0 User & Role Management Logic
```text
User & Role Management
  Search Accounts
    Name, email, Student ID, or department
    Oninput
      Apply filters
  Role
    All Roles, Borrower, Custodian, or Administrator
    Onchange
      Apply filters
  Account Status
    All Accounts, Pending Approval, Active, Inactive, or Rejected
    Onchange
      Apply filters
  Verify Registrations
    Onclick
      Redirect to [32.0 - Registration Verification]
  Create Account
    Onclick
      Redirect to [33.0 - Create Account]
  Account Records Table
    Name, Student ID Number, Email, Role, Status, Registered, Action
  Edit
    Onclick
      Redirect to [34.0 - Edit Account & Role]
  Delete
    Onclick
      Display confirmation "Delete this account? Accounts with borrowing history are deactivated instead."
      If the account is the Administrator's own account
        Display error message "You cannot delete your own account."
      If the account is the last active Administrator
        Display error message "Keep at least one active, verified Administrator account."
      If confirmed and the account has no borrowing history
        Delete account
        Display message "<Name>'s account was deleted."
      If confirmed and the account has borrowing history
        Deactivate account
        Display message "<Name> has borrowing history, so the account was deactivated instead of deleted. They can no longer sign in."
      If the account cannot be deleted
        Display error message "The account could not be deleted. Please try again."
      Else
        Close confirmation
```

### 32.0 Registration Verification Logic
```text
Registration Verification
  Onload
    Check if no registrations are pending
      Display message "No registrations are waiting"
    Else
      Display Pending Registrations Table
  Pending Registrations Table
    Name, Student ID Number, Email, Role, Status, Registered, Action
  Approve
    Onclick
      If the registration was already decided
        Display error message "<Name>'s registration has already been decided."
      If the account was changed by another user
        Display error message "This account changed while the page was open. Review it and try again."
      Else
        Set account to Approved; account can sign in
        Display message "<Name>'s account was approved."
  Reject
    Onclick
      If the registration was already decided
        Display error message "<Name>'s registration has already been decided."
      If the account was changed by another user
        Display error message "This account changed while the page was open. Review it and try again."
      Else
        Set account to Rejected; sign-in is refused
        Display message "<Name>'s registration was rejected."
        A rejected registration can still be approved later in [34.0 - Edit Account & Role]
  Show All Accounts
    Onclick
      Redirect to [31.0 - User & Role Management]
```

### 33.0 Create Account Logic
```text
Create Account
  First Name / Last Name
    Required; maximum of 100 characters each
  Email
    Required; must be unique
  Role
    Borrower, Custodian, or Administrator
    Onchange
      Show Borrower Profile fields only when Borrower is selected
  Temporary Password
    Required; minimum of 8 characters
    Must contain an uppercase letter, a lowercase letter, a digit, and a symbol
  Contact Number
    Optional; maximum of 32 characters
  Borrower Profile: Student ID Number / Department
    Borrower role only; Student ID Number must be unique
  Save
    Onclick
      Check if First Name is empty
        Display error message "The First name field is required."
      Check if Last Name is empty
        Display error message "The Last name field is required."
      Check if Email is empty
        Display error message "The Email field is required."
      Check if Email format is invalid
        Display error message "The Email field is not a valid e-mail address."
      Check if Temporary Password is empty
        Display error message "The Temporary password field is required."
      Check if Temporary Password is shorter than 8 characters
        Display error message "The field Temporary password must be a string with a minimum length of 8 and a maximum length of 100."
      Check if Temporary Password fails the password policy
        Display error message "Passwords must have at least one non alphanumeric character." / "Passwords must have at least one digit ('0'-'9')." / "Passwords must have at least one lowercase ('a'-'z')." / "Passwords must have at least one uppercase ('A'-'Z')."
      If Role is not Borrower, Custodian, or Administrator
        Display error message "Choose Borrower, Custodian, or Administrator."
      If borrower fields are entered for a staff role
        Display error message "Student ID Number and department apply to borrowers only."
      If email is already used
        Display error message "An account already uses this email address."
      If Student ID Number is already registered
        Display error message "This Student ID Number is already registered."
      If the role cannot be assigned
        Display error message "The role could not be assigned. Please try again."
      Else
        Create account, approved and verified at once
        Display message "<Name> can now sign in as <Role>."
        Redirect to [31.0 - User & Role Management]
  Back to Users
    Onclick
      Redirect to [31.0 - User & Role Management]
```

### 34.0 Edit Account & Role Logic
```text
Edit Account & Role
  Onload
    Load the account's details
    Check if the account is not approved
      Disable the Custodian and Administrator roles
    Else
      Proceed with Edit Account & Role page
  First Name / Last Name
    Required; maximum of 100 characters each
  Email / Contact
    Shown read-only
  Department
    Optional; maximum of 160 characters
  Role
    Borrower, Custodian, or Administrator
  Account Is Active
    Deactivation signs the user out; reservations and history stay intact
  Student ID Number
    Optional; maximum of 64 characters; must be unique
  Borrowing Access (Eligible to Reserve Equipment)
    Controls whether the borrower can submit reservations
  Eligibility Notes
    Optional; maximum of 1000 characters
  Save Changes
    Onclick
      Check if First Name is empty
        Display error message "The First name field is required."
      Check if Last Name is empty
        Display error message "The Last name field is required."
      If Role is not Borrower, Custodian, or Administrator
        Display error message "Choose Borrower, Custodian, or Administrator."
      If the Administrator deactivates or removes the Administrator role from their own account
        Display error message "You cannot deactivate your own account or remove your own Administrator role."
      If a staff role is assigned to an unapproved account
        Display error message "The account must be approved before receiving a staff role."
      If the change would leave no active Administrator
        Display error message "Keep at least one active, verified Administrator account."
      If borrower profile details are entered for a staff role
        Display error message "Borrower profile details apply when Borrower is selected. Existing profiles are retained when roles change."
      If Student ID Number is already used
        Display error message "This Student ID Number is already registered."
      If the account was changed by another user
        Display error message "This account changed while the form was open. Review the latest details and try again."
      If another administration action is running on the account
        Display error message "Account administration is busy. Please try again."
      If the account cannot be saved
        Display error message "We could not update the account. Please try again."
      Else
        Save account
        If role or active status changed
          Sign the user out; they must sign in again
        Display message "Account details and access permissions updated."
        Redirect to [31.0 - User & Role Management]
  Cancel / Back to Users
    Onclick
      Redirect to [31.0 - User & Role Management]
```

### 35.0 System Audit Log Logic
```text
System Audit Log
  Onload
    Display audit history; read-only; paginated; newest first; Manila time
  Search Audit Events
    Action, actor, record, result, or IP address
    Oninput
      Apply search
  Clear
    Onclick
      Clear search
  Audit History Table
    Time, Action / Actor, Record, Result, Details
    Details
      Onclick
        Expand to show Before and After values
```

### 36.0 Development Email Inbox Logic
```text
Development Email Inbox
  Onload
    Check if the system is not in the Development environment or the page is not opened from the local machine
      Return "Not Found" (404)
    Else
      Display captured password reset and administrator sign-in code emails
      Messages disappear when the app restarts
  Refresh Inbox
    Onclick
      Reload the list
  Sign In
    Onclick
      Redirect to [1.0 - Home / Login]
```

### 37.0 Error Logic
```text
Error
  Onload
    Shown when an unhandled system error occurs (non-development environment)
    Display Request ID for troubleshooting reference
```

### Common Logic (All Workspace Screens, 10.0–35.0)
```text
Workspace Screen
  Onload
    Check if the user is not signed in
      Redirect to [1.0 - Home / Login]
    Check if the account was deactivated
      End session
      Redirect to [1.0 - Home / Login]
    Check if the user's role is not allowed on the screen
      Redirect to [9.0 - Access Denied]
    Else
      Proceed with the screen
Sidebar Menu
  Onload
    Show only the menu items allowed for the user's role
      Borrower: Home, Browse Equipment, My Reservations, Availability
      Custodian: Dashboard, Approval & Release, Return & Condition Check, Borrowing History,
                 Availability Calendar, Maintenance, Equipment Categories, Borrower Profiles
      Administrator: Dashboard, Equipment Items, Equipment Categories, Maintenance, Borrowing History,
                     Availability Calendar, Borrower Profiles, Users & Roles, Audit Log
CampusGear Logo
  Onclick
    If role is Administrator
      Redirect to [25.0 - Administrator Dashboard]
    If role is Custodian
      Redirect to [15.0 - Custodian Dashboard]
    If role is Borrower
      Redirect to [10.0 - Borrower Home]
Theme Toggle
  Onclick
    Switch between light and dark mode
Notifications
  Success and error messages appear as toasts
Log Out
  Onclick
    End session
    Redirect to [1.0 - Home / Login]
```

### Common Logic (Authentication Screens, 1.0–7.0)
```text
Any Form Submit (Sign In, Create Account, Verify, Send Reset Code, Update Password, Resend)
  Onclick
    Check if a required field is empty
      Display browser message "Please fill out this field."
    Check if the form's security token has expired
      Display error message "Your form is out of date. Please enter your details and try again."
      Reload the same screen with an empty form
    Check if more than 20 submissions per minute come from the same IP address
      Reject the request (HTTP 429 Too Many Requests)
    Else
      Continue with the screen's own Onclick logic
```

Administrators take no part in the borrowing flow: there is no Administrator screen to reserve, approve, reject, release, or return. The former Email Verification, Registration Success, Reservation Management, and New Reservation screens were removed. See [PERMISSIONS.md](PERMISSIONS.md) for the full function-by-role table.
