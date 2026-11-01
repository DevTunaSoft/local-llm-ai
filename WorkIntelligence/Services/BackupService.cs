using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace WorkIntelligence.Services;

public sealed class BackupService
{
    private readonly KnowledgeStore store;
    private readonly Func<string, string> _resolve;

    public BackupService(KnowledgeStore store) : this(store, RuntimeBundle.Resolve) { }

    internal BackupService(KnowledgeStore store, Func<string, string> resolve)
    {
        this.store = store;
        _resolve = resolve;
    }

    public async Task<string> BackupAsync(string repository, string backupPassword, CancellationToken cancellationToken)
    {
        Validate(repository, backupPassword);
        var restic = _resolve("restic.exe");
        repository = Path.GetFullPath(repository);
        var appRoot = Path.GetFullPath(LocalPaths.Root);
        if (repository.Equals(appRoot, StringComparison.OrdinalIgnoreCase) ||
            repository.StartsWith(appRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup repository cannot be located inside the active data folder.");
        using var repositoryLock = await FileOperationLock.AcquireAsync(FileOperationLock.RepositoryKey(repository), cancellationToken);
        Directory.CreateDirectory(repository);
        var staging = Path.Combine(LocalPaths.Root, "Snapshots", Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(staging, "payload");
        Directory.CreateDirectory(payload);
        try
        {
            await Task.Run(() => store.CreateBackupSnapshot(payload, cancellationToken), cancellationToken);
            if (!File.Exists(Path.Combine(repository, "config")))
                await RunAsync(restic, repository, backupPassword, ["init"], cancellationToken);
            var summary = await RunAsync(restic, repository, backupPassword,
                ["backup", payload, "--tag", "work-intelligence", "--json"], cancellationToken);
            var snapshotId = ReadSnapshotId(summary);
            // 'check' alone verifies repository metadata, not the backed-up file contents.
            await RunAsync(restic, repository, backupPassword, ["check", "--read-data"], cancellationToken);
            var verificationTarget = Path.Combine(staging, "verification");
            Directory.CreateDirectory(verificationTarget);
            await RunAsync(restic, repository, backupPassword,
                ["restore", snapshotId, "--target", verificationTarget, "--verify"], cancellationToken);
            var restored = Directory.GetFiles(verificationTarget, "vault.json", SearchOption.AllDirectories);
            if (restored.Length != 1) throw new InvalidDataException("Restored backup has no unique vault metadata.");
            var restoredPayload = Path.GetDirectoryName(restored[0])!;
            foreach (var sourceFile in Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(payload, sourceFile);
                var targetFile = Path.Combine(restoredPayload, relative);
                if (!File.Exists(targetFile)) throw new InvalidDataException("Restored snapshot is missing a file.");
                using var original = File.OpenRead(sourceFile);
                using var recovered = File.OpenRead(targetFile);
                if (!SHA256.HashData(original).AsSpan().SequenceEqual(SHA256.HashData(recovered)))
                    throw new InvalidDataException("Restored snapshot did not match its source contents.");
            }
            // This verifies only a local restic repository, NOT remote OneDrive arrival.
            AuditLog.Info("Backup", "Local encrypted backup read, restored and byte-verified.");
            return "Local encrypted backup restored and verified. OneDrive transfer and second-PC recovery remain UNVERIFIED.";
        }
        finally { try { Directory.Delete(staging, recursive: true); } catch (Exception ex) { AuditLog.Error("BackupCleanup", ex); } }
    }

    public static Task RestoreToEmptyVaultAsync(string repository, string backupPassword, CancellationToken cancellationToken)
        => RestoreToEmptyVaultAsync(repository, backupPassword, cancellationToken, RuntimeBundle.Resolve);

    internal static async Task RestoreToEmptyVaultAsync(string repository, string backupPassword, CancellationToken cancellationToken,
        Func<string, string> resolve)
    {
        Validate(repository, backupPassword);
        LocalPaths.Ensure();
        if (File.Exists(LocalPaths.VaultConfig) || File.Exists(LocalPaths.Database))
            throw new InvalidOperationException("Restore requires an empty vault. Existing knowledge will never be overwritten by this recovery workflow.");
        var restic=resolve("restic.exe");
        using var repositoryLock = await FileOperationLock.AcquireAsync(FileOperationLock.RepositoryKey(repository), cancellationToken);
        var destination=Path.Combine(LocalPaths.Root,"RestoreStaging",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        try
        {
            await RunAsync(restic, Path.GetFullPath(repository), backupPassword, ["check"], cancellationToken);
            await RunAsync(restic, Path.GetFullPath(repository), backupPassword,
                ["restore","latest","--tag","work-intelligence","--target",destination,"--verify"], cancellationToken);
            var vaultFiles=Directory.GetFiles(destination,"vault.json",SearchOption.AllDirectories);
            if (vaultFiles.Length != 1) throw new InvalidDataException("Cannot locate a unique vault configuration in the restored snapshot.");
            var payload=Path.GetDirectoryName(vaultFiles[0])!;
            var db=Path.Combine(payload,"knowledge.db");
            if (!File.Exists(db) || new FileInfo(db).Length==0) throw new InvalidDataException("Restored database missing.");
            var blobSource=Path.Combine(payload,"Blobs");
            var existingBlobs=Directory.GetFiles(LocalPaths.Blobs,"*",SearchOption.TopDirectoryOnly);
            if (existingBlobs.Length>0) throw new InvalidOperationException("Destination has existing attachments; manual reconciliation required.");
            LocalPaths.Ensure();
            var created = new List<string>();
            try
            {
                // Settings copied last: incomplete recovery cannot be presented as an unlocked vault.
                if (Directory.Exists(blobSource)) foreach(var file in Directory.GetFiles(blobSource,"*.enc"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var target=Path.Combine(LocalPaths.Blobs,Path.GetFileName(file));
                    File.Copy(file,target,overwrite:false);created.Add(target);
                }
                File.Copy(db,LocalPaths.Database,overwrite:false);created.Add(LocalPaths.Database);
                File.Copy(vaultFiles[0],LocalPaths.VaultConfig,overwrite:false);created.Add(LocalPaths.VaultConfig);
                AuditLog.Info("Restore","Encrypted vault files recovered into previously empty destination.");
            }
            catch
            {
                foreach (var createdFile in created.AsEnumerable().Reverse())
                    try { File.Delete(createdFile); } catch(Exception cleanupEx) { AuditLog.Error("RestoreRollback",cleanupEx); }
                throw;
            }
        }
        finally { try { Directory.Delete(destination,true); } catch(Exception ex) { AuditLog.Error("RestoreCleanup",ex); } }
    }

    private static void Validate(string repository, string backupPassword)
    {
        if (string.IsNullOrWhiteSpace(repository)) throw new ArgumentException("Choose an approved local OneDrive-synchronized or offline repository folder.");
        if (backupPassword is null || backupPassword.Length < 16) throw new ArgumentException("Backup passphrase must be at least 16 characters.");
    }

    internal static string ReadSnapshotId(string output)
    {
        string? id = null;
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("message_type", out var type) && type.GetString() == "summary")
            {
                if (id is not null) throw new InvalidDataException("Backup returned ambiguous snapshot summaries.");
                if (!doc.RootElement.TryGetProperty("snapshot_id", out var value) ||
                    value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: 64 } snapshot ||
                    !snapshot.All(Uri.IsHexDigit))
                    throw new InvalidDataException("Backup did not return a valid snapshot identifier.");
                id = snapshot;
            }
        }
        return id ?? throw new InvalidDataException("Backup did not confirm creation of a snapshot.");
    }

    private static async Task<string> RunAsync(string executable, string repository, string password, IEnumerable<string> arguments, CancellationToken ct)
    {
        var start=new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        start.Environment["RESTIC_PASSWORD"]=password;
        start.ArgumentList.Add("--repo"); start.ArgumentList.Add(repository);
        foreach(var arg in arguments) start.ArgumentList.Add(arg);
        using var proc=new Process {StartInfo=start};
        if (!proc.Start()) throw new InvalidOperationException("Could not start bundled backup engine.");
        using var registration=ct.Register(() => ProcessTermination.RequestStop(proc));
        var stdoutTask=proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask=proc.StandardError.ReadToEndAsync(ct);
        await ProcessTermination.WaitForExitAsync(proc, ct);
        var stdout=await stdoutTask;
        var stderr=await stderrTask;
        if(proc.ExitCode != 0)
        {
            AuditLog.Info("Restic", $"Backup command returned exit code {proc.ExitCode}");
            // Never include arbitrary command output (possible file paths or data) in persistent logs.
            throw new InvalidOperationException($"Backup engine failed (code {proc.ExitCode}). Review the local backup diagnostics; command output has been suppressed to protect confidential paths.");
        }
        _=stderr;
        return stdout;
    }

}
