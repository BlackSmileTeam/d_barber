namespace Barber.Domain.Enums;

/// <summary>Interval after last visit for recurring notification templates.</summary>
public enum TriggerIntervalType
{
    /// <summary>Event-based or manual — use free-text TriggerDescription.</summary>
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Custom = 4
}
