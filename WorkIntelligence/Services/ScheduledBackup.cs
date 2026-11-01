using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorkIntelligence.Services;

/// <summary>Unattended user-session backups when the WPF window is closed.
/// DPAPI secrets are bound to the current Windows account, NOT portable.
/// Backups themselves remain portable using independently retained passphrases.</summary>
public static class ScheduledBackup
{
    private sealed record Credentials(string VaultPassword,string BackupPassword,string Repository,string? OneDriveRemote);
    private const string TaskName="WorkIntelligence-UserDailyBackup";
    private static string SecretsPath=>Path.Combine(LocalPaths.Data,"scheduled-backup.dpapi");

    public static async Task ConfigureAsync(string vaultPassword,string backupPassword,string repository,string? remote,CancellationToken ct)
    {
        if(vaultPassword.Length<16 || backupPassword.Length<16)
            throw new ArgumentException("Both vault and backup passphrases must be at least 16 characters.");
        if(!Vault.Exists)throw new InvalidOperationException("Unlock and initialize your vault before enabling scheduled backup.");
        using(var vault=Vault.Open(vaultPassword))
        using(var store=new KnowledgeStore(vault))
        {
            if(store.CheckDatabase()!="ok")throw new InvalidDataException("Vault failed integrity check.");
        }
        var executable=Environment.ProcessPath;
        if(string.IsNullOrWhiteSpace(executable)||!File.Exists(executable))
            throw new InvalidOperationException("Cannot register scheduled backup without a stable executable path.");
        LocalPaths.Ensure();
        var settings=JsonSerializer.SerializeToUtf8Bytes(new Credentials(vaultPassword,backupPassword,Path.GetFullPath(repository),
            string.IsNullOrWhiteSpace(remote)?null:remote.Trim()));
        byte[] protectedBytes;
        try{protectedBytes=ProtectedData.Protect(settings,null,DataProtectionScope.CurrentUser);}
        finally{CryptographicOperations.ZeroMemory(settings);}
        var temp=SecretsPath+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp,protectedBytes,ct);
            // /IT executes only while this Windows user is signed in. A logged-out job is never promised.
            // Windows Task Scheduler is installed with Windows; no additional runtime is required.
            await RunTaskSchedulerAsync(["/Create","/F","/SC","DAILY","/ST","02:00",
                "/TN",TaskName,"/TR",$"\"{executable}\" --scheduled-backup","/IT"],ct);
            // Preserve the previous working credentials if registration fails or is canceled.
            File.Move(temp,SecretsPath,true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if(File.Exists(temp))File.Delete(temp);
        }
    }

    public static async Task RemoveAsync(CancellationToken ct)
    {
        // A failed deletion must not discard the only credentials while the scheduled
        // task remains installed: preserve the working configuration for recovery.
        // A task reported missing by schtasks is an error requiring reconciliation.
        await RunTaskSchedulerAsync(["/Delete","/F","/TN",TaskName],ct);
        if(File.Exists(SecretsPath)) File.Delete(SecretsPath);
    }

    public static async Task RunJobAsync(CancellationToken ct)
    {
        if(!File.Exists(SecretsPath))throw new InvalidOperationException("No scheduled backup credentials are configured.");
        var stored=await File.ReadAllBytesAsync(SecretsPath,ct);
        byte[] clear=ProtectedData.Unprotect(stored,null,DataProtectionScope.CurrentUser);
        Credentials credentials;
        try { credentials=JsonSerializer.Deserialize<Credentials>(clear) ?? throw new InvalidDataException("Scheduled backup settings missing."); }
        finally{CryptographicOperations.ZeroMemory(clear);}
        if(!Vault.Exists)throw new InvalidOperationException("Scheduled backup requires an existing local vault.");
        using var vault=Vault.Open(credentials.VaultPassword);
        using var store=new KnowledgeStore(vault);
        await new BackupService(store).BackupAsync(credentials.Repository,credentials.BackupPassword,ct);
        if(!string.IsNullOrWhiteSpace(credentials.OneDriveRemote))
            await RemoteBackupService.SyncAndVerifyAsync(credentials.Repository,credentials.OneDriveRemote,ct);
        AuditLog.Info("ScheduledBackup",string.IsNullOrEmpty(credentials.OneDriveRemote)
            ?"Scheduled local encrypted repository verified; OneDrive not configured."
            :"Scheduled local+remote encrypted repositories byte-verified.");
    }
    private static async Task RunTaskSchedulerAsync(IEnumerable<string> args,CancellationToken ct)
    {
        var info=new ProcessStartInfo("schtasks.exe") {UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args) info.ArgumentList.Add(arg);
        using var process=new Process {StartInfo=info};
        if(!process.Start())throw new InvalidOperationException("Windows Task Scheduler did not start.");
        using var reg=ct.Register(() => ProcessTermination.RequestStop(process));
        var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);
        await ProcessTermination.WaitForExitAsync(process,ct);_=await stdout;_=await stderr;
        if(process.ExitCode!=0)throw new InvalidOperationException($"Windows Task Scheduler registration failed (code {process.ExitCode}).");
    }
}
