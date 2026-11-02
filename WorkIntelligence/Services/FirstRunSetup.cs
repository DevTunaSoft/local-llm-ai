using System.Text.Json;

namespace WorkIntelligence.Services;

public sealed record SetupChoices(string ModelDownloadFolder,string? ExistingModel,string? KnowledgeFolder,
    string? BackupRepository,bool OrganizationApproved);
public sealed record SetupResult(int ImportedDocuments,int RegisteredModels,string Summary);

public static class FirstRunSetup
{
    public static string ValidateDataLocation(string folder)
    {
        if (!Path.IsPathFullyQualified(folder)) throw new ArgumentException("Choose an absolute local data directory.");
        var full=Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if(Directory.GetParent(full) is null)throw new ArgumentException("Choose a dedicated application folder, not a drive root.");
        LocalPaths.ValidateDataRoot(full,[Environment.GetEnvironmentVariable("OneDrive"),Environment.GetEnvironmentVariable("OneDriveCommercial"),Environment.GetEnvironmentVariable("OneDriveConsumer")]);
        LegacyEncryptedDataGuard.AssertNoLegacyData(full);
        return full;
    }
    public static async Task<SetupResult> ApplyAsync(SetupChoices choices,CancellationToken ct)
    {
        if (!choices.OrganizationApproved) throw new InvalidOperationException("Confirm organizational approval for plaintext work data before completing setup.");
        ct.ThrowIfCancellationRequested();
        var modelFolder=WorkspacePreferencesStore.ValidateModelFolder(choices.ModelDownloadFolder);
        var repository=string.IsNullOrWhiteSpace(choices.BackupRepository)?null:BackupService.ValidateRepository(choices.BackupRepository);
        if(!string.IsNullOrWhiteSpace(choices.KnowledgeFolder) && !Path.IsPathFullyQualified(choices.KnowledgeFolder))
            throw new ArgumentException("Choose an absolute source folder.");
        var knowledge=string.IsNullOrWhiteSpace(choices.KnowledgeFolder)?null:Path.GetFullPath(choices.KnowledgeFolder);
        if(knowledge is not null)
        {
            PathSafety.RejectReparsePoints(knowledge);
            if(!Directory.Exists(knowledge))throw new DirectoryNotFoundException("Approved knowledge folder does not exist.");
            if(knowledge.Equals(LocalPaths.Root,StringComparison.OrdinalIgnoreCase)||knowledge.StartsWith(LocalPaths.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a source folder outside application data.");
        }
        if(!string.IsNullOrWhiteSpace(choices.ExistingModel))
        {
            PathSafety.RejectReparsePoints(choices.ExistingModel);
            if(!File.Exists(choices.ExistingModel))throw new FileNotFoundException("Selected model no longer exists.");
        }
        Directory.CreateDirectory(modelFolder);
        LocalPaths.Ensure();
        using var store=new KnowledgeStore();
        var models=0;
        if(!string.IsNullOrWhiteSpace(choices.ExistingModel))
        {
            await Task.Run(()=>store.ImportModel(choices.ExistingModel),ct);
            models=1;
        }
        var imported=0;
        if(knowledge is not null)
        {
            store.AddWatchFolder(knowledge,1);
            var scan=await store.ScanApprovedFoldersAsync(ct);
            imported=scan.Imported;
            if(scan.Failed>0)throw new InvalidDataException("Approved source scan reported errors; successfully imported data was retained. Review the folder and retry setup.");
        }
        ct.ThrowIfCancellationRequested();
        // Snapshot preferences after all validation/imports succeed. It contains no OAuth credentials.
        WorkspacePreferencesStore.Save(new(modelFolder,repository,false));
        if(repository is not null)await new BackupService(store).BackupAsync(repository,ct);
        WorkspacePreferencesStore.Save(new(modelFolder,repository,true));
        return new(imported,models,"Setup complete. Model files stay external; work data and backups are plaintext. No cloud account was connected and no scheduled task was installed.");
    }
}

/// <summary>Remembers only the selected local data path, outside the chosen data tree.</summary>
public static class StartupSelection
{
    private static string FilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorkIntelligenceLauncher","startup.json");
    public static string? Load()
    {
        var root=Read(FilePath);
        if(root is not null && !Directory.Exists(root))throw new DirectoryNotFoundException("Saved data location is unavailable.");
        return root;
    }
    public static void Save(string root)=>Write(FilePath,FirstRunSetup.ValidateDataLocation(root));
    internal static string? Read(string file)
    {
        PathSafety.RejectReparsePoints(file);
        if(!File.Exists(file))return null;
        if(new FileInfo(file).Length>8192)throw new InvalidDataException("Saved data location is malformed.");
        var root=JsonSerializer.Deserialize<string>(File.ReadAllText(file));
        return root is null?throw new InvalidDataException("Saved data location is missing."):FirstRunSetup.ValidateDataLocation(root);
    }
    internal static void Write(string file,string root)
    {
        PathSafety.RejectReparsePoints(file);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temp,JsonSerializer.Serialize(root)); File.Move(temp,file,true); }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }
}
