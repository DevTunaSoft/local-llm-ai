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
        using var repositoryLock = FileOperationLock.Acquire(FileOperationLock.RepositoryKey(root), ct);
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
    internal sealed record Point(string Path,DateTimeOffset Created,long Bytes);
    public sealed record CalendarResult(int Removed,int Retained,long VerifiedBytes,bool QuotaExceeded);
    internal static HashSet<string> KeepCalendar(IReadOnlyList<Point> points,CalendarRetention policy)
    {
        policy.Validate();
        var ordered=points.OrderByDescending(x=>x.Created).ThenByDescending(x=>x.Path,StringComparer.Ordinal).ToArray();
        var keep=ordered.Take(policy.KeepNewest).Select(x=>x.Path).ToHashSet(StringComparer.Ordinal);
        void Buckets(Func<Point,string> key,int count)
        {foreach(var point in ordered.GroupBy(key).Take(count).Select(x=>x.First()))keep.Add(point.Path);}
        Buckets(x=>x.Created.UtcDateTime.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),policy.Daily);
        Buckets(x=>x.Created.UtcDateTime.ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture),policy.Monthly);
        Buckets(x=>x.Created.UtcDateTime.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),policy.Yearly);
        return keep;
    }
    public static CalendarResult PruneCalendar(string repository,CalendarRetention policy,CancellationToken ct)
    {
        policy.Validate();var root=BackupService.ValidateRepository(repository);
        using var repositoryLock=FileOperationLock.Acquire(FileOperationLock.RepositoryKey(root),ct);
        if(!Directory.Exists(root))throw new DirectoryNotFoundException("Backup repository is missing.");
        var points=new List<Point>();
        foreach(var snapshot in Directory.EnumerateDirectories(root,"snap-*",SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                BackupService.VerifySnapshot(snapshot,ct);
                var bytes=Directory.EnumerateFiles(snapshot,"*",SearchOption.AllDirectories).Sum(x=>new FileInfo(x).Length);
                points.Add(new(snapshot,BackupService.SnapshotTime(snapshot),bytes));
            }
            catch(Exception ex) when(ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {AuditLog.Info("BackupRetention","Unverifiable snapshot retained for manual inspection.");}
        }
        var keep=KeepCalendar(points,policy);var removed=0;
        foreach(var point in points.Where(x=>!keep.Contains(x.Path)))
        {ct.ThrowIfCancellationRequested();BackupService.VerifySnapshot(point.Path,ct);Directory.Delete(point.Path,true);removed++;}
        var retained=points.Where(x=>keep.Contains(x.Path)).ToArray();var remaining=retained.Sum(x=>x.Bytes);
        return new(removed,retained.Length,remaining,policy.QuotaBytes is long quota && remaining>quota);
    }

}
