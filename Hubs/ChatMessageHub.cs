using Microsoft.AspNetCore.SignalR;

namespace ChatHub.Hubs;

public class ChatMessageHub : Hub
{
    // Skickar ett meddelande till alla anslutna klienter
    public async Task SendMessage(string username, string message)
    {
        await Clients.All.SendAsync(
            "ReceiveMessage",
            username,
            message);
    }
}
