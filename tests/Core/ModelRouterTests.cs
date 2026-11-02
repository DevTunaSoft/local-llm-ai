using WorkIntelligence.Models;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ModelRouterTests
{
    private const ulong GiB=1024UL*1024*1024;
    private static ModelRecord Model(long id,char hash)=>new(id,"Synthetic "+id,"/synthetic/model"+id,new string(hash,64),DateTimeOffset.UtcNow);
    [Theory]
    [InlineData("Debug this SQL exception",WorkModelTask.Code)]
    [InlineData("Compare the approved policy",WorkModelTask.Analysis)]
    [InlineData("Write a short reply",WorkModelTask.General)]
    public void RoutingClassificationIsDeterministicAndExplainable(string prompt,WorkModelTask expected)=>Assert.Equal(expected,ModelRouter.Classify(prompt));
    [Fact]
    public void PreferredTaskModelWinsWhenItFits()
    {
        var general=Model(1,'A');var code=Model(2,'B');
        var decision=ModelRouter.Decide(new(true,general.Sha256,null,code.Sha256),[new(general,3*GiB),new(code,6*GiB)],WorkModelTask.Code,general,false,12*GiB);
        Assert.Equal(code,decision.Model);Assert.Contains("Code profile",decision.Reason);
    }
    [Fact]
    public void OversizedTaskPreferenceFallsBackWithReasonAndReserve()
    {
        var general=Model(1,'A');var code=Model(2,'B');
        var decision=ModelRouter.Decide(new(true,general.Sha256,null,code.Sha256),[new(general,3*GiB),new(code,11*GiB)],WorkModelTask.Code,general,false,12*GiB);
        Assert.Equal(general,decision.Model);Assert.Contains("memory budget",decision.Reason);
    }
    [Fact]
    public void ManualOverrideNeverSilentlySubstitutesAnotherModel()
    {
        var general=Model(1,'A');var code=Model(2,'B');var preferences=new RoutingPreferences(true,general.Sha256,null,code.Sha256);
        var candidates=new RoutingCandidate[]{new(general,3*GiB),new(code,6*GiB)};
        Assert.Equal(general,ModelRouter.Decide(preferences,candidates,WorkModelTask.Code,general,true,12*GiB).Model);
        Assert.Throws<InvalidOperationException>(()=>ModelRouter.Decide(preferences,candidates,WorkModelTask.Code,code,true,6*GiB));
    }
    [Fact]
    public void MissingPreferencesChooseSmallestAvailableModelAndNeverUnavailableFiles()
    {
        var unavailable=Model(1,'A');var small=Model(2,'B');var big=Model(3,'C');
        var decision=ModelRouter.Decide(new(true,unavailable.Sha256),[new(unavailable,1*GiB,false),new(small,3*GiB),new(big,5*GiB)],WorkModelTask.Analysis,null,false,12*GiB);
        Assert.Equal(small,decision.Model);Assert.Contains("smallest eligible",decision.Reason);
    }
    [Fact]
    public void NoFitFailsInsteadOfLoadingAnUnsafeModel()
    {
        var model=Model(1,'A');Assert.Throws<InvalidOperationException>(()=>ModelRouter.Decide(new(true,model.Sha256),[new(model,6*GiB)],WorkModelTask.General,null,false,7*GiB));
    }
    [Fact]
    public void PreferencesPersistWithoutPromptsOrCloudCredentials()
    {
        var settings=new RoutingPreferences(true,new string('A',64),new string('B',64),null);
        ModelRouter.Save(settings);Assert.Equal(settings,ModelRouter.Load());ModelRouter.Save(new());
    }
}
