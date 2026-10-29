using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MacClipboardMonitor.Services;

// Cifrado AES-256-CBC de entradas de texto marcadas como secretas.
//
// Esto oculta el contenido en SQLite frente a una mirada casual; no es un
// cofre frente a alguien con acceso a esta cuenta de macOS. La clave v2 se
// genera al azar y se guarda en ~/.MacClipboardMonitor.key (permisos 0600).
// Las entradas antiguas (sin prefijo) se descifran con el esquema legado.
// Al copiar una entrada encriptada, el texto vuelve al portapapeles en claro.
public static class EncryptionService
{
    private const string V2Prefix = "v2:";
    private const int KeySizeBytes = 32;
    private const int AesBlockBytes = 16;

    // Esquema legado: PBKDF2-SHA1 (1000 iteraciones) con contraseña y sal fijas.
    // Solo se usa para descifrar CipherText existente sin prefijo v2.
    private const string LegacyPassword = "NAyT78HkUFKvLi81njDpX2)EB71JjNV(KwFWU2TB#";
    private static readonly byte[] LegacySalt = { 205, 154, 25, 96, 86, 25, 0, 8 };

    private static readonly object KeySync = new();
    private static byte[]? _machineKey;

    private static string KeyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".MacClipboardMonitor.key");

    public static string? Encrypt(string? plainText)
    {
        if (plainText is null) return null;

        var key = GetOrCreateMachineKey();
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var payload = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, aes.IV.Length, cipherBytes.Length);

        return V2Prefix + Convert.ToBase64String(payload);
    }

    public static string? Decrypt(string? encryptedText)
    {
        if (encryptedText is null) return null;

        if (encryptedText.StartsWith(V2Prefix, StringComparison.Ordinal))
        {
            return DecryptV2(encryptedText.Substring(V2Prefix.Length));
        }

        return DecryptLegacy(encryptedText);
    }

    private static string DecryptV2(string payloadBase64)
    {
        var payload = Convert.FromBase64String(payloadBase64);
        if (payload.Length <= AesBlockBytes)
            throw new CryptographicException("CipherText v2 inválido.");

        var key = GetOrCreateMachineKey();
        var iv = new byte[AesBlockBytes];
        var cipherBytes = new byte[payload.Length - AesBlockBytes];
        Buffer.BlockCopy(payload, 0, iv, 0, AesBlockBytes);
        Buffer.BlockCopy(payload, AesBlockBytes, cipherBytes, 0, cipherBytes.Length);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static string DecryptLegacy(string encryptedText)
    {
        var bytesToBeDecrypted = Convert.FromBase64String(encryptedText);
        var passwordBytes = SHA512.HashData(Encoding.UTF8.GetBytes(LegacyPassword));
        var bytesDecrypted = TransformLegacy(bytesToBeDecrypted, passwordBytes, encrypt: false);
        return Encoding.UTF8.GetString(bytesDecrypted);
    }

    private static byte[] TransformLegacy(byte[] data, byte[] passwordBytes, bool encrypt)
    {
        using var ms = new MemoryStream();
        using var aes = CreateLegacyAes(passwordBytes);
        using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        using var cs = new CryptoStream(ms, transform, CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    private static Aes CreateLegacyAes(byte[] passwordBytes)
    {
        var key = new Rfc2898DeriveBytes(passwordBytes, LegacySalt, 1000, HashAlgorithmName.SHA1);
        var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = key.GetBytes(aes.KeySize / 8);
        aes.IV = key.GetBytes(aes.BlockSize / 8);
        aes.Mode = CipherMode.CBC;
        return aes;
    }

    private static byte[] GetOrCreateMachineKey()
    {
        if (_machineKey is { Length: KeySizeBytes }) return _machineKey;

        lock (KeySync)
        {
            if (_machineKey is { Length: KeySizeBytes }) return _machineKey;

            try
            {
                if (File.Exists(KeyPath))
                {
                    var existing = File.ReadAllBytes(KeyPath);
                    if (existing.Length == KeySizeBytes)
                    {
                        _machineKey = existing;
                        return _machineKey;
                    }
                }
            }
            catch
            {
                // Si no se puede leer, se genera una clave nueva.
            }

            var key = RandomNumberGenerator.GetBytes(KeySizeBytes);
            var directory = Path.GetDirectoryName(KeyPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(KeyPath, key);
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(KeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            _machineKey = key;
            return _machineKey;
        }
    }
}
