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
        ?? "http://localhost:5271/api";
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
    CancelPick,
    ReschedulePick,
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

        logger.LogInformation("[{At}] Token loaded (length {Len}). Waiting for Telegram updates…", Now(), token.Length);

        var apiBase = config["Api:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("API_BASE_URL")
            ?? "http://localhost:5271/api";
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

                await bot.ReceiveAsync(
                    updateHandler: (client, update, ct) => HandleUpdateAsync(client, update, token, ct),
                    pollingErrorHandler: (_, ex, _) =>
                    {
                        logger.LogError(ex, "[{At}] Telegram polling error", Now());
                        return Task.CompletedTask;
                    },
                    receiverOptions: new ReceiverOptions
                    {
                        AllowedUpdates = [UpdateType.Message],
                        ThrowPendingUpdates = true
                    },
                    cancellationToken: stoppingToken);
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

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, string botToken, CancellationToken ct)
    {
        LogIncomingUpdate(update);
        if (update.Message is not { } message) return;

        var chatId = message.Chat.Id;
        var userId = message.From?.Id;
        var session = sessions.Get(chatId);
        var text = message.Text?.Trim() ?? string.Empty;

        try
        {
            if (message.Contact is { } contact)
            {
                await EnsureClientAsync(bot, chatId, contact.PhoneNumber, DisplayName(message.From), botToken, ct);
                return;
            }

            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                sessions.Reset(chatId);
                await SendWelcomeAsync(bot, chatId, ct);
                return;
            }

            if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase)
                || text.Equals("ℹ️ Помощь", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Помощь", StringComparison.OrdinalIgnoreCase))
            {
                await bot.SendTextMessageAsync(chatId,
                    "ℹ️ <b>Помощь</b>\n\n"
                    + "✂️ Записаться — выбрать услугу, день и время\n"
                    + "📅 Мои записи — ближайшие визиты\n"
                    + "❌ Отменить — отменить запись\n"
                    + "🔄 Перенести — выбрать новое время\n\n"
                    + "Пароль в боте не нужен. Для сайта временный пароль придёт сюда при первом входе.",
                    parseMode: ParseMode.Html,
                    replyMarkup: MainMenu(),
                    cancellationToken: ct);
                return;
            }

            if (text.StartsWith("/chatid", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/id", StringComparison.OrdinalIgnoreCase))
            {
                // Quiet admin helper — not advertised in welcome/help.
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
                await StartBookingAsync(bot, chatId, botToken, ct);
                return;
            }

            if (IsMainAction(text, "📅 Мои записи", "Мои записи"))
            {
                await ShowAppointmentsAsync(bot, chatId, botToken, ct);
                return;
            }

            if (IsMainAction(text, "❌ Отменить запись", "Отменить запись", "Отменить"))
            {
                await StartCancelAsync(bot, chatId, botToken, ct);
                return;
            }

            if (IsMainAction(text, "🔄 Перенести", "Перенести"))
            {
                await StartRescheduleAsync(bot, chatId, botToken, ct);
                return;
            }

            var phone = ExtractPhone(text);
            if (phone is not null && (session.Mode is BotMode.Idle or BotMode.NeedPhone))
            {
                await EnsureClientAsync(bot, chatId, phone, DisplayName(message.From), botToken, ct);
                return;
            }

            switch (session.Mode)
            {
                case BotMode.BookPickService:
                    await HandleBookServiceAsync(bot, chatId, text, botToken, ct);
                    return;
                case BotMode.BookPickDate:
                    await HandleBookDateAsync(bot, chatId, text, botToken, ct);
                    return;
                case BotMode.BookPickSlot:
                    await HandleBookSlotAsync(bot, chatId, text, botToken, ct);
                    return;
                case BotMode.CancelPick:
                    await HandleCancelPickAsync(bot, chatId, text, botToken, ct);
                    return;
                case BotMode.ReschedulePick:
                    await HandleReschedulePickAsync(bot, chatId, text, botToken, ct);
                    return;
                case BotMode.ReschedulePickDate:
                    await HandleRescheduleDateAsync(bot, chatId, text, botToken, ct);
                    return;
                case BotMode.ReschedulePickSlot:
                    await HandleRescheduleSlotAsync(bot, chatId, text, botToken, ct);
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

    private async Task StartBookingAsync(ITelegramBotClient bot, long chatId, string botToken, CancellationToken ct)
    {
        if (!await EnsureLinkedAsync(bot, chatId, botToken, ct)) return;
        var services = await ApiGetAsync<List<JsonElement>>(botToken, "telegram/services", ct);
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

    private async Task HandleBookServiceAsync(ITelegramBotClient bot, long chatId, string text, string botToken, CancellationToken ct)
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

    private async Task HandleBookDateAsync(ITelegramBotClient bot, long chatId, string text, string botToken, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        if (!TryParseDayButton(text, out var date))
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите день кнопкой.", cancellationToken: ct);
            return;
        }

        session.Date = date;
        await LoadSlotsAndAskAsync(bot, chatId, botToken, session, forReschedule: false, ct);
    }

    private async Task HandleBookSlotAsync(ITelegramBotClient bot, long chatId, string text, string botToken, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        var slot = session.Slots.FirstOrDefault(s => s.Label == text);
        if (slot.Utc == default)
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите время кнопкой.", cancellationToken: ct);
            return;
        }

        var body = new { chatId, serviceId = session.ServiceId, startAtUtc = slot.Utc };
        var (ok, err, data) = await ApiPostAsync(botToken, "telegram/appointments", body, ct);
        sessions.Reset(chatId);
        if (!ok)
        {
            await bot.SendTextMessageAsync(chatId, $"😔 Не удалось записаться: {err}", replyMarkup: MainMenu(), cancellationToken: ct);
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

    private async Task ShowAppointmentsAsync(ITelegramBotClient bot, long chatId, string botToken, CancellationToken ct)
    {
        if (!await EnsureLinkedAsync(bot, chatId, botToken, ct)) return;
        var items = await ApiGetAsync<List<JsonElement>>(botToken, $"telegram/appointments?chatId={chatId}", ct);
        if (items is null || items.Count == 0)
        {
            await bot.SendTextMessageAsync(chatId, "📭 Ближайших записей нет.", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        var lines = items.Select(a =>
        {
            var name = a.GetProperty("serviceName").GetString();
            var start = a.GetProperty("startAtUtc").GetDateTime();
            if (start.Kind == DateTimeKind.Unspecified) start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(start.ToUniversalTime(), Tz);
            return $"• ✂️ {name}\n  📅 {local:dd.MM.yyyy} 🕒 {local:HH:mm}";
        });
        await bot.SendTextMessageAsync(chatId,
            "📅 <b>Ваши записи</b>\n\n" + string.Join("\n\n", lines),
            parseMode: ParseMode.Html,
            replyMarkup: MainMenu(),
            cancellationToken: ct);
    }

    private async Task StartCancelAsync(ITelegramBotClient bot, long chatId, string botToken, CancellationToken ct)
    {
        if (!await EnsureLinkedAsync(bot, chatId, botToken, ct)) return;
        var items = await ApiGetAsync<List<JsonElement>>(botToken, $"telegram/appointments?chatId={chatId}", ct);
        if (items is null || items.Count == 0)
        {
            await bot.SendTextMessageAsync(chatId, "📭 Нечего отменять.", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        var session = sessions.Get(chatId);
        session.Mode = BotMode.CancelPick;
        session.Appointments = items.Select(a =>
        {
            var id = a.GetProperty("id").GetGuid();
            var name = a.GetProperty("serviceName").GetString() ?? "Услуга";
            var start = a.GetProperty("startAtUtc").GetDateTime();
            if (start.Kind == DateTimeKind.Unspecified) start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(start.ToUniversalTime(), Tz);
            return (id, $"❌ {name} — {local:dd.MM HH:mm}");
        }).ToList();

        var rows = session.Appointments.Select(a => new[] { new KeyboardButton(a.Label) }).ToList();
        rows.Add([new KeyboardButton("🏠 Меню")]);
        await bot.SendTextMessageAsync(chatId, "❌ Какую запись отменить?",
            replyMarkup: new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    private async Task HandleCancelPickAsync(ITelegramBotClient bot, long chatId, string text, string botToken, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        var match = session.Appointments.FirstOrDefault(a => a.Label == text);
        if (match.Id == Guid.Empty)
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите запись кнопкой.", cancellationToken: ct);
            return;
        }

        var (ok, err, _) = await ApiPostAsync(botToken, $"telegram/appointments/{match.Id}/cancel", new { chatId }, ct);
        sessions.Reset(chatId);
        await bot.SendTextMessageAsync(chatId,
            ok ? "✅ Запись отменена." : $"😔 Не удалось отменить: {err}",
            replyMarkup: MainMenu(),
            cancellationToken: ct);
    }

    private async Task StartRescheduleAsync(ITelegramBotClient bot, long chatId, string botToken, CancellationToken ct)
    {
        if (!await EnsureLinkedAsync(bot, chatId, botToken, ct)) return;
        var items = await ApiGetAsync<List<JsonElement>>(botToken, $"telegram/appointments?chatId={chatId}", ct);
        if (items is null || items.Count == 0)
        {
            await bot.SendTextMessageAsync(chatId, "📭 Нечего переносить.", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        var session = sessions.Get(chatId);
        session.Mode = BotMode.ReschedulePick;
        session.Appointments = [];
        session.Slots = [];
        var rows = new List<KeyboardButton[]>();
        foreach (var a in items)
        {
            var id = a.GetProperty("id").GetGuid();
            var serviceId = a.GetProperty("serviceId").GetGuid();
            var name = a.GetProperty("serviceName").GetString() ?? "Услуга";
            var start = a.GetProperty("startAtUtc").GetDateTime();
            if (start.Kind == DateTimeKind.Unspecified) start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(start.ToUniversalTime(), Tz);
            var label = $"🔄 {name} — {local:dd.MM HH:mm}";
            session.Appointments.Add((id, label));
            session.Slots.Add(($"sid:{id}:{serviceId}", default));
            rows.Add([new KeyboardButton(label)]);
        }

        rows.Add([new KeyboardButton("🏠 Меню")]);
        await bot.SendTextMessageAsync(chatId, "🔄 Какую запись перенести?",
            replyMarkup: new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    private async Task HandleReschedulePickAsync(ITelegramBotClient bot, long chatId, string text, string botToken, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        var match = session.Appointments.FirstOrDefault(a => a.Label == text);
        if (match.Id == Guid.Empty)
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите запись кнопкой.", cancellationToken: ct);
            return;
        }

        session.AppointmentId = match.Id;
        var sidEntry = session.Slots.FirstOrDefault(s => s.Label.StartsWith($"sid:{match.Id}:", StringComparison.Ordinal));
        if (sidEntry.Label is string sid && Guid.TryParse(sid.Split(':').LastOrDefault(), out var serviceId))
            session.ServiceId = serviceId;

        session.Mode = BotMode.ReschedulePickDate;
        await bot.SendTextMessageAsync(chatId, "📅 Новый день:", replyMarkup: DateKeyboard(), cancellationToken: ct);
    }

    private async Task HandleRescheduleDateAsync(ITelegramBotClient bot, long chatId, string text, string botToken, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        if (!TryParseDayButton(text, out var date))
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите день кнопкой.", cancellationToken: ct);
            return;
        }

        session.Date = date;
        await LoadSlotsAndAskAsync(bot, chatId, botToken, session, forReschedule: true, ct);
    }

    private async Task HandleRescheduleSlotAsync(ITelegramBotClient bot, long chatId, string text, string botToken, CancellationToken ct)
    {
        var session = sessions.Get(chatId);
        var slot = session.Slots.FirstOrDefault(s => s.Label == text);
        if (slot.Utc == default)
        {
            await bot.SendTextMessageAsync(chatId, "👆 Выберите время кнопкой.", cancellationToken: ct);
            return;
        }

        var (ok, err, _) = await ApiPostAsync(botToken,
            $"telegram/appointments/{session.AppointmentId}/reschedule",
            new { chatId, serviceId = session.ServiceId, startAtUtc = slot.Utc },
            ct);
        sessions.Reset(chatId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(slot.Utc, Tz);
        await bot.SendTextMessageAsync(chatId,
            ok
                ? $"✅ Запись перенесена на 📅 {local:dd.MM.yyyy} 🕒 {local:HH:mm}"
                : $"😔 Не удалось перенести: {err}",
            replyMarkup: MainMenu(),
            cancellationToken: ct);
    }

    private async Task LoadSlotsAndAskAsync(
        ITelegramBotClient bot, long chatId, string botToken, BotSession session, bool forReschedule, CancellationToken ct)
    {
        if (session.ServiceId is null || session.Date is null)
        {
            sessions.Reset(chatId);
            await bot.SendTextMessageAsync(chatId, "⚠️ Сессия сброшена. /start", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        var dateStr = session.Date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var resp = await ApiGetAsync<SlotsPayload>(botToken, $"telegram/slots?serviceId={session.ServiceId}&date={dateStr}", ct);
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
        ITelegramBotClient bot, long chatId, string rawPhone, string name, string botToken, CancellationToken ct)
    {
        var (ok, err, data) = await ApiPostAsync(botToken, "telegram/ensure-client",
            new { phone = rawPhone, chatId, name }, ct);
        sessions.Reset(chatId);
        if (!ok)
        {
            await bot.SendTextMessageAsync(chatId, $"😔 Не удалось сохранить номер: {err}", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        var created = data?.TryGetProperty("created", out var c) == true && c.GetBoolean();
        await bot.SendTextMessageAsync(chatId,
            created
                ? "✅ Аккаунт создан по вашему номеру. Можно записываться прямо здесь — пароль не нужен."
                : "✅ Номер привязан. Можно записываться на услуги.",
            replyMarkup: MainMenu(),
            cancellationToken: ct);
    }

    private async Task<bool> EnsureLinkedAsync(ITelegramBotClient bot, long chatId, string botToken, CancellationToken ct)
    {
        var items = await ApiGetAsync<List<JsonElement>>(botToken, $"telegram/appointments?chatId={chatId}", ct);
        // 404 means not linked — ApiGet returns null; distinguish via ensure check
        var probe = await ApiRawGetAsync(botToken, $"telegram/appointments?chatId={chatId}", ct);
        if (probe is { StatusCode: HttpStatusCode.NotFound })
        {
            sessions.Get(chatId).Mode = BotMode.NeedPhone;
            await bot.SendTextMessageAsync(chatId,
                "📱 Чтобы записываться, поделитесь номером телефона кнопкой ниже.",
                replyMarkup: SharePhoneKeyboard(),
                cancellationToken: ct);
            return false;
        }

        _ = items;
        return true;
    }

    private static async Task SendWelcomeAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        await bot.SendTextMessageAsync(chatId,
            "👋 <b>Добро пожаловать в D_Barber!</b>\n\n"
            + "Здесь можно записаться на стрижку, посмотреть свои визиты, отменить или перенести запись.\n\n"
            + "📱 Сначала поделитесь номером — аккаунт создастся сам, пароль в боте не нужен.",
            parseMode: ParseMode.Html,
            replyMarkup: SharePhoneKeyboard(),
            cancellationToken: ct);
    }

    private static ReplyKeyboardMarkup MainMenu() => new(new[]
    {
        new[] { new KeyboardButton("✂️ Записаться"), new KeyboardButton("📅 Мои записи") },
        new[] { new KeyboardButton("❌ Отменить запись"), new KeyboardButton("🔄 Перенести") },
        new[] { new KeyboardButton("ℹ️ Помощь") }
    })
    { ResizeKeyboard = true };

    private static ReplyKeyboardMarkup SharePhoneKeyboard() => new(new[]
    {
        new[] { KeyboardButton.WithRequestContact("📱 Поделиться номером") },
        new[] { new KeyboardButton("ℹ️ Помощь") }
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

    private HttpClient ApiClient(string botToken)
    {
        var api = httpClientFactory.CreateClient("api");
        api.DefaultRequestHeaders.Remove("X-Telegram-Bot-Token");
        api.DefaultRequestHeaders.TryAddWithoutValidation("X-Telegram-Bot-Token", botToken);
        return api;
    }

    private async Task<T?> ApiGetAsync<T>(string botToken, string path, CancellationToken ct)
    {
        try
        {
            var api = ApiClient(botToken);
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

    private async Task<HttpResponseMessage?> ApiRawGetAsync(string botToken, string path, CancellationToken ct)
    {
        try
        {
            var api = ApiClient(botToken);
            return await api.GetAsync(path, ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<(bool Ok, string Error, JsonElement? Data)> ApiPostAsync(
        string botToken, string path, object body, CancellationToken ct)
    {
        try
        {
            var api = ApiClient(botToken);
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
            return (false, message ?? $"HTTP {(int)resp.StatusCode}", data);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{At}] API POST {Path} failed", Now(), path);
            return (false, "нет связи с сервером", null);
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
            config["TelegramBot:Token"], config["Telegram:BotToken"], config["TELEGRAM_BOT_TOKEN"],
            Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN"),
            Environment.GetEnvironmentVariable("Telegram__BotToken"));

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
