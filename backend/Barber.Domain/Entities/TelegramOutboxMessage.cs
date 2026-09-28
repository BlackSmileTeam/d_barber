namespace Barber.Domain.Entities;

/// <summary>
/// Messages queued by API for the Telegram bot to deliver (bot owns Telegram I/O).
/// </summary>
public class TelegramOutboxMessage
{
    public Guid Id { get; set; }
    public long ChatId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
}
