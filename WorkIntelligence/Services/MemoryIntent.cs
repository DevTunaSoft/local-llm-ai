namespace WorkIntelligence.Services;

/// <summary>Only explicit user-authored commands propose persistent memory.
/// Untrusted document and model-generated text are never auto-promoted.</summary>
public static class MemoryIntent
{
    private static readonly string[] Prefixes =
    [
        "remember that ", "remember: ", "remember ",
        "going forward, ", "going forward: ", "going forward ",
        "from now on, ", "from now on: ", "from now on ",
        "we decided that ", "our standing procedure is ",
        "please remember that "
    ];

    public static bool TryExtract(string? message, out string text)
    {
        text = "";
        if (string.IsNullOrWhiteSpace(message)) return false;
        var trimmed = message.Trim();
        foreach (var prefix in Prefixes)
        {
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var candidate = trimmed[prefix.Length..].Trim();
            if (candidate.Length is < 1 or > 8000) return false;
            text = candidate;
            return true;
        }
        return false;
    }
}
