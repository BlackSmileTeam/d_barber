namespace Barber.Domain.Entities;

public class TimeOff
{
    public Guid Id { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string? Reason { get; set; }
}
