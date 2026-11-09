using CampusGear.Models;
namespace CampusGear.Data.QueryModels;
public sealed record AccountRecord(string FirstName, string LastName, string FullName, string? Email);
public sealed record BorrowerRecord(string? StudentNumber, string? Department, string? ContactNumber, AccountRecord User);
public sealed record LoanRecord(DateTime ReleasedAtUtc, DateTime DueAtUtc, DateTime? ReturnedAtUtc,
    EquipmentCondition ConditionAtRelease, EquipmentCondition? ConditionAtReturn, string? ReleaseNotes, string? ReturnNotes,
    AccountRecord ReleasedByUser, AccountRecord? ReceivedByUser);
public sealed record ReservationRecord(Guid Id, DateTime StartAtUtc, DateTime EndAtUtc, DateTime? HoldExpiresAtUtc,
    ReservationStatus Status, string Purpose, string? Notes, string? DecisionReason, DateTime? DecidedAtUtc, DateTime CreatedAtUtc,
    BorrowerRecord BorrowerProfile, EquipmentRecord EquipmentItem, AccountRecord? DecidedByUser, LoanRecord? Loan)
{
    private static AccountRecord Person(ApplicationUser x) => new(x.FirstName, x.LastName, x.FullName, x.Email);
    public static ReservationRecord From(Reservation x) => new(x.Id, x.StartAtUtc, x.EndAtUtc, x.HoldExpiresAtUtc,
        x.Status, x.Purpose, x.Notes, x.DecisionReason, x.DecidedAtUtc, x.CreatedAtUtc,
        new(x.BorrowerProfile.StudentNumber, x.BorrowerProfile.Department, x.BorrowerProfile.ContactNumber, Person(x.BorrowerProfile.User)),
        EquipmentRecord.From(x.EquipmentItem), x.DecidedByUser is null ? null : Person(x.DecidedByUser),
        x.Loan is null ? null : new(x.Loan.ReleasedAtUtc, x.Loan.DueAtUtc, x.Loan.ReturnedAtUtc, x.Loan.ConditionAtRelease,
            x.Loan.ConditionAtReturn, x.Loan.ReleaseNotes, x.Loan.ReturnNotes, Person(x.Loan.ReleasedByUser),
            x.Loan.ReceivedByUser is null ? null : Person(x.Loan.ReceivedByUser)));
}
public sealed record CustodianRows(IReadOnlyList<Reservation> Rows, int Page, int Pages, int Total, int Pending, int Approved, int Active);
