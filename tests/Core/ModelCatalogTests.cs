using System.Net;
using System.Text;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ModelCatalogTests
{
    private sealed class Handler(string json):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("huggingface.co",request.RequestUri!.Host);Assert.EndsWith("?blobs=true",request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")});
        }
    }
    private static string Metadata(string file="model.gguf",string? hash=null,string? revision=null,string repository="Publisher/Model")
        =>System.Text.Json.JsonSerializer.Serialize(new{id=repository,sha=revision??new string('a',40),cardData=new{license="apache-2.0"},siblings=new[]{new{rfilename=file,lfs=new{sha256=hash??new string('b',64),size=1024*1024}}}});
    [Fact]
    public async Task CatalogPinsPublisherCommitHashAndDownloadDestination()
    {
        using var client=new HttpClient(new Handler(Metadata()));var model=Assert.Single(await ModelCatalog.FetchWithClientAsync(client,"Publisher/Model",CancellationToken.None));
        Assert.Equal(new string('B',64),model.Sha256);Assert.Contains("/resolve/"+new string('a',40)+"/model.gguf",model.DownloadUrl);Assert.Equal("apache-2.0",model.License);
    }
    [Theory]
    [InlineData("../model.gguf")]
    [InlineData("models/../model.gguf")]
    [InlineData("model-00001-of-00002.gguf")]
    public async Task UnsafeAndUnverifiedShardSelectionsAreNotOffered(string name)
    {
        using var client=new HttpClient(new Handler(Metadata(name)));
        await Assert.ThrowsAsync<InvalidDataException>(()=>ModelCatalog.FetchWithClientAsync(client,"Publisher/Model",CancellationToken.None));
    }
    [Fact]
    public async Task MissingImmutableCommitCannotBecomeACatalogDownload()
    {
        using var client=new HttpClient(new Handler(Metadata(revision:"main")));
        await Assert.ThrowsAsync<InvalidDataException>(()=>ModelCatalog.FetchWithClientAsync(client,"Publisher/Model",CancellationToken.None));
    }
    [Fact]
    public async Task PublisherIdentityChangeFailsClosed()
    {
        using var client=new HttpClient(new Handler(Metadata(repository:"Other/Model")));
        await Assert.ThrowsAsync<InvalidDataException>(()=>ModelCatalog.FetchWithClientAsync(client,"Publisher/Model",CancellationToken.None));
    }
}

public sealed class LiveCatalogFactAttribute:FactAttribute
{
    public LiveCatalogFactAttribute(){if(Environment.GetEnvironmentVariable("WI_REQUIRE_LIVE_CATALOG")!="1")Skip="Requires explicit publisher metadata network access; no weights are downloaded.";}
}
public sealed class LiveCatalogTests
{
    [LiveCatalogFact]
    public async Task PrimaryPublisherCatalogsProvideImmutableTrustedGgufArtifacts()
    {
        foreach(var publisher in ModelCatalog.Publishers)
        {
            try
            {
                var models=await ModelCatalog.FetchAsync(publisher.Repository,CancellationToken.None);
                Assert.NotEmpty(models);Assert.All(models,x=>{Assert.Equal(40,x.Revision.Length);Assert.Equal(64,x.Sha256.Length);Assert.True(x.Bytes>1024);});
            }
            catch(Exception ex)
            {
                Console.WriteLine($"::error::Publisher catalog {publisher.Repository}: {ex.GetType().Name}: {ex.Message.Replace('\n',' ')}");throw;
            }
        }
    }
}
