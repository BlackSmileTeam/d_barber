using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Barber.Infrastructure.Services;

public class JwtTokenService(IConfiguration config)
{
    public string CreateToken(Guid userId, string role, string name, string? phone = null)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing");
        var issuer = config["Jwt:Issuer"] ?? "DBarberApi";
        var audience = config["Jwt:Audience"] ?? "DBarberClient";
        var hours = int.TryParse(config["Jwt:ExpireHours"], out var h) ? h : 72;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, name)
        };
        if (!string.IsNullOrWhiteSpace(phone))
            claims.Add(new Claim("phone", phone));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
