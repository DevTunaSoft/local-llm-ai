using System.Text.RegularExpressions;

namespace WorkIntelligence.Services;

public static class AuditLog
{
    private static readonly object Gate = new();
    public static void Info(string category, string description) => Write("INFO", category, description);
    public static void Error(string category, Exception ex) => Write("ERROR", category, ex.GetType().Name + " (details redacted; see application error display)");
    private static void Write(string level, string category, string description)
    {
        try
        {
            LocalPaths.Ensure();
            // Logs must not contain chat contents, credentials, document text or full file paths.
            var safe = Regex.Replace(description, @"[\r\n\t]+", " ");
            safe = Regex.Replace(safe, @"[A-Za-z]:\\[^ ]+", "[path]");
            if (safe.Length > 300) safe = safe[..300];
            lock (Gate) File.AppendAllText(Path.Combine(LocalPaths.Logs, "diagnostics.log"), $"{DateTimeOffset.UtcNow:O} {level} [{category}] {safe}{Environment.NewLine}");
        }
        catch { /* Diagnostics must never crash the process. */ }
    }
}
