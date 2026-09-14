using Barber.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Barber.Infrastructure.Data;

public class BarberDbContext : DbContext
{
    public BarberDbContext(DbContextOptions<BarberDbContext> options) : base(options)
    {
    }

    public DbSet<Service> Services => Set<Service>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<TimeOff> TimeOffs => Set<TimeOff>();
    public DbSet<PortfolioItem> PortfolioItems => Set<PortfolioItem>();
    public DbSet<NewsPost> NewsPosts => Set<NewsPost>();
    public DbSet<SalonSettings> SalonSettings => Set<SalonSettings>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(e =>
        {
            e.HasIndex(x => x.Phone).IsUnique();
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(128);
        });

        modelBuilder.Entity<AdminUser>(e =>
        {
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.Login).HasMaxLength(64);
        });

        modelBuilder.Entity<Service>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasIndex(x => x.StartAtUtc);
            e.HasOne(x => x.Client).WithMany(c => c.Appointments).HasForeignKey(x => x.ClientId);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
        });

        modelBuilder.Entity<NotificationTemplate>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(64);
        });

        modelBuilder.Entity<PortfolioItem>(e =>
        {
            e.Property(x => x.DisplayPrice).HasPrecision(10, 2);
        });
    }
}
