using System.Text.Json;

namespace WorkIntelligence.Services;

public sealed record WorkspacePreferences(string? ModelDownloadFolder = null,string? BackupRepository = null,bool SetupCompleted = false);

public static class WorkspacePreferencesStore
{
    internal const string FileName = "workspace-preferences.json";
    public static WorkspacePreferences Load()
    {
        var file = Path.Combine(LocalPaths.Data,FileName);
        PathSafety.RejectReparsePoints(file);
        if (!File.Exists(file)) return new();
        if (new FileInfo(file).Length > 16384) throw new InvalidDataException("Workspace preferences are oversized.");
        return JsonSerializer.Deserialize<WorkspacePreferences>(File.ReadAllText(file))
            ?? throw new InvalidDataException("Workspace preferences are malformed.");
    }
    public static void Save(WorkspacePreferences preferences)
    {
        LocalPaths.Ensure();
        if (preferences.ModelDownloadFolder is string folder) ValidateModelFolder(folder);
        if (preferences.BackupRepository is string repository) BackupService.ValidateRepository(repository);
        var file = Path.Combine(LocalPaths.Data,FileName);
        PathSafety.RejectReparsePoints(file);
        var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temp,JsonSerializer.Serialize(preferences)); File.Move(temp,file,true); }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }
    internal static string ValidateModelFolder(string folder)
    {
        if (!Path.IsPathFullyQualified(folder)) throw new ArgumentException("Choose an absolute model download directory.");
        var full=Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        PathSafety.RejectReparsePoints(full);
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        foreach(var protectedFolder in new[]{LocalPaths.Data,LocalPaths.Runtime,LocalPaths.Logs})
            if(full.Equals(protectedFolder,comparison)||full.StartsWith(protectedFolder+Path.DirectorySeparatorChar,comparison))
                throw new InvalidOperationException("Keep model downloads separate from database, runtime and log directories.");
        return full;
    }
    public static string DownloadFolder()
    {
        var folder=Load().ModelDownloadFolder ?? LocalPaths.Models;
        folder=ValidateModelFolder(folder);
        Directory.CreateDirectory(folder);
        PathSafety.RejectReparsePoints(folder);
        return folder;
    }
}

internal static class PortableSettings
{
    internal static readonly string[] Files = ["model-settings.json",WorkspacePreferencesStore.FileName,"model-routing.json"];
}
