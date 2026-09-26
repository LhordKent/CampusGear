namespace CampusGear.Models;

public sealed class BorrowerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string? StudentNumber { get; set; }
    public string? Department { get; set; }
    public string? ContactNumber { get; set; }
    public bool IsEligible { get; set; } = true;
    public string? EligibilityNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ApplicationUser User { get; set; } = null!;
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
