using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Barber.Api.Tests;

public class ApiSmokeTests : IClassFixture<BarberApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ApiSmokeTests(BarberApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Services_Seeded_ThreeItems()
    {
        var response = await _client.GetAsync("/api/services");
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOpts);
        Assert.NotNull(list);
        Assert.True(list.Count >= 3);
    }

    [Fact]
    public async Task Register_Login_Book_Flow()
    {
        var phone = $"+7999{Random.Shared.Next(1000000, 9999999)}";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            phone,
            password = "secret12",
            name = "Тест Клиент"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthDto>(JsonOpts);
        Assert.NotNull(auth?.Token);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);

        var services = await _client.GetFromJsonAsync<List<ServiceDto>>("/api/services", JsonOpts);
        Assert.NotNull(services);
        var service = services[0];

        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        // find weekday Mon-Sat
        while (day.DayOfWeek is DayOfWeek.Sunday)
            day = day.AddDays(1);

        var slotsResp = await _client.GetAsync($"/api/appointments/slots?serviceId={service.Id}&date={day:yyyy-MM-dd}");
        slotsResp.EnsureSuccessStatusCode();
        var slots = await slotsResp.Content.ReadFromJsonAsync<SlotsDto>(JsonOpts);
        Assert.NotNull(slots);
        Assert.NotEmpty(slots.SlotsUtc);

        var book = await _client.PostAsJsonAsync("/api/appointments", new
        {
            serviceId = service.Id,
            startAtUtc = slots.SlotsUtc[0]
        });
        Assert.Equal(HttpStatusCode.OK, book.StatusCode);

        var adminLogin = await _client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            login = "admin",
            password = "admin123"
        });
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
    }

    [Fact]
    public async Task Admin_Salon_Socials_And_Templates_And_Portfolio()
    {
        var adminLogin = await _client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            login = "admin",
            password = "admin123"
        });
        adminLogin.EnsureSuccessStatusCode();
        var admin = await adminLogin.Content.ReadFromJsonAsync<AuthDto>(JsonOpts);
        Assert.NotNull(admin?.Token);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin.Token);

        var salon = await _client.GetFromJsonAsync<JsonElement>("/api/salon", JsonOpts);
        Assert.True(salon.TryGetProperty("instagramUrl", out _));
        Assert.True(salon.TryGetProperty("telegramUrl", out _));

        var templates = await _client.GetFromJsonAsync<List<JsonElement>>("/api/admin/templates", JsonOpts);
        Assert.NotNull(templates);
        Assert.True(templates.Count >= 3);
        Assert.Contains(templates, t => t.GetProperty("triggerDescription").GetString()?.Length > 0);

        var createTpl = await _client.PostAsJsonAsync("/api/admin/templates", new
        {
            key = $"promo_{Random.Shared.Next(1000, 9999)}",
            title = "Тестовый шаблон",
            triggerDescription = "Только для проверки",
            body = "Текст {Имя}"
        });
        Assert.Equal(HttpStatusCode.OK, createTpl.StatusCode);
        var createdTpl = await createTpl.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var tplId = createdTpl.GetProperty("id").GetGuid();

        var delTpl = await _client.DeleteAsync($"/api/admin/templates/{tplId}");
        Assert.Equal(HttpStatusCode.NoContent, delTpl.StatusCode);

        var createSvc = await _client.PostAsJsonAsync("/api/services", new
        {
            name = "Тест услуга",
            description = "desc",
            price = 1000,
            durationMinutes = 30,
            isActive = true,
            sortOrder = 99
        });
        createSvc.EnsureSuccessStatusCode();
        var svc = await createSvc.Content.ReadFromJsonAsync<ServiceDto>(JsonOpts);
        Assert.NotNull(svc);

        var createPort = await _client.PostAsJsonAsync("/api/admin/portfolio", new
        {
            title = "Тест портфолио",
            description = "desc",
            imageUrl = "https://example.com/a.jpg",
            serviceId = svc.Id,
            displayPrice = 1000,
            sortOrder = 1
        });
        Assert.Equal(HttpStatusCode.OK, createPort.StatusCode);
        var port = await createPort.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var portId = port.GetProperty("id").GetGuid();

        var delPort = await _client.DeleteAsync($"/api/admin/portfolio/{portId}");
        Assert.Equal(HttpStatusCode.NoContent, delPort.StatusCode);

        var delSvc = await _client.DeleteAsync($"/api/services/{svc.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delSvc.StatusCode);
    }

    private sealed record AuthDto(string Token, string Role, string Name, string? Phone, Guid UserId);
    private sealed record ServiceDto(Guid Id, string Name, decimal Price, int DurationMinutes);
    private sealed record SlotsDto(Guid ServiceId, string Date, List<DateTime> SlotsUtc);
}
