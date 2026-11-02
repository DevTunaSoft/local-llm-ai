using System.Text.Json;

namespace WorkIntelligence.Services;

public sealed record BackupHealth(string Status="Never run",DateTimeOffset? LastAttempt=null,DateTimeOffset? LastSuccess=null,
    DateTimeOffset? LastLocalSuccess=null,DateTimeOffset? LastRemoteSuccess=null,DateTimeOffset? NextRetry=null,
    int ConsecutiveFailures=0,string? ErrorKind=null,int SnapshotCount=0,long LocalBytes=0,bool QuotaExceeded=false);
public sealed record CalendarRetention(int KeepNewest=2,int Daily=7,int Monthly=12,int Yearly=3,long? QuotaBytes=null)
{
    public void Validate()
    {
        if(KeepNewest is <2 or >10000 || Daily is <0 or >3660 || Monthly is <0 or >1200 || Yearly is <0 or >100
            || QuotaBytes is <=0)throw new ArgumentOutOfRangeException(nameof(KeepNewest),"Retention requires two newest recovery points and nonnegative bounded calendar counts/quota.");
    }
}
public sealed record BackupJobSettings(string Repository,string? OneDriveRemote=null,int? KeepNewestSnapshots=null,CalendarRetention? Calendar=null);

public static class BackupHealthStore
{
    private static string FileName=>Path.Combine(LocalPaths.Data,"backup-health.json");
    public static BackupHealth Load()
    {
        PathSafety.RejectReparsePoints(FileName);
        if(!File.Exists(FileName))return new();
        if(new FileInfo(FileName).Length>16384)throw new InvalidDataException("Backup health state is oversized.");
        var state=JsonSerializer.Deserialize<BackupHealth>(File.ReadAllText(FileName)) ?? throw new InvalidDataException("Backup health state is malformed.");
        if(state.ConsecutiveFailures<0 || state.SnapshotCount<0 || state.LocalBytes<0)throw new InvalidDataException("Backup health counters are invalid.");
        return state;
    }
    internal static void Save(BackupHealth state)
    {
        LocalPaths.Ensure();PathSafety.RejectReparsePoints(FileName);
        var temp=FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,JsonSerializer.Serialize(state));File.Move(temp,FileName,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    internal static DateTimeOffset RetryAt(int failures,DateTimeOffset now)
        =>now.AddMinutes(failures switch{<=1=>5,2=>15,3=>60,4=>240,_=>1440});
}

public static class BackupJobRunner
{
    public static Task RunAsync(BackupJobSettings settings,CancellationToken ct)
        =>RunCoreAsync(settings,ct,(repository,remote,token)=>RemoteBackupService.SyncAndVerifyAsync(repository,remote,token));
    internal static async Task RunCoreAsync(BackupJobSettings settings,CancellationToken ct,Func<string,string,CancellationToken,Task> remoteSync)
    {
        using var jobLock=await FileOperationLock.AcquireAsync("scheduled-backup-job",ct);
        var state=BackupHealthStore.Load();
        var repository=BackupService.ValidateRepository(settings.Repository);
        settings.Calendar?.Validate();
        if(settings.KeepNewestSnapshots is <2 or >10000)throw new ArgumentOutOfRangeException(nameof(settings.KeepNewestSnapshots));
        state=state with{Status="Running",LastAttempt=DateTimeOffset.UtcNow,ErrorKind=null};BackupHealthStore.Save(state);
        try
        {
            using var store=new KnowledgeStore();
            await new BackupService(store).BackupAsync(repository,ct);
            state=state with{LastLocalSuccess=DateTimeOffset.UtcNow};BackupHealthStore.Save(state);
            if(!string.IsNullOrWhiteSpace(settings.OneDriveRemote))
            {
                await remoteSync(repository,settings.OneDriveRemote,ct);
                state=state with{LastRemoteSuccess=DateTimeOffset.UtcNow};
            }
            var quotaExceeded=false;
            if(settings.Calendar is not null)quotaExceeded=BackupRetention.PruneCalendar(repository,settings.Calendar,ct).QuotaExceeded;
            else if(settings.KeepNewestSnapshots is int keep)BackupRetention.Prune(repository,keep,ct);
            PathSafety.RejectReparsePoints(repository);
            var snapshots=Directory.GetDirectories(repository,"snap-*",SearchOption.TopDirectoryOnly);
            long bytes=0;
            foreach(var snapshot in snapshots)
                foreach(var file in Directory.EnumerateFiles(snapshot,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}))
                    bytes=checked(bytes+new FileInfo(file).Length);
            state=state with{Status=quotaExceeded?"Verified; quota exceeded":"Verified",LastSuccess=DateTimeOffset.UtcNow,NextRetry=null,
                ConsecutiveFailures=0,ErrorKind=null,SnapshotCount=snapshots.Length,LocalBytes=bytes,QuotaExceeded=quotaExceeded};
            BackupHealthStore.Save(state);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested)
        {
            BackupHealthStore.Save(state with{Status="Cancelled",NextRetry=DateTimeOffset.UtcNow.AddMinutes(5),ErrorKind=null});throw;
        }
        catch(Exception ex)
        {
            var failures=Math.Min(state.ConsecutiveFailures+1,10000);
            BackupHealthStore.Save(state with{Status="Failed",ConsecutiveFailures=failures,NextRetry=BackupHealthStore.RetryAt(failures,DateTimeOffset.UtcNow),ErrorKind=ex.GetType().Name});
            throw;
        }
    }
}
