using System.Security.Claims;
using ChatHub.Data;
using ChatHub.DTOs;
using ChatHub.Models;
using ChatHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatHub.Hubs;

// Endast inloggade användare får ansluta
[Authorize]
public class ChatMessageHub : Hub
{
    private readonly ChatDbContext _db;
    private readonly ChatEncryptionService _encryption;

    public ChatMessageHub(
        ChatDbContext db,
        ChatEncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }


    // =========================
// Global chatt
// =========================

public async Task SendMessage(
    EncryptedMessageDto encryptedMessage)
{
    var username =
        Context.User?.Identity?.Name;

    if (string.IsNullOrWhiteSpace(username))
    {
        throw new HubException(
            "Användaren kunde inte identifieras."
        );
    }

    // Kontrollera det krypterade meddelandet
    ValidateEncryptedMessage(
        encryptedMessage
    );

    /*
        Servern vidarebefordrar endast ciphertext.
        Servern behöver inte dekryptera meddelandet.
    */
    await Clients.All.SendAsync(
        "ReceiveMessage",
        username,
        encryptedMessage
    );
}


    // =========================
    // Hämta rum
    // =========================

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


    // =========================
    // Skapa rum
    // =========================

    public async Task<RoomDto> CreateRoom(
        string roomName,
        bool isPrivate)
    {
        var userId = GetCurrentUserId();

        roomName = roomName.Trim();

        // Validera rumsnamn
        if (string.IsNullOrWhiteSpace(roomName))
        {
            throw new HubException(
                "Rumsnamn krävs."
            );
        }

        if (roomName.Length > 100)
        {
            throw new HubException(
                "Rumsnamnet får vara max 100 tecken."
            );
        }

        // Kontrollera att namnet är unikt
        var roomExists =
            await _db.ChatRooms
                .AnyAsync(room =>
                    room.Name == roomName);

        if (roomExists)
        {
            throw new HubException(
                "Ett rum med det namnet finns redan."
            );
        }

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


    // =========================
    // Gå med i rum
    // =========================

    public async Task JoinRoom(int roomId)
    {
        var userId = GetCurrentUserId();

        var room = await _db.ChatRooms
            .Include(room => room.Members)
            .FirstOrDefaultAsync(
                room => room.Id == roomId
            );

        if (room is null)
        {
            throw new HubException(
                "Rummet kunde inte hittas."
            );
        }

        // Kontrollera om användaren redan är medlem
        var isMember =
            room.Members.Any(member =>
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


    // =========================
    // Bjud in användare
    // =========================

    public async Task InviteUserToRoom(
        int roomId,
        string invitedUsername)
    {
        var currentUserId =
            GetCurrentUserId();

        invitedUsername =
            invitedUsername.Trim();

        if (string.IsNullOrWhiteSpace(invitedUsername))
        {
            throw new HubException(
                "Användarnamn krävs."
            );
        }

        // Hämta rummet och dess medlemmar
        var room = await _db.ChatRooms
            .Include(room => room.Members)
            .FirstOrDefaultAsync(
                room => room.Id == roomId
            );

        if (room is null)
        {
            throw new HubException(
                "Rummet kunde inte hittas."
            );
        }

        // Inbjudningar används endast för privata rum
        if (!room.IsPrivate)
        {
            throw new HubException(
                "Endast privata rum kräver inbjudningar."
            );
        }

        // Endast rummets ägare får bjuda in
        if (room.CreatedByUserId != currentUserId)
        {
            throw new HubException(
                "Endast rummets ägare får bjuda in användare."
            );
        }

        // Hitta användaren i databasen
        var invitedUser =
            await _db.Users
                .FirstOrDefaultAsync(user =>
                    user.Username.ToLower() ==
                    invitedUsername.ToLower());

        if (invitedUser is null)
        {
            throw new HubException(
                "Användaren kunde inte hittas."
            );
        }

        // Kontrollera om användaren redan är medlem
        var alreadyMember =
            room.Members.Any(member =>
                member.UserId == invitedUser.Id);

        if (alreadyMember)
        {
            throw new HubException(
                "Användaren är redan medlem i rummet."
            );
        }

        // Lägg till användaren som medlem
        _db.ChatRoomMembers.Add(
            new ChatRoomMember
            {
                ChatRoomId = room.Id,
                UserId = invitedUser.Id
            }
        );

        await _db.SaveChangesAsync();

        // Skicka notifiering om användaren är online
        await Clients
            .User(invitedUser.Id.ToString())
            .SendAsync(
                "ReceiveRoomInvitation",
                new RoomDto
                {
                    Id = room.Id,
                    Name = room.Name,
                    IsPrivate = room.IsPrivate,
                    IsOwner = false
                }
            );
    }


    // =========================
    // Lämna SignalR-rum
    // =========================

    public async Task LeaveRoom(int roomId)
    {
        // Tar endast bort den aktuella anslutningen
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetGroupName(roomId)
        );
    }


    // =========================
    // Skicka rumsmeddelande
    // =========================

    public async Task SendRoomMessage(
        int roomId,
        EncryptedMessageDto encryptedMessage)
    {
        var userId =
            GetCurrentUserId();

        // Kontrollera det krypterade meddelandet
        ValidateEncryptedMessage(
            encryptedMessage
        );

        // Kontrollera medlemskap
        var isMember =
            await _db.ChatRoomMembers
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
        {
            throw new HubException(
                "Användaren kunde inte identifieras."
            );
        }

    /*
        Servern skickar ciphertext endast
        till SignalR-gruppen för rummet.
    */
    await Clients
        .Group(GetGroupName(roomId))
        .SendAsync(
            "ReceiveRoomMessage",
            roomId,
            username,
            encryptedMessage
        );
    }


    // =========================
    // Krypteringssession
    // =========================

    public KeyExchangeResponse ExchangeEncryptionPublicKey(
        string clientPublicKey)
    {
        if (string.IsNullOrWhiteSpace(clientPublicKey))
        {
            throw new HubException(
                "Publik nyckel saknas."
            );
        }

        try
        {
            /*
                Klienten skickar endast sin publika ECDH-nyckel.
                Servern skapar därefter en gemensam sessionsnyckel.
            */
            return _encryption.CreateSession(
                Context.ConnectionId,
                clientPublicKey
            );
        }
        catch (FormatException)
        {
            throw new HubException(
                "Den publika nyckeln är ogiltig."
            );
        }
        catch (Exception exception)
            when (exception is not HubException)
        {
            throw new HubException(
                "Nyckelutbytet misslyckades."
            );
        }
    }


    // =========================
    // Hämta AES-nyckel
    // =========================

    public async Task<EncryptedKeyDto>
        GetEncryptedChannelKey(int? roomId)
    {
        var userId = GetCurrentUserId();

        // General använder en gemensam kanalnyckel
        if (roomId is null)
        {
            return GetEncryptedChannelKey(
                "general"
            );
        }

        var room = await _db.ChatRooms
            .Include(room => room.Members)
            .FirstOrDefaultAsync(
                room => room.Id == roomId.Value
            );

        if (room is null)
        {
            throw new HubException(
                "Rummet kunde inte hittas."
            );
        }

        // Privata rum kräver medlemskap
        if (room.IsPrivate)
        {
            var isMember =
                room.Members.Any(member =>
                    member.UserId == userId);

            if (!isMember)
            {
                throw new HubException(
                    "Du har inte behörighet till rummets krypteringsnyckel."
                );
            }
        }

        return GetEncryptedChannelKey(
            GetGroupName(room.Id)
        );
    }


    // =========================
    // Anslutning
    // =========================

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


    // =========================
    // Frånkoppling
    // =========================

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        // Ta bort krypteringssessionen
        _encryption.RemoveSession(
            Context.ConnectionId
        );

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

        await base.OnDisconnectedAsync(
            exception
        );
    }


    // =========================
    // Hjälpmetoder
    // =========================

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


    private EncryptedKeyDto GetEncryptedChannelKey(
        string channelId)
    {
        try
        {
            /*
                Kanalens AES-nyckel krypteras med
                sessionsnyckeln från ECDH.
            */
            return _encryption
                .GetEncryptedChannelKey(
                    Context.ConnectionId,
                    channelId
                );
        }
        catch (InvalidOperationException)
        {
            throw new HubException(
                "Krypteringssession saknas. Gör ett nytt nyckelutbyte."
            );
        }
    }
    private static void ValidateEncryptedMessage(
    EncryptedMessageDto message)
    {
        if (
            message is null ||
            string.IsNullOrWhiteSpace(message.Iv) ||
            string.IsNullOrWhiteSpace(message.Ciphertext)
        )
        {
            throw new HubException(
                "Det krypterade meddelandet är ogiltigt."
            );
        }

        try
        {
            var iv =
                Convert.FromBase64String(
                    message.Iv
                );

            var ciphertext =
                Convert.FromBase64String(
                    message.Ciphertext
                );

            // AES-GCM använder 12 bytes IV
            if (iv.Length != 12)
            {
                throw new HubException(
                    "Meddelandets IV är ogiltigt."
                );
            }

            /*
                Begränsar storleken även om servern
                inte kan läsa klartexten.
            */
            if (
                ciphertext.Length < 16 ||
                ciphertext.Length > 4096
            )
            {
                throw new HubException(
                    "Det krypterade meddelandet har ogiltig storlek."
                );
            }
        }
        catch (FormatException)
        {
            throw new HubException(
                "Det krypterade meddelandet har ogiltigt format."
            );
        }
    }
}