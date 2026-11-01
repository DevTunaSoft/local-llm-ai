using System.Diagnostics;

namespace WorkIntelligence.Services;

internal static class ProcessTermination
{
    internal static void RequestStop(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception ex) { AuditLog.Error("NativeCancellation", ex); }
    }

    internal static async Task StopAsync(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { return; } // Not started or already disposed.
        catch (System.ComponentModel.Win32Exception) when (process.HasExited) { }
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    internal static async Task WaitForExitAsync(Process process, CancellationToken ct)
    {
        try { await process.WaitForExitAsync(ct); }
        catch (OperationCanceledException)
        {
            // Do not release repository/file locks while a canceled native command is still running.
            await StopAsync(process);
            throw;
        }
    }
}
