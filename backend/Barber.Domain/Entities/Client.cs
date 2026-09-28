namespace Barber.Domain.Entities;

public class Client
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? TelegramChatId { get; set; }
    /// <summary>Telegram user id from Login Widget / bot (private chat id == user id).</summary>
    public long? TelegramUserId { get; set; }
    public string? TelegramUsername { get; set; }
    public string? TelegramFirstName { get; set; }
    public string? TelegramLastName { get; set; }
    public string? TelegramPhotoUrl { get; set; }
    public DateTime? TelegramAuthAtUtc { get; set; }
    /// <summary>True after website register or after a login password was issued/chosen.</summary>
    public bool HasUserPassword { get; set; }
    public bool CreatedViaTelegram { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastVisitAtUtc { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
