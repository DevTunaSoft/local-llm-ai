namespace WorkIntelligence.Services;

public static class StartupOptions
{
    public static string? DataRoot(IReadOnlyList<string> args)
    {
        string? root = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].Equals("--data-root", StringComparison.OrdinalIgnoreCase)) continue;
            if (root is not null || ++i >= args.Count || !Path.IsPathFullyQualified(args[i]))
                throw new ArgumentException("Specify --data-root once, followed by an absolute local directory.");
            root = Path.GetFullPath(args[i]);
        }
        return root;
    }

    internal static string ScheduledCommand(string executable, string? entryAssembly, string dataRoot)
    {
        static string Quote(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(['"', '\r', '\n']) >= 0 || !Path.IsPathFullyQualified(path))
                throw new ArgumentException("Scheduled paths must be absolute and contain no quotes or line breaks.");
            // Windows command-line parsing doubles backslashes before the closing quote.
            var trailing = path.Length - path.TrimEnd('\\').Length;
            return "\"" + path + new string('\\', trailing) + "\"";
        }
        var command = Quote(executable);
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (entryAssembly is null || !entryAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Source-only scheduled backups require the application DLL path.");
            command += " " + Quote(entryAssembly);
        }
        return command + " --scheduled-backup --data-root " + Quote(dataRoot);
    }
}
