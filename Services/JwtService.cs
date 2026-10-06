using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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

    public string GenerateToken(string username)
    {
        // Skapar en symmetrisk nyckel från den hemliga JWT-nyckel
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

        // Anger att token ska signeras med HMAC-SHA256
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        // Claims = information om användaren som bäddas in i token
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username)
        };

        // Skapar själva JWT-token med issuer, audience, claims och utgångstid
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],     
            audience: _configuration["Jwt:Audience"], 
            claims: claims,                           
            expires: DateTime.UtcNow.AddHours(12),    
            signingCredentials: credentials);         // Signering med den hemliga nyckel

        // Serialiserar token till en sträng som klienten kan använda
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
