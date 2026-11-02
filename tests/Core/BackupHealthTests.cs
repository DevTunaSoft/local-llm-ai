using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class BackupHealthTests : IDisposable
{
    private readonly string _repository=Path.Combine(Path.GetTempPath(),"wi-health-"+Guid.NewGuid().ToString("N"));
    public BackupHealthTests(){if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);LocalPaths.Ensure();}
    [Fact]
    public async Task SuccessfulLocalJobRecordsVerificationAndStorage()
    {
        await BackupJobRunner.RunAsync(new(_repository),CancellationToken.None);
        var health=BackupHealthStore.Load();Assert.Equal("Verified",health.Status);Assert.NotNull(health.LastSuccess);
        Assert.NotNull(health.LastLocalSuccess);Assert.Null(health.LastRemoteSuccess);Assert.Equal(1,health.SnapshotCount);Assert.True(health.LocalBytes>0);
    }
    [Fact]
    public async Task FailedRemoteUploadPreservesRecoveryPointsAndRecordsRedactedRetry()
    {
        await BackupJobRunner.RunAsync(new(_repository),CancellationToken.None);
        await BackupJobRunner.RunAsync(new(_repository),CancellationToken.None);
        var previousSuccess=BackupHealthStore.Load().LastSuccess;
        await Assert.ThrowsAsync<IOException>(()=>BackupJobRunner.RunCoreAsync(new(_repository,"synthetic:backup",2),CancellationToken.None,
            (_,_,_)=>throw new IOException("synthetic-sensitive-message")));
        var health=BackupHealthStore.Load();Assert.Equal("Failed",health.Status);Assert.Equal(1,health.ConsecutiveFailures);
        Assert.Equal(previousSuccess,health.LastSuccess);Assert.NotNull(health.LastLocalSuccess);Assert.Null(health.LastRemoteSuccess);Assert.NotNull(health.NextRetry);
        Assert.Equal(3,Directory.GetDirectories(_repository,"snap-*").Length);
        Assert.DoesNotContain("synthetic-sensitive-message",File.ReadAllText(Path.Combine(LocalPaths.Data,"backup-health.json")));
    }
    [Theory]
    [InlineData(1,5)] [InlineData(2,15)] [InlineData(3,60)] [InlineData(4,240)] [InlineData(5,1440)] [InlineData(100,1440)]
    public void RetryBackoffIsBounded(int failures,int minutes)
    {var now=DateTimeOffset.UtcNow;Assert.Equal(now.AddMinutes(minutes),BackupHealthStore.RetryAt(failures,now));}
    [Fact]
    public void CalendarSelectionRetainsNewestAndDistinctYearBuckets()
    {
        var points=new[]{new BackupRetention.Point("old-a",new(2024,1,1,0,0,0,TimeSpan.Zero),100),
            new BackupRetention.Point("old-b",new(2024,12,1,0,0,0,TimeSpan.Zero),100),new BackupRetention.Point("previous",new(2025,6,1,0,0,0,TimeSpan.Zero),100),
            new BackupRetention.Point("recent",new(2026,11,1,0,0,0,TimeSpan.Zero),100),new BackupRetention.Point("newest",new(2026,11,2,0,0,0,TimeSpan.Zero),100)};
        var kept=BackupRetention.KeepCalendar(points,new(2,0,0,3));
        Assert.Equal(4,kept.Count);Assert.Contains("old-b",kept);Assert.DoesNotContain("old-a",kept);Assert.Contains("previous",kept);
    }
    [Fact]
    public async Task CalendarQuotaNeverDeletesTheLastTwoVerifiedSnapshots()
    {
        for(var i=0;i<4;i++)await BackupJobRunner.RunAsync(new(_repository),CancellationToken.None);
        var result=BackupRetention.PruneCalendar(_repository,new(2,0,0,0,1),CancellationToken.None);
        Assert.Equal(2,result.Removed);Assert.Equal(2,result.Retained);Assert.True(result.QuotaExceeded);
        Assert.Equal(2,Directory.GetDirectories(_repository,"snap-*").Length);
    }
    [Fact]
    public void TaskXmlPreservesPathArgumentsSessionAndRetryPolicy()
    {
        var executable=Path.Combine(Path.GetTempPath(),"Program Files","dotnet.exe");
        var assembly=Path.Combine(Path.GetTempPath(),"Work Assistant","WorkIntelligence.dll");
        var root=Path.Combine(Path.GetTempPath(),"Synthetic Data & Sources");
        var text=ScheduledTaskDefinition.Build(executable,assembly,root,"S-1-5-21-123-456-789-1001",new DateTime(2035,1,2,2,0,0));
        var xml=System.Xml.Linq.XDocument.Parse(text);System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";
        Assert.Equal(executable,xml.Descendants(ns+"Command").Single().Value);Assert.Contains(assembly,xml.Descendants(ns+"Arguments").Single().Value);
        Assert.Contains(root,xml.Descendants(ns+"Arguments").Single().Value);Assert.Equal("InteractiveToken",xml.Descendants(ns+"LogonType").Single().Value);
        Assert.Equal("true",xml.Descendants(ns+"StartWhenAvailable").Single().Value);Assert.Equal("PT15M",xml.Descendants(ns+"RestartOnFailure").Single().Element(ns+"Interval")!.Value);
    }
    public void Dispose(){if(Directory.Exists(_repository))Directory.Delete(_repository,true);if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);}
}
