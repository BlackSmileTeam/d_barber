using Barber.Api.Controllers;
using Barber.Domain.Enums;
using Barber.Infrastructure.Data;
using Barber.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Barber.Api.Workers;

/// <summary>
/// Sends scheduled Telegram notifications:
/// - reminder_2h: ~2 hours before appointment start
/// - templates with TriggerIntervalType Monthly/Custom/Daily/Weekly: after last visit
/// </summary>
public sealed class ReminderHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReminderHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Reminder2hWindow = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ReminderHostedService started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BarberDbContext>();
                var telegram = scope.ServiceProvider.GetRequiredService<TelegramNotifyService>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                await TickOnceAsync(db, telegram, config, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reminder tick failed");
            }

            try
            {
                await Task.Delay(Tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal static async Task TickOnceAsync(
        BarberDbContext db,
        TelegramNotifyService telegram,
        IConfiguration config,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await SyncLastVisitsAsync(db, now, ct);
        await SendTwoHourRemindersAsync(db, telegram, config, now, ct);
        await SendComebackRemindersAsync(db, telegram, config, now, ct);
    }

    private static async Task SyncLastVisitsAsync(BarberDbContext db, DateTime now, CancellationToken ct)
    {
        var ended = await db.Appointments
            .Include(a => a.Client)
            .Where(a => a.Status != AppointmentStatus.Cancelled
                        && a.EndAtUtc <= now
                        && (a.Client.LastVisitAtUtc == null || a.EndAtUtc > a.Client.LastVisitAtUtc))
            .ToListAsync(ct);

        foreach (var group in ended.GroupBy(a => a.ClientId))
        {
            var latest = group.Max(a => a.EndAtUtc);
            var client = group.First().Client;
            if (client.LastVisitAtUtc is null || latest > client.LastVisitAtUtc)
                client.LastVisitAtUtc = latest;
        }

        if (ended.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private static async Task SendTwoHourRemindersAsync(
        BarberDbContext db,
        TelegramNotifyService telegram,
        IConfiguration config,
        DateTime now,
        CancellationToken ct)
    {
        var windowStart = now.AddHours(2).Subtract(Reminder2hWindow / 2);
        var windowEnd = now.AddHours(2).Add(Reminder2hWindow / 2);

        var due = await db.Appointments
            .Include(a => a.Client)
            .Include(a => a.Service)
            .Where(a => a.Status != AppointmentStatus.Cancelled
                        && a.Reminder2hSentAtUtc == null
                        && a.Client.TelegramChatId != null
                        && a.StartAtUtc >= windowStart
                        && a.StartAtUtc <= windowEnd)
            .ToListAsync(ct);

        if (due.Count == 0)
            return;

        var settings = await db.SalonSettings.AsNoTracking().FirstAsync(ct);
        var template = await telegram.GetTemplateAsync(db, "reminder_2h", ct);
        if (string.IsNullOrWhiteSpace(template))
            template = "{Имя}, через 2 часа запись на «{Услуга}» — {Дата} в {Время}.";

        var frontend = config["App:FrontendPublicUrl"] ?? "";
        foreach (var appt in due)
        {
            var text = TelegramNotifyService.Render(
                template,
                AppointmentsController.BuildValues(appt, settings, frontend));
            await telegram.NotifyChatAsync(appt.Client.TelegramChatId!.Value, text, ct);
            appt.Reminder2hSentAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SendComebackRemindersAsync(
        BarberDbContext db,
        TelegramNotifyService telegram,
        IConfiguration config,
        DateTime now,
        CancellationToken ct)
    {
        var templates = await db.NotificationTemplates.AsNoTracking()
            .Where(t => t.TriggerIntervalType != TriggerIntervalType.None)
            .ToListAsync(ct);
        if (templates.Count == 0)
            return;

        var settings = await db.SalonSettings.AsNoTracking().FirstAsync(ct);
        var frontend = config["App:FrontendPublicUrl"] ?? "";

        var clients = await db.Clients
            .Where(c => c.TelegramChatId != null && c.LastVisitAtUtc != null)
            .ToListAsync(ct);

        foreach (var client in clients)
        {
            var lastVisit = client.LastVisitAtUtc!.Value;
            foreach (var tpl in templates)
            {
                var days = ResolveIntervalDays(tpl.TriggerIntervalType, tpl.TriggerIntervalDays);
                if (days is null or <= 0)
                    continue;

                var dueAt = lastVisit.AddDays(days.Value);
                if (now < dueAt)
                    continue;

                // Already reminded for this cycle?
                var already = await db.Appointments
                    .Where(a => a.ClientId == client.Id
                                && a.MonthlyReminderSentAtUtc != null
                                && a.MonthlyReminderSentAtUtc >= dueAt)
                    .AnyAsync(ct);
                if (already)
                    continue;

                var lastAppt = await db.Appointments
                    .Include(a => a.Service)
                    .Where(a => a.ClientId == client.Id && a.Status != AppointmentStatus.Cancelled && a.EndAtUtc <= now)
                    .OrderByDescending(a => a.EndAtUtc)
                    .FirstOrDefaultAsync(ct);

                var values = new Dictionary<string, string>
                {
                    ["Имя"] = client.Name,
                    ["Клиент"] = client.Name,
                    ["Услуга"] = lastAppt?.Service.Name ?? "стрижку",
                    ["Дата"] = lastVisit.ToLocalTime().ToString("dd.MM.yyyy"),
                    ["Время"] = lastVisit.ToLocalTime().ToString("HH:mm"),
                    ["НазваниеСалона"] = settings.SalonName,
                    ["Адрес"] = settings.Address,
                    ["СсылкаНаЗапись"] = $"{frontend.TrimEnd('/')}{settings.BookingUrl}"
                };

                var text = TelegramNotifyService.Render(
                    string.IsNullOrWhiteSpace(tpl.Body) ? "{Имя}, пора записаться снова: {СсылкаНаЗапись}" : tpl.Body,
                    values);
                await telegram.NotifyChatAsync(client.TelegramChatId!.Value, text, ct);

                if (lastAppt is not null)
                {
                    lastAppt.MonthlyReminderSentAtUtc = now;
                }
                else
                {
                    // No past appointment row to stamp — create a marker via a no-op save on client only.
                    // Use a synthetic stamp: update LastVisitAtUtc unchanged but we need a flag.
                    // Fallback: bump LastVisitAtUtc tracking by inserting sent time on newest any appointment.
                    var any = await db.Appointments
                        .Where(a => a.ClientId == client.Id)
                        .OrderByDescending(a => a.CreatedAtUtc)
                        .FirstOrDefaultAsync(ct);
                    if (any is not null)
                        any.MonthlyReminderSentAtUtc = now;
                }

                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static int? ResolveIntervalDays(TriggerIntervalType type, int? customDays) => type switch
    {
        TriggerIntervalType.Daily => 1,
        TriggerIntervalType.Weekly => 7,
        TriggerIntervalType.Monthly => customDays is > 0 ? customDays : 30,
        TriggerIntervalType.Custom => customDays is > 0 ? customDays : null,
        _ => null
    };
}
