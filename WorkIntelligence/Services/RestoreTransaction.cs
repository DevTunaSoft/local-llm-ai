using System.Text.Json;

namespace WorkIntelligence.Services;

/// <summary>Recoverable directory promotion into an empty data root. The source snapshot is never modified.</summary>
internal static class RestoreTransaction
{
    private sealed record Journal(string Id);
    internal static Action<string>? Checkpoint {get;set;} // deterministic process-termination seam used only by the test helper
    internal static string JournalPath=>Path.Combine(LocalPaths.Root,".restore-transaction.json");

    internal static void AssertEmptyData(string data)
    {
        PathSafety.RejectReparsePoints(data);
        if(!Directory.Exists(data))return;
        foreach(var entry in Directory.EnumerateFileSystemEntries(data))
        {
            PathSafety.RejectReparsePoints(entry);
            if(Path.GetFileName(entry)!="Blobs" || !Directory.Exists(entry) || Directory.EnumerateFileSystemEntries(entry).Any())
                throw new InvalidOperationException("Restore requires an empty data folder, including settings and SQLite journals. Existing files are preserved; choose a new data location.");
        }
    }
    internal static void RecoverIfPending()
    {
        PathSafety.RejectReparsePoints(JournalPath);
        if(!File.Exists(JournalPath))return;
        using var fileLock=FileOperationLock.Acquire("vault-files");
        if(File.Exists(JournalPath))Complete();
    }
    internal static void Promote(string staging,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        AssertEmptyData(LocalPaths.Data);
        var name=Path.GetFileName(staging);
        if(!string.Equals(Path.GetDirectoryName(Path.GetFullPath(staging)),Path.TrimEndingDirectorySeparator(LocalPaths.Root),
                OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) || !name.StartsWith(".restore-",StringComparison.Ordinal)
            || !Guid.TryParseExact(name[9..],"N",out _))throw new InvalidDataException("Restore staging location is invalid.");
        BackupService.VerifySnapshot(staging,ct);
        ct.ThrowIfCancellationRequested();
        PathSafety.RejectReparsePoints(JournalPath);
        using(var journal=new FileStream(JournalPath,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {
            JsonSerializer.Serialize(journal,new Journal(name[9..]));journal.Flush(true);
        }
        // Once the durable journal exists, finish promotion instead of returning
        // cancellation between renames. A process exit is recovered on next startup.
        Checkpoint?.Invoke("journal-created");
        Complete();
    }
    private static void Complete()
    {
        PathSafety.RejectReparsePoints(JournalPath);
        if(new FileInfo(JournalPath).Length>1024)throw new InvalidDataException("Restore transaction journal is oversized.");
        var journal=JsonSerializer.Deserialize<Journal>(File.ReadAllText(JournalPath));
        if(journal is null || !Guid.TryParseExact(journal.Id,"N",out _))throw new InvalidDataException("Restore transaction journal is malformed. No data was overwritten.");
        var staging=Path.Combine(LocalPaths.Root,".restore-"+journal.Id);
        var original=Path.Combine(LocalPaths.Root,".restore-original-"+journal.Id);
        PathSafety.RejectReparsePoints(staging);PathSafety.RejectReparsePoints(original);
        if(Directory.Exists(staging))
        {
            BackupService.VerifySnapshot(staging,CancellationToken.None);
            if(Directory.Exists(LocalPaths.Data))
            {
                AssertEmptyData(LocalPaths.Data);
                if(Directory.Exists(original))
                {AssertEmptyData(original);Directory.Delete(LocalPaths.Data,true);}
                else Directory.Move(LocalPaths.Data,original);
            }
            else if(!Directory.Exists(original))throw new InvalidDataException("Restore transaction lost its original empty directory; inspect the retained staging snapshot.");
            Checkpoint?.Invoke("original-renamed");
            Directory.Move(staging,LocalPaths.Data);
            Checkpoint?.Invoke("data-renamed");
        }
        // Only a complete byte-verified directory becomes the live data tree.
        // If another program modified it, preserve all copies and fail closed.
        BackupService.VerifySnapshot(LocalPaths.Data,CancellationToken.None);
        if(Directory.Exists(original)){AssertEmptyData(original);Directory.Delete(original,true);}
        File.Delete(JournalPath);
        File.Delete(Path.Combine(LocalPaths.Data,"manifest.json"));
    }
}
