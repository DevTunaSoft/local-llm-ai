using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorkIntelligence.Services;

public sealed class Vault : IDisposable
{
    private sealed record Settings(int Schema, string Salt, int Iterations, string Nonce, string Verifier);
    private readonly byte[] _dbKey;
    private readonly byte[] _fileKey;
    private bool _disposed;
    public string DbPassword => !_disposed ? Convert.ToHexString(_dbKey) : throw new ObjectDisposedException(nameof(Vault));
    private Vault(byte[] dbKey, byte[] fileKey) { _dbKey = dbKey; _fileKey = fileKey; }
    public static bool Exists => File.Exists(LocalPaths.VaultConfig);

    public static Vault Open(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("A passphrase is required.");
        LocalPaths.Ensure();
        if (!Exists && File.Exists(LocalPaths.Database)) throw new InvalidOperationException("Database exists but vault configuration is missing. Restore configuration instead of creating a new key.");
        if (!Exists)
        {
            if (password.Length < 16) throw new ArgumentException("New vault passphrase must be at least 16 characters.");
            Initialize(password);
        }
        if (new FileInfo(LocalPaths.VaultConfig).Length > 4096)
            throw new InvalidDataException("Vault configuration exceeds the size limit.");
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(LocalPaths.VaultConfig)) ?? throw new InvalidDataException("Vault configuration missing.");
        if (settings.Schema != 1 || settings.Iterations is < 310_000 or > 2_000_000)
            throw new InvalidDataException("Unsupported or unsafe vault configuration.");
        byte[] salt, verifier, nonce;
        try
        {
            salt = Convert.FromBase64String(settings.Salt);
            verifier = Convert.FromBase64String(settings.Verifier);
            nonce = Convert.FromBase64String(settings.Nonce);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentNullException)
        {
            throw new InvalidDataException("Malformed vault configuration.", ex);
        }
        if (salt.Length != 32 || nonce.Length != 12 || verifier.Length != "WorkIntelligence-v1"u8.Length + 16)
            throw new InvalidDataException("Malformed vault configuration.");
        var root = Rfc2898DeriveBytes.Pbkdf2(password, salt, settings.Iterations, HashAlgorithmName.SHA256, 64);
        var dbKey = root.AsSpan(0, 32).ToArray();
        var fileKey = root.AsSpan(32, 32).ToArray();
        CryptographicOperations.ZeroMemory(root);
        try
        {
            var plain = new byte[verifier.Length - 16];
            using var aes = new AesGcm(fileKey, 16);
            aes.Decrypt(nonce, verifier.AsSpan(0, plain.Length), verifier.AsSpan(plain.Length, 16), plain);
            var valid = CryptographicOperations.FixedTimeEquals(plain, Encoding.UTF8.GetBytes("WorkIntelligence-v1"));
            CryptographicOperations.ZeroMemory(plain);
            if (!valid) throw new CryptographicException("Incorrect passphrase or damaged vault.");
            return new Vault(dbKey, fileKey);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(dbKey);
            CryptographicOperations.ZeroMemory(fileKey);
            throw new CryptographicException("Incorrect passphrase or damaged vault.");
        }
    }

    private static void Initialize(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        const int iterations = 500_000;
        var root = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 64);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var text = Encoding.UTF8.GetBytes("WorkIntelligence-v1");
        var cipher = new byte[text.Length + 16];
        using (var aes = new AesGcm(root.AsSpan(32, 32), 16))
            aes.Encrypt(nonce, text, cipher.AsSpan(0, text.Length), cipher.AsSpan(text.Length));
        CryptographicOperations.ZeroMemory(root);
        var config = new Settings(1, Convert.ToBase64String(salt), iterations, Convert.ToBase64String(nonce), Convert.ToBase64String(cipher));
        var destination = LocalPaths.VaultConfig;
        var temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, config);
                stream.Flush(true);
            }
            File.Move(temp, destination);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    // Restricted to 25 MiB imported files; protects attachments while at rest.
    public byte[] Encrypt(ReadOnlySpan<byte> source)
    {
        ThrowIfDisposed();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var result = new byte[4 + 12 + source.Length + 16];
        Encoding.ASCII.GetBytes("WIA1").CopyTo(result, 0);
        nonce.CopyTo(result, 4);
        using var aes = new AesGcm(_fileKey, 16);
        aes.Encrypt(nonce, source, result.AsSpan(16, source.Length), result.AsSpan(16 + source.Length, 16));
        return result;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> source)
    {
        ThrowIfDisposed();
        if (source.Length < 32 || !source[..4].SequenceEqual("WIA1"u8)) throw new InvalidDataException("Invalid attachment format.");
        var plain = new byte[source.Length - 32];
        using var aes = new AesGcm(_fileKey, 16);
        aes.Decrypt(source.Slice(4, 12), source.Slice(16, plain.Length), source.Slice(16 + plain.Length, 16), plain);
        return plain;
    }
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(Vault)); }
    public void Dispose() { if (_disposed) return; _disposed = true; CryptographicOperations.ZeroMemory(_dbKey); CryptographicOperations.ZeroMemory(_fileKey); }
}
