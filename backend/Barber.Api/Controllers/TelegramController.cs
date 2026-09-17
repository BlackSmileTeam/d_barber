using Barber.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barber.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramController(BarberDbContext db, IConfiguration config) : ControllerBase
{
    public record LinkTelegramDto(string Phone, long ChatId);

    /// <summary>
    /// Called by Barber.TelegramBot after the user shares a phone contact.
    /// Auth: header X-Telegram-Bot-Token must match Telegram:BotToken.
    /// </summary>
    [HttpPost("link")]
    public async Task<IActionResult> Link([FromBody] LinkTelegramDto dto, CancellationToken ct)
    {
        if (!IsBotAuthorized())
            return Unauthorized(new { message = "Неверный токен бота" });

        if (string.IsNullOrWhiteSpace(dto.Phone) || dto.ChatId == 0)
            return BadRequest(new { message = "Нужны phone и chatId" });

        var phone = AuthController.NormalizePhone(dto.Phone);
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        if (client is null)
            return NotFound(new { message = "Клиент с таким телефоном не найден. Сначала зарегистрируйтесь на сайте." });

        // One chat → one client; clear previous owner of this chat id.
        var previous = await db.Clients
            .Where(c => c.TelegramChatId == dto.ChatId && c.Id != client.Id)
            .ToListAsync(ct);
        foreach (var p in previous)
            p.TelegramChatId = null;

        client.TelegramChatId = dto.ChatId;
        await db.SaveChangesAsync(ct);

        return Ok(new { linked = true, name = client.Name, phone = client.Phone, chatId = dto.ChatId });
    }

    private bool IsBotAuthorized()
    {
        var expected = config["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(expected))
            return false;
        if (!Request.Headers.TryGetValue("X-Telegram-Bot-Token", out var provided))
            return false;
        return string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }
}
