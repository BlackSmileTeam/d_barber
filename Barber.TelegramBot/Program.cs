using System.Collections.Concurrent;
using System.Globalization;
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
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});
builder.Services.AddHttpClient("api", (sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>()["Api:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("API_BASE_URL")
        ?? "http://139.100.225.234:55332/api";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<BotSessionStore>();
builder.Services.AddHostedService<BotWorker>();
await builder.Build().RunAsync();

internal enum BotMode
{
    Idle,
    NeedPhone,
    BookPickService,
    BookPickDate,
    BookPickSlot,
    ReschedulePickDate,
    ReschedulePickSlot
}

internal sealed class BotSession
{
    public BotMode Mode { get; set; } = BotMode.Idle;
    public Guid? ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public DateOnly? Date { get; set; }
    public Guid? AppointmentId { get; set; }
    public List<(string Label, DateTime Utc)> Slots { get; set; } = [];
    public List<(Guid Id, string Label)> Appointments { get; set; } = [];
}

public sealed class BotSessionStore
{
    private readonly ConcurrentDictionary<long, BotSession> _map = new();
    internal BotSession Get(long chatId) => _map.GetOrAdd(chatId, _ => new BotSession());
    public void Reset(long chatId) => _map[chatId] = new BotSession();
}

public sealed class BotWorker(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    BotSessionStore sessions,
    ILogger<BotWorker> logger) : BackgroundService
{
    private const string MsgGenericFail = "😔 Не удалось выполнить действие. Попробуйте позже или нажмите /start.";
    private const string MsgNeedPhone = "📱 Учётная запись не найдена. Для регистрации необходимо поделиться номером телефона.";

    private static readonly TimeZoneInfo Tz = ResolveTz();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static TimeZoneInfo ResolveTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"); }
            catch { return TimeZoneInfo.Local; }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = ResolveBotToken(config);
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning(
                "[{At}] Telegram bot token is not configured — Worker idle. "
                + "Set TELEGRAM_BOT_TOKEN or: dotnet user-secrets set \"TelegramBot:Token\" \"YOUR_TOKEN\" --project Barber.TelegramBot",
                Now());
            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            return;
        }

        var apiKey = ResolveApiKey(config);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning(
                "[{At}] BOT_API_KEY is not configured — cannot call site API. "
                + "Set BOT_API_KEY (same value as GitHub secret / Bot:ApiKey on API).",
                Now());
            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            return;
        }

        logger.LogInformation("[{At}] Token loaded (length {Len}). Waiting for Telegram updates…", Now(), token.Length);

        var apiBase = config["Api:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("API_BASE_URL")
            ?? "http://139.100.225.234:55332/api";
        var proxyUrl = ResolveProxyUrl(config);
        logger.LogInformation("[{At}] D_Barber Telegram bot starting. API: {Api}; Proxy: {Proxy}",
            Now(), apiBase, string.IsNullOrWhiteSpace(proxyUrl) ? "(none)" : "(configured)");

        var delay = TimeSpan.FromSeconds(5);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var httpClient = CreateTelegramHttpClient(proxyUrl);
                var bot = new TelegramBotClient(token, httpClient);
                var me = await bot.GetMeAsync(stoppingToken);
                logger.LogInformation("[{At}] Bot authorized as @{Username} (id {Id})", Now(), me.Username, me.Id);
                delay = TimeSpan.FromSeconds(5);

                using var roundCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var outboxTask = PollOutboxAsync(bot, apiKey, roundCts.Token);
                try
                {
                    await bot.ReceiveAsync(
                        updateHandler: (client, update, ct) => HandleUpdateAsync(client, update, apiKey, ct),
                        pollingErrorHandler: (_, ex, _) =>
                        {
                            logger.LogError(ex, "[{At}] Telegram polling error", Now());
                            return Task.CompletedTask;
                        },
                        receiverOptions: new ReceiverOptions
                        {
                            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
                            ThrowPendingUpdates = true
                        },
                        cancellationToken: roundCts.Token);
                }
                finally
                {
                    roundCts.Cancel();
                    try { await outboxTask; } catch (OperationCanceledException) { /* expected */ }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[{At}] Telegram API unreachable. Retry in {Seconds}s. Set TELEGRAM_PROXY_URL if needed.",
                    Now(), delay.TotalSeconds);
                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { break; }
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 120));
            }
        }
    }

    private async Task PollOutboxAsync(ITelegramBotClient bot, string apiKey, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var items = await ApiGetAsync<List<JsonElement>>(apiKey, "telegram/outbox?take=20", ct);
                if (items is { Count: > 0 })
                {
                    foreach (var item in items)
                    {
                        var id = item.GetProperty("id").GetGuid();
                        var chatId = item.GetProperty("chatId").GetInt64();
                        var text = item.GetProperty("text").GetString() ?? "";
                        try
                        {
                            await bot.SendTextMessageAsync(chatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
                            await ApiPostAsync(apiKey, $"telegram/outbox/{id}/ack", new { }, ct);
                            logger.LogInformation("[{At}] Outbox delivered {Id} → chat {ChatId}", Now(), id, chatId);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "[{At}] Outbox send failed {Id} chat {ChatId}", Now(), id, chatId);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[{At}] Outbox poll failed", Now());
            }

            try { await Task.Delay(TimeSpan.FromSeconds(2), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, string apiKey, CancellationToken ct)
    {
        LogIncomingUpdate(update);

        if (update.CallbackQuery is { } callback)
        {
            await HandleCallbackAsync(bot, callback, apiKey, ct);
            return;
        }

        if (update.Message is not { } message) return;

        var chatId = message.Chat.Id;
        var userId = message.From?.Id;
        var session = sessions.Get(chatId);
        var text = message.Text?.Trim() ?? string.Empty;

        try
        {
            if (message.Contact is { } contact)
            {
                await EnsureClientAsync(bot, chatId, contact.PhoneNumber, DisplayName(message.From), apiKey, ct);
                return;
            }

            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                sessions.Reset(chatId);
                await SendWelcomeAsync(bot, chatId, ct);
                return;
            }

            if (text.StartsWith("/chatid", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/id", StringComparison.OrdinalIgnoreCase))
            {
                await bot.SendTextMessageAsync(chatId,
                    $"🛠 Chat id: <code>{chatId}</code>",
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
                return;
            }

            if (text.Equals("🏠 Меню", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Меню", StringComparison.OrdinalIgnoreCase)
                || text.Equals("❌ Отмена", StringComparison.OrdinalIgnoreCase))
            {
                sessions.Reset(chatId);
                await bot.SendTextMessageAsync(chatId, "🏠 Главное меню", replyMarkup: MainMenu(), cancellationToken: ct);
                return;
            }

            if (IsMainAction(text, "✂️ Записаться", "Записаться"))
            {
                await StartBookingAsync(bot, chatId, apiKey, ct);
                return;
            }

            if (IsMainAction(text, "📅 Мои записи", "Мои записи"))
            {
                await ShowAppointmentsAsync(bot, chatId, apiKey, ct);
                return;
            }

            var phone = ExtractPhone(text);
            if (phone is not null && (session.Mode is BotMode.Idle or BotMode.NeedPhone))
            {
                await EnsureClientAsync(bot, chatId, phone, DisplayName(message.From), apiKey, ct);
                return;
            }

            switch (session.Mode)
            {
                case BotMode.BookPickService:
                    await HandleBookServiceAsync(bot, chatId, text, apiKey, ct);
                    return;
                case BotMode.BookPickDate:
                    await HandleBookDateAsync(bot, chatId, text, apiKey, ct);
                    return;
                case BotMode.BookPickSlot:
                    await HandleBookSlotAsync(bot, chatId, text, apiKey, ct);
                    return;
                case BotMode.ReschedulePickDate:
                    await HandleRescheduleDateAsync(bot, chatId, text, apiKey, ct);
                    return;
                case BotMode.ReschedulePickSlot:
                    await HandleRescheduleSlotAsync(bot, chatId, text, apiKey, ct);
                    return;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                await bot.SendTextMessageAsync(chatId,
                    "🤔 Не понял. Выберите действие в меню ниже.",
                    replyMarkup: MainMenu(),
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{At}] Failed update UserId={UserId} ChatId={ChatId}", Now(), userId, chatId);
            await SafeSend(bot, chatId, "⚠️ Временная ошибка. Нажмите /start", ct);
        }
    }

    private async Task HandleCallbackAsync(
        ITelegramBotClient bot, CallbackQuery callback, string apiKey, CancellationToken ct)
    {
        var chatId = callback.Message?.Chat.Id ?? callback.From.Id;
        var data = callback.Data ?? "";
        logger.LogInformation(
            "[{At}] Callback UserId={UserId} ChatId={ChatId} Data={Data}",
            Now(), callback.From.Id, chatId, data);

        try
        {
            await bot.AnswerCallbackQueryAsync(callback.Id, cancellationToken: ct);

            if (data.StartsWith("cancel:", StringComparison.Ordinal))
            {
                var idText = data["cancel:".Length..];
                if (!Guid.TryParse(idText, out var apptId))
                {
                    await bot.SendTextMessageAsync(chatId, "⚠️ Некорректная запись.", replyMarkup: MainMenu(), cancellationToken: ct);
                    return;
                }

                var (ok, err, _) = await ApiPostAsync(apiKey, $"telegram/appointments/{apptId}/cancel", new { chatId }, ct);
                if (!ok) logger.LogWarning("[{At}] Cancel failed: {Err}", Now(), err);
                await bot.SendTextMessageAsync(chatId,
                    ok ? "✅ Запись отменена." : MsgGenericFail,
                    replyMarkup: MainMenu(),
                    cancellationToken: ct);
                return;
            }

            if (data.StartsWith("reschedule:", StringComparison.Ordinal))
            {
                var parts = data.Split(':');
                if (parts.Length < 3
                    || !Guid.TryParse(parts[1], out var apptId)
                    || !Guid.TryParse(parts[2], out var serviceId))
                {
                    await bot.SendTextMessageAsync(chatId, "⚠️ Некорректная запись.", replyMarkup: MainMenu(), cancellationToken: ct);
                    return;
                }

                var session = sessions.Get(chatId);
                session.Mode = BotMode.ReschedulePickDate;
                session.AppointmentId = apptId;
                session.ServiceId = serviceId;
                await bot.SendTextMessageAsync(chatId, "📅 Выберите новый день:", replyMarkup: DateKeyboard(), cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{At}] Callback failed ChatId={ChatId}", Now(), chatId);
            await SafeSend(bot, chatId, "⚠️ Временная ошибка. Нажмите /start", ct);
        }
    }

    private async Task StartBookingAsync(ITelegramBotClient bot, long chatId, string apiKey, CancellationToken ct)
    {
        if (!await EnsureLinkedAsync(bot, chatId, apiKey, ct)) return;
        var services = await ApiGetAsync<List<JsonElement>>(apiKey, "telegram/services", ct);
        if (services is null || services.Count == 0)
        {
            await bot.SendTextMessageAsync(chatId, "😔 Пока нет доступных услуг.", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        var session = sessions.Get(chatId);
        session.Mode = BotMode.BookPickService;
        var rows = services.Select(s =>
        {
            var name = s.GetProperty("name").GetString() ?? "Услуга";
            var price = s.GetProperty("price").GetDecimal();
            return new[] { new KeyboardButton($"✂️ {name} — {price:0} ₽") };
        }).ToList();
        rows.Add([new KeyboardButton("🏠 Меню")]);

        // stash services in session via labels
        session.Appointments = services.Select(s =>
        {
            var id = s.GetProperty("id").GetGuid();
            var name = s.GetProperty("name").GetString() ?? "Услуга";
            var price = s.GetProperty("price").GetDecimal();
            return (id, $"✂️ {name} — {price:0} ₽");
        }).ToList();

        await bot.SendTextMessageAsync(chatId,
            "✂️ <b>Запись</b>\nВыберите услугу:",
            parseMode: ParseMode.Html,
            replyMarkup: new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    private async Task HandleBookServiceAsync(ITelegramBotClient bot, long chatId, string text, string apiKey, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        var match = session.Appointments.FirstOrDefault(a => a.Label == text);
        if (match.Id == Guid.Empty)
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите услугу кнопкой ниже.", cancellationToken: ct);
            return;
        }

        session.ServiceId = match.Id;
        session.ServiceName = match.Label;
        session.Mode = BotMode.BookPickDate;
        await bot.SendTextMessageAsync(chatId,
            "📅 Выберите день:",
            replyMarkup: DateKeyboard(),
            cancellationToken: ct);
    }

    private async Task HandleBookDateAsync(ITelegramBotClient bot, long chatId, string text, string apiKey, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        if (!TryParseDayButton(text, out var date))
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите день кнопкой.", cancellationToken: ct);
            return;
        }

        session.Date = date;
        await LoadSlotsAndAskAsync(bot, chatId, apiKey, session, forReschedule: false, ct);
    }

    private async Task HandleBookSlotAsync(ITelegramBotClient bot, long chatId, string text, string apiKey, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        var slot = session.Slots.FirstOrDefault(s => s.Label == text);
        if (slot.Utc == default)
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите время кнопкой.", cancellationToken: ct);
            return;
        }

        var body = new { chatId, serviceId = session.ServiceId, startAtUtc = slot.Utc };
        var (ok, err, data) = await ApiPostAsync(apiKey, "telegram/appointments", body, ct);
        sessions.Reset(chatId);
        if (!ok)
        {
            logger.LogWarning("[{At}] Create appointment failed: {Err}", Now(), err);
            await bot.SendTextMessageAsync(chatId, MsgGenericFail, replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(slot.Utc, Tz);
        await bot.SendTextMessageAsync(chatId,
            $"✅ <b>Вы записаны!</b>\n\n✂️ {session.ServiceName}\n📅 {local:dd.MM.yyyy}\n🕒 {local:HH:mm}\n\nДо встречи в D_Barber 💈",
            parseMode: ParseMode.Html,
            replyMarkup: MainMenu(),
            cancellationToken: ct);
        _ = data;
    }

    private async Task ShowAppointmentsAsync(ITelegramBotClient bot, long chatId, string apiKey, CancellationToken ct)
    {
        if (!await EnsureLinkedAsync(bot, chatId, apiKey, ct)) return;
        var items = await ApiGetAsync<List<JsonElement>>(apiKey, $"telegram/appointments?chatId={chatId}", ct);
        if (items is null || items.Count == 0)
        {
            await bot.SendTextMessageAsync(chatId, "📭 Ближайших записей нет.", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        await bot.SendTextMessageAsync(chatId, "📅 <b>Ваши записи</b>", parseMode: ParseMode.Html, replyMarkup: MainMenu(), cancellationToken: ct);

        foreach (var a in items)
        {
            var id = a.GetProperty("id").GetGuid();
            var serviceId = a.GetProperty("serviceId").GetGuid();
            var name = a.GetProperty("serviceName").GetString() ?? "Услуга";
            var start = a.GetProperty("startAtUtc").GetDateTime();
            if (start.Kind == DateTimeKind.Unspecified) start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(start.ToUniversalTime(), Tz);

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("❌ Отменить", $"cancel:{id}"),
                    InlineKeyboardButton.WithCallbackData("🔄 Перенести", $"reschedule:{id}:{serviceId}")
                }
            });

            await bot.SendTextMessageAsync(chatId,
                $"✂️ <b>{name}</b>\n📅 {local:dd.MM.yyyy} 🕒 {local:HH:mm}",
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
        }
    }

    private async Task HandleRescheduleDateAsync(ITelegramBotClient bot, long chatId, string text, string apiKey, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        if (!TryParseDayButton(text, out var date))
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите день кнопкой.", cancellationToken: ct);
            return;
        }

        session.Date = date;
        await LoadSlotsAndAskAsync(bot, chatId, apiKey, session, forReschedule: true, ct);
    }

    private async Task HandleRescheduleSlotAsync(ITelegramBotClient bot, long chatId, string text, string apiKey, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        var slot = session.Slots.FirstOrDefault(s => s.Label == text);
        if (slot.Utc == default)
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите время кнопкой.", cancellationToken: ct);
            return;
        }

        var (ok, err, _) = await ApiPostAsync(apiKey,
            $"telegram/appointments/{session.AppointmentId}/reschedule",
            new { chatId, serviceId = session.ServiceId, startAtUtc = slot.Utc },
            ct);
        sessions.Reset(chatId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(slot.Utc, Tz);
        if (!ok) logger.LogWarning("[{At}] Reschedule failed: {Err}", Now(), err);
        await bot.SendTextMessageAsync(chatId,
            ok
                ? $"✅ Запись перенесена на 📅 {local:dd.MM.yyyy} 🕒 {local:HH:mm}"
                : MsgGenericFail,
            replyMarkup: MainMenu(),
            cancellationToken: ct);
    }

    private async Task LoadSlotsAndAskAsync(
        ITelegramBotClient bot, long chatId, string apiKey, BotSession session, bool forReschedule, CancellationToken ct)
    {
        if (session.ServiceId is null || session.Date is null)
        {
            sessions.Reset(chatId);
            await bot.SendTextMessageAsync(chatId, "⚠️ Сессия сброшена. /start", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        var dateStr = session.Date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var resp = await ApiGetAsync<SlotsPayload>(apiKey, $"telegram/slots?serviceId={session.ServiceId}&date={dateStr}", ct);
        if (resp?.SlotsUtc is null || resp.SlotsUtc.Count == 0)
        {
            await bot.SendTextMessageAsync(chatId,
                "😔 На этот день свободного времени нет. Выберите другой день:",
                replyMarkup: DateKeyboard(),
                cancellationToken: ct);
            session.Mode = forReschedule ? BotMode.ReschedulePickDate : BotMode.BookPickDate;
            return;
        }

        session.Slots = resp.SlotsUtc.Select(utc =>
        {
            if (utc.Kind == DateTimeKind.Unspecified) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            utc = utc.ToUniversalTime();
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, Tz);
            return ($"🕒 {local:HH:mm}", utc);
        }).ToList();

        session.Mode = forReschedule ? BotMode.ReschedulePickSlot : BotMode.BookPickSlot;
        var rows = session.Slots.Select(s => new[] { new KeyboardButton(s.Label) }).ToList();
        rows.Add([new KeyboardButton("🏠 Меню")]);
        await bot.SendTextMessageAsync(chatId,
            $"🕒 Свободное время на {session.Date:dd.MM.yyyy}:",
            replyMarkup: new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    private async Task EnsureClientAsync(
        ITelegramBotClient bot, long chatId, string rawPhone, string name, string apiKey, CancellationToken ct)
    {
        var (ok, err, data) = await ApiPostAsync(apiKey, "telegram/ensure-client",
            new { phone = rawPhone, chatId, name }, ct);
        sessions.Reset(chatId);
        if (!ok)
        {
            logger.LogWarning("[{At}] Ensure-client failed: {Err}", Now(), err);
            sessions.Get(chatId).Mode = BotMode.NeedPhone;
            await bot.SendTextMessageAsync(chatId, MsgGenericFail, replyMarkup: SharePhoneKeyboard(), cancellationToken: ct);
            return;
        }

        var created = data?.TryGetProperty("created", out var c) == true && c.GetBoolean();
        var passwordIssued = data?.TryGetProperty("passwordIssued", out var p) == true && p.GetBoolean();
        var text = passwordIssued
            ? "✅ Номер привязан. Пароль для сайта отправлен в этот чат."
            : created
                ? "✅ Аккаунт создан по вашему номеру. Можно записываться прямо здесь."
                : "✅ Номер привязан. Можно записываться на услуги.";
        await bot.SendTextMessageAsync(chatId, text, replyMarkup: MainMenu(), cancellationToken: ct);
    }

    private async Task<bool> EnsureLinkedAsync(ITelegramBotClient bot, long chatId, string apiKey, CancellationToken ct)
    {
        var status = await ApiGetStatusAsync(apiKey, $"telegram/appointments?chatId={chatId}", ct);
        if (status is null)
        {
            logger.LogWarning("[{At}] API unreachable while checking link for chat {ChatId}", Now(), chatId);
            sessions.Get(chatId).Mode = BotMode.NeedPhone;
            await bot.SendTextMessageAsync(chatId, MsgGenericFail, replyMarkup: SharePhoneKeyboard(), cancellationToken: ct);
            return false;
        }

        if (status == HttpStatusCode.NotFound)
        {
            sessions.Get(chatId).Mode = BotMode.NeedPhone;
            await bot.SendTextMessageAsync(chatId, MsgNeedPhone, replyMarkup: SharePhoneKeyboard(), cancellationToken: ct);
            return false;
        }

        if (status != HttpStatusCode.OK)
        {
            logger.LogWarning("[{At}] Link check HTTP {Status} for chat {ChatId}", Now(), (int)status, chatId);
            await bot.SendTextMessageAsync(chatId, MsgGenericFail, replyMarkup: SharePhoneKeyboard(), cancellationToken: ct);
            return false;
        }

        return true;
    }

    private static async Task SendWelcomeAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        await bot.SendTextMessageAsync(chatId,
            "👋 <b>Добро пожаловать в D_Barber!</b>\n\n"
            + "Здесь можно записаться на стрижку, посмотреть свои визиты, отменить или перенести запись.\n\n"
            + "📱 Учётная запись не найдена. Для регистрации необходимо поделиться номером телефона.",
            parseMode: ParseMode.Html,
            replyMarkup: SharePhoneKeyboard(),
            cancellationToken: ct);
    }

    private static ReplyKeyboardMarkup MainMenu() => new(new[]
    {
        new[] { new KeyboardButton("✂️ Записаться"), new KeyboardButton("📅 Мои записи") }
    })
    { ResizeKeyboard = true };

    private static ReplyKeyboardMarkup SharePhoneKeyboard() => new(new[]
    {
        new[] { KeyboardButton.WithRequestContact("📱 Поделиться номером") }
    })
    { ResizeKeyboard = true };

    private static ReplyKeyboardMarkup DateKeyboard()
    {
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz).Date;
        var buttons = Enumerable.Range(0, 14)
            .Select(i => today.AddDays(i))
            .Select(d => new KeyboardButton($"📅 {d:dd.MM ddd}"))
            .Chunk(2)
            .Select(chunk => chunk.ToArray())
            .ToList();
        buttons.Add([new KeyboardButton("🏠 Меню")]);
        return new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };
    }

    private static bool TryParseDayButton(string text, out DateOnly date)
    {
        date = default;
        // "📅 28.09 пн" or "28.09"
        var m = Regex.Match(text, @"(\d{2})\.(\d{2})");
        if (!m.Success) return false;
        var day = int.Parse(m.Groups[1].Value);
        var month = int.Parse(m.Groups[2].Value);
        var year = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz).Year;
        try
        {
            date = new DateOnly(year, month, day);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz));
            if (date < today) date = new DateOnly(year + 1, month, day);
            return true;
        }
        catch { return false; }
    }

    private static bool IsMainAction(string text, params string[] labels) =>
        labels.Any(l => text.Equals(l, StringComparison.OrdinalIgnoreCase));

    private static string DisplayName(User? from)
    {
        if (from is null) return "Клиент Telegram";
        var name = $"{from.FirstName} {from.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? (from.Username ?? "Клиент Telegram") : name;
    }

    private HttpClient ApiClient(string apiKey)
    {
        var api = httpClientFactory.CreateClient("api");
        api.DefaultRequestHeaders.Remove("X-Bot-Api-Key");
        api.DefaultRequestHeaders.TryAddWithoutValidation("X-Bot-Api-Key", apiKey);
        return api;
    }

    private async Task<T?> ApiGetAsync<T>(string apiKey, string path, CancellationToken ct)
    {
        try
        {
            var api = ApiClient(apiKey);
            using var resp = await api.GetAsync(path, ct);
            if (!resp.IsSuccessStatusCode) return default;
            return await resp.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{At}] API GET {Path} failed", Now(), path);
            return default;
        }
    }

    private async Task<HttpStatusCode?> ApiGetStatusAsync(string apiKey, string path, CancellationToken ct)
    {
        try
        {
            var api = ApiClient(apiKey);
            using var resp = await api.GetAsync(path, ct);
            return resp.StatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{At}] API GET {Path} failed", Now(), path);
            return null;
        }
    }

    private async Task<(bool Ok, string Error, JsonElement? Data)> ApiPostAsync(
        string apiKey, string path, object body, CancellationToken ct)
    {
        try
        {
            var api = ApiClient(apiKey);
            using var resp = await api.PostAsJsonAsync(path, body, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            JsonElement? data = null;
            string? message = null;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                data = doc.RootElement.Clone();
                if (doc.RootElement.TryGetProperty("message", out var m))
                    message = m.GetString();
            }
            catch { /* ignore */ }

            if (resp.IsSuccessStatusCode)
                return (true, "", data);
            return (false, "api_error", data);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{At}] API POST {Path} failed", Now(), path);
            return (false, "unreachable", null);
        }
    }

    private void LogIncomingUpdate(Update update)
    {
        var at = Now();
        if (update.Message is { } msg)
        {
            var from = msg.From;
            var command = msg.Text is { Length: > 0 } t && t.StartsWith('/')
                ? t.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]
                : null;
            logger.LogInformation(
                "[{At}] Incoming UpdateId={UpdateId} MessageId={MessageId} "
                + "UserId={UserId} Username={Username} FirstName={FirstName} "
                + "ChatId={ChatId} Command={Command} Text={Text} HasContact={HasContact} ContactPhone={ContactPhone}",
                at, update.Id, msg.MessageId, from?.Id, from?.Username ?? "", from?.FirstName ?? "",
                msg.Chat.Id, command ?? "(none)", msg.Text ?? "(empty)",
                msg.Contact is not null, msg.Contact?.PhoneNumber ?? "");
            return;
        }

        logger.LogInformation("[{At}] Incoming UpdateId={UpdateId} Type={Type}", at, update.Id, update.Type);
    }

    private static async Task SafeSend(ITelegramBotClient bot, long chatId, string text, CancellationToken ct)
    {
        try { await bot.SendTextMessageAsync(chatId, text, replyMarkup: MainMenu(), cancellationToken: ct); }
        catch { /* ignore */ }
    }

    private static HttpClient CreateTelegramHttpClient(string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            return new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
        return new HttpClient(new HttpClientHandler
        {
            Proxy = new WebProxy(proxyUrl),
            UseProxy = true
        })
        { Timeout = TimeSpan.FromSeconds(100) };
    }

    private static string? ResolveBotToken(IConfiguration config) =>
        FirstNonEmpty(
            config["TelegramBot:Token"],
            config["Telegram:BotToken"],
            config["TELEGRAM_BOT_TOKEN"],
            Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN"),
            Environment.GetEnvironmentVariable("Telegram__BotToken"));

    private static string? ResolveApiKey(IConfiguration config) =>
        FirstNonEmpty(
            config["Bot:ApiKey"],
            config["BOT_API_KEY"],
            Environment.GetEnvironmentVariable("BOT_API_KEY"),
            Environment.GetEnvironmentVariable("Bot__ApiKey"));

    private static string? ResolveProxyUrl(IConfiguration config) =>
        FirstNonEmpty(
            config["TelegramBot:ProxyUrl"], config["Telegram:ProxyUrl"], config["TELEGRAM_PROXY_URL"],
            Environment.GetEnvironmentVariable("TELEGRAM_PROXY_URL"),
            Environment.GetEnvironmentVariable("TelegramBot__ProxyUrl"),
            Environment.GetEnvironmentVariable("Telegram__ProxyUrl"),
            Environment.GetEnvironmentVariable("HTTPS_PROXY"),
            Environment.GetEnvironmentVariable("HTTP_PROXY"));

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? ExtractPhone(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith('/')) return null;
        var digits = Regex.Replace(text, @"\D", "");
        if (digits.Length is < 10 or > 15) return null;
        return text.Trim();
    }

    private static string Now() => DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");

    private sealed record SlotsPayload(Guid ServiceId, string Date, List<DateTime> SlotsUtc);
}
