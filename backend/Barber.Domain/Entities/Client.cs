namespace Barber.Domain.Entities;

public class Client
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? TelegramChatId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastVisitAtUtc { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
