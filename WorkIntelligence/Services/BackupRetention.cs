namespace WorkIntelligence.Services;

/// <summary>
/// Explicit, conservative local plaintext snapshot retention. No pruning happens
/// unless the user has enabled a positive keep count, and a snapshot is never
/// deleted without integrity verification and at least one surviving valid copy.
/// </summary>
public static class BackupRetention
{
    internal static bool IsOverdue(DateTimeOffset? lastSuccess, DateTimeOffset now)
        => lastSuccess is null || now - lastSuccess.Value >= TimeSpan.FromHours(25);

    public static int Prune(string repository, int keepNewest, CancellationToken ct)
    {
        if (keepNewest is < 2 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(keepNewest), "Keep at least two recovery points.");
        if (string.IsNullOrWhiteSpace(repository) || !Directory.Exists(repository))
            throw new DirectoryNotFoundException("Approved backup repository does not exist.");
        var root = Path.GetFullPath(repository);
        PathSafety.RejectReparsePoints(root);
        var snapshots = Directory.EnumerateDirectories(root, "snap-*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        // Never delete unverifiable or corrupt snapshots automatically: those
        // require manual inspection so we cannot mistakenly erase recovery data.
        var valid = new List<string>();
        foreach (var path in snapshots)
        {
            ct.ThrowIfCancellationRequested();
            try { BackupService.VerifySnapshot(path, ct); valid.Add(path); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
                or System.Text.Json.JsonException)
            {
                AuditLog.Info("BackupRetention", "Corrupt snapshot excluded from automatic deletion.");
            }
        }
        if (valid.Count <= keepNewest) return 0;
        var removed = 0;
        foreach (var path in valid.Skip(keepNewest))
        {
            ct.ThrowIfCancellationRequested();
            PathSafety.RejectReparsePoints(path);
            Directory.Delete(path, recursive: true);
            removed++;
        }
        return removed;
    }
}
