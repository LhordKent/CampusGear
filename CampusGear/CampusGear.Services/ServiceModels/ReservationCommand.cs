namespace CampusGear.Services.ServiceModels;
public sealed class ReservationCommand
{
    public Guid RequestId { get; set; }
    public Guid EquipmentItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? StartLocal { get; set; }
    public string? EndLocal { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
}
