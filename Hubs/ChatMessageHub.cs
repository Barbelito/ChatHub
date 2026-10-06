using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatHub.Hubs;

// SignalR-hub som hanterar all kommunikation mellan klienter
[Authorize] // Endast användare med en giltig JWT-token får ansluta
public class ChatMessageHub : Hub
{
    // Skickar ett meddelande till alla anslutna klienter
    public async Task SendMessage(string message)
    {
        // Hämtar användarnamnet från den inloggade användarens JWT
        var username = Context.User?.Identity?.Name;

        // Avbryt om användaren inte kunde identifieras
        if (string.IsNullOrWhiteSpace(username))
            return;

        await Clients.All.SendAsync(
            "ReceiveMessage",
            username,
            message);
    }

    // Körs när en användare ansluter till hubben
    public override async Task OnConnectedAsync()
    {
        // Hämtar användarnamnet från JWT
        var username = Context.User?.Identity?.Name;

        // Spara användarnamnet så att det kan användas vid frånkoppling
        Context.Items["Username"] = username;

        // Skicka ett systemmeddelande till alla användare
        if (!string.IsNullOrWhiteSpace(username))
        {
            await Clients.All.SendAsync(
                "ReceiveSystemMessage",
                $"{username} anslöt till chatten.");
        }

        await base.OnConnectedAsync();
    }

    // Körs när en användare kopplas bort
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Hämta användarnamnet som sparades vid anslutning
        if (Context.Items.TryGetValue("Username", out var username))
        {
            await Clients.All.SendAsync(
                "ReceiveSystemMessage",
                $"{username} lämnade chatten.");
        }

        await base.OnDisconnectedAsync(exception);
    }
}