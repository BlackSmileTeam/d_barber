using Barber.Application.DTOs;
using Barber.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barber.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/clients")]
public class AdminClientsController(BarberDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminClientDto>>> List(CancellationToken ct)
    {
        var items = await db.Clients.AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new AdminClientDto(
                c.Id,
                c.Name,
                c.Phone,
                c.CreatedAtUtc,
                c.LastVisitAtUtc,
                c.TelegramChatId != null))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResultDto>> ResetPassword(
        Guid id,
        ResetClientPasswordDto? dto,
        CancellationToken ct)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return NotFound(new { message = "Клиент не найден" });

        var password = (dto?.NewPassword ?? "").Trim();
        if (string.IsNullOrWhiteSpace(password))
            password = GeneratePassword();

        if (password.Length < 6)
            return BadRequest(new { message = "Пароль должен быть не короче 6 символов" });

        client.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        await db.SaveChangesAsync(ct);

        return Ok(new ResetPasswordResultDto(password));
    }

    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[10];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        return new string(chars);
    }
}
