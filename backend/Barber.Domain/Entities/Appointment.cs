using Barber.Domain.Enums;

namespace Barber.Domain.Entities;

public class Appointment
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? Reminder2hSentAtUtc { get; set; }
    public DateTime? MonthlyReminderSentAtUtc { get; set; }
}
