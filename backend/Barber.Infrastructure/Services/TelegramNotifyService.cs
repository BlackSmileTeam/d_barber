using Barber.Domain.Entities;
using Barber.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Barber.Infrastructure.Services;

/// <summary>
/// Queues Telegram messages for the bot process to deliver. API never talks to Telegram Bot API.
/// </summary>
public class TelegramNotifyService(BarberDbContext db, IConfigurationAccessor config, ILogger<TelegramNotifyService> logger)
{
    public async Task NotifyChatAsync(long chatId, string text, CancellationToken ct = default)
    {
        if (chatId == 0 || string.IsNullOrWhiteSpace(text))
            return;

        db.TelegramOutbox.Add(new TelegramOutboxMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            Text = text,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Telegram outbox queued for chat {ChatId}", chatId);
    }

    public async Task NotifyAdminAsync(BarberDbContext _, string text, CancellationToken ct = default)
    {
        var adminChat = config.Get("Telegram:AdminChatId");
        if (long.TryParse(adminChat, out var chatId))
            await NotifyChatAsync(chatId, text, ct);
        else
            logger.LogInformation("Admin notify (no Telegram:AdminChatId): {Text}", text);
    }

    public static string Render(string template, IDictionary<string, string> values)
    {
        var result = template;
        foreach (var (key, value) in values)
            result = result.Replace("{" + key + "}", value, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    public async Task<string> GetTemplateAsync(BarberDbContext database, string key, CancellationToken ct = default)
    {
        var t = await database.NotificationTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        return t?.Body ?? string.Empty;
    }
}

public interface IConfigurationAccessor
{
    string? Get(string key);
}

public class ConfigurationAccessor(Microsoft.Extensions.Configuration.IConfiguration configuration) : IConfigurationAccessor
{
    public string? Get(string key) => configuration[key];
}
