using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChatHub.Models;
using Microsoft.IdentityModel.Tokens;

namespace ChatHub.Services;

public class JwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        // Hämtar konfiguration (user secrets / appsettings)
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        // Skapar en symmetrisk nyckel från den hemliga JWT-nyckeln
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

        // Anger att token ska signeras med HMAC-SHA256
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        // Claims = information om användaren som bäddas in i token
        var claims = new[]
        {
            // Användarens ID används för authorization
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()
            ),

            // Användarnamnet används för visning i chatten
            new Claim(
                ClaimTypes.Name,
                user.Username
            )
        };

        // Skapar själva JWT-token med issuer, audience, claims och utgångstid
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials);

        // Serialiserar token till en sträng som klienten kan använda
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}