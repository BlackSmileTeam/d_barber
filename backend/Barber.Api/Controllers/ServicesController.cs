using Barber.Application.DTOs;
using Barber.Domain.Entities;
using Barber.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barber.Api.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController(BarberDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceDto>>> List([FromQuery] bool all = false, CancellationToken ct = default)
    {
        var q = db.Services.AsNoTracking().AsQueryable();
        if (!all) q = q.Where(s => s.IsActive);
        var items = await q.OrderBy(s => s.SortOrder).ToListAsync(ct);
        return Ok(items.Select(Map));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ServiceDto>> Create(UpsertServiceDto dto, CancellationToken ct)
    {
        var entity = new Service
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            DurationMinutes = dto.DurationMinutes <= 0 ? 60 : dto.DurationMinutes,
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder
        };
        db.Services.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ServiceDto>> Update(Guid id, UpsertServiceDto dto, CancellationToken ct)
    {
        var entity = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return NotFound(new { message = "Услуга не найдена" });
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.DurationMinutes = dto.DurationMinutes <= 0 ? 60 : dto.DurationMinutes;
        entity.IsActive = dto.IsActive;
        entity.SortOrder = dto.SortOrder;
        await db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return NotFound(new { message = "Услуга не найдена" });

        var hasAppointments = await db.Appointments.AnyAsync(a => a.ServiceId == id, ct);
        if (hasAppointments)
            return BadRequest(new { message = "Нельзя удалить услугу: есть связанные записи. Снимите галочку «Активна»." });

        var linkedPortfolio = await db.PortfolioItems.Where(p => p.ServiceId == id).ToListAsync(ct);
        foreach (var item in linkedPortfolio)
            item.ServiceId = null;

        db.Services.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ServiceDto Map(Service s) =>
        new(s.Id, s.Name, s.Description, s.Price, s.DurationMinutes, s.IsActive, s.SortOrder);
}
