using System.Security.Claims;
using ChatHub.Data;
using ChatHub.DTOs;
using ChatHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatHub.Hubs;

// Endast inloggade användare får ansluta
[Authorize]
public class ChatMessageHub : Hub
{
    private readonly ChatDbContext _db;

    public ChatMessageHub(ChatDbContext db)
    {
        _db = db;
    }

    // Global chatt

    public async Task SendMessage(string message)
    {
        var username = Context.User?.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            throw new HubException("Användaren kunde inte identifieras.");

        message = message.Trim();

        if (string.IsNullOrWhiteSpace(message))
            return;

        if (message.Length > 500)
            throw new HubException("Meddelandet är för långt.");

        await Clients.All.SendAsync(
            "ReceiveMessage",
            username,
            message
        );
    }

    // Hämtar rum användaren har tillgång till

    public async Task<List<RoomDto>> GetAvailableRooms()
    {
        var userId = GetCurrentUserId();

        // Visa offentliga rum och privata rum där användaren är medlem
        return await _db.ChatRooms
            .Where(room =>
                !room.IsPrivate ||
                room.Members.Any(member =>
                    member.UserId == userId))
            .Select(room => new RoomDto
            {
                Id = room.Id,
                Name = room.Name,
                IsPrivate = room.IsPrivate,
                IsOwner =
                    room.CreatedByUserId == userId
            })
            .OrderBy(room => room.Name)
            .ToListAsync();
    }

    // Skapar ett nytt rum

    public async Task<RoomDto> CreateRoom(
        string roomName,
        bool isPrivate)
    {
        var userId = GetCurrentUserId();

        roomName = roomName.Trim();

        // Validera rumsnamn
        if (string.IsNullOrWhiteSpace(roomName))
            throw new HubException("Rumsnamn krävs.");

        if (roomName.Length > 100)
            throw new HubException(
                "Rumsnamnet får vara max 100 tecken."
            );

        // Kontrollera att namnet är unikt
        var roomExists = await _db.ChatRooms
            .AnyAsync(room => room.Name == roomName);

        if (roomExists)
            throw new HubException(
                "Ett rum med det namnet finns redan."
            );

        var room = new ChatRoom
        {
            Name = roomName,
            IsPrivate = isPrivate,
            CreatedByUserId = userId
        };

        // Skaparen blir automatiskt medlem
        room.Members.Add(
            new ChatRoomMember
            {
                UserId = userId
            }
        );

        _db.ChatRooms.Add(room);

        await _db.SaveChangesAsync();

        // Lägg till skaparen i SignalR-gruppen
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetGroupName(room.Id)
        );

        return new RoomDto
        {
            Id = room.Id,
            Name = room.Name,
            IsPrivate = room.IsPrivate,
            IsOwner = true
        };
    }

    // Gå med i ett rum

    public async Task JoinRoom(int roomId)
    {
        var userId = GetCurrentUserId();

        var room = await _db.ChatRooms
            .Include(room => room.Members)
            .FirstOrDefaultAsync(
                room => room.Id == roomId
            );

        if (room is null)
            throw new HubException(
                "Rummet kunde inte hittas."
            );

        // Kontrollera om användaren redan är medlem
        var isMember = room.Members
            .Any(member =>
                member.UserId == userId);

        // Privata rum kräver medlemskap
        if (room.IsPrivate && !isMember)
        {
            throw new HubException(
                "Du har inte behörighet till det här rummet."
            );
        }

        // Spara medlemskap för offentliga rum
        if (!isMember)
        {
            _db.ChatRoomMembers.Add(
                new ChatRoomMember
                {
                    ChatRoomId = room.Id,
                    UserId = userId
                }
            );

            await _db.SaveChangesAsync();
        }

        // Lägg till anslutningen i SignalR-gruppen
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetGroupName(room.Id)
        );

        var username =
            Context.User?.Identity?.Name;

        if (!string.IsNullOrWhiteSpace(username))
        {
            await Clients
                .Group(GetGroupName(room.Id))
                .SendAsync(
                    "ReceiveRoomSystemMessage",
                    room.Id,
                    $"{username} gick med i rummet."
                );
        }
    }

    // Lämna ett SignalR-rum

    public async Task LeaveRoom(int roomId)
    {
        // Tar endast bort den aktuella anslutningen
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetGroupName(roomId)
        );
    }

    // Skicka meddelande till ett rum

    public async Task SendRoomMessage(
        int roomId,
        string message)
    {
        var userId = GetCurrentUserId();

        message = message.Trim();

        if (string.IsNullOrWhiteSpace(message))
            return;

        if (message.Length > 500)
            throw new HubException(
                "Meddelandet får vara max 500 tecken."
            );

        // Servern verifierar medlemskap innan meddelandet skickas
        var isMember = await _db.ChatRoomMembers
            .AnyAsync(member =>
                member.ChatRoomId == roomId &&
                member.UserId == userId);

        if (!isMember)
        {
            throw new HubException(
                "Du har inte behörighet att skicka meddelanden i det här rummet."
            );
        }

        var username =
            Context.User?.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            throw new HubException(
                "Användaren kunde inte identifieras."
            );

        // Skicka endast till användare i rummet
        await Clients
            .Group(GetGroupName(roomId))
            .SendAsync(
                "ReceiveRoomMessage",
                roomId,
                username,
                message
            );
    }

    // Körs när en användare ansluter

    public override async Task OnConnectedAsync()
    {
        var username =
            Context.User?.Identity?.Name;

        // Sparas för senare användning
        Context.Items["Username"] =
            username;

        if (!string.IsNullOrWhiteSpace(username))
        {
            await Clients.All.SendAsync(
                "ReceiveSystemMessage",
                $"{username} anslöt till chatten."
            );
        }

        await base.OnConnectedAsync();
    }

    // Körs när en användare kopplar från

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        // Hämta sparat användarnamn
        if (
            Context.Items.TryGetValue(
                "Username",
                out var username
            ) &&
            username is not null)
        {
            await Clients.All.SendAsync(
                "ReceiveSystemMessage",
                $"{username} lämnade chatten."
            );
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Hjälpmetoder

    private int GetCurrentUserId()
    {
        // UserId hämtas från den verifierade JWT-token
        var userIdClaim =
            Context.User?.FindFirst(
                ClaimTypes.NameIdentifier
            )?.Value;

        if (
            !int.TryParse(
                userIdClaim,
                out var userId
            ))
        {
            throw new HubException(
                "Användaren kunde inte identifieras."
            );
        }

        return userId;
    }

    private static string GetGroupName(
        int roomId)
    {
        // Skapar ett konsekvent gruppnamn
        return $"room-{roomId}";
    }
}