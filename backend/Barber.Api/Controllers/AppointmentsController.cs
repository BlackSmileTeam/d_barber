using System.Security.Claims;
using Barber.Application.DTOs;
using Barber.Domain.Entities;
using Barber.Domain.Enums;
using Barber.Infrastructure.Data;
using Barber.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barber.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController(
    BarberDbContext db,
    SlotService slots,
    TelegramNotifyService telegram,
    IConfiguration config) : ControllerBase
{
    [HttpGet("slots")]
    public async Task<ActionResult<SlotsResponseDto>> GetSlots([FromQuery] Guid serviceId, [FromQuery] string date, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var d))
            return BadRequest(new { message = "Некорректная дата" });
        var list = await slots.GetAvailableSlotsAsync(serviceId, d, ct);
        return Ok(new SlotsResponseDto(serviceId, date, list));
    }

    [Authorize(Roles = "Client")]
    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create(CreateAppointmentDto dto, CancellationToken ct)
    {
        var clientId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == dto.ServiceId && s.IsActive, ct);
        if (service is null)
            return BadRequest(new { message = "Услуга не найдена" });

        var start = DateTime.SpecifyKind(dto.StartAtUtc, DateTimeKind.Utc);
        var day = DateOnly.FromDateTime(start);
        var available = await slots.GetAvailableSlotsAsync(service.Id, day, ct);
        if (!available.Any(s => Math.Abs((s - start).TotalSeconds) < 1))
            return Conflict(new { message = "Выбранное время уже занято. Выберите другой слот." });

        var entity = new Appointment
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ServiceId = service.Id,
            StartAtUtc = start,
            EndAtUtc = start.AddMinutes(service.DurationMinutes),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(entity);
        await db.SaveChangesAsync(ct);

        await db.Entry(entity).Reference(a => a.Client).LoadAsync(ct);
        await db.Entry(entity).Reference(a => a.Service).LoadAsync(ct);
        await NotifyBookingAsync(entity, ct);

        return Ok(Map(entity));
    }

    [Authorize(Roles = "Client")]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<AppointmentDto>>> Mine(CancellationToken ct)
    {
        var clientId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var items = await db.Appointments.AsNoTracking()
            .Include(a => a.Service)
            .Include(a => a.Client)
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.StartAtUtc)
            .ToListAsync(ct);
        return Ok(items.Select(Map));
    }

    [Authorize(Roles = "Client")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<AppointmentDto>> Cancel(Guid id, CancellationToken ct)
    {
        var clientId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var entity = await db.Appointments.Include(a => a.Service).Include(a => a.Client)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == clientId, ct);
        if (entity is null) return NotFound(new { message = "Запись не найдена" });
        if (entity.Status == AppointmentStatus.Cancelled)
            return BadRequest(new { message = "Запись уже отменена" });

        entity.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await telegram.NotifyAdminAsync(db, $"Отмена записи: {entity.Client.Name}, {entity.Service.Name}, {entity.StartAtUtc:u}", ct);
        return Ok(Map(entity));
    }

    [Authorize(Roles = "Client")]
    [HttpPost("{id:guid}/reschedule")]
    public async Task<ActionResult<AppointmentDto>> Reschedule(Guid id, RescheduleAppointmentDto dto, CancellationToken ct)
    {
        var clientId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var entity = await db.Appointments.Include(a => a.Service).Include(a => a.Client)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == clientId, ct);
        if (entity is null) return NotFound(new { message = "Запись не найдена" });
        if (entity.Status == AppointmentStatus.Cancelled)
            return BadRequest(new { message = "Нельзя перенести отменённую запись" });

        var start = DateTime.SpecifyKind(dto.StartAtUtc, DateTimeKind.Utc);
        var end = start.AddMinutes(entity.Service.DurationMinutes);
        var previousStart = entity.StartAtUtc;
        var previousEnd = entity.EndAtUtc;
        entity.StartAtUtc = DateTime.UtcNow.AddYears(100);
        entity.EndAtUtc = entity.StartAtUtc.AddMinutes(1);
        await db.SaveChangesAsync(ct);

        var daySlots = await slots.GetAvailableSlotsAsync(entity.ServiceId, DateOnly.FromDateTime(start), ct);
        if (!daySlots.Any(s => Math.Abs((s - start).TotalSeconds) < 1))
        {
            entity.StartAtUtc = previousStart;
            entity.EndAtUtc = previousEnd;
            await db.SaveChangesAsync(ct);
            return Conflict(new { message = "Выбранное время недоступно" });
        }

        entity.StartAtUtc = start;
        entity.EndAtUtc = end;
        entity.Status = AppointmentStatus.Rescheduled;
        await db.SaveChangesAsync(ct);
        await telegram.NotifyAdminAsync(db, $"Перенос записи: {entity.Client.Name}, {entity.Service.Name}, {entity.StartAtUtc:u}", ct);
        return Ok(Map(entity));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppointmentDto>>> AdminList([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var q = db.Appointments.AsNoTracking().Include(a => a.Service).Include(a => a.Client).AsQueryable();
        if (from.HasValue) q = q.Where(a => a.StartAtUtc >= from.Value.ToUniversalTime());
        if (to.HasValue) q = q.Where(a => a.StartAtUtc <= to.Value.ToUniversalTime());
        var items = await q.OrderBy(a => a.StartAtUtc).ToListAsync(ct);
        return Ok(items.Select(Map));
    }

    [HttpGet("{id:guid}/calendar.ics")]
    [Authorize(Roles = "Client,Admin")]
    public async Task<IActionResult> CalendarIcs(Guid id, CancellationToken ct)
    {
        var entity = await db.Appointments.AsNoTracking()
            .Include(a => a.Service).Include(a => a.Client)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return NotFound();

        var settings = await db.SalonSettings.AsNoTracking().FirstAsync(ct);
        var ics = BuildIcs(entity, settings);
        return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar", "dbarber-appointment.ics");
    }

    private async Task NotifyBookingAsync(Appointment entity, CancellationToken ct)
    {
        var settings = await db.SalonSettings.AsNoTracking().FirstAsync(ct);
        var template = await telegram.GetTemplateAsync(db, "booking_created", ct);
        var values = BuildValues(entity, settings, config["App:FrontendPublicUrl"] ?? "");
        var text = TelegramNotifyService.Render(template, values);
        await telegram.NotifyAdminAsync(db, text, ct);
        if (entity.Client.TelegramChatId is long chatId)
            await telegram.NotifyChatAsync(chatId, text, ct);
    }

    public static Dictionary<string, string> BuildValues(Appointment entity, SalonSettings settings, string frontendUrl) => new()
    {
        ["Имя"] = entity.Client.Name,
        ["Клиент"] = entity.Client.Name,
        ["Услуга"] = entity.Service.Name,
        ["Дата"] = entity.StartAtUtc.ToLocalTime().ToString("dd.MM.yyyy"),
        ["Время"] = entity.StartAtUtc.ToLocalTime().ToString("HH:mm"),
        ["НазваниеСалона"] = settings.SalonName,
        ["Адрес"] = settings.Address,
        ["СсылкаНаЗапись"] = $"{frontendUrl.TrimEnd('/')}{settings.BookingUrl}"
    };

    private static AppointmentDto Map(Appointment a) => new(
        a.Id, a.ServiceId, a.Service.Name, a.Service.Price, a.Service.DurationMinutes,
        a.StartAtUtc, a.EndAtUtc, a.Status.ToString(), a.Client.Name, a.Client.Phone);

    private static string BuildIcs(Appointment a, SalonSettings s)
    {
        string Fmt(DateTime dt) => dt.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");
        return $"""
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//D_Barber//RU
            BEGIN:VEVENT
            UID:{a.Id}@dbarber
            DTSTAMP:{Fmt(DateTime.UtcNow)}
            DTSTART:{Fmt(a.StartAtUtc)}
            DTEND:{Fmt(a.EndAtUtc)}
            SUMMARY:{a.Service.Name} — {s.BrandName}
            LOCATION:{s.Address}
            DESCRIPTION:{s.SalonName}. {a.Service.Name}
            END:VEVENT
            END:VCALENDAR
            """.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }
}
