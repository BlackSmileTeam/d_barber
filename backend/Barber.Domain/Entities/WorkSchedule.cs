namespace Barber.Domain.Entities;

public class WorkSchedule
{
    public Guid Id { get; set; }
    /// <summary>0 = Sunday ... 6 = Saturday</summary>
    public int DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsDayOff { get; set; }
}
