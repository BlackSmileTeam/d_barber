using System.Security.Claims;
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
public class AuthController(BarberDbContext db, JwtTokenService jwt) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(ClientRegisterDto dto, CancellationToken ct)
    {
        var phone = NormalizePhone(dto.Phone);
        if (await db.Clients.AnyAsync(c => c.Phone == phone, ct))
            return Conflict(new { message = "Клиент с таким телефоном уже зарегистрирован" });

        var client = new Client
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            Name = dto.Name.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);

        var token = jwt.CreateToken(client.Id, "Client", client.Name, client.Phone);
        return Ok(new AuthResponseDto(token, "Client", client.Name, client.Phone, client.Id));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(ClientLoginDto dto, CancellationToken ct)
    {
        var phone = NormalizePhone(dto.Phone);
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        if (client is null || !BCrypt.Net.BCrypt.Verify(dto.Password, client.PasswordHash))
            return Unauthorized(new { message = "Неверный телефон или пароль" });

        var token = jwt.CreateToken(client.Id, "Client", client.Name, client.Phone);
        return Ok(new AuthResponseDto(token, "Client", client.Name, client.Phone, client.Id));
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
            : Ok(new { role, name = client.Name, phone = client.Phone, userId = client.Id, telegramLinked = client.TelegramChatId != null });
    }

    public static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith('8') && digits.Length == 11)
            digits = "7" + digits[1..];
        if (digits.Length == 10)
            digits = "7" + digits;
        return "+" + digits;
    }
}
