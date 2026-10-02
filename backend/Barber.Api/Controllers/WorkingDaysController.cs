using Barber.Application.DTOs;
using Barber.Domain.Entities;
using Barber.Infrastructure.Data;
using Barber.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barber.Api.Controllers;

[ApiController]
[Route("api/admin/working-days")]
[Authorize(Roles = "Admin")]
public class WorkingDaysController(BarberDbContext db, SlotService slots) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkingDayDto>>> List(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        if (!TryParseRange(from, to, out var fromDate, out var toDate, out var error))
            return BadRequest(new { message = error });

        var items = await db.WorkingDays.AsNoTracking()
            .Where(w => w.Date >= fromDate && w.Date <= toDate)
            .OrderBy(w => w.Date)
            .ToListAsync(ct);

        return Ok(items.Select(Map));
    }

    [HttpGet("{date}")]
    public async Task<ActionResult<WorkingDayDto>> Get(string date, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var d))
            return BadRequest(new { message = "Некорректная дата" });

        var item = await db.WorkingDays.AsNoTracking().FirstOrDefaultAsync(w => w.Date == d, ct);
        if (item is null)
            return Ok(new WorkingDayDto(d.ToString("yyyy-MM-dd"), false, null, null));
        return Ok(Map(item));
    }

    [HttpPut("{date}")]
    public async Task<ActionResult<WorkingDayDto>> Set(string date, [FromBody] SetWorkingDayDto dto, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var d))
            return BadRequest(new { message = "Некорректная дата" });

        var existing = await db.WorkingDays.FirstOrDefaultAsync(w => w.Date == d, ct);

        if (!dto.IsWorking)
        {
            if (existing is not null)
            {
                db.WorkingDays.Remove(existing);
                await db.SaveChangesAsync(ct);
            }

            return Ok(new WorkingDayDto(d.ToString("yyyy-MM-dd"), false, null, null));
        }

        var (defaultStart, defaultEnd) = await slots.GetDefaultHoursAsync(d.DayOfWeek, ct);
        var start = ParseTime(dto.StartTime) ?? defaultStart;
        var end = ParseTime(dto.EndTime) ?? defaultEnd;
        if (end <= start)
            return BadRequest(new { message = "Конец рабочего дня должен быть позже начала" });

        if (existing is null)
        {
            existing = new WorkingDay
            {
                Id = Guid.NewGuid(),
                Date = d,
                StartTime = start,
                EndTime = end
            };
            db.WorkingDays.Add(existing);
        }
        else
        {
            existing.StartTime = start;
            existing.EndTime = end;
        }

        await db.SaveChangesAsync(ct);
        return Ok(Map(existing));
    }

    private static WorkingDayDto Map(WorkingDay w) => new(
        w.Date.ToString("yyyy-MM-dd"),
        true,
        FormatTime(w.StartTime),
        FormatTime(w.EndTime));

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";

    private static TimeSpan? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return TimeSpan.TryParse(value, out var t) ? t : null;
    }

    private static bool TryParseRange(
        string? from, string? to, out DateOnly fromDate, out DateOnly toDate, out string? error)
    {
        fromDate = default;
        toDate = default;
        error = null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        fromDate = DateOnly.TryParse(from, out var f) ? f : new DateOnly(today.Year, today.Month, 1);
        toDate = DateOnly.TryParse(to, out var t)
            ? t
            : fromDate.AddMonths(1).AddDays(-1);

        if (toDate < fromDate)
        {
            error = "Диапазон дат некорректен";
            return false;
        }

        if (toDate.DayNumber - fromDate.DayNumber > 366)
        {
            error = "Слишком большой диапазон";
            return false;
        }

        return true;
    }
}
