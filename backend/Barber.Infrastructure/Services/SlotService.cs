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

        var settings = await db.SalonSettings.AsNoTracking().FirstAsync(ct);
        var tz = GetTz(settings.TimeZoneId);
        var dow = (int)date.DayOfWeek;
        var schedule = await db.WorkSchedules.AsNoTracking().FirstOrDefaultAsync(s => s.DayOfWeek == dow, ct);
        if (schedule is null || schedule.IsDayOff)
            return [];

        var dayStartLocal = date.ToDateTime(TimeOnly.FromTimeSpan(schedule.StartTime));
        var dayEndLocal = date.ToDateTime(TimeOnly.FromTimeSpan(schedule.EndTime));
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dayStartLocal, DateTimeKind.Unspecified), tz);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dayEndLocal, DateTimeKind.Unspecified), tz);

        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.Status != AppointmentStatus.Cancelled
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
}
