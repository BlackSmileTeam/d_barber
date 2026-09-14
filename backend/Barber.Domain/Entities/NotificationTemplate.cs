using Barber.Domain.Enums;

namespace Barber.Domain.Entities;

public class NotificationTemplate
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>Human-readable Russian hint: when this notification is sent.</summary>
    public string TriggerDescription { get; set; } = string.Empty;
    /// <summary>Structured interval after last visit (for recurring reminders).</summary>
    public TriggerIntervalType TriggerIntervalType { get; set; } = TriggerIntervalType.None;
    /// <summary>Days after last visit when TriggerIntervalType is Custom.</summary>
    public int? TriggerIntervalDays { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
