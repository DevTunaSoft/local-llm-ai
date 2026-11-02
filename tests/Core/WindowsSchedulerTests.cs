using System.Diagnostics;
using System.Security.Principal;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class WindowsSchedulerFactAttribute : FactAttribute
{
    public WindowsSchedulerFactAttribute(){if(!OperatingSystem.IsWindows())Skip="Requires actual Windows Task Scheduler; no task is registered on other platforms.";}
}

public sealed class WindowsSchedulerTests
{
    [WindowsSchedulerFact]
    public async Task ActualSchedulerAcceptsQueriesAndRemovesFutureInteractiveBackupDefinition()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        var name="WorkIntelligence-CI-"+Guid.NewGuid().ToString("N");
        var file=Path.Combine(Path.GetTempPath(),name+".xml");
        using var identity=WindowsIdentity.GetCurrent();
        var sid=identity.User!.Value;
        var target=Path.Combine(Path.GetTempPath(),"wi-tests-scheduled-"+Guid.NewGuid().ToString("N"),"Work Data");
        var repository=Path.Combine(Path.GetTempPath(),"wi-schedule-repository-"+Guid.NewGuid().ToString("N"));
        var marker="ScheduledFalcon"+Guid.NewGuid().ToString("N");
        using(var seed=new KnowledgeStore())
        {
            seed.AddMemory(marker,"synthetic scheduler fixture");
            seed.CreateBackupSnapshot(Path.Combine(target,"Data"),CancellationToken.None);
        }
        await File.WriteAllTextAsync(Path.Combine(target,"Data","scheduled-backup.json"),System.Text.Json.JsonSerializer.Serialize(new BackupJobSettings(repository)));
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"WorkIntelligence.slnx")))directory=directory.Parent;
        Assert.NotNull(directory);
        var assembly=Path.Combine(directory.FullName,"WorkIntelligence","bin","Release","net10.0-windows","win-x64","WorkIntelligence.dll");
        Assert.True(File.Exists(assembly),"Build Windows source before running scheduled-backup acceptance.");
        var dotnetRoot=Environment.GetEnvironmentVariable("DOTNET_ROOT");
        var executable=dotnetRoot is null?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe"):Path.Combine(dotnetRoot,"dotnet.exe");
        // A future daily trigger cannot fire during the test. Explicit /Run below
        // executes the real source DLL with no GUI, using only synthetic data.
        var definition=ScheduledTaskDefinition.Build(executable,assembly,target,sid,DateTime.Today.AddYears(5).AddHours(2));
        await File.WriteAllTextAsync(file,definition,System.Text.Encoding.Unicode);
        async Task<(int code,string output)> Run(params string[] args)
        {
            var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"schtasks.exe"))
                {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in args)start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var cancel=timeout.Token.Register(()=>ProcessTermination.RequestStop(process));
            var stdout=process.StandardOutput.ReadToEndAsync(timeout.Token);var stderr=process.StandardError.ReadToEndAsync(timeout.Token);
            await ProcessTermination.WaitForExitAsync(process,timeout.Token);
            return(process.ExitCode,((await stdout)+(await stderr)).Replace("\0",""));
        }
        var created=false;
        try
        {
            var create=await Run("/Create","/TN",name,"/XML",file);Assert.True(create.code==0,"Task Scheduler rejected the source definition: "+create.output);created=true;
            var query=await Run("/Query","/TN",name,"/XML");Assert.Equal(0,query.code);
            Assert.Contains("StartWhenAvailable",query.output);Assert.Contains("RestartOnFailure",query.output);Assert.Contains("InteractiveToken",query.output);
            var started=await Run("/Run","/TN",name);Assert.Equal(0,started.code);
            var success=Path.Combine(target,"Data","scheduled-backup-last-success.txt");
            for(var attempt=0;attempt<120 && !File.Exists(success);attempt++)await Task.Delay(500);
            Assert.True(File.Exists(success),"Actual Task Scheduler did not complete a source-only backup with the window closed.");
            var snapshot=Assert.Single(Directory.GetDirectories(repository,"snap-*"));
            BackupService.VerifySnapshot(snapshot,CancellationToken.None);
            using var db=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=Path.Combine(snapshot,"knowledge.db"),Mode=Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,Pooling=false}.ToString());
            db.Open();using var command=db.CreateCommand();command.CommandText="SELECT count(*) FROM memories WHERE content=$marker";command.Parameters.AddWithValue("$marker",marker);
            Assert.Equal(1L,command.ExecuteScalar());
        }
        finally
        {
            if(created){var deleted=await Run("/Delete","/TN",name,"/F");Assert.Equal(0,deleted.code);}
            File.Delete(file);
            if(Directory.Exists(repository))Directory.Delete(repository,true);
            if(Directory.Exists(target))Directory.Delete(target,true);
        }
    }
}
