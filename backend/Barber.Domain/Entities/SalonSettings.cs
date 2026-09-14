namespace Barber.Domain.Entities;

public class SalonSettings
{
    public Guid Id { get; set; }
    public string BrandName { get; set; } = "D_Barber";
    public string SalonName { get; set; } = "Icon, барбершоп";
    public string Address { get; set; } = "Санкт-Петербург, Российский просп., 6";
    public string City { get; set; } = "Санкт-Петербург";
    public string? Phone { get; set; }
    public string? AboutHtml { get; set; }
    public string? AboutImageUrl { get; set; }
    public string? InstagramUrl { get; set; } = "https://www.instagram.com/Denis_ryabtsov";
    public string? TelegramUrl { get; set; } = "https://t.me/DenisRyabtsov";
    public string? MapLat { get; set; } = "59.9343";
    public string? MapLon { get; set; } = "30.3351";
    public string TimeZoneId { get; set; } = "Europe/Moscow";
    public string BookingUrl { get; set; } = "/book";
}
