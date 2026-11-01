namespace WorkIntelligence.Services;

public static class LocalPaths
{
    public static readonly string Root = Path.GetFullPath(
        Environment.GetEnvironmentVariable("WORKINTELLIGENCE_DATA_ROOT") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkIntelligence"));
    public static readonly string Data = Path.Combine(Root, "Data");
    public static readonly string Models = Path.Combine(Root, "Models");
    public static readonly string Blobs = Path.Combine(Data, "Blobs");
    public static readonly string Runtime = Path.Combine(Root, "Runtime");
    public static readonly string Logs = Path.Combine(Root, "Logs");
    public static string VaultConfig => Path.Combine(Data, "vault.json");
    public static string Database => Path.Combine(Data, "knowledge.db");

    /// <summary>
    /// Live encrypted SQLCipher files are never synchronized directly by OneDrive.
    /// Only an independently encrypted restic repository may be copied to OneDrive.
    /// </summary>
    public static void Ensure()
    {
        ValidateDataRoot(Root, new[]
        {
            Environment.GetEnvironmentVariable("OneDrive"),
            Environment.GetEnvironmentVariable("OneDriveCommercial"),
            Environment.GetEnvironmentVariable("OneDriveConsumer")
        });
        foreach (var path in new[] { Root, Data, Models, Blobs, Runtime, Logs })
        {
            PathSafety.RejectReparsePoints(path);
            Directory.CreateDirectory(path);
            PathSafety.RejectReparsePoints(path);
        }
    }

    // Exposed internally so native integration tests can exercise OneDrive safety
    // without modifying the process-wide data-root environment variable.
    internal static void ValidateDataRoot(string dataRoot, IEnumerable<string?> syncRoots)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
            throw new ArgumentException("A local data folder is required.", nameof(dataRoot));
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        PathSafety.RejectReparsePoints(full);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var folder in syncRoots)
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            var sync = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            var syncPrefix = Path.EndsInDirectorySeparator(sync) ? sync : sync + Path.DirectorySeparatorChar;
            if (string.Equals(full, sync, comparison) ||
                full.StartsWith(syncPrefix, comparison))
                throw new InvalidOperationException(
                    "The live Work Intelligence vault cannot be stored in a OneDrive-synchronized folder. " +
                    "Choose an unsynchronized local folder; OneDrive is for encrypted restic backups only.");
        }
    }
}
