namespace ChatHub.DTOs;

public class EncryptedKeyDto
{
    public string Iv { get; set; } = string.Empty;

    public string Ciphertext { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;
}