using ChatHub.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Registrerar SignalR
builder.Services.AddSignalR();

// Registrera databasen
builder.Services.AddDbContext<ChatHub.Data.ChatDbContext>(options =>
    options.UseSqlite("Data Source=chatbook.db"));
    
// Konfigurerar Kestrel
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureEndpointDefaults(endpoint =>
        endpoint.Protocols = HttpProtocols.Http1));

var app = builder.Build();

// Serverar filer från wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Mappar SignalR-hubben
app.MapHub<ChatMessageHub>("/chatHub");

app.Run();