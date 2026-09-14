using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddHttpClient("api", (sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>()["Api:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("API_BASE_URL")
        ?? "http://localhost:5271/api";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});
builder.Services.AddHostedService<BotWorker>();
await builder.Build().RunAsync();

public sealed class BotWorker(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<BotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = config["TelegramBot:Token"] ?? config["TELEGRAM_BOT_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Telegram bot token is not configured. Worker idle.");
            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            return;
        }

        logger.LogInformation("D_Barber Telegram bot starting. API: {Api}", config["Api:BaseUrl"]);
        var api = httpClientFactory.CreateClient("api");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var health = await api.GetAsync("health", stoppingToken);
                logger.LogInformation("API health: {Status}", health.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "API health check failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
