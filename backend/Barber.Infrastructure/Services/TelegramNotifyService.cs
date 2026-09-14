using System.Net.Http.Json;
using Barber.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Barber.Infrastructure.Services;

/// <summary>
/// Outbound Telegram notifications. Uses Bot API if Telegram:BotToken is configured.
/// </summary>
public class TelegramNotifyService(IHttpClientFactory httpClientFactory, IConfigurationAccessor config, ILogger<TelegramNotifyService> logger)
{
    public async Task NotifyChatAsync(long chatId, string text, CancellationToken ct = default)
    {
        var token = config.Get("Telegram:BotToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogInformation("Telegram skip (no token): {ChatId} {Text}", chatId, text);
            return;
        }

        var client = httpClientFactory.CreateClient();
        var url = $"https://api.telegram.org/bot{token}/sendMessage";
        var payload = new { chat_id = chatId, text, parse_mode = "HTML" };
        var response = await client.PostAsJsonAsync(url, payload, ct);
        if (!response.IsSuccessStatusCode)
            logger.LogWarning("Telegram send failed: {Status}", response.StatusCode);
    }

    public async Task NotifyAdminAsync(BarberDbContext db, string text, CancellationToken ct = default)
    {
        var adminChat = config.Get("Telegram:AdminChatId");
        if (long.TryParse(adminChat, out var chatId))
            await NotifyChatAsync(chatId, text, ct);
        else
            logger.LogInformation("Admin notify: {Text}", text);
    }

    public static string Render(string template, IDictionary<string, string> values)
    {
        var result = template;
        foreach (var (key, value) in values)
            result = result.Replace("{" + key + "}", value, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    public async Task<string> GetTemplateAsync(BarberDbContext db, string key, CancellationToken ct = default)
    {
        var t = await db.NotificationTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
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
