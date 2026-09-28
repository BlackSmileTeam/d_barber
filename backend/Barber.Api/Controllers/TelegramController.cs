using Barber.Application.DTOs;
using Barber.Domain.Entities;
using Barber.Domain.Enums;
using Barber.Infrastructure.Data;
using Barber.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Barber.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramController(
    BarberDbContext db,
    SlotService slots,
    TelegramNotifyService telegram,
    IConfiguration config) : ControllerBase
{
    public record EnsureClientDto(string Phone, long ChatId, string? Name);
    public record LinkTelegramDto(string Phone, long ChatId, string? Name);
    public record BotCreateAppointmentDto(long ChatId, Guid ServiceId, DateTime StartAtUtc);
    public record BotChatDto(long ChatId);

    /// <summary>Create or link client by phone from Telegram contact. No website registration required.</summary>
    [HttpPost("ensure-client")]
    public async Task<IActionResult> EnsureClient([FromBody] EnsureClientDto dto, CancellationToken ct)
    {
        if (!IsBotAuthorized())
            return Unauthorized(new { message = "Неверный ключ бота" });

        if (string.IsNullOrWhiteSpace(dto.Phone) || dto.ChatId == 0)
            return BadRequest(new { message = "Нужны phone и chatId" });

        var phone = AuthController.NormalizePhone(dto.Phone);
        var displayName = string.IsNullOrWhiteSpace(dto.Name) ? "Клиент Telegram" : dto.Name.Trim();

        var previous = await db.Clients
            .Where(c => c.TelegramChatId == dto.ChatId)
            .ToListAsync(ct);
        foreach (var p in previous)
        {
            if (p.Phone != phone)
                p.TelegramChatId = null;
        }

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        var created = false;
        if (client is null)
        {
            client = new Client
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                Name = displayName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                HasUserPassword = false,
                CreatedViaTelegram = true,
                TelegramChatId = dto.ChatId
            };
            db.Clients.Add(client);
            created = true;
        }
        else
        {
            client.TelegramChatId = dto.ChatId;
            if (client.CreatedViaTelegram && (string.IsNullOrWhiteSpace(client.Name) || client.Name == "Клиент Telegram"))
                client.Name = displayName;
        }

        // Website started registration (telegram-pass) before the chat was linked — finish with thanks + password.
        string? issuedPassword = null;
        var websitePending = !created && !client.HasUserPassword && !client.CreatedViaTelegram;
        if (websitePending)
        {
            issuedPassword = AuthController.GenerateTempPassword();
            client.PasswordHash = BCrypt.Net.BCrypt.HashPassword(issuedPassword);
            client.HasUserPassword = true;
        }

        await db.SaveChangesAsync(ct);

        if (issuedPassword is not null)
        {
            await telegram.NotifyChatAsync(
                dto.ChatId,
                "🙏 Спасибо за регистрацию в D_Barber!\n\n"
                + $"Пароль для входа на сайт: <code>{issuedPassword}</code>",
                ct);
        }

        return Ok(new
        {
            linked = true,
            created,
            name = client.Name,
            phone = client.Phone,
            chatId = dto.ChatId,
            hasUserPassword = client.HasUserPassword,
            passwordIssued = issuedPassword is not null
        });
    }

    /// <summary>Backward-compatible alias → ensure-client.</summary>
    [HttpPost("link")]
    public Task<IActionResult> Link([FromBody] LinkTelegramDto dto, CancellationToken ct) =>
        EnsureClient(new EnsureClientDto(dto.Phone, dto.ChatId, dto.Name), ct);

    [HttpGet("services")]
    public async Task<IActionResult> Services(CancellationToken ct)
    {
        if (!IsBotAuthorized()) return Unauthorized();
        var items = await db.Services.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new { s.Id, s.Name, s.Price, s.DurationMinutes, s.Description })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("slots")]
    public async Task<IActionResult> Slots([FromQuery] Guid serviceId, [FromQuery] string date, CancellationToken ct)
    {
        if (!IsBotAuthorized()) return Unauthorized();
        if (!DateOnly.TryParse(date, out var d))
            return BadRequest(new { message = "Некорректная дата" });
        var list = await slots.GetAvailableSlotsAsync(serviceId, d, ct);
        return Ok(new SlotsResponseDto(serviceId, date, list));
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> Appointments([FromQuery] long chatId, CancellationToken ct)
    {
        if (!IsBotAuthorized()) return Unauthorized();
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.TelegramChatId == chatId, ct);
        if (client is null)
            return NotFound(new { message = "Сначала поделитесь номером в боте (/start)" });

        var items = await db.Appointments.AsNoTracking()
            .Include(a => a.Service)
            .Where(a => a.ClientId == client.Id && a.Status != AppointmentStatus.Cancelled && a.StartAtUtc >= DateTime.UtcNow.AddHours(-1))
            .OrderBy(a => a.StartAtUtc)
            .Select(a => new
            {
                a.Id,
                a.ServiceId,
                serviceName = a.Service.Name,
                a.StartAtUtc,
                a.EndAtUtc,
                status = a.Status.ToString(),
                price = a.Service.Price
            })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("appointments")]
    public async Task<IActionResult> CreateAppointment([FromBody] BotCreateAppointmentDto dto, CancellationToken ct)
    {
        if (!IsBotAuthorized()) return Unauthorized();
        var client = await db.Clients.FirstOrDefaultAsync(c => c.TelegramChatId == dto.ChatId, ct);
        if (client is null)
            return NotFound(new { message = "Сначала поделитесь номером в боте (/start)" });

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
            ClientId = client.Id,
            ServiceId = service.Id,
            StartAtUtc = start,
            EndAtUtc = start.AddMinutes(service.DurationMinutes),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(entity);
        await db.SaveChangesAsync(ct);
        await db.Entry(entity).Reference(a => a.Client).LoadAsync(ct);
        await db.Entry(entity).Reference(a => a.Service).LoadAsync(ct);

        var settings = await db.SalonSettings.AsNoTracking().FirstAsync(ct);
        var template = await telegram.GetTemplateAsync(db, "booking_created", ct);
        var values = AppointmentsController.BuildValues(entity, settings, config["App:FrontendPublicUrl"] ?? "");
        var text = TelegramNotifyService.Render(template, values);
        await telegram.NotifyAdminAsync(db, text, ct);
        if (entity.Client.TelegramChatId is long chat)
            await telegram.NotifyChatAsync(chat, text, ct);

        return Ok(new
        {
            entity.Id,
            serviceName = entity.Service.Name,
            entity.StartAtUtc,
            entity.EndAtUtc,
            status = entity.Status.ToString()
        });
    }

    [HttpPost("appointments/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] BotChatDto dto, CancellationToken ct)
    {
        if (!IsBotAuthorized()) return Unauthorized();
        var client = await db.Clients.FirstOrDefaultAsync(c => c.TelegramChatId == dto.ChatId, ct);
        if (client is null) return NotFound(new { message = "Клиент не найден" });

        var entity = await db.Appointments.Include(a => a.Service).Include(a => a.Client)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == client.Id, ct);
        if (entity is null) return NotFound(new { message = "Запись не найдена" });
        if (entity.Status == AppointmentStatus.Cancelled)
            return BadRequest(new { message = "Запись уже отменена" });

        entity.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await telegram.NotifyAdminAsync(db, $"❌ Отмена (Telegram): {entity.Client.Name}, {entity.Service.Name}, {entity.StartAtUtc:u}", ct);
        return Ok(new { cancelled = true, entity.Id });
    }

    [HttpPost("appointments/{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] BotCreateAppointmentDto dto, CancellationToken ct)
    {
        if (!IsBotAuthorized()) return Unauthorized();
        var client = await db.Clients.FirstOrDefaultAsync(c => c.TelegramChatId == dto.ChatId, ct);
        if (client is null) return NotFound(new { message = "Клиент не найден" });

        var entity = await db.Appointments.Include(a => a.Service).Include(a => a.Client)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == client.Id, ct);
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
        await telegram.NotifyAdminAsync(db, $"🔄 Перенос (Telegram): {entity.Client.Name}, {entity.Service.Name}, {entity.StartAtUtc:u}", ct);
        return Ok(new
        {
            entity.Id,
            serviceName = entity.Service.Name,
            entity.StartAtUtc,
            entity.EndAtUtc,
            status = entity.Status.ToString()
        });
    }

    /// <summary>Pending outbound messages for the bot to deliver via Telegram.</summary>
    [HttpGet("outbox")]
    public async Task<IActionResult> OutboxPending([FromQuery] int take = 20, CancellationToken ct = default)
    {
        if (!IsBotAuthorized()) return Unauthorized();
        take = Math.Clamp(take, 1, 100);
        var items = await db.TelegramOutbox.AsNoTracking()
            .Where(x => x.SentAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new { x.Id, x.ChatId, x.Text, x.CreatedAtUtc })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("outbox/{id:guid}/ack")]
    public async Task<IActionResult> OutboxAck(Guid id, CancellationToken ct)
    {
        if (!IsBotAuthorized()) return Unauthorized();
        var row = await db.TelegramOutbox.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return NotFound();
        if (row.SentAtUtc is null)
            row.SentAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { acked = true });
    }

    /// <summary>Shared secret between API and Telegram bot process (not the BotFather token).</summary>
    private bool IsBotAuthorized()
    {
        var expected = FirstNonEmpty(
            config["Bot:ApiKey"],
            config["BOT_API_KEY"],
            Environment.GetEnvironmentVariable("BOT_API_KEY"),
            Environment.GetEnvironmentVariable("Bot__ApiKey"));
        if (string.IsNullOrWhiteSpace(expected))
            return false;
        if (!Request.Headers.TryGetValue("X-Bot-Api-Key", out var provided))
            return false;
        return string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
