namespace WorkIntelligence.Services;

/// <summary>Fail closed for existing encrypted-format data; never silently overwrite or delete it.</summary>
internal static class LegacyEncryptedDataGuard
{
    internal static void AssertNoLegacyData()
    {
        if (File.Exists(LocalPaths.VaultConfig))
            throw new InvalidOperationException("A legacy encrypted vault was detected. Its original files have not been changed. Export and migrate it using the older application before using the unencrypted edition.");
        if (Directory.Exists(LocalPaths.Blobs) && Directory.EnumerateFiles(LocalPaths.Blobs, "*.enc").Any())
            throw new InvalidOperationException("Legacy encrypted attachments were detected. The original files have not been changed.");
        if (!File.Exists(LocalPaths.Database)) return;
        using var stream = File.OpenRead(LocalPaths.Database);
        Span<byte> signature = stackalloc byte[16];
        if (stream.Read(signature) != signature.Length || !signature.SequenceEqual("SQLite format 3\0"u8))
            throw new InvalidOperationException("Existing database is not standard SQLite. The original data will not be modified; migrate the encrypted vault separately.");
    }
}
