using WorkIntelligence.Services;
using System.Diagnostics;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class FileOperationLockTests
{
    [Fact]
    public async Task LockIsEnforcedAcrossSeparateLinuxProcesses()
    {
        var key = "synthetic-process-lock-" + Guid.NewGuid().ToString("N");
        string path;
        using (var held = (FileStream)FileOperationLock.Acquire(key))
        {
            path = held.Name;
            Assert.NotEqual(0, await TryExternalLock(path));
        }
        Assert.Equal(0, await TryExternalLock(path));
    }

    private static async Task<int> TryExternalLock(string path)
    {
        var info = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardError = true };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("import fcntl, sys; f = open(sys.argv[1], 'r+b'); fcntl.flock(f, fcntl.LOCK_EX | fcntl.LOCK_NB)");
        info.ArgumentList.Add(path);
        using var process = Process.Start(info)!;
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _ = await error;
        return process.ExitCode;
    }

    [Fact]
    public async Task AnotherHandleCannotEnterAndCanCancelItsWait()
    {
        var key = "synthetic-lock-" + Guid.NewGuid().ToString("N");
        using (FileOperationLock.Acquire(key))
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                using var unexpected = await FileOperationLock.AcquireAsync(key, cancellation.Token);
            });
        }
        using var afterRelease = await FileOperationLock.AcquireAsync(key, CancellationToken.None);
    }

    [Fact]
    public void EquivalentRepositoryPathsShareOneKey()
    {
        var folder = Path.Combine(Path.GetTempPath(), "synthetic-repository");
        Assert.Equal(FileOperationLock.RepositoryKey(folder),
            FileOperationLock.RepositoryKey(Path.Combine(folder, ".") + Path.DirectorySeparatorChar));
    }
}
