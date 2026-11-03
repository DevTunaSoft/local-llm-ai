using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class PolicyTests:IDisposable
{
    private readonly KnowledgeStore _store;
    public PolicyTests(){if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);_store=new();}
    private long Policy(string text,DateOnly start,DateOnly? end=null,IReadOnlyList<long>? reviewed=null,long workspace=1,PolicyAuthority authority=PolicyAuthority.DepartmentPolicy)
        =>_store.ApprovePolicy(workspace,"travel approval",text,"Synthetic approved policy document",authority,start,end,"Synthetic explicit review",reviewed);
    [Fact]
    public void OverlappingRulesRequireExplicitConflictReviewWithoutSilentChanges()
    {
        var first=Policy("PolicyFalcon original rule",new(2026,1,1));
        var conflict=Assert.Throws<PolicyConflictException>(()=>Policy("PolicyFalcon revised rule",new(2026,2,1)));
        Assert.Equal(first,Assert.Single(conflict.Conflicts).Id);Assert.Single(_store.Policies(1));
        Assert.Equal("PolicyFalcon original rule",Assert.Single(_store.SearchMemories("PolicyFalcon",workspaceId:1,asOf:new(2026,3,1))).Text);
    }
    [Fact]
    public void FuturePolicyDoesNotPrematurelyReplaceCurrentApprovedRule()
    {
        var first=Policy("PolicyFalcon current rule",new(2026,1,1));
        var next=Policy("PolicyFalcon future rule",new(2027,1,1),reviewed:[first]);
        Assert.Equal("PolicyFalcon current rule",Assert.Single(_store.SearchMemories("PolicyFalcon",workspaceId:1,asOf:new(2026,12,31))).Text);
        Assert.Equal("PolicyFalcon future rule",Assert.Single(_store.SearchMemories("PolicyFalcon",workspaceId:1,asOf:new(2027,1,1))).Text);
        Assert.Equal(2,_store.Policies(1).Count);Assert.Equal(next,_store.MemoryHistory(workspaceId:1).Single(x=>x.Id==first).SupersededBy);
    }
    [Fact]
    public void DeletingReplacementDoesNotReviveClosedPolicy()
    {
        var first=Policy("PolicyFalcon original",new(2026,1,1));
        var next=Policy("PolicyFalcon replacement",new(2027,1,1),reviewed:[first]);_store.DeleteMemory(next);
        Assert.Empty(_store.SearchMemories("PolicyFalcon",workspaceId:1,asOf:new(2027,2,1)));
        Assert.Equal(new DateOnly(2027,1,1),Assert.Single(_store.Policies(1)).ClosedOn);
    }
    [Fact]
    public void PolicyAuthorityAndWorkspaceScopesRemainExplicit()
    {
        var other=_store.AddWorkspace("Policy isolated workspace");
        Policy("PolicyFalcon department rule",new(2026,1,1));
        Policy("PolicyFalcon isolated organization rule",new(2026,1,1),workspace:other,authority:PolicyAuthority.OrganizationPolicy);
        Assert.Equal("PolicyFalcon department rule",Assert.Single(_store.SearchMemories("PolicyFalcon",workspaceId:1,asOf:new(2026,2,1))).Text);
        Assert.Equal("PolicyFalcon isolated organization rule",Assert.Single(_store.SearchMemories("PolicyFalcon",workspaceId:other,asOf:new(2026,2,1))).Text);
        _store.ApprovePolicy(1,"organization travel","PolicyFalcon organization rule","Synthetic source",PolicyAuthority.OrganizationPolicy,new(2026,1,1),null,"Reviewed");
        Assert.Equal("PolicyFalcon organization rule",_store.SearchMemories("PolicyFalcon",workspaceId:1,asOf:new(2026,2,1))[0].Text);
    }
    [Fact]
    public void ExpiryAndStaleReviewAreNotSilentlyIgnored()
    {
        var first=Policy("PolicyFalcon temporary",new(2026,1,1),new(2026,2,1));
        Assert.Empty(_store.SearchMemories("PolicyFalcon",workspaceId:1,asOf:new(2026,2,1)));
        Assert.Throws<InvalidDataException>(()=>Policy("PolicyFalcon unrelated replacement",new(2026,3,1),reviewed:[first]));
        Assert.Throws<InvalidOperationException>(()=>_store.ProposeMemory("Bypass dated policy","fixture",first));
        Assert.Single(_store.Policies(1));
    }
    [Fact]
    public async Task PolicyMetadataAndSupersessionSurvivePortableRecovery()
    {
        var repository=Path.Combine(Path.GetTempPath(),"wi-policy-backup-"+Guid.NewGuid().ToString("N"));
        try
        {
            var first=Policy("PolicyFalcon old",new(2026,1,1));Policy("PolicyFalcon new",new(2027,1,1),reviewed:[first]);
            await new BackupService(_store).BackupAsync(repository,CancellationToken.None);
            Directory.Delete(LocalPaths.Root,true);await BackupService.RestoreToEmptyVaultAsync(repository,CancellationToken.None);
            using var recovered=new KnowledgeStore();Assert.Equal(2,recovered.Policies(1).Count);
            Assert.Equal("PolicyFalcon new",Assert.Single(recovered.SearchMemories("PolicyFalcon",workspaceId:1,asOf:new(2027,2,1))).Text);
        }finally{if(Directory.Exists(repository))Directory.Delete(repository,true);}
    }
    public void Dispose(){_store.Dispose();if(Directory.Exists(LocalPaths.Root))Directory.Delete(LocalPaths.Root,true);}
}
