using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ChatHub.DTOs;

namespace ChatHub.Services;

public class ChatEncryptionService
{
    /*
        Sessionsnycklar används mellan
        servern och varje SignalR-anslutning.
    */
    private readonly ConcurrentDictionary<string, byte[]>
        _connectionKeys = new();

    /*
        Varje chattrum får en egen AES-nyckel.
        Nycklarna ligger endast i serverns minne.
    */
    private readonly ConcurrentDictionary<string, byte[]>
        _channelKeys = new();


    // =========================
    // ECDH-nyckelutbyte
    // =========================

    public KeyExchangeResponse CreateSession(
        string connectionId,
        string clientPublicKey)
    {
        // Läs klientens publika ECDH-nyckel
        var clientPublicKeyBytes =
            Convert.FromBase64String(
                clientPublicKey
            );

        using var clientEcdh =
            ECDiffieHellman.Create();

        clientEcdh.ImportSubjectPublicKeyInfo(
            clientPublicKeyBytes,
            out _
        );


        /*
            Servern skapar ett nytt tillfälligt
            ECDH-nyckelpar för anslutningen.
        */
        using var serverEcdh =
            ECDiffieHellman.Create(
                ECCurve.NamedCurves.nistP256
            );


        /*
            Serverns privata nyckel kombineras
            med klientens publika nyckel.

            Klienten kan göra samma beräkning
            med sin privata nyckel och serverns
            publika nyckel.
        */
        var sharedSecret =
            serverEcdh.DeriveRawSecretAgreement(
                clientEcdh.PublicKey
            );


        // Slumpmässigt salt för HKDF
        var salt =
            RandomNumberGenerator.GetBytes(16);


        /*
            HKDF omvandlar ECDH-hemligheten
            till en 256-bitars sessionsnyckel.
        */
        var sessionKey =
            HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                sharedSecret,
                32,
                salt,
                Encoding.UTF8.GetBytes(
                    "ChatBook-Key-Wrap-v1"
                )
            );


        // Rensa gammal sessionsnyckel
        if (
            _connectionKeys.TryRemove(
                connectionId,
                out var oldKey
            )
        )
        {
            CryptographicOperations.ZeroMemory(
                oldKey
            );
        }


        // Spara den nya sessionsnyckeln
        _connectionKeys[connectionId] =
            sessionKey;


        // Exportera endast serverns publika nyckel
        var serverPublicKey =
            serverEcdh
                .ExportSubjectPublicKeyInfo();


        // Rensa ECDH-hemligheten från minnet
        CryptographicOperations.ZeroMemory(
            sharedSecret
        );


        return new KeyExchangeResponse
        {
            ServerPublicKey =
                Convert.ToBase64String(
                    serverPublicKey
                ),

            Salt =
                Convert.ToBase64String(
                    salt
                )
        };
    }


    // =========================
    // Kryptera kanalnyckel
    // =========================

    public EncryptedKeyDto GetEncryptedChannelKey(
        string connectionId,
        string channelId)
    {
        /*
            Klienten måste först ha gjort
            ECDH-nyckelutbytet.
        */
        if (
            !_connectionKeys.TryGetValue(
                connectionId,
                out var sessionKey
            )
        )
        {
            throw new InvalidOperationException(
                "Ingen krypteringssession finns."
            );
        }


        /*
            Skapa kanalens AES-nyckel första
            gången den efterfrågas.

            32 bytes = AES-256.
        */
        var channelKey =
            _channelKeys.GetOrAdd(
                channelId,
                _ =>
                    RandomNumberGenerator
                        .GetBytes(32)
            );


        /*
            AES-GCM behöver ett unikt nonce/IV
            för varje kryptering med samma nyckel.
        */
        var iv =
            RandomNumberGenerator.GetBytes(12);

        var ciphertext =
            new byte[channelKey.Length];

        var tag =
            new byte[16];


        using var aes =
            new AesGcm(
                sessionKey,
                16
            );


        /*
            Kanalnyckeln krypteras med
            sessionsnyckeln från ECDH.
        */
        aes.Encrypt(
            iv,
            channelKey,
            ciphertext,
            tag
        );


        return new EncryptedKeyDto
        {
            Iv =
                Convert.ToBase64String(iv),

            Ciphertext =
                Convert.ToBase64String(
                    ciphertext
                ),

            Tag =
                Convert.ToBase64String(tag)
        };
    }


    // =========================
    // Ta bort session
    // =========================

    public void RemoveSession(
        string connectionId)
    {
        if (
            _connectionKeys.TryRemove(
                connectionId,
                out var key
            )
        )
        {
            // Rensa nyckeln från minnet
            CryptographicOperations.ZeroMemory(
                key
            );
        }
    }
}