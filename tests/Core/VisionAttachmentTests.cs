using System.Net;
using System.Text;
using System.Text.Json;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class VisionAttachmentTests
{
    private static readonly byte[] Png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j0X8AAAAASUVORK5CYII=");
    [Fact]
    public void LocalRasterAttachmentUsesOnlyAnEmbeddedDataUriAndHash()
    {
        var image=VisionAttachment.FromBytes("synthetic.png",Png);
        Assert.StartsWith("data:image/png;base64,",image.DataUrl);Assert.Equal(64,image.Sha256.Length);Assert.Equal("synthetic.png",image.Name);
        Assert.Equal(Png,Convert.FromBase64String(image.DataUrl.Split(',',2)[1]));
    }
    [Fact]
    public void ActiveFormatsAndDecompressionBombDimensionsAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>VisionAttachment.FromBytes("active.svg",Encoding.UTF8.GetBytes("<svg><script>never execute</script></svg>")));
        var oversized=Png.ToArray();System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(oversized.AsSpan(16),100000);
        Assert.Throws<InvalidDataException>(()=>VisionAttachment.FromBytes("large.png",oversized));
    }
    [Fact]
    public void ProjectorRequiresBothAbsolutePathAndTrustedHash()
    {
        Assert.Throws<ArgumentException>(()=>new ModelRuntimeOptions(VisionProjectorPath:Path.Combine(Path.GetTempPath(),"projector.gguf")).Validate());
        Assert.Throws<ArgumentException>(()=>new ModelRuntimeOptions(VisionProjectorPath:"relative.gguf",VisionProjectorSha256:new string('A',64)).Validate());
        new ModelRuntimeOptions(VisionProjectorPath:Path.Combine(Path.GetTempPath(),"projector.gguf"),VisionProjectorSha256:new string('A',64)).Validate();
    }
    private sealed class Handler:HttpMessageHandler
    {
        internal bool Called;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Called=true;Assert.Equal("127.0.0.1",request.RequestUri!.Host);
            using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var messages=json.RootElement.GetProperty("messages");Assert.Equal(JsonValueKind.String,messages[0].GetProperty("content").ValueKind);
            var content=messages[1].GetProperty("content");Assert.Equal("text",content[0].GetProperty("type").GetString());
            Assert.Equal("Describe this synthetic image",content[0].GetProperty("text").GetString());
            Assert.StartsWith("data:image/png;base64,",content[1].GetProperty("image_url").GetProperty("url").GetString());
            return new(HttpStatusCode.OK){Content=new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"Synthetic answer\"}}]}\n\ndata: [DONE]\n\n")};
        }
    }
    [Fact]
    public async Task ImagePayloadStaysLocalAndAttachesToLatestUserTurn()
    {
        using var handler=new Handler();using var client=new HttpClient(handler){BaseAddress=new Uri("http://127.0.0.1/")};
        var answer=new StringBuilder();
        await foreach(var fragment in ModelRuntime.StreamResponseAsync(client,new(),[("system","Synthetic context"),("user","Describe this synthetic image")],CancellationToken.None,image:VisionAttachment.FromBytes("fixture.png",Png)))answer.Append(fragment);
        Assert.True(handler.Called);Assert.Equal("Synthetic answer",answer.ToString());
    }
    [Fact]
    public async Task ImageWithoutUserTurnNeverSendsARequest()
    {
        using var handler=new Handler();using var client=new HttpClient(handler){BaseAddress=new Uri("http://127.0.0.1/")};
        await Assert.ThrowsAsync<ArgumentException>(async()=>
        {await foreach(var fragment in ModelRuntime.StreamResponseAsync(client,new(),[("system","No user message")],CancellationToken.None,image:VisionAttachment.FromBytes("fixture.png",Png))){} });
        Assert.False(handler.Called);
    }
}

public sealed class LiveVisionFactAttribute:FactAttribute
{
    public LiveVisionFactAttribute(){if(Environment.GetEnvironmentVariable("WI_REQUIRE_LIVE_VISION")!="1")Skip="Requires a verified compatible vision GGUF, projector and authorized synthetic raster fixture.";}
}
public sealed class LiveVisionTests
{
    [LiveVisionFact]
    public async Task RealCpuVisionModelReadsSyntheticImage()
    {
        string Required(string name)=>Environment.GetEnvironmentVariable(name) is string value && value.Length>0?value:throw new InvalidOperationException("Required live vision input is missing: "+name);
        var model=Required("WI_LIVE_VISION_MODEL");var hash=Required("WI_LIVE_VISION_MODEL_SHA256");
        var projector=Required("WI_LIVE_VISION_PROJECTOR");var projectorHash=Required("WI_LIVE_VISION_PROJECTOR_SHA256");
        _=Required("WI_DEV_RUNTIME_DIR");
        var image=VisionAttachment.FromFile(Required("WI_LIVE_VISION_IMAGE"));
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(20));
        await using var runtime=new ModelRuntime();
        await runtime.StartAsync(model,hash,new ModelRuntimeOptions(ContextSize:4096,MaxTokens:128,Temperature:0,VisionProjectorPath:projector,VisionProjectorSha256:projectorHash),deadline.Token);
        Assert.True(runtime.SupportsVision);var answer=new StringBuilder();
        await foreach(var part in runtime.StreamAsync([("user","Read the text and number in this synthetic test image.")],deadline.Token,image:image))answer.Append(part);
        Assert.Contains("427",answer.ToString());
        await runtime.StopAsync();Assert.False(runtime.IsRunning);
    }
}
