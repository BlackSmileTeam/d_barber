using Barber.Infrastructure.Data;
using Barber.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Barber.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = NormalizeMySqlConnectionString(
            configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=dbarber.db");

        services.AddDbContext<BarberDbContext>(options =>
        {
            if (string.Equals(provider, "MySql", StringComparison.OrdinalIgnoreCase))
                options.UseMySQL(connectionString);
            else
                options.UseSqlite(connectionString);
        });

        services.AddHttpClient();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<SlotService>();
        services.AddSingleton<IConfigurationAccessor, ConfigurationAccessor>();
        services.AddScoped<TelegramNotifyService>();

        return services;
    }

    /// <summary>
    /// Oracle MySql.Data rejects Pomelo-style SslMode=None; map to Disabled.
    /// </summary>
    internal static string NormalizeMySqlConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        return connectionString
            .Replace("SslMode=None", "SslMode=Disabled", StringComparison.OrdinalIgnoreCase)
            .Replace("Ssl Mode=None", "Ssl Mode=Disabled", StringComparison.OrdinalIgnoreCase);
    }
}
