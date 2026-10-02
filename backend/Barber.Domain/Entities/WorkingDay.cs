namespace Barber.Domain.Entities;

/// <summary>
/// Explicit working calendar day. Days without a row are days off (no bookable slots).
/// </summary>
public class WorkingDay
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}
