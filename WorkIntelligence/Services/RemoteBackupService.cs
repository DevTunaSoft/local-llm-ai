using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WorkIntelligence.Services;

/// <summary>Copies verified PLAINTEXT snapshots to an explicitly configured organizational OneDrive remote.</summary>
public static class RemoteBackupService
{
    // Requires a configured and explicitly authorized rclone OneDrive remote.
    public static async Task SyncAndVerifyAsync(string localRepository,string remote,CancellationToken ct)
    {
        ValidateRemote(remote);
        using var repositoryLock = await FileOperationLock.AcquireAsync(FileOperationLock.RepositoryKey(localRepository), ct);
        if(!Directory.Exists(localRepository) || !Directory.EnumerateDirectories(localRepository, "snap-*").Any())
            throw new InvalidDataException("No plaintext snapshots exist. Create a verified local backup first.");
        var rclone=RuntimeBundle.Resolve("rclone.exe");
        await RequireOneDriveBackendAsync(rclone, remote, ct);
        await RunAsync(rclone,["copy",Path.GetFullPath(localRepository),remote,"--transfers","4","--checkers","8"],ct);
        // Compare both sides byte-for-byte. Remote copies are plaintext, not protected by this app.
        await RunAsync(rclone,["check",Path.GetFullPath(localRepository),remote,"--download","--one-way"],ct);
        AuditLog.Info("OneDrive", "Plaintext remote repository byte-verification completed.");
    }
    public static async Task RestoreRemoteToEmptyVaultAsync(string remote,CancellationToken ct)
    {
        ValidateRemote(remote);
        LocalPaths.Ensure();
        if(File.Exists(LocalPaths.VaultConfig) || File.Exists(LocalPaths.Database) || Directory.EnumerateFileSystemEntries(LocalPaths.Blobs).Any())
            throw new InvalidOperationException("Remote recovery requires an empty local database and attachment folder.");
        var temp=Path.Combine(Path.GetTempPath(),"WorkIntelligence-RemoteRestore-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var rclone=RuntimeBundle.Resolve("rclone.exe");
            await RequireOneDriveBackendAsync(rclone, remote, ct);
            await RunAsync(rclone,["copy",remote,temp,"--transfers","4"],ct);
            // A successful transfer alone is not acceptance evidence. Byte-check
            // every downloaded snapshot against the authorized remote BEFORE
            // writing any files into the actual local vault.
            await RunAsync(rclone, BuildRestoreVerificationArgs(remote, temp), ct);
            await BackupService.RestoreToEmptyVaultAsync(temp,ct);
        }
        finally { try {Directory.Delete(temp,recursive:true);}catch(Exception ex){AuditLog.Error("RemoteRestoreCleanup",ex);} }
    }
    private static void ValidateRemote(string remote)
    {
        if(string.IsNullOrWhiteSpace(remote) || remote.Length>255 ||
           !Regex.IsMatch(remote,@"^[A-Za-z0-9_-]{1,64}:[A-Za-z0-9_./-]{2,180}$") ||
           remote.Contains("..",StringComparison.Ordinal) || remote.EndsWith(':') || remote.EndsWith('/'))
           throw new ArgumentException("Choose an existing configured rclone OneDrive remote and a dedicated subfolder, e.g., workonedrive:WIBackups.");
    }
    // This exact command is used as the gate before restoring into a live vault.
    // Preserve source/destination order: the authorized OneDrive remote must be
    // the source, and every source byte must match the staged download.
    internal static string[] BuildRestoreVerificationArgs(string remote, string stagedDirectory)
    {
        ValidateRemote(remote);
        if (string.IsNullOrWhiteSpace(stagedDirectory) || !Path.IsPathFullyQualified(stagedDirectory))
            throw new ArgumentException("Staged restore location must be absolute.", nameof(stagedDirectory));
        return ["check", remote, stagedDirectory, "--download", "--one-way"];
    }

    internal static bool IsOneDriveConfig(string remoteName, string redactedConfig)
    {
        if (redactedConfig.Length > 32768) return false;
        var lines = redactedConfig.Split('\n');
        var section = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line == "[" + remoteName + "]";
                continue;
            }
            if (section && Regex.IsMatch(line, @"^type\s*=\s*onedrive$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return true;
        }
        return false;
    }

    private static async Task RequireOneDriveBackendAsync(string executable, string remote, CancellationToken ct)
    {
        var remoteName = remote.Split(':', 2)[0];
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var value in new[] { "config", "redacted", remoteName, "--ask-password=false" })
            info.ArgumentList.Add(value);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("Cannot verify OneDrive remote configuration.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var registration = timeout.Token.Register(() => ProcessTermination.RequestStop(process));
        var outputTask = BoundedProcessOutput.ReadAsync(process.StandardOutput, 32768, timeout);
        var errorTask = BoundedProcessOutput.ReadAsync(process.StandardError, 32768, timeout);
        try
        {
            await Task.WhenAll(ProcessTermination.WaitForExitAsync(process, timeout.Token), outputTask, errorTask);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("OneDrive provider validation exceeded its safe time limit.");
        }
        var redacted = await outputTask;
        _ = await errorTask; // Never reveal config, remotes, account names, tokens, or error output.
        if (process.ExitCode != 0 || !IsOneDriveConfig(remoteName, redacted))
            throw new InvalidOperationException("Configured backup remote must be a direct Microsoft OneDrive backend.");
    }

    private static async Task RunAsync(string executable,IEnumerable<string> args,CancellationToken ct)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args)info.ArgumentList.Add(arg);
        using var proc=new Process{StartInfo=info};
        if(!proc.Start())throw new InvalidOperationException("Bundled OneDrive transfer utility did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        using var registration=timeout.Token.Register(() => ProcessTermination.RequestStop(proc));
        var outTask=BoundedProcessOutput.ReadAsync(proc.StandardOutput, 262144, timeout);
        var errTask=BoundedProcessOutput.ReadAsync(proc.StandardError, 262144, timeout);
        try
        {
            await Task.WhenAll(ProcessTermination.WaitForExitAsync(proc, timeout.Token), outTask, errTask);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("OneDrive copy or verification timed out; remote backup is unverified.");
        }
        _=await outTask;_=await errTask; // Never log remote paths, account info or tokens.
        if(proc.ExitCode!=0)throw new InvalidOperationException($"OneDrive plaintext copy/check failed (code {proc.ExitCode}); backup is not remotely verified.");
    }
}
