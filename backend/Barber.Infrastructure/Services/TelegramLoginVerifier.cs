using System.Security.Cryptography;
using System.Text;

namespace Barber.Infrastructure.Services;

/// <summary>
/// Verifies Telegram Login Widget payloads (HMAC-SHA256 over bot token).
/// </summary>
public class TelegramLoginVerifier(IConfigurationAccessor config)
{
    public sealed record Payload(
        long Id,
        string FirstName,
        string? LastName,
        string? Username,
        string? PhotoUrl,
        long AuthDate,
        string Hash);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(GetBotToken()) && !string.IsNullOrWhiteSpace(GetBotUsername());

    public string? GetBotUsername()
    {
        var u = config.Get("Telegram:BotUsername")
            ?? config.Get("TELEGRAM_BOT_USERNAME");
        return string.IsNullOrWhiteSpace(u) ? null : u.Trim().TrimStart('@');
    }

    private string? GetBotToken() =>
        config.Get("Telegram:BotToken")
        ?? config.Get("TELEGRAM_BOT_TOKEN");

    public bool TryValidate(Payload payload, out string error)
    {
        error = "";
        var token = GetBotToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            error = "Вход через Telegram не настроен";
            return false;
        }

        if (payload.Id <= 0 || string.IsNullOrWhiteSpace(payload.FirstName) || string.IsNullOrWhiteSpace(payload.Hash))
        {
            error = "Некорректные данные Telegram";
            return false;
        }

        var authUtc = DateTimeOffset.FromUnixTimeSeconds(payload.AuthDate).UtcDateTime;
        if (authUtc < DateTime.UtcNow.AddDays(-1) || authUtc > DateTime.UtcNow.AddMinutes(5))
        {
            error = "Сессия Telegram устарела — войдите ещё раз";
            return false;
        }

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = payload.AuthDate.ToString(),
            ["first_name"] = payload.FirstName,
            ["id"] = payload.Id.ToString()
        };
        if (!string.IsNullOrEmpty(payload.LastName)) fields["last_name"] = payload.LastName;
        if (!string.IsNullOrEmpty(payload.Username)) fields["username"] = payload.Username;
        if (!string.IsNullOrEmpty(payload.PhotoUrl)) fields["photo_url"] = payload.PhotoUrl;

        var dataCheckString = string.Join('\n', fields.Select(kv => $"{kv.Key}={kv.Value}"));
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var hashBytes = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var computed = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var provided = payload.Hash.Trim().ToLowerInvariant();
        if (computed.Length != provided.Length
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(provided)))
        {
            error = "Не удалось подтвердить вход через Telegram";
            return false;
        }

        return true;
    }

    public static string DisplayName(Payload p)
    {
        var name = $"{p.FirstName} {p.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? (p.Username ?? "Клиент Telegram") : name;
    }

    public static string PlaceholderPhone(long telegramUserId) => $"tg:{telegramUserId}";
}
