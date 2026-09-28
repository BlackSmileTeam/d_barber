using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddUserSecrets(typeof(BotWorker).Assembly, optional: true);
builder.Services.Configure<HostOptions>(options =>
{
    // Do not tear down the process on a single Telegram API timeout (common on RU hosts).
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});
builder.Services.AddHttpClient("api", (sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>()["Api:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("API_BASE_URL")
        ?? "http://localhost:5271/api";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHostedService<BotWorker>();
await builder.Build().RunAsync();

public sealed class BotWorker(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<BotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = ResolveBotToken(config);
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning(
                "[{At}] Telegram bot token is not configured — Worker idle, /start will NOT be handled. "
                + "Set env TELEGRAM_BOT_TOKEN, or TelegramBot:Token in appsettings.json, or: "
                + "dotnet user-secrets set \"TelegramBot:Token\" \"YOUR_TOKEN\" --project Barber.TelegramBot",
                DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            return;
        }

        logger.LogInformation(
            "[{At}] Token loaded (length {Len}). Waiting for Telegram updates…",
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            token.Length);

        var apiBase = config["Api:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("API_BASE_URL")
            ?? "http://localhost:5271/api";
        var proxyUrl = ResolveProxyUrl(config);
        logger.LogInformation(
            "D_Barber Telegram bot starting. API: {Api}; Proxy: {Proxy}",
            apiBase,
            string.IsNullOrWhiteSpace(proxyUrl) ? "(none)" : "(configured)");

        var delay = TimeSpan.FromSeconds(5);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var httpClient = CreateTelegramHttpClient(proxyUrl);
                var bot = new TelegramBotClient(token, httpClient);
                var me = await bot.GetMeAsync(stoppingToken);
                logger.LogInformation("Bot authorized as @{Username} (id {Id})", me.Username, me.Id);
                delay = TimeSpan.FromSeconds(5);

                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = [UpdateType.Message],
                    ThrowPendingUpdates = true
                };

                await bot.ReceiveAsync(
                    updateHandler: (client, update, ct) => HandleUpdateAsync(client, update, token, ct),
                    pollingErrorHandler: (_, ex, _) =>
                    {
                        logger.LogError(ex, "Telegram polling error");
                        return Task.CompletedTask;
                    },
                    receiverOptions: receiverOptions,
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Telegram API unreachable (timeout/blocked?). Retry in {Seconds}s. "
                    + "Check: curl -m 15 https://api.telegram.org from the server; "
                    + "if it hangs, set GitHub secret TELEGRAM_PROXY_URL (HTTP proxy) and redeploy.",
                    delay.TotalSeconds);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 120));
            }
        }
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        string botToken,
        CancellationToken ct)
    {
        LogIncomingUpdate(update);

        if (update.Message is not { } message)
        {
            logger.LogInformation(
                "[{At}] Update #{UpdateId} ignored (type={Type}, no message).",
                DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                update.Id,
                update.Type);
            return;
        }

        var chatId = message.Chat.Id;
        var userId = message.From?.Id;
        try
        {
            if (message.Contact is { } contact)
            {
                logger.LogInformation(
                    "[{At}] Action=link_contact UserId={UserId} ChatId={ChatId} Phone={Phone}",
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                    userId,
                    chatId,
                    contact.PhoneNumber);
                await LinkByPhoneAsync(bot, chatId, contact.PhoneNumber, botToken, ct);
                return;
            }

            var text = message.Text?.Trim() ?? string.Empty;
            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "[{At}] Action=command_start UserId={UserId} ChatId={ChatId} Payload={Text}",
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                    userId,
                    chatId,
                    text);
                await SendWelcomeAsync(bot, chatId, ct);
                return;
            }

            if (text.StartsWith("/chatid", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/id", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "[{At}] Action=command_chatid UserId={UserId} ChatId={ChatId}",
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                    userId,
                    chatId);
                await bot.SendTextMessageAsync(
                    chatId,
                    $"Ваш chat id: <code>{chatId}</code>\n\nСкопируйте это значение в GitHub Secret <code>TELEGRAM_ADMIN_CHAT_ID</code> (для админ-уведомлений).",
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
                return;
            }

            if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "[{At}] Action=command_help UserId={UserId} ChatId={ChatId}",
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                    userId,
                    chatId);
                await bot.SendTextMessageAsync(
                    chatId,
                    "Команды:\n/start — привязать телефон\n/chatid — показать chat id\n/help — справка\n\nИли отправьте номер телефона текстом (+7…).",
                    cancellationToken: ct);
                return;
            }

            var phone = ExtractPhone(text);
            if (phone is not null)
            {
                logger.LogInformation(
                    "[{At}] Action=link_phone_text UserId={UserId} ChatId={ChatId} Phone={Phone}",
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                    userId,
                    chatId,
                    phone);
                await LinkByPhoneAsync(bot, chatId, phone, botToken, ct);
                return;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                logger.LogInformation(
                    "[{At}] Action=unknown_text UserId={UserId} ChatId={ChatId} Text={Text}",
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                    userId,
                    chatId,
                    text);
                await bot.SendTextMessageAsync(
                    chatId,
                    "Не понял сообщение. Нажмите /start и поделитесь контактом, или отправьте номер телефона.",
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "[{At}] Failed to handle update from UserId={UserId} ChatId={ChatId}",
                DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                userId,
                chatId);
            try
            {
                await bot.SendTextMessageAsync(
                    chatId,
                    "Временная ошибка. Попробуйте позже или напишите /start.",
                    cancellationToken: ct);
            }
            catch
            {
                // ignore secondary send failure
            }
        }
    }

    private void LogIncomingUpdate(Update update)
    {
        var at = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
        if (update.Message is { } msg)
        {
            var from = msg.From;
            var command = msg.Text is { Length: > 0 } t && t.StartsWith('/')
                ? t.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]
                : null;
            logger.LogInformation(
                "[{At}] Incoming UpdateId={UpdateId} MessageId={MessageId} "
                + "UserId={UserId} Username={Username} FirstName={FirstName} LastName={LastName} Language={Lang} "
                + "ChatId={ChatId} ChatType={ChatType} "
                + "Command={Command} Text={Text} HasContact={HasContact} ContactPhone={ContactPhone} "
                + "DateUtc={MsgDateUtc}",
                at,
                update.Id,
                msg.MessageId,
                from?.Id,
                from?.Username ?? "",
                from?.FirstName ?? "",
                from?.LastName ?? "",
                from?.LanguageCode ?? "",
                msg.Chat.Id,
                msg.Chat.Type,
                command ?? "(none)",
                msg.Text ?? "(empty)",
                msg.Contact is not null,
                msg.Contact?.PhoneNumber ?? "",
                msg.Date.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss'Z'"));
            return;
        }

        logger.LogInformation(
            "[{At}] Incoming UpdateId={UpdateId} Type={Type} (no Message payload)",
            at,
            update.Id,
            update.Type);
    }

    private static async Task SendWelcomeAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            KeyboardButton.WithRequestContact("Поделиться номером")
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

        await bot.SendTextMessageAsync(
            chatId,
            "Добро пожаловать в D_Barber!\n\n"
            + "Чтобы получать подтверждения записи и напоминания, привяжите аккаунт с сайта:\n"
            + "1) Зарегистрируйтесь на сайте с тем же телефоном\n"
            + "2) Нажмите кнопку ниже «Поделиться номером» (или отправьте номер текстом)\n\n"
            + "Админу: /chatid — узнать chat id для уведомлений.",
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    private async Task LinkByPhoneAsync(
        ITelegramBotClient bot,
        long chatId,
        string rawPhone,
        string botToken,
        CancellationToken ct)
    {
        var api = httpClientFactory.CreateClient("api");
        using var request = new HttpRequestMessage(HttpMethod.Post, "telegram/link");
        request.Headers.TryAddWithoutValidation("X-Telegram-Bot-Token", botToken);
        request.Content = JsonContent.Create(new { phone = rawPhone, chatId });

        HttpResponseMessage response;
        try
        {
            response = await api.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "API link call failed");
            await bot.SendTextMessageAsync(
                chatId,
                "Не удалось связаться с сервером записи. Попробуйте позже.",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.IsSuccessStatusCode)
        {
            string? name = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("name", out var n))
                    name = n.GetString();
            }
            catch
            {
                // ignore parse
            }

            await bot.SendTextMessageAsync(
                chatId,
                string.IsNullOrWhiteSpace(name)
                    ? "✅ Telegram привязан. Теперь вы будете получать уведомления о записях."
                    : $"✅ Telegram привязан к аккаунту {name}. Теперь вы будете получать уведомления о записях.",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct);
            return;
        }

        var message = TryReadMessage(body)
            ?? (response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "Клиент с таким телефоном не найден. Сначала зарегистрируйтесь на сайте, затем снова поделитесь номером."
                : "Не удалось привязать Telegram. Проверьте номер и попробуйте снова.");

        await bot.SendTextMessageAsync(
            chatId,
            message,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);
    }

    private static HttpClient CreateTelegramHttpClient(string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            return new HttpClient { Timeout = TimeSpan.FromSeconds(100) };

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxyUrl),
            UseProxy = true
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(100) };
    }

    private static string? TryReadMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m))
                return m.GetString();
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? ResolveBotToken(IConfiguration config) =>
        FirstNonEmpty(
            config["TelegramBot:Token"],
            config["Telegram:BotToken"],
            config["TELEGRAM_BOT_TOKEN"],
            Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN"),
            Environment.GetEnvironmentVariable("Telegram__BotToken"));

    private static string? ResolveProxyUrl(IConfiguration config) =>
        FirstNonEmpty(
            config["TelegramBot:ProxyUrl"],
            config["Telegram:ProxyUrl"],
            config["TELEGRAM_PROXY_URL"],
            Environment.GetEnvironmentVariable("TELEGRAM_PROXY_URL"),
            Environment.GetEnvironmentVariable("TelegramBot__ProxyUrl"),
            Environment.GetEnvironmentVariable("Telegram__ProxyUrl"),
            Environment.GetEnvironmentVariable("HTTPS_PROXY"),
            Environment.GetEnvironmentVariable("HTTP_PROXY"));

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? ExtractPhone(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith('/'))
            return null;
        var digits = Regex.Replace(text, @"\D", "");
        if (digits.Length is < 10 or > 15)
            return null;
        return text.Trim();
    }
}
