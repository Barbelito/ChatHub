using Microsoft.AspNetCore.SignalR;

namespace ChatHub.Hubs;

// SignalR‑hub som hanterar chatmeddelanden
public class ChatMessageHub : Hub
{
    // Skickar ett meddelande till alla klienter
    public async Task SendMessage(string username, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", username, message);
    }

    // Registrerar användaren och skickar systemmeddelande
    public async Task JoinChat(string username)
    {
        Context.Items["Username"] = username;

        await Clients.All.SendAsync(
            "ReceiveSystemMessage",
            $"{username} anslöt till chatten.");
    }

    // Skickas när en användare kopplas bort
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("Username", out var username))
        {
            await Clients.All.SendAsync(
                "ReceiveSystemMessage",
                $"{username} lämnade chatten.");
        }

        await base.OnDisconnectedAsync(exception);
    }
}
