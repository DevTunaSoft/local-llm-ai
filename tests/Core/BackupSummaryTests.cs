using System.Security.Cryptography;
using System.Text.Json;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class BackupManifestTests
{
    private static string PrepareSnapshot(string root)
    {
        Directory.CreateDirectory(root);
        var database = Path.Combine(root, "knowledge.db");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            KnowledgeStore.InitializeSchema(connection);
        }
        var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(database)));
        var manifest = new
        {
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            Files = new[] { new { Path = "knowledge.db", Length = new FileInfo(database).Length, Sha256 = fileHash } }
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest));
        return database;
    }

    [Fact]
    public void ManifestVerifiesKnownPlaintextSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-manifest-" + Guid.NewGuid().ToString("N"));
        try
        {
            PrepareSnapshot(root);
            BackupService.VerifySnapshot(root, CancellationToken.None);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ManifestRejectsModifiedBytesEvenIfFileLengthIsUnchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-manifest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = PrepareSnapshot(root);
            var bytes = File.ReadAllBytes(db);
            bytes[0] ^= 1;
            File.WriteAllBytes(db, bytes);
            Assert.Throws<InvalidDataException>(() => BackupService.VerifySnapshot(root, CancellationToken.None));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ManifestRejectsUnlistedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-manifest-" + Guid.NewGuid().ToString("N"));
        try
        {
            PrepareSnapshot(root);
            File.WriteAllText(Path.Combine(root, "unexpected.txt"), "Synthetic unrelated content");
            Assert.Throws<InvalidDataException>(() => BackupService.VerifySnapshot(root, CancellationToken.None));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
