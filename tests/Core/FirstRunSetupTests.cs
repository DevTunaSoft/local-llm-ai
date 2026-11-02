using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class FirstRunSetupTests : IDisposable
{
    private readonly string _folder=Path.Combine(Path.GetTempPath(),"wi-setup-"+Guid.NewGuid().ToString("N"));
    public FirstRunSetupTests()
    {
        if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);
        Directory.CreateDirectory(_folder);
    }
    [Fact]
    public async Task SetupImportsApprovedSourcesAndCreatesVerifiedInitialBackup()
    {
        var source=Path.Combine(_folder,"approved");Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"synthetic.txt"),"Synthetic onboarding Falcon instructions");
        var models=Path.Combine(_folder,"models");var backup=Path.Combine(_folder,"backups");
        var result=await FirstRunSetup.ApplyAsync(new(models,null,source,backup,true),CancellationToken.None);
        Assert.Equal(1,result.ImportedDocuments);
        Assert.True(WorkspacePreferencesStore.Load().SetupCompleted);
        Assert.Equal(models,WorkspacePreferencesStore.DownloadFolder());
        Assert.Single(Directory.GetDirectories(backup,"snap-*"));
        using(var store=new KnowledgeStore())Assert.Single(store.Search("Falcon",workspaceId:1));
        Directory.Delete(LocalPaths.Root,true);
        await BackupService.RestoreToEmptyVaultAsync(backup,CancellationToken.None);
        Assert.Equal(models,WorkspacePreferencesStore.Load().ModelDownloadFolder);
        Assert.Equal(backup,WorkspacePreferencesStore.Load().BackupRepository);
        using var restored=new KnowledgeStore();Assert.Single(restored.Search("Falcon",workspaceId:1));
    }
    [Fact]
    public async Task MissingApprovalCannotCreateDatabaseOrPreferences()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(()=>FirstRunSetup.ApplyAsync(new(Path.Combine(_folder,"models"),null,null,null,false),CancellationToken.None));
        Assert.False(File.Exists(LocalPaths.Database));
        Assert.False(File.Exists(Path.Combine(LocalPaths.Data,WorkspacePreferencesStore.FileName)));
    }
    [Fact]
    public async Task InvalidBackupLocationIsRejectedBeforeSourceImports()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(()=>FirstRunSetup.ApplyAsync(new(Path.Combine(_folder,"models"),null,null,LocalPaths.Data,true),CancellationToken.None));
        Assert.False(File.Exists(LocalPaths.Database));
    }
    [Fact]
    public async Task CancelledSetupDoesNotClaimCompletion()
    {
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>FirstRunSetup.ApplyAsync(new(Path.Combine(_folder,"models"),null,null,null,true),cancellation.Token));
        Assert.False(WorkspacePreferencesStore.Load().SetupCompleted);
    }
    [Fact]
    public void SelectedDataRootRoundTripsWithoutMovingOldData()
    {
        var selected=Path.Combine(_folder,"selected");
        var settings=Path.Combine(_folder,"startup.json");
        StartupSelection.Write(settings,selected);
        Assert.Equal(selected,StartupSelection.Read(settings));
        Assert.False(Directory.Exists(selected));
    }
    [Fact]
    public void LegacyEncryptedDataIsNeverReplacedDuringSelection()
    {
        var selected=Path.Combine(_folder,"legacy");Directory.CreateDirectory(Path.Combine(selected,"Data"));
        var original=Path.Combine(selected,"Data","vault.json");File.WriteAllText(original,"Synthetic legacy metadata");
        Assert.Throws<InvalidOperationException>(()=>FirstRunSetup.ValidateDataLocation(selected));
        Assert.Equal("Synthetic legacy metadata",File.ReadAllText(original));
    }
    [Fact]
    public void ModelDownloadDirectoryCannotTargetLiveDatabaseStorage()
        =>Assert.Throws<InvalidOperationException>(()=>WorkspacePreferencesStore.ValidateModelFolder(LocalPaths.Blobs));
    public void Dispose()
    {
        if(Directory.Exists(_folder))Directory.Delete(_folder,true);
        if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);
    }
}
