using Barber.Application.DTOs;
using Barber.Domain.Entities;
using Barber.Domain.Enums;
using Barber.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Файл не выбран" });

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedImageExt.Contains(ext))
            return BadRequest(new { message = "Допустимы JPG, PNG или WebP" });

        var wwwroot = env.WebRootPath;
        if (string.IsNullOrWhiteSpace(wwwroot))
            wwwroot = Path.Combine(env.ContentRootPath, "wwwroot");

        var uploads = Path.Combine(wwwroot, "uploads");
        Directory.CreateDirectory(uploads);

        var fileName = $"about-{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploads, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream, ct);

        var s = await db.SalonSettings.FirstAsync(ct);
        s.AboutImageUrl = $"/uploads/{fileName}";
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
        return Ok(items.Select(p => new PortfolioDto(
            p.Id, p.Title, p.Description, p.ImageUrl, p.Service?.Name, p.DisplayPrice ?? p.Service?.Price)));
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
        return Ok(items.Select(t => new NotificationTemplateDto(t.Id, t.Key, t.Title, t.Body)));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("admin/templates/{id:guid}")]
    public async Task<ActionResult<NotificationTemplateDto>> UpdateTemplate(Guid id, NotificationTemplateDto dto, CancellationToken ct)
    {
        var entity = await db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return NotFound();
        entity.Title = dto.Title;
        entity.Body = dto.Body;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new NotificationTemplateDto(entity.Id, entity.Key, entity.Title, entity.Body));
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

    private static SalonSettingsDto MapSalon(SalonSettings s) =>
        new(s.BrandName, s.SalonName, s.Address, s.City, s.Phone, s.AboutHtml, s.AboutImageUrl, s.MapLat, s.MapLon, s.BookingUrl);

    private static NewsDto MapNews(NewsPost n) =>
        new(n.Id, n.Title, n.Body, n.CoverImageUrl, n.Status.ToString(), n.PublishAtUtc, n.CreatedAtUtc);
}
