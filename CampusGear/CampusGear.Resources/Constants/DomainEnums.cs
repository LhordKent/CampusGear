namespace CampusGear.Models;

public enum EquipmentCondition
{
    Excellent,
    Good,
    Fair,
    Damaged,
    Unserviceable
}

public enum ReservationStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Expired,
    Released,
    Completed
}

public enum MaintenanceStatus
{
    Open,
    InProgress,
    Closed
}

public enum EmailChallengePurpose
{
    SignupVerification,
    AdministratorLogin,
    PasswordReset
}
