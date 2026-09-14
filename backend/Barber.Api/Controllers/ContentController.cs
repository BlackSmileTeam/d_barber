using Barber.Application.DTOs;
using Barber.Domain.Entities;
using Barber.Domain.Enums;
using Barber.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TriggerInterval = Barber.Domain.Enums.TriggerIntervalType;

namespace Barber.Api.Controllers;

[ApiController]
[Route("api")]
public class ContentController(BarberDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private static readonly HashSet<string> AllowedImageExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var ok = await db.Database.CanConnectAsync(ct);
        return ok ? Ok(new { status = "ok" }) : StatusCode(503, new { status = "db_unavailable" });
    }

    [HttpGet("salon")]
    public async Task<ActionResult<SalonSettingsDto>> Salon(CancellationToken ct)
    {
        var s = await db.SalonSettings.AsNoTracking().FirstAsync(ct);
        return Ok(MapSalon(s));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("admin/salon")]
    public async Task<ActionResult<SalonSettingsDto>> UpdateSalon(SalonSettingsDto dto, CancellationToken ct)
    {
        var s = await db.SalonSettings.FirstAsync(ct);
        s.BrandName = dto.BrandName;
        s.SalonName = dto.SalonName;
        s.Address = dto.Address;
        s.City = dto.City;
        s.Phone = dto.Phone;
        s.AboutHtml = dto.AboutHtml;
        if (!string.IsNullOrWhiteSpace(dto.AboutImageUrl))
            s.AboutImageUrl = dto.AboutImageUrl;
        s.InstagramUrl = string.IsNullOrWhiteSpace(dto.InstagramUrl) ? null : dto.InstagramUrl.Trim();
        s.TelegramUrl = string.IsNullOrWhiteSpace(dto.TelegramUrl) ? null : dto.TelegramUrl.Trim();
        s.MapLat = dto.MapLat;
        s.MapLon = dto.MapLon;
        s.BookingUrl = dto.BookingUrl;
        await db.SaveChangesAsync(ct);
        return Ok(MapSalon(s));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/salon/about-image")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<SalonSettingsDto>> UploadAboutImage(IFormFile file, CancellationToken ct)
    {
        var url = await SaveUploadAsync(file, "about", ct);
        if (url.Result is not null) return url.Result;

        var s = await db.SalonSettings.FirstAsync(ct);
        s.AboutImageUrl = url.Value;
        await db.SaveChangesAsync(ct);
        return Ok(MapSalon(s));
    }

    [HttpGet("portfolio")]
    public async Task<ActionResult<IEnumerable<PortfolioDto>>> Portfolio(CancellationToken ct)
    {
        var items = await db.PortfolioItems.AsNoTracking()
            .Include(p => p.Service)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);
        return Ok(items.Select(MapPortfolio));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/portfolio")]
    public async Task<ActionResult<PortfolioDto>> CreatePortfolio(UpsertPortfolioDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Укажите название" });
        if (string.IsNullOrWhiteSpace(dto.ImageUrl))
            return BadRequest(new { message = "Укажите изображение" });

        var entity = new PortfolioItem
        {
            Id = Guid.NewGuid(),
            Title = dto.Title.Trim(),
            Description = dto.Description,
            ImageUrl = dto.ImageUrl.Trim(),
            ServiceId = dto.ServiceId,
            DisplayPrice = dto.DisplayPrice,
            SortOrder = dto.SortOrder
        };
        db.PortfolioItems.Add(entity);
        await db.SaveChangesAsync(ct);
        await db.Entry(entity).Reference(p => p.Service).LoadAsync(ct);
        return Ok(MapPortfolio(entity));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("admin/portfolio/{id:guid}")]
    public async Task<ActionResult<PortfolioDto>> UpdatePortfolio(Guid id, UpsertPortfolioDto dto, CancellationToken ct)
    {
        var entity = await db.PortfolioItems.Include(p => p.Service).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Укажите название" });
        if (string.IsNullOrWhiteSpace(dto.ImageUrl))
            return BadRequest(new { message = "Укажите изображение" });

        entity.Title = dto.Title.Trim();
        entity.Description = dto.Description;
        entity.ImageUrl = dto.ImageUrl.Trim();
        entity.ServiceId = dto.ServiceId;
        entity.DisplayPrice = dto.DisplayPrice;
        entity.SortOrder = dto.SortOrder;
        await db.SaveChangesAsync(ct);
        await db.Entry(entity).Reference(p => p.Service).LoadAsync(ct);
        return Ok(MapPortfolio(entity));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("admin/portfolio/{id:guid}")]
    public async Task<IActionResult> DeletePortfolio(Guid id, CancellationToken ct)
    {
        var entity = await db.PortfolioItems.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null) return NotFound();
        db.PortfolioItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/portfolio/image")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<object>> UploadPortfolioImage(IFormFile file, CancellationToken ct)
    {
        var url = await SaveUploadAsync(file, "portfolio", ct);
        if (url.Result is not null) return url.Result;
        return Ok(new { imageUrl = url.Value });
    }

    [HttpGet("news")]
    public async Task<ActionResult<IEnumerable<NewsDto>>> News(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var items = await db.NewsPosts.AsNoTracking()
            .Where(n => n.Status == NewsStatus.Published
                        || (n.Status == NewsStatus.Scheduled && n.PublishAtUtc != null && n.PublishAtUtc <= now))
            .OrderByDescending(n => n.PublishAtUtc ?? n.CreatedAtUtc)
            .ToListAsync(ct);
        return Ok(items.Select(MapNews));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/news")]
    public async Task<ActionResult<IEnumerable<NewsDto>>> AdminNews(CancellationToken ct)
    {
        var items = await db.NewsPosts.AsNoTracking().OrderByDescending(n => n.UpdatedAtUtc).ToListAsync(ct);
        return Ok(items.Select(MapNews));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/news")]
    public async Task<ActionResult<NewsDto>> CreateNews(UpsertNewsDto dto, CancellationToken ct)
    {
        var entity = new NewsPost
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Body = dto.Body,
            CoverImageUrl = dto.CoverImageUrl,
            Status = Enum.Parse<NewsStatus>(dto.Status, true),
            PublishAtUtc = dto.PublishAtUtc
        };
        db.NewsPosts.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(MapNews(entity));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("admin/news/{id:guid}")]
    public async Task<ActionResult<NewsDto>> UpdateNews(Guid id, UpsertNewsDto dto, CancellationToken ct)
    {
        var entity = await db.NewsPosts.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (entity is null) return NotFound();
        entity.Title = dto.Title;
        entity.Body = dto.Body;
        entity.CoverImageUrl = dto.CoverImageUrl;
        entity.Status = Enum.Parse<NewsStatus>(dto.Status, true);
        entity.PublishAtUtc = dto.PublishAtUtc;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(MapNews(entity));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/templates")]
    public async Task<ActionResult<IEnumerable<NotificationTemplateDto>>> Templates(CancellationToken ct)
    {
        var items = await db.NotificationTemplates.AsNoTracking().OrderBy(t => t.Key).ToListAsync(ct);
        return Ok(items.Select(MapTemplate));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/templates")]
    public async Task<ActionResult<NotificationTemplateDto>> CreateTemplate(CreateNotificationTemplateDto dto, CancellationToken ct)
    {
        var key = (dto.Key ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { message = "Укажите ключ шаблона" });
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Укажите название" });
        if (await db.NotificationTemplates.AnyAsync(t => t.Key == key, ct))
            return BadRequest(new { message = "Шаблон с таким ключом уже есть" });

        if (!TryParseInterval(dto.TriggerIntervalType, dto.TriggerIntervalDays, out var interval, out var days, out var intervalError))
            return BadRequest(new { message = intervalError });

        var entity = new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            Key = key,
            Title = dto.Title.Trim(),
            TriggerDescription = ResolveTriggerDescription(dto.TriggerDescription, interval, days),
            TriggerIntervalType = interval,
            TriggerIntervalDays = days,
            Body = dto.Body ?? ""
        };
        db.NotificationTemplates.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(MapTemplate(entity));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("admin/templates/{id:guid}")]
    public async Task<ActionResult<NotificationTemplateDto>> UpdateTemplate(Guid id, NotificationTemplateDto dto, CancellationToken ct)
    {
        var entity = await db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return NotFound();
        if (!TryParseInterval(dto.TriggerIntervalType, dto.TriggerIntervalDays, out var interval, out var days, out var intervalError))
            return BadRequest(new { message = intervalError });

        entity.Title = dto.Title;
        entity.TriggerDescription = ResolveTriggerDescription(dto.TriggerDescription, interval, days);
        entity.TriggerIntervalType = interval;
        entity.TriggerIntervalDays = days;
        entity.Body = dto.Body;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(MapTemplate(entity));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("admin/templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        var entity = await db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return NotFound();
        db.NotificationTemplates.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/stats")]
    public async Task<ActionResult<object>> Stats(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var confirmed = await db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Confirmed && a.StartAtUtc >= today && a.StartAtUtc < tomorrow, ct);
        var cancelled = await db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Cancelled, ct);
        var rescheduled = await db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Rescheduled, ct);
        return Ok(new { todayConfirmed = confirmed, cancelledTotal = cancelled, rescheduledTotal = rescheduled });
    }

    private async Task<(string? Value, ActionResult? Result)> SaveUploadAsync(IFormFile file, string prefix, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return (null, BadRequest(new { message = "Файл не выбран" }));

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedImageExt.Contains(ext))
            return (null, BadRequest(new { message = "Допустимы JPG, PNG или WebP" }));

        var wwwroot = env.WebRootPath;
        if (string.IsNullOrWhiteSpace(wwwroot))
            wwwroot = Path.Combine(env.ContentRootPath, "wwwroot");

        var uploads = Path.Combine(wwwroot, "uploads");
        Directory.CreateDirectory(uploads);

        var fileName = $"{prefix}-{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploads, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream, ct);

        return ($"/uploads/{fileName}", null);
    }

    private static SalonSettingsDto MapSalon(SalonSettings s) =>
        new(s.BrandName, s.SalonName, s.Address, s.City, s.Phone, s.AboutHtml, s.AboutImageUrl,
            s.InstagramUrl, s.TelegramUrl, s.MapLat, s.MapLon, s.BookingUrl);

    private static PortfolioDto MapPortfolio(PortfolioItem p) =>
        new(p.Id, p.Title, p.Description, p.ImageUrl, p.Service?.Name, p.DisplayPrice ?? p.Service?.Price,
            p.ServiceId, p.SortOrder);

    private static NotificationTemplateDto MapTemplate(NotificationTemplate t) =>
        new(t.Id, t.Key, t.Title, t.TriggerDescription, t.TriggerIntervalType.ToString(),
            t.TriggerIntervalDays, t.Body);

    private static NewsDto MapNews(NewsPost n) =>
        new(n.Id, n.Title, n.Body, n.CoverImageUrl, n.Status.ToString(), n.PublishAtUtc, n.CreatedAtUtc);

    private static bool TryParseInterval(
        string? rawType,
        int? rawDays,
        out TriggerInterval interval,
        out int? days,
        out string? error)
    {
        interval = TriggerInterval.None;
        days = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawType))
        {
            interval = TriggerInterval.None;
            days = null;
            return true;
        }

        if (!Enum.TryParse(rawType.Trim(), true, out interval))
        {
            error = "Укажите тип интервала отправки";
            return false;
        }

        if (interval == TriggerInterval.Custom)
        {
            if (rawDays is null or < 1)
            {
                error = "Укажите число дней для кастомного интервала";
                return false;
            }

            days = rawDays;
            return true;
        }

        days = interval switch
        {
            TriggerInterval.Daily => 1,
            TriggerInterval.Weekly => 7,
            TriggerInterval.Monthly => 30,
            _ => null
        };
        return true;
    }

    private static string ResolveTriggerDescription(string? freeText, TriggerInterval interval, int? days)
    {
        var text = (freeText ?? "").Trim();
        if (interval == TriggerInterval.None)
            return text;

        var auto = interval switch
        {
            TriggerInterval.Daily => "Каждый день после последнего визита",
            TriggerInterval.Weekly => "Раз в неделю после последнего визита",
            TriggerInterval.Monthly => "Раз в месяц после последнего визита",
            TriggerInterval.Custom => $"Через {days} дн. после последнего визита",
            _ => text
        };
        return string.IsNullOrWhiteSpace(text) ? auto : text;
    }
}
