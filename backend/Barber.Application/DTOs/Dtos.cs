namespace Barber.Application.DTOs;

public record AuthResponseDto(string Token, string Role, string Name, string? Phone, Guid UserId);

public record ClientRegisterDto(string Phone, string Password, string Name);

public record ClientLoginDto(string Phone, string Password);

public record AdminLoginDto(string Login, string Password);

public record ServiceDto(Guid Id, string Name, string? Description, decimal Price, int DurationMinutes, bool IsActive, int SortOrder);

public record UpsertServiceDto(string Name, string? Description, decimal Price, int DurationMinutes, bool IsActive, int SortOrder);

public record CreateAppointmentDto(Guid ServiceId, DateTime StartAtUtc);

public record RescheduleAppointmentDto(DateTime StartAtUtc);

public record AppointmentDto(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    decimal Price,
    int DurationMinutes,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Status,
    string ClientName,
    string ClientPhone);

public record PortfolioDto(Guid Id, string Title, string? Description, string ImageUrl, string? ServiceName, decimal? DisplayPrice);

public record NewsDto(Guid Id, string Title, string Body, string? CoverImageUrl, string Status, DateTime? PublishAtUtc, DateTime CreatedAtUtc);

public record UpsertNewsDto(string Title, string Body, string? CoverImageUrl, string Status, DateTime? PublishAtUtc);

public record SalonSettingsDto(
    string BrandName,
    string SalonName,
    string Address,
    string City,
    string? Phone,
    string? AboutHtml,
    string? AboutImageUrl,
    string? MapLat,
    string? MapLon,
    string BookingUrl);

public record NotificationTemplateDto(Guid Id, string Key, string Title, string Body);

public record SlotsResponseDto(Guid ServiceId, string Date, IReadOnlyList<DateTime> SlotsUtc);
