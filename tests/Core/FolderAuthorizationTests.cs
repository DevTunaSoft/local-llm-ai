using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class FolderAuthorizationTests:IDisposable
{
    private readonly string _folder=Path.Combine(Path.GetTempPath(),"wi-folder-consent-"+Guid.NewGuid().ToString("N"));
    private readonly string _repository=Path.Combine(Path.GetTempPath(),"wi-folder-recovery-"+Guid.NewGuid().ToString("N"));
    public FolderAuthorizationTests(){if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);Directory.CreateDirectory(_folder);}
    [Fact]
    public async Task RestoredWatchedPathsCannotSilentlyIngestNewDeviceContents()
    {
        var source=Path.Combine(_folder,"synthetic.txt");File.WriteAllText(source,"Original Falcon policy");
        using(var store=new KnowledgeStore())
        {
            store.AddWatchFolder(_folder,1);await store.ScanApprovedFoldersAsync(CancellationToken.None);
            await new BackupService(store).BackupAsync(_repository,CancellationToken.None);
        }
        Directory.Delete(LocalPaths.Root,true);File.WriteAllText(source,"Unauthorized ChangedFalcon content after recovery");
        await BackupService.RestoreToEmptyVaultAsync(_repository,CancellationToken.None);
        using var recovered=new KnowledgeStore();
        Assert.False(FolderAuthorization.IsApproved(_folder,1));
        var paused=await recovered.ScanApprovedFoldersAsync(CancellationToken.None);Assert.Equal(1,paused.Failed);Assert.Equal(0,paused.Imported);
        Assert.Empty(recovered.Search("ChangedFalcon",workspaceId:1));
        recovered.AddWatchFolder(_folder,1);var approved=await recovered.ScanApprovedFoldersAsync(CancellationToken.None);
        Assert.Equal(1,approved.Imported);Assert.Single(recovered.Search("ChangedFalcon",workspaceId:1));
    }
    [Fact]
    public void ApprovalIsWorkspaceBoundAndNotPartOfPortableSettings()
    {
        using var store=new KnowledgeStore();store.AddWatchFolder(_folder,1);
        Assert.True(FolderAuthorization.IsApproved(_folder,1));Assert.False(FolderAuthorization.IsApproved(_folder,2));
        Assert.DoesNotContain(FolderAuthorization.FileName,PortableSettings.Files);
    }
    public void Dispose()
    {
        if(Directory.Exists(_folder))Directory.Delete(_folder,true);if(Directory.Exists(_repository))Directory.Delete(_repository,true);
        if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);
    }
}
