namespace ChatHub.DTOs;

public class KeyExchangeResponse
{
    public string ServerPublicKey { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;
}