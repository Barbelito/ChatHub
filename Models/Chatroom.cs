namespace ChatHub.Models;

public class ChatRoom
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }

    // Användaren som skapade rummet
    public int CreatedByUserId { get; set; }

    public User CreatedByUser { get; set; } = null!;

    // Alla användare som är medlemmar i rummet
    public ICollection<ChatRoomMember> Members { get; set; }
        = new List<ChatRoomMember>();
}