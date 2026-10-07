namespace ChatHub.Models;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public ICollection<ChatRoomMember> ChatRoomMemberships { get; set; }
        = new List<ChatRoomMember>();

    public ICollection<ChatRoom> CreatedRooms { get; set; }
        = new List<ChatRoom>();
}