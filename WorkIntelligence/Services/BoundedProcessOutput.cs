using System.Text;

namespace WorkIntelligence.Services;

/// <summary>
/// Prevent untrusted native subprocesses from exhausting managed memory by
/// emitting unbounded stdout/stderr. Cancels the owning process token when
/// a configured ceiling is exceeded; process termination is the caller's job.
/// </summary>
internal static class BoundedProcessOutput
{
    internal static async Task<string> ReadAsync(TextReader reader, int maxCharacters,
        CancellationTokenSource owner)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(owner);
        if (maxCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        var text = new StringBuilder(Math.Min(8192, maxCharacters));
        var buffer = new char[8192];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), owner.Token);
            if (count == 0) return text.ToString();
            if (count > maxCharacters - text.Length)
            {
                owner.Cancel();
                throw new InvalidDataException("Native subprocess output exceeded its safety limit.");
            }
            text.Append(buffer, 0, count);
        }
    }
}
