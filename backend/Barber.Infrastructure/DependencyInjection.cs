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
        services.AddHttpClient("telegram", (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(100);
            // Base address unused; full URL built in TelegramNotifyService.
        }).ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var proxyUrl = FirstNonEmpty(
                cfg["Telegram:ProxyUrl"],
                cfg["TELEGRAM_PROXY_URL"],
                Environment.GetEnvironmentVariable("TELEGRAM_PROXY_URL"),
                Environment.GetEnvironmentVariable("Telegram__ProxyUrl"),
                Environment.GetEnvironmentVariable("HTTPS_PROXY"),
                Environment.GetEnvironmentVariable("HTTP_PROXY"));

            if (string.IsNullOrWhiteSpace(proxyUrl))
                return new HttpClientHandler();

            return new HttpClientHandler
            {
                Proxy = new System.Net.WebProxy(proxyUrl),
                UseProxy = true
            };
        });
        services.AddScoped<JwtTokenService>();
        services.AddScoped<SlotService>();
        services.AddSingleton<IConfigurationAccessor, ConfigurationAccessor>();
        services.AddScoped<TelegramNotifyService>();

        return services;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

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
