using System.Net;
using System.Text.Json;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ContextWindowTests
{
    private sealed class Handler:HttpMessageHandler
    {
        internal List<string> Prompts {get;}=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("127.0.0.1",request.RequestUri!.Host);
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            object result;
            if(request.RequestUri.AbsolutePath=="/apply-template")
            {
                var prompt=string.Join("|",body.RootElement.GetProperty("messages").EnumerateArray().Select(x=>x.GetProperty("content").GetString()));
                Prompts.Add(prompt);result=new{prompt};
            }
            else
            {
                Assert.Equal("/tokenize",request.RequestUri.AbsolutePath);
                result=new{tokens=Enumerable.Range(0,body.RootElement.GetProperty("content").GetString()!.Length).ToArray()};
            }
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(result))};
        }
    }
    [Fact]
    public async Task TokenizerTrimmingPreservesInstructionsAndLatestUserWithoutDeletingOriginalHistory()
    {
        using var handler=new Handler();using var client=new HttpClient(handler){BaseAddress=new Uri("http://127.0.0.1/")};
        var original=new List<(string role,string content)>{("system","approved evidence"),("user",new string('a',180)),("assistant",new string('b',180)),("user","latest question")};
        var fitted=await ContextWindow.FitAsync(client,original,300,64,CancellationToken.None);
        Assert.Equal(4,original.Count);Assert.Equal(2,fitted.Count);Assert.Equal(original[0],fitted[0]);Assert.Equal(original[^1],fitted[^1]);Assert.Equal(2,handler.Prompts.Count);
    }
    [Fact]
    public async Task OversizedLatestQuestionIsRefusedInsteadOfTruncatingEvidenceOrUserIntent()
    {
        using var client=new HttpClient(new Handler()){BaseAddress=new Uri("http://127.0.0.1/")};
        var messages=new[]{(role:"system",content:"approved evidence"),(role:"user",content:new string('q',500))};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>ContextWindow.FitAsync(client,messages,300,64,CancellationToken.None));
        Assert.Equal(500,messages[1].content.Length);
    }
    [Fact]
    public async Task CancellationNeverStartsALocalRequest()
    {
        using var handler=new Handler();using var client=new HttpClient(handler){BaseAddress=new Uri("http://127.0.0.1/")};using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ContextWindow.FitAsync(client,[("user","synthetic")],512,64,cancelled.Token));Assert.Empty(handler.Prompts);
    }
}
