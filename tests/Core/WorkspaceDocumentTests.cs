using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class WorkspaceDocumentTests:IDisposable
{
    private readonly KnowledgeStore _store;
    public WorkspaceDocumentTests(){if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);_store=new();}
    [Fact]
    public void LibraryAndRemovalRespectSharedWorkspaceMembership()
    {
        var file=Path.Combine(LocalPaths.Root,"synthetic.txt");File.WriteAllText(file,"WorkspaceFalcon approved shared source");
        var imported=_store.Import(file);_store.AssignDocument(imported.DocumentId,1);var other=_store.AddWorkspace("Other document context");
        Assert.Single(_store.Documents(1));Assert.Empty(_store.Documents(other));
        Assert.Throws<InvalidDataException>(()=>_store.RemoveDocumentFromWorkspace(imported.DocumentId,other));
        _store.AssignDocument(imported.DocumentId,other);_store.RemoveDocumentFromWorkspace(imported.DocumentId,1);
        Assert.Empty(_store.Documents(1));Assert.Empty(_store.Search("WorkspaceFalcon",workspaceId:1));
        Assert.Single(_store.Documents(other));Assert.Single(_store.Search("WorkspaceFalcon",workspaceId:other));
        Assert.True(File.Exists(Path.Combine(LocalPaths.Blobs,imported.DocumentId+".bin")));
        _store.RemoveDocumentFromWorkspace(imported.DocumentId,other);Assert.Empty(_store.Documents());Assert.False(File.Exists(Path.Combine(LocalPaths.Blobs,imported.DocumentId+".bin")));
    }
    public void Dispose(){_store.Dispose();if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);}
}
