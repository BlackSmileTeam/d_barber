using System.Security.Claims;
using System.Text;
using Barber.Api.Workers;
using Barber.Infrastructure;
using Barber.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ReminderHostedService>();
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "DBarber_Dev_Secret_Key_Change_Me_32chars!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "DBarberApi",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "DBarberClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", p =>
    {
        var origins = new List<string>
        {
            "http://localhost:5173",
            "http://127.0.0.1:5173",
            "http://localhost:4173",
            "http://139.100.225.234:55333"
        };
        var publicUrl = builder.Configuration["App:FrontendPublicUrl"];
        if (!string.IsNullOrWhiteSpace(publicUrl))
            origins.Add(publicUrl.TrimEnd('/'));

        p.WithOrigins(origins.Distinct().ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(wwwroot))
    Directory.CreateDirectory(wwwroot);
Directory.CreateDirectory(Path.Combine(wwwroot, "uploads"));
app.Environment.WebRootPath = wwwroot;

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BarberDbContext>();
    try
    {
        await DbSeeder.SeedAsync(db);
    }
    catch (Exception ex) when (LooksLikeMysqlAccessDenied(ex))
    {
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        log.LogCritical(
            ex,
            "MySQL Access Denied for the configured app user (often wrong GitHub secret password, " +
            "or user exists only as @localhost while Docker connects from 172.18.x). " +
            "Fix MySQL grants/password (database/05_fix_docker_access.sql) and DB_CONNECTION_STRING, then redeploy.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("frontend");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static bool LooksLikeMysqlAccessDenied(Exception ex)
{
    for (var e = ex; e != null; e = e.InnerException)
    {
        var msg = e.Message;
        if (msg.Contains("Access denied for user", StringComparison.OrdinalIgnoreCase)
            || (msg.Contains("caching_sha2_password", StringComparison.OrdinalIgnoreCase)
                && msg.Contains("Authentication to host", StringComparison.OrdinalIgnoreCase)))
            return true;
    }

    return false;
}

public partial class Program;
