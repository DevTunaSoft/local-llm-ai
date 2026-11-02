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
            // Persist per-model local configuration with the portable vault snapshot.
            var modelSettingsPath = Path.Combine(LocalPaths.Data, "model-settings.json");
            PathSafety.RejectReparsePoints(modelSettingsPath);
            if (File.Exists(modelSettingsPath))
                File.Copy(modelSettingsPath, Path.Combine(staging, "model-settings.json"), overwrite: false);
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
        using var repositoryLock = await FileOperationLock.AcquireAsync(FileOperationLock.RepositoryKey(location), ct);
        using var vaultLock = await FileOperationLock.AcquireAsync("vault-files", ct);
        LegacyEncryptedDataGuard.AssertNoLegacyData();
        var targetSettings = Path.Combine(LocalPaths.Data, "model-settings.json");
        if (new[] { "", "-wal", "-shm", "-journal" }.Any(suffix => File.Exists(LocalPaths.Database + suffix))
            || Directory.EnumerateFileSystemEntries(LocalPaths.Blobs).Any())
            throw new InvalidOperationException("Restore requires an empty data directory. Existing data will never be overwritten.");
        if (!Directory.Exists(location)) throw new DirectoryNotFoundException("Backup repository not found.");
        var snapshot = SelectLatestVerifiedSnapshot(location, ct);
        var savedSettings = Path.Combine(snapshot, "model-settings.json");
        PathSafety.RejectReparsePoints(targetSettings);
        if (File.Exists(savedSettings) && File.Exists(targetSettings))
            throw new InvalidOperationException("Restore refuses to overwrite existing model settings.");
        // Copy and validate away from the live paths. A disk-full/cancelled copy
        // cannot leave a partial live database or attachment behind.
        var staging = Path.Combine(LocalPaths.Root, ".restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var created = new List<string>();
        try
        {
            foreach (var file in EnumerateSnapshotFiles(snapshot))
            {
                ct.ThrowIfCancellationRequested();
                var target = Path.Combine(staging, Path.GetRelativePath(snapshot, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, false);
            }
            VerifySnapshot(staging, ct);
            // Promote the database last: the app must never open a restored DB
            // whose referenced attachments have not yet been installed.
            foreach (var file in EnumerateSnapshotFiles(staging)
                .Where(x => Path.GetFileName(x) != "manifest.json")
                .OrderBy(x => Path.GetFileName(x) == "knowledge.db" ? 1 : 0))
            {
                ct.ThrowIfCancellationRequested();
                var destination = Path.Combine(LocalPaths.Data, Path.GetRelativePath(staging, file));
                PathSafety.RejectReparsePoints(destination);
                File.Move(file, destination, false);
                created.Add(destination);
            }
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
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
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

    internal static string ValidateRepository(string repository)
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

    private static bool IsSafeDataPath(string? path) => path is "knowledge.db" or "model-settings.json" ||
        path is not null && Regex.IsMatch(path, @"^Blobs/[0-9]+\.bin$", RegexOptions.CultureInvariant);

    internal static void VerifySnapshot(string snapshot, CancellationToken ct)
    {
        PathSafety.RejectReparsePoints(snapshot);
        var manifestPath = Path.Combine(snapshot, "manifest.json");
        PathSafety.RejectReparsePoints(manifestPath);
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
        var found = EnumerateSnapshotFiles(snapshot)
            .Select(x => Path.GetRelativePath(snapshot, x).Replace('\\', '/')).ToArray();
        if (found.Length != listed.Count + 1 || found.Any(x => x != "manifest.json" && !listed.Contains(x)))
            throw new InvalidDataException("Snapshot contains unexpected or unverified files.");
        using (var db = File.OpenRead(Path.Combine(snapshot, "knowledge.db")))
        {
            Span<byte> header = stackalloc byte[16];
            if (db.Read(header) != 16 || !header.SequenceEqual("SQLite format 3\0"u8))
                throw new InvalidDataException("Snapshot database is not standard SQLite.");
        }
        ct.ThrowIfCancellationRequested();
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(snapshot, "knowledge.db"), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            using var cancellation = ct.Register(() => command.Cancel());
            command.CommandText = "PRAGMA integrity_check";
            if (command.ExecuteScalar()?.ToString() != "ok")
                throw new InvalidDataException("Snapshot database failed integrity check.");
            command.CommandText = "PRAGMA foreign_key_check";
            if (command.ExecuteScalar() is not null)
                throw new InvalidDataException("Snapshot database has broken record references.");
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='documents'";
            if (Convert.ToInt32(command.ExecuteScalar()) > 0)
            {
                command.CommandText = "SELECT id,sha256 FROM documents";
                using var documents = command.ExecuteReader();
                var payloads = manifest.Files.ToDictionary(x => x.Path, StringComparer.Ordinal);
                while (documents.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    var blob = "Blobs/" + documents.GetInt64(0).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin";
                    if (!payloads.TryGetValue(blob, out var payload) || documents.IsDBNull(1) ||
                        !payload.Sha256.Equals(documents.GetString(1), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Snapshot attachments do not match the document records.");
                }
            }
        }
        catch (SqliteException ex)
        {
            ct.ThrowIfCancellationRequested();
            throw new InvalidDataException("Snapshot database could not be verified.", ex);
        }
    }

    // Do not recurse through arbitrary snapshot directories (including links).
    // The format permits only root files and one Blobs directory.
    private static IEnumerable<string> EnumerateSnapshotFiles(string snapshot)
    {
        foreach (var item in Directory.EnumerateFileSystemEntries(snapshot))
        {
            PathSafety.RejectReparsePoints(item);
            if (!Directory.Exists(item)) { yield return item; continue; }
            if (Path.GetFileName(item) != "Blobs")
                throw new InvalidDataException("Snapshot contains an unexpected directory.");
            foreach (var blob in Directory.EnumerateFileSystemEntries(item))
            {
                PathSafety.RejectReparsePoints(blob);
                if (Directory.Exists(blob)) throw new InvalidDataException("Snapshot contains a nested attachment directory.");
                yield return blob;
            }
        }
    }
}
