using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace WorkIntelligence.Services;

/// <summary>Unencrypted, versioned snapshots. Integrity hashes detect corruption; they do not provide confidentiality.</summary>
public sealed class BackupService
{
    private sealed record ManifestFile(string Path, long Length, string Sha256);
    private sealed record SnapshotManifest(int Version, string CreatedAt, List<ManifestFile> Files);
    private readonly KnowledgeStore _store;
    public BackupService(KnowledgeStore store) => _store = store;

    public async Task<string> BackupAsync(string repository, CancellationToken ct)
    {
        var location = ValidateRepository(repository);
        using var repositoryLock = await FileOperationLock.AcquireAsync(FileOperationLock.RepositoryKey(location), ct);
        Directory.CreateDirectory(location);
        PathSafety.RejectReparsePoints(location);
        var staging = Path.Combine(location, ".pending-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            await Task.Run(() => _store.CreateBackupSnapshot(staging, ct), ct);
            var manifest = BuildManifest(staging, ct);
            var manifestPath = Path.Combine(staging, "manifest.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest), ct);
            VerifySnapshot(staging, ct);
            var completed = Path.Combine(location, "snap-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + Guid.NewGuid().ToString("N"));
            Directory.Move(staging, completed);
            VerifySnapshot(completed, ct);
            AuditLog.Info("Backup", "Plaintext snapshot created and SHA-256 verified locally.");
            return "Plaintext versioned snapshot created and verified locally. Files are NOT encrypted; remote recovery is not yet verified.";
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    public static async Task RestoreToEmptyVaultAsync(string repository, CancellationToken ct)
    {
        var location = ValidateRepository(repository);
        LocalPaths.Ensure();
        LegacyEncryptedDataGuard.AssertNoLegacyData();
        if (File.Exists(LocalPaths.Database) || Directory.EnumerateFileSystemEntries(LocalPaths.Blobs).Any())
            throw new InvalidOperationException("Restore requires an empty data directory. Existing data will never be overwritten.");
        using var repositoryLock = await FileOperationLock.AcquireAsync(FileOperationLock.RepositoryKey(location), ct);
        if (!Directory.Exists(location)) throw new DirectoryNotFoundException("Backup repository not found.");
        PathSafety.RejectReparsePoints(location);
        var snapshot = SelectLatestVerifiedSnapshot(location, ct);
        var blobsPath = Path.Combine(snapshot, "Blobs");
        var files = Directory.Exists(blobsPath) ? Directory.EnumerateFiles(blobsPath, "*.bin", SearchOption.TopDirectoryOnly).ToArray() : Array.Empty<string>();
        var created = new List<string>();
        try
        {
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var destination = Path.Combine(LocalPaths.Blobs, Path.GetFileName(file));
                File.Copy(file, destination, false);
                created.Add(destination);
            }
            var targetDatabase = LocalPaths.Database;
            File.Copy(Path.Combine(snapshot, "knowledge.db"), targetDatabase, false);
            created.Add(targetDatabase);
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = targetDatabase, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check";
            if (command.ExecuteScalar()?.ToString() != "ok") throw new InvalidDataException("Restored database failed integrity check.");
            AuditLog.Info("Restore", "Verified plaintext snapshot restored into an empty data directory.");
        }
        catch
        {
            foreach (var createdFile in created.AsEnumerable().Reverse())
            {
                try { File.Delete(createdFile); } catch (Exception e) { AuditLog.Error("RestoreRollback", e); }
            }
            throw;
        }
        await Task.CompletedTask;
    }

    // If the newest backup is incomplete/corrupt, recover from the newest valid
    // version instead of discarding all older recovery points.
    internal static string SelectLatestVerifiedSnapshot(string repository, CancellationToken ct)
    {
        if (!Directory.Exists(repository)) throw new DirectoryNotFoundException("Backup repository not found.");
        PathSafety.RejectReparsePoints(repository);
        var snapshots = Directory.EnumerateDirectories(repository, "snap-*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(x => Path.GetFileName(x), StringComparer.Ordinal).ToArray();
        foreach (var snapshot in snapshots)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                VerifySnapshot(snapshot, ct);
                return snapshot;
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException or UnauthorizedAccessException)
            {
                AuditLog.Info("Restore", "A snapshot failed integrity or access checks; trying an older recovery point.");
            }
        }
        throw new InvalidDataException("No valid plaintext snapshot could be verified. Existing data remains unchanged.");
    }

    private static string ValidateRepository(string repository)
    {
        if (string.IsNullOrWhiteSpace(repository)) throw new ArgumentException("Choose an organization-approved backup directory.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository));
        var dataRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(LocalPaths.Root));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (full.Equals(dataRoot, comparison) || full.StartsWith(dataRoot + Path.DirectorySeparatorChar, comparison)
            || dataRoot.StartsWith(full + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("Backup repository must be separate from the application data directory.");
        PathSafety.RejectReparsePoints(full);
        return full;
    }

    private static SnapshotManifest BuildManifest(string snapshot, CancellationToken ct)
    {
        var files = Directory.EnumerateFiles(snapshot, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal)
            .Select(path =>
            {
                ct.ThrowIfCancellationRequested();
                PathSafety.RejectReparsePoints(path);
                var relative = Path.GetRelativePath(snapshot, path).Replace('\\', '/');
                if (!IsSafeDataPath(relative)) throw new InvalidDataException("Unsafe backup payload filename.");
                using var input = File.OpenRead(path);
                return new ManifestFile(relative, input.Length, Convert.ToHexString(SHA256.HashData(input)));
            }).ToList();
        if (files.Count == 0 || files.All(x => x.Path != "knowledge.db")) throw new InvalidDataException("Backup has no database.");
        return new SnapshotManifest(1, DateTimeOffset.UtcNow.ToString("O"), files);
    }

    private static bool IsSafeDataPath(string path) => path == "knowledge.db" ||
        Regex.IsMatch(path, @"^Blobs/[0-9]+\.bin$", RegexOptions.CultureInvariant);

    internal static void VerifySnapshot(string snapshot, CancellationToken ct)
    {
        PathSafety.RejectReparsePoints(snapshot);
        var manifestPath = Path.Combine(snapshot, "manifest.json");
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 4 * 1024 * 1024)
            throw new InvalidDataException("Snapshot manifest is missing or oversized.");
        var manifest = JsonSerializer.Deserialize<SnapshotManifest>(File.ReadAllText(manifestPath))
            ?? throw new InvalidDataException("Snapshot manifest malformed.");
        if (manifest.Version != 1 || manifest.Files is null || manifest.Files.Count is < 1 or > 100000)
            throw new InvalidDataException("Unsupported or invalid snapshot manifest.");
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            if (record is null || !IsSafeDataPath(record.Path) || !listed.Add(record.Path) || record.Length < 0 ||
                record.Sha256 is null || record.Sha256.Length != 64 || !record.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("Snapshot contains invalid or duplicate file records.");
            var file = Path.Combine(snapshot, record.Path.Replace('/', Path.DirectorySeparatorChar));
            PathSafety.RejectReparsePoints(file);
            if (!File.Exists(file) || new FileInfo(file).Length != record.Length)
                throw new InvalidDataException("Snapshot is incomplete.");
            using var stream = File.OpenRead(file);
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(record.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Snapshot checksum mismatch.");
        }
        if (!listed.Contains("knowledge.db")) throw new InvalidDataException("Snapshot missing the database.");
        var found = Directory.EnumerateFiles(snapshot, "*", SearchOption.AllDirectories)
            .Select(x => Path.GetRelativePath(snapshot, x).Replace('\\', '/')).ToArray();
        if (found.Length != listed.Count + 1 || found.Any(x => x != "manifest.json" && !listed.Contains(x)))
            throw new InvalidDataException("Snapshot contains unexpected or unverified files.");
        using var db = File.OpenRead(Path.Combine(snapshot, "knowledge.db"));
        Span<byte> header = stackalloc byte[16];
        if (db.Read(header) != 16 || !header.SequenceEqual("SQLite format 3\0"u8))
            throw new InvalidDataException("Snapshot database is not standard SQLite.");
    }
}
