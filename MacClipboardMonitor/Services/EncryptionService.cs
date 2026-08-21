using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MacClipboardMonitor.Services;

// Cifrado/descifrado AES-256-CBC de las entradas de texto encriptadas.
// La clave se deriva de una contraseña fija con PBKDF2 y una sal fija.
public static class EncryptionService
{
    private const string Password = "NAyT78HkUFKvLi81njDpX2)EB71JjNV(KwFWU2TB#";

    private static readonly byte[] Salt = { 205, 154, 25, 96, 86, 25, 0, 8 };

    public static string? Encrypt(string? plainText)
    {
        if (plainText is null) return null;

        var bytesToBeEncrypted = Encoding.UTF8.GetBytes(plainText);
        var passwordBytes = SHA512.HashData(Encoding.UTF8.GetBytes(Password));

        var bytesEncrypted = Encrypt(bytesToBeEncrypted, passwordBytes);
        return Convert.ToBase64String(bytesEncrypted);
    }

    public static string? Decrypt(string? encryptedText)
    {
        if (encryptedText is null) return null;

        var bytesToBeDecrypted = Convert.FromBase64String(encryptedText);
        var passwordBytes = SHA512.HashData(Encoding.UTF8.GetBytes(Password));

        var bytesDecrypted = Decrypt(bytesToBeDecrypted, passwordBytes);
        return Encoding.UTF8.GetString(bytesDecrypted);
    }

    private static byte[] Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes)
    {
        using var ms = new MemoryStream();
        using var aes = CreateAes(passwordBytes);

        using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cs.Write(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
        cs.FlushFinalBlock();

        return ms.ToArray();
    }

    private static byte[] Decrypt(byte[] bytesToBeDecrypted, byte[] passwordBytes)
    {
        using var ms = new MemoryStream();
        using var aes = CreateAes(passwordBytes);

        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
        cs.Write(bytesToBeDecrypted, 0, bytesToBeDecrypted.Length);
        cs.FlushFinalBlock();

        return ms.ToArray();
    }

    // Aes.Create() equivale a RijndaelManaged con bloque de 128 bits (AES),
    // evitando la API obsoleta.
    private static Aes CreateAes(byte[] passwordBytes)
    {
        // SHA1 es el hash por defecto del constructor original; se especifica
        // explícitamente para mantener la compatibilidad y evitar la API obsoleta.
        var key = new Rfc2898DeriveBytes(passwordBytes, Salt, 1000, HashAlgorithmName.SHA1);

        var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = key.GetBytes(aes.KeySize / 8);
        aes.IV = key.GetBytes(aes.BlockSize / 8);
        aes.Mode = CipherMode.CBC;

        return aes;
    }
}
