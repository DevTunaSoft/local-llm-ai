using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace WorkIntelligence.Services;

/// <summary>Cooperating GUI/scheduled processes share an OS file lock, retained across awaits.</summary>
internal static class FileOperationLock
{
    private static string LockPath(string key)
    {
        var folder = Path.Combine(LocalPaths.Root, "Locks");
        PathSafety.RejectReparsePoints(folder);
        Directory.CreateDirectory(folder);
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        var path = Path.Combine(folder, name + ".lock");
        PathSafety.RejectReparsePoints(path);
        return path;
    }

    internal static string RepositoryKey(string repository)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository));
        return "repository:" + (OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path);
    }

    internal static IDisposable Acquire(string key, CancellationToken ct = default)
    {
        var path = LockPath(key);
        var timer = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (timer.Elapsed < TimeSpan.FromSeconds(30))
            {
                ct.WaitHandle.WaitOne(75);
            }
        }
    }

    internal static async Task<IDisposable> AcquireAsync(string key, CancellationToken ct)
    {
        var path = LockPath(key);
        var timer = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (timer.Elapsed < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(75, ct);
            }
        }
    }
}
