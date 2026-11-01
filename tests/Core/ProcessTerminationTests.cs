using System.Diagnostics;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ProcessTerminationTests
{
    [Fact]
    public void CancellationCallbackIsSafeBeforeStartAndAfterDisposal()
    {
        var process = new Process();
        ProcessTermination.RequestStop(process);
        process.Dispose();
        ProcessTermination.RequestStop(process);
    }

    private static Process StartBlockedProcess()
    {
        var info = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, RedirectStandardInput = true };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("read input"); // Blocks on synthetic stdin; no document or credential data.
        return Process.Start(info)!;
    }

    [Fact]
    public async Task StopWaitsForTheOperatingSystemToReapTheProcess()
    {
        using var process = StartBlockedProcess();
        await ProcessTermination.StopAsync(process);
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task CancellationDoesNotReturnWhileTheNativeProcessIsAlive()
    {
        using var process = StartBlockedProcess();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProcessTermination.WaitForExitAsync(process, cancellation.Token));
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task AlreadyExitedProcessIsSafeToStop()
    {
        using var process = StartBlockedProcess();
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        await ProcessTermination.StopAsync(process);
        Assert.True(process.HasExited);
    }
}
