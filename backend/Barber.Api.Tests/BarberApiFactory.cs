using Barber.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Barber.Api.Tests;

public class BarberApiFactory : WebApplicationFactory<Program>
{
    public const string TestBotToken = "test-bot-token-for-api-tests";

    private readonly string _dbName = $"dbarber-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telegram:BotToken"] = TestBotToken,
                ["Telegram:AdminChatId"] = "1"
            });
        });
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<BarberDbContext>) ||
                d.ServiceType == typeof(BarberDbContext) ||
                (d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition().Name.Contains("DbContextOptions"))).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<BarberDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
