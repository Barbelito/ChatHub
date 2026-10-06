using ChatHub.Hubs;
using ChatHub.Data;
using ChatHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Registrerar SignalR
builder.Services.AddSignalR();

// Konfigurerar Kestrel
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureEndpointDefaults(endpoint =>
        endpoint.Protocols = HttpProtocols.Http1));

        
// Registrera databasen
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite("Data Source=chatbook.db"));

// Registrera hasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Aktiverar autentisering och anger att JWT ska vara standardmetoden
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Regler för hur inkommande JWT-tokens ska valideras
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,

                // Kontrollera att signaturen är korrekt (dvs rätt hemlig nyckel)
                ValidateIssuerSigningKey = true,

                // Värden hämtas från user secrets / appsettings
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],

                // Skapar en symmetrisk nyckel från den hemliga JWT-nyckel
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:Key"]!
                        ))
            };
    });


// Aktiverar authorization (krav på roller, policies, [Authorize]-attribut)
builder.Services.AddAuthorization();


var app = builder.Build();


app.MapPost("/api/register", async (
    RegisterRequest request,
    ChatDbContext db,
    IPasswordHasher<User> passwordHasher) =>
{
    // Trimma användarnamnet för att undvika mellanslag-problem
    var username = request.Username.Trim();

    // Grundläggande validering av input
    if (string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest("Användarnamn och lösenord krävs.");
    }

    // Kontrollera om användarnamnet redan finns i databasen
    var usernameExists = await db.Users
        .AnyAsync(user => user.Username == username);

    if (usernameExists)
    {
        return Results.BadRequest("Användarnamnet finns redan.");
    }

    // Skapa en ny användare
    var user = new User
    {
        Username = username
    };

    // Hasha lösenordet innan det sparas
    user.PasswordHash = passwordHasher.HashPassword(
        user,
        request.Password
    );

    // Lägg till användaren i databasen
    db.Users.Add(user);

    // Spara ändringarna
    await db.SaveChangesAsync();

    // Bekräfta att kontot skapades
    return Results.Ok("Kontot skapades.");
});



// Serverar filer från wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Mappar SignalR-hubben
app.MapHub<ChatMessageHub>("/chatHub");

app.Run();