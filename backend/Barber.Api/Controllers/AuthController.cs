using System.Security.Claims;
using System.Security.Cryptography;
using Barber.Application.DTOs;
using Barber.Domain.Entities;
using Barber.Infrastructure.Data;
using Barber.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barber.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    BarberDbContext db,
    JwtTokenService jwt,
    TelegramNotifyService telegram,
    TelegramLoginVerifier telegramLogin) : ControllerBase
{
    [HttpGet("telegram-widget")]
    public IActionResult TelegramWidgetConfig()
    {
        var username = telegramLogin.GetBotUsername();
        return Ok(new
        {
            enabled = telegramLogin.IsConfigured,
            botUsername = username
        });
    }

    /// <summary>Official Telegram Login Widget callback — verifies hash and issues JWT.</summary>
    [HttpPost("telegram")]
    public async Task<ActionResult<AuthResponseDto>> TelegramLogin(TelegramWidgetLoginDto dto, CancellationToken ct)
    {
        var payload = new TelegramLoginVerifier.Payload(
            dto.Id,
            dto.FirstName ?? "",
            dto.LastName,
            dto.Username,
            dto.PhotoUrl,
            dto.AuthDate,
            dto.Hash ?? "");

        if (!telegramLogin.TryValidate(payload, out var error))
            return Unauthorized(new { message = error });

        var client = await db.Clients.FirstOrDefaultAsync(
            c => c.TelegramUserId == payload.Id || c.TelegramChatId == payload.Id, ct);

        if (client is null)
        {
            client = new Client
            {
                Id = Guid.NewGuid(),
                Phone = TelegramLoginVerifier.PlaceholderPhone(payload.Id),
                Name = TelegramLoginVerifier.DisplayName(payload),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                HasUserPassword = false,
                CreatedViaTelegram = true,
                TelegramChatId = payload.Id,
                TelegramUserId = payload.Id
            };
            db.Clients.Add(client);
        }

        ApplyTelegramProfile(client, payload);
        await db.SaveChangesAsync(ct);

        var token = jwt.CreateToken(client.Id, "Client", client.Name, client.Phone);
        return Ok(new AuthResponseDto(token, "Client", client.Name, client.Phone, client.Id));
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(ClientRegisterDto dto, CancellationToken ct)
    {
        var phone = NormalizePhone(dto.Phone);
        var existing = await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        if (existing is not null)
        {
            if (existing.CreatedViaTelegram && !existing.HasUserPassword)
            {
                return Conflict(new
                {
                    message = "Этот телефон уже есть из Telegram. Войдите через Telegram или получите пароль на странице входа."
                });
            }

            return Conflict(new { message = "Клиент с таким телефоном уже зарегистрирован" });
        }

        var client = new Client
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            Name = dto.Name.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            HasUserPassword = true,
            CreatedViaTelegram = false
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);

        var token = jwt.CreateToken(client.Id, "Client", client.Name, client.Phone);
        return Ok(new AuthResponseDto(token, "Client", client.Name, client.Phone, client.Id));
    }

    /// <summary>
    /// Issues a site password via Telegram. New accounts get a thank-you + password; existing get password only.
    /// If Telegram is not linked yet, creates/updates the client and returns needTelegram.
    /// </summary>
    [HttpPost("telegram-pass")]
    public async Task<IActionResult> TelegramPass(TelegramPassDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Phone))
            return BadRequest(new { message = "Укажите телефон" });

        var phone = NormalizePhone(dto.Phone);
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        var isNew = client is null;

        if (client is null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Сначала зарегистрируйтесь" });

            client = new Client
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                Name = dto.Name.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                HasUserPassword = false,
                CreatedViaTelegram = false
            };
            db.Clients.Add(client);
        }
        else if (!string.IsNullOrWhiteSpace(dto.Name)
                 && (string.IsNullOrWhiteSpace(client.Name) || client.Name == "Клиент Telegram"))
        {
            client.Name = dto.Name.Trim();
        }

        var notifyChatId = client.TelegramChatId ?? client.TelegramUserId;
        if (notifyChatId is null)
        {
            await db.SaveChangesAsync(ct);
            return Ok(new { sent = false, needTelegram = true });
        }

        client.TelegramChatId ??= notifyChatId;

        var password = GenerateTempPassword();
        client.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        client.HasUserPassword = true;
        await db.SaveChangesAsync(ct);

        var text = isNew
            ? "🙏 Спасибо за регистрацию в D_Barber!\n\n"
              + $"Пароль для входа на сайт: <code>{password}</code>"
            : $"🔑 Пароль для входа: <code>{password}</code>";

        await telegram.NotifyChatAsync(notifyChatId.Value, text, ct);
        return Ok(new { sent = true, needTelegram = false });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(ClientLoginDto dto, CancellationToken ct)
    {
        var phone = NormalizePhone(dto.Phone);
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        if (client is null)
            return Unauthorized(new { message = "Неверный телефон или пароль" });

        if (client.HasUserPassword)
        {
            if (!BCrypt.Net.BCrypt.Verify(dto.Password, client.PasswordHash))
                return Unauthorized(new { message = "Неверный телефон или пароль" });

            var tokenOk = jwt.CreateToken(client.Id, "Client", client.Name, client.Phone);
            return Ok(new AuthResponseDto(tokenOk, "Client", client.Name, client.Phone, client.Id));
        }

        var notifyChatId = client.TelegramChatId ?? client.TelegramUserId;
        if (notifyChatId is null)
        {
            return Unauthorized(new
            {
                message = "Войдите через Telegram или привяжите номер в боте"
            });
        }

        client.TelegramChatId ??= notifyChatId;

        var tempPassword = GenerateTempPassword();
        client.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
        client.HasUserPassword = true;
        await db.SaveChangesAsync(ct);

        await telegram.NotifyChatAsync(
            notifyChatId.Value,
            "🔑 Вход на сайт D_Barber\n\n"
            + $"Ваш пароль: <code>{tempPassword}</code>",
            ct);

        return Unauthorized(new
        {
            message = "Пароль отправлен в Telegram",
            codeSentToTelegram = true
        });
    }

    [HttpPost("admin/login")]
    public async Task<ActionResult<AuthResponseDto>> AdminLogin(AdminLoginDto dto, CancellationToken ct)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Login == dto.Login, ct);
        if (admin is null || !BCrypt.Net.BCrypt.Verify(dto.Password, admin.PasswordHash))
            return Unauthorized(new { message = "Неверный логин или пароль" });

        var token = jwt.CreateToken(admin.Id, "Admin", admin.DisplayName);
        return Ok(new AuthResponseDto(token, "Admin", admin.DisplayName, null, admin.Id));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<object>> Me(CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (!Guid.TryParse(id, out var userId))
            return Unauthorized();

        if (role == "Admin")
        {
            var admin = await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == userId, ct);
            return admin is null ? NotFound() : Ok(new { role, name = admin.DisplayName, userId });
        }

        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == userId, ct);
        return client is null
            ? NotFound()
            : Ok(new
            {
                role,
                name = client.Name,
                phone = client.Phone,
                userId = client.Id,
                telegramLinked = client.TelegramChatId != null || client.TelegramUserId != null,
                telegramUsername = client.TelegramUsername,
                telegramPhotoUrl = client.TelegramPhotoUrl
            });
    }

    public static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;
        if (phone.StartsWith("tg:", StringComparison.OrdinalIgnoreCase))
            return phone.Trim();

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith('8') && digits.Length == 11)
            digits = "7" + digits[1..];
        if (digits.Length == 10)
            digits = "7" + digits;
        return "+" + digits;
    }

    public static string GenerateTempPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(8);
        var chars = new char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    private static void ApplyTelegramProfile(Client client, TelegramLoginVerifier.Payload payload)
    {
        client.TelegramUserId = payload.Id;
        client.TelegramChatId ??= payload.Id;
        client.TelegramUsername = payload.Username;
        client.TelegramFirstName = payload.FirstName;
        client.TelegramLastName = payload.LastName;
        client.TelegramPhotoUrl = payload.PhotoUrl;
        client.TelegramAuthAtUtc = DateTimeOffset.FromUnixTimeSeconds(payload.AuthDate).UtcDateTime;

        var display = TelegramLoginVerifier.DisplayName(payload);
        if (string.IsNullOrWhiteSpace(client.Name) || client.Name == "Клиент Telegram")
            client.Name = display;
    }
}
