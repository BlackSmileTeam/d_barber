using Barber.Domain.Entities;
using Barber.Domain.Enums;
using Barber.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Barber.Infrastructure.Services;

public class SlotService(BarberDbContext db)
{
    private static TimeZoneInfo GetTz(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }

    public async Task<IReadOnlyList<DateTime>> GetAvailableSlotsAsync(Guid serviceId, DateOnly date, CancellationToken ct = default)
    {
        var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive, ct)
            ?? throw new InvalidOperationException("Услуга не найдена");

        // Explicit working days only — unmarked dates are days off.
        var workingDay = await db.WorkingDays.AsNoTracking().FirstOrDefaultAsync(w => w.Date == date, ct);
        if (workingDay is null)
            return [];

        var settings = await db.SalonSettings.AsNoTracking().FirstAsync(ct);
        var tz = GetTz(settings.TimeZoneId);

        var dayStartLocal = date.ToDateTime(TimeOnly.FromTimeSpan(workingDay.StartTime));
        var dayEndLocal = date.ToDateTime(TimeOnly.FromTimeSpan(workingDay.EndTime));
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dayStartLocal, DateTimeKind.Unspecified), tz);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dayEndLocal, DateTimeKind.Unspecified), tz);

        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.Status != AppointmentStatus.Cancelled
                        && a.Status != AppointmentStatus.NoShow
                        && a.Status != AppointmentStatus.Completed
                        && a.StartAtUtc < dayEndUtc
                        && a.EndAtUtc > dayStartUtc)
            .Select(a => new { a.StartAtUtc, a.EndAtUtc })
            .ToListAsync(ct);

        var timeOffs = await db.TimeOffs.AsNoTracking()
            .Where(t => t.StartAtUtc < dayEndUtc && t.EndAtUtc > dayStartUtc)
            .ToListAsync(ct);

        var slots = new List<DateTime>();
        var duration = TimeSpan.FromMinutes(service.DurationMinutes);
        for (var cursor = dayStartUtc; cursor + duration <= dayEndUtc; cursor = cursor.AddMinutes(service.DurationMinutes))
        {
            var end = cursor + duration;
            if (cursor < DateTime.UtcNow.AddMinutes(30))
                continue;

            var busy = appointments.Any(a => cursor < a.EndAtUtc && end > a.StartAtUtc)
                       || timeOffs.Any(t => cursor < t.EndAtUtc && end > t.StartAtUtc);
            if (!busy)
                slots.Add(cursor);
        }

        return slots;
    }

    /// <summary>
    /// Resolves default open hours for a weekday from WorkSchedule, or 10:00–20:00.
    /// </summary>
    public async Task<(TimeSpan Start, TimeSpan End)> GetDefaultHoursAsync(DayOfWeek dayOfWeek, CancellationToken ct = default)
    {
        var schedule = await db.WorkSchedules.AsNoTracking()
            .FirstOrDefaultAsync(s => s.DayOfWeek == (int)dayOfWeek && !s.IsDayOff, ct);
        if (schedule is not null)
            return (schedule.StartTime, schedule.EndTime);
        return (new TimeSpan(10, 0, 0), new TimeSpan(20, 0, 0));
    }
}
