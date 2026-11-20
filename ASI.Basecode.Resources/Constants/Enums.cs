// ENUMS  -  the fixed lists of values used across the app. They are saved in the database as text (e.g. "Approved").
namespace ASI.Basecode.Resources.Constants
{
    // An item's physical condition. Damaged = can't be booked or handed over; it goes to maintenance.
    public enum EquipmentCondition
    {
        Excellent,
        Good,
        Fair,
        Damaged,
    }

    // Where a booking is in its life: Pending -> Approved -> Released (on loan) -> Completed.
    // Rejected, Cancelled and Expired are the ways it can end early.
    public enum ReservationStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled,
        Expired,
        Released,
        Completed,
    }

    // A repair case: still being worked on, or finished.
    public enum MaintenanceStatus
    {
        Open,
        InProgress,
        Closed,
    }

    // What an emailed code is for.
    public enum EmailChallengePurpose
    {
        SignupVerification,
        AdministratorLogin,
        PasswordReset,
    }

    // A student sign-up: waiting for an admin, approved, or rejected.
    public enum AccountApprovalStatus
    {
        Approved,
        PendingApproval,
        Rejected,
    }

    // What an item can do right now (shown on cards and pickers).
    public enum ItemStatus
    {
        Available,
        Reserved,
        Borrowed,
        ForRepair,
        Inactive,
    }
}
