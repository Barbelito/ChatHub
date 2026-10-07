namespace ChatHub.DTOs;

public class RoomDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }

    public bool IsOwner { get; set; }
}