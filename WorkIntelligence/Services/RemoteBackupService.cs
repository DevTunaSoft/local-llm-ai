using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WorkIntelligence.Services;

/// <summary>Copy only an already encrypted restic repository. Never sends plaintext documents to OneDrive.</summary>
public static class RemoteBackupService
{
    // Requires a configured and explicitly authorized rclone OneDrive remote.
    public static async Task SyncAndVerifyAsync(string localRepository,string remote,CancellationToken ct)
    {
        ValidateRemote(remote);
        using var repositoryLock = await FileOperationLock.AcquireAsync(FileOperationLock.RepositoryKey(localRepository), ct);
        if(!File.Exists(Path.Combine(localRepository,"config")))
            throw new InvalidDataException("Local restic repository not initialized.");
        var rclone=RuntimeBundle.Resolve("rclone.exe");
        await RunAsync(rclone,["copy",Path.GetFullPath(localRepository),remote,"--transfers","4","--checkers","8"],ct);
        // Download both sides and compare bytes; never treat a sync icon as proof of correctness.
        await RunAsync(rclone,["check",Path.GetFullPath(localRepository),remote,"--download","--one-way"],ct);
        AuditLog.Info("OneDrive", "Encrypted remote repository byte-verification completed.");
    }
    public static async Task RestoreRemoteToEmptyVaultAsync(string remote,string vaultBackupPassword,CancellationToken ct)
    {
        ValidateRemote(remote);
        LocalPaths.Ensure();
        if(File.Exists(LocalPaths.VaultConfig) || File.Exists(LocalPaths.Database))
            throw new InvalidOperationException("Remote recovery requires an empty vault.");
        var temp=Path.Combine(LocalPaths.Root,"RemoteRestore",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var rclone=RuntimeBundle.Resolve("rclone.exe");
            await RunAsync(rclone,["copy",remote,temp,"--transfers","4"],ct);
            await BackupService.RestoreToEmptyVaultAsync(temp,vaultBackupPassword,ct);
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
    private static async Task RunAsync(string executable,IEnumerable<string> args,CancellationToken ct)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args)info.ArgumentList.Add(arg);
        using var proc=new Process{StartInfo=info};
        if(!proc.Start())throw new InvalidOperationException("Bundled OneDrive transfer utility did not start.");
        using var registration=ct.Register(() => ProcessTermination.RequestStop(proc));
        var outTask=proc.StandardOutput.ReadToEndAsync(ct);var errTask=proc.StandardError.ReadToEndAsync(ct);
        await ProcessTermination.WaitForExitAsync(proc, ct);
        _=await outTask;_=await errTask; // Never log remote paths, account info or tokens.
        if(proc.ExitCode!=0)throw new InvalidOperationException($"Encrypted OneDrive copy/check failed (code {proc.ExitCode}); backup is not remotely verified.");
    }
}
