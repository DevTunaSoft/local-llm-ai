using System.Diagnostics;
using System.Text.Json;

namespace WorkIntelligence.Services;

/// <summary>Unencrypted backup settings and unattended user-session snapshots. No secrets stored by this app.</summary>
public static class ScheduledBackup
{
    private sealed record Settings(string Repository, string? OneDriveRemote, int? KeepNewestSnapshots = null);
    private const string TaskName = "WorkIntelligence-UserDailyBackup";
    private static string SuccessPath => Path.Combine(LocalPaths.Data, "scheduled-backup-last-success.txt");
    private static string SettingsPath => Path.Combine(LocalPaths.Data, "scheduled-backup.json");
    private static string LegacySecretsPath => Path.Combine(LocalPaths.Data, "scheduled-backup.dpapi");

    public static async Task ConfigureAsync(string repository, string? remote, CancellationToken ct, int? keepNewestSnapshots = null)
    {
        LocalPaths.Ensure();
        LegacyEncryptedDataGuard.AssertNoLegacyData();
        if (File.Exists(LegacySecretsPath))
            throw new InvalidOperationException("Legacy encrypted scheduled-backup configuration detected. Remove it using the earlier application before replacing it.");
        using (var store = new KnowledgeStore())
            if (store.CheckDatabase() != "ok") throw new InvalidDataException("Database integrity check failed.");
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            throw new InvalidOperationException("A stable executable path is required for Task Scheduler.");
        if (keepNewestSnapshots is int keep && keep is < 2 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(keepNewestSnapshots), "Keep at least two recovery snapshots.");
        // Validate backup destination with same rules as interactive backups before persisting settings.
        if (string.IsNullOrWhiteSpace(repository)) throw new ArgumentException("Backup folder required.");
        var config = new Settings(BackupService.ValidateRepository(repository), string.IsNullOrWhiteSpace(remote) ? null : remote.Trim(), keepNewestSnapshots);
        var command = StartupOptions.ScheduledCommand(executable, System.Reflection.Assembly.GetEntryAssembly()?.Location, LocalPaths.Root);
        var temp = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(config), ct);
            await RunTaskSchedulerAsync(["/Create", "/F", "/SC", "DAILY", "/ST", "02:00",
                "/TN", TaskName, "/TR", command, "/IT"], ct);
            File.Move(temp, SettingsPath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static async Task RemoveAsync(CancellationToken ct)
    {
        // Keep prior settings if task deletion fails.
        await RunTaskSchedulerAsync(["/Delete", "/F", "/TN", TaskName], ct);
        if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
    }

    public static async Task RunJobAsync(CancellationToken ct)
    {
        if (!File.Exists(SettingsPath)) throw new InvalidOperationException("No scheduled backup configured.");
        if (new FileInfo(SettingsPath).Length > 8192) throw new InvalidDataException("Scheduled backup settings too large.");
        var settings = JsonSerializer.Deserialize<Settings>(await File.ReadAllTextAsync(SettingsPath, ct))
            ?? throw new InvalidDataException("Scheduled backup settings missing.");
        using var store = new KnowledgeStore();
        await new BackupService(store).BackupAsync(settings.Repository, ct);
        if (!string.IsNullOrWhiteSpace(settings.OneDriveRemote))
            await RemoteBackupService.SyncAndVerifyAsync(settings.Repository, settings.OneDriveRemote, ct);
        if (settings.KeepNewestSnapshots is int keep)
            BackupRetention.Prune(settings.Repository, keep, ct);
        var completed = DateTimeOffset.UtcNow.ToString("O");
        var tempSuccess = SuccessPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(tempSuccess, completed, ct); File.Move(tempSuccess, SuccessPath, true); }
        finally { if (File.Exists(tempSuccess)) File.Delete(tempSuccess); }
        AuditLog.Info("ScheduledBackup", "Scheduled plaintext snapshot completed. OneDrive status is recorded by the synchronization step.");
    }

    /// <summary>
    /// Catch up after a missed daily trigger when the user next opens the application.
    /// This does not guarantee backup during logout/sleep and never silently configures
    /// a cloud account. A failed backup remains overdue for the next attempt.
    /// </summary>
    public static async Task<bool> CatchUpIfOverdueAsync(CancellationToken ct)
    {
        if (!File.Exists(SettingsPath)) return false;
        DateTimeOffset? last = null;
        if (File.Exists(SuccessPath))
        {
            if (new FileInfo(SuccessPath).Length > 128)
                throw new InvalidDataException("Scheduled backup completion state is malformed.");
            if (!DateTimeOffset.TryParse(await File.ReadAllTextAsync(SuccessPath, ct), out var parsed))
                throw new InvalidDataException("Scheduled backup completion timestamp is invalid.");
            last = parsed;
        }
        if (!BackupRetention.IsOverdue(last, DateTimeOffset.UtcNow)) return false;
        await RunJobAsync(ct);
        return true;
    }

    private static async Task RunTaskSchedulerAsync(IEnumerable<string> args, CancellationToken ct)
    {
        var info = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("Windows Task Scheduler did not start.");
        using var reg = ct.Register(() => ProcessTermination.RequestStop(process));
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await ProcessTermination.WaitForExitAsync(process, ct); _ = await stdout; _ = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"Windows Task Scheduler failed (code {process.ExitCode}).");
    }
}
