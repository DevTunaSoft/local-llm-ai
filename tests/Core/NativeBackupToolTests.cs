using System.Diagnostics;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class NativeBackupToolTests
{
    [NativeOcrFact]
    public async Task PinnedRcloneRedactedConfigIsAcceptedOnlyForADirectOneDriveBackend()
    {
        var root=Environment.GetEnvironmentVariable("WI_NATIVE_TEST_ROOT") ?? throw new InvalidOperationException("Native fixture directory is missing.");
        var previous=Environment.GetEnvironmentVariable("WI_DEV_RUNTIME_DIR");
        var config=Path.Combine(Path.GetTempPath(),"wi-synthetic-rclone-"+Guid.NewGuid().ToString("N")+".conf");
        try
        {
            Environment.SetEnvironmentVariable("WI_DEV_RUNTIME_DIR",Path.Combine(root,"runtime"));
            await File.WriteAllTextAsync(config,"[synthetic-onedrive]\ntype = onedrive\ntoken = synthetic-test-token\n\n[synthetic-local]\ntype = local\n");
            var start=new ProcessStartInfo(RuntimeBundle.Resolve("rclone.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"config","redacted","synthetic-onedrive","--config",config,"--ask-password=false"})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));using var registration=timeout.Token.Register(()=>ProcessTermination.RequestStop(process));
            var stdout=process.StandardOutput.ReadToEndAsync(timeout.Token);var stderr=process.StandardError.ReadToEndAsync(timeout.Token);
            await ProcessTermination.WaitForExitAsync(process,timeout.Token);var output=await stdout;_=await stderr;
            Assert.Equal(0,process.ExitCode);Assert.True(RemoteBackupService.IsOneDriveConfig("synthetic-onedrive",output));
            Assert.False(RemoteBackupService.IsOneDriveConfig("synthetic-local",output));Assert.DoesNotContain("synthetic-test-token",output);
        }
        finally{Environment.SetEnvironmentVariable("WI_DEV_RUNTIME_DIR",previous);File.Delete(config);}
    }
}
