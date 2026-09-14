namespace Barber.Domain.Entities;

public class PortfolioItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public Guid? ServiceId { get; set; }
    public Service? Service { get; set; }
    public decimal? DisplayPrice { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
