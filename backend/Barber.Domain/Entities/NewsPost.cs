using Barber.Domain.Enums;

namespace Barber.Domain.Entities;

public class NewsPost
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public NewsStatus Status { get; set; } = NewsStatus.Draft;
    public DateTime? PublishAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
