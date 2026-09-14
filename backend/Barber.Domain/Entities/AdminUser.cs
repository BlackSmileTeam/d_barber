namespace Barber.Domain.Entities;

public class AdminUser
{
    public Guid Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "D_Barber";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
