namespace ChatHub.DTOs;

public class EncryptedMessageDto
{
    public string Iv { get; set; } = string.Empty;

    // Innehåller ciphertext och AES-GCM authentication tag
    public string Ciphertext { get; set; } = string.Empty;
}