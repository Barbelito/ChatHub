using ChatHub.Hubs;
using ChatHub.Data;
using ChatHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Text;
using ChatHub.Services;


var builder = WebApplication.CreateBuilder(args);

// Registrerar SignalR
builder.Services.AddSignalR();

// Registrerar ECDH och AES-nyckelhantering
builder.Services.AddSingleton<ChatEncryptionService>();

// Konfigurerar Kestrel (stöd för både HTTP/1.1 och HTTP/2)
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureEndpointDefaults(endpoint =>
        endpoint.Protocols = HttpProtocols.Http1AndHttp2));

// Registrerar JWT service
builder.Services.AddScoped<JwtService>();

// Rate limiting för chatten
builder.Services.AddSingleton<ChatRateLimiter>();

// Registrera databasen
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite("Data Source=chatbook.db"));

// Registrera hasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// CORS – behövs för frontend + SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("https://localhost")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Aktiverar autentisering och anger JWT Bearer som standardmetod
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Regler för hur inkommande JWT-tokens ska verifieras
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,

                // Kontrollerar att token har signerats med rätt hemliga nyckel
                ValidateIssuerSigningKey = true,

                // Token blir ogiltig direkt när giltighetstiden går ut
                ClockSkew = TimeSpan.Zero,

                // Issuer och Audience hämtas från konfigurationen
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],

                // Den hemliga JWT-nyckeln hämtas från User Secrets
                // och används för att verifiera token-signaturen
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:Key"]!
                        ))
            };

        // SignalR kan skicka JWT som "access_token" när en WebSocket-anslutning etableras
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Försöker läsa token från query string
                var accessToken =
                    context.Request.Query["access_token"];

                // Hämtar vilken endpoint klienten försöker ansluta till
                var path =
                    context.HttpContext.Request.Path;

                // Använd endast query-token för SignalR-hubben
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

// =========================
// Rate limiting
// =========================

builder.Services.AddRateLimiter(options =>
{
    // HTTP-status när gränsen nås
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    // Login och registrering
    options.AddPolicy(
        "auth",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        // Max 5 försök per minut
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }
            )
    );

    // SignalR-anslutningar
    options.AddPolicy(
        "signalr",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        // Max 30 anslutningsförsök per minut
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }
            )
    );

    // Svar när användaren når gränsen
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (!context.HttpContext.Response.HasStarted)
        {
            await context.HttpContext.Response.WriteAsJsonAsync(
                new
                {
                    message = "För många försök. Försök igen senare."
                },
                cancellationToken
            );
        }
    };
});

// Aktiverar authorization (krav på roller, policies, [Authorize]-attribut)
builder.Services.AddAuthorization();


var app = builder.Build();

// Omdirigerar HTTP till HTTPS
app.UseHttpsRedirection();

// Aktiverar routing
app.UseRouting();

// Aktiverar CORS
app.UseCors();

// Aktiverar rate limiting
app.UseRateLimiter();

// API - Registrering
app.MapPost("/api/register", async (
    RegisterRequest request,
    ChatDbContext db,
    IPasswordHasher<User> passwordHasher) =>
{
    // Trimma användarnamnet för att undvika mellanslag
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
})
.RequireRateLimiting("auth");

// API - Inloggning
app.MapPost("/api/login", async (
    LoginRequest request,
    ChatDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtService jwtService) =>
{
    // Trimma användarnamnet
    var username = request.Username.Trim();

    // Hämta användaren
    var user = await db.Users
        .FirstOrDefaultAsync(u => u.Username == username);

    // Samma felmeddelande används oavsett om användaren finns eller inte
    if (user is null)
        return Results.BadRequest("Fel användarnamn eller lösenord.");

    // Verifiera lösenordet
    var result = passwordHasher.VerifyHashedPassword(
        user,
        user.PasswordHash,
        request.Password);

    if (result == PasswordVerificationResult.Failed)
        return Results.BadRequest("Fel användarnamn eller lösenord.");

    // Token skapas
    var token = jwtService.GenerateToken(user);

    // Token returneras till klienten
    return Results.Ok(new
    {
        token
    });
})
.RequireRateLimiting("auth");

// Serverar filer från wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Aktiverar autentisering och authorization
app.UseAuthentication();
app.UseAuthorization();

// Mappar SignalR-hubben
app.MapHub<ChatMessageHub>("/chatHub")
    .RequireRateLimiting("signalr");

app.Run();