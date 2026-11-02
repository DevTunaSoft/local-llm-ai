using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ModelDownloadTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }
    private static readonly byte[] Model = "GGUF synthetic model download fixture"u8.ToArray();
    private static string Hash => Convert.ToHexString(SHA256.HashData(Model));

    [Fact]
    public async Task ResumeAppendsOnlyTheRequestedVerifiedRange()
    {
        LocalPaths.Ensure();
        var name = Guid.NewGuid().ToString("N") + ".gguf";
        var final = Path.Combine(LocalPaths.Models, name);
        File.WriteAllBytes(final + ".partial", Model[..9]);
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal(9, request.Headers.Range!.Ranges.Single().From);
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(Model[9..]) };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(9, Model.Length - 1, Model.Length);
            return response;
        }));
        try
        {
            Assert.Equal(final, await ModelDownloader.DownloadWithClientAsync(client,
                "https://huggingface.co/test/resolve/version/model.gguf", name, Hash, null, CancellationToken.None));
            Assert.Equal(Model, File.ReadAllBytes(final));
            Assert.False(File.Exists(final + ".partial"));
        }
        finally { File.Delete(final); File.Delete(final + ".partial"); }
    }

    [Fact]
    public async Task ServerIgnoringRangeRestartsThePartialFile()
    {
        LocalPaths.Ensure();
        var name = Guid.NewGuid().ToString("N") + ".gguf";
        var final = Path.Combine(LocalPaths.Models, name);
        File.WriteAllText(final + ".partial", "wrong stale prefix");
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Model) }));
        try
        {
            await ModelDownloader.DownloadWithClientAsync(client, "https://huggingface.co/test/model.gguf", name, Hash, null, CancellationToken.None);
            Assert.Equal(Model, File.ReadAllBytes(final));
        }
        finally { File.Delete(final); File.Delete(final + ".partial"); }
    }

    [Theory]
    [InlineData("http://huggingface.co/model")]
    [InlineData("https://huggingface.co:8443/model")]
    [InlineData("https://user:secret@huggingface.co/model")]
    public async Task HostileRedirectIsRejectedBeforeASecondRequest(string target)
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(_ =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri(target);
            return response;
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ModelDownloader.DownloadWithClientAsync(client,
            "https://huggingface.co/test/model.gguf", Guid.NewGuid().ToString("N") + ".gguf", Hash, null, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task BadChecksumNeverPromotesTheModel()
    {
        var name = Guid.NewGuid().ToString("N") + ".gguf";
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Model) }));
        await Assert.ThrowsAsync<InvalidDataException>(() => ModelDownloader.DownloadWithClientAsync(client,
            "https://huggingface.co/test/model.gguf", name, new string('0', 64), null, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(LocalPaths.Models, name)));
        Assert.False(File.Exists(Path.Combine(LocalPaths.Models, name + ".partial")));
    }

    [Fact]
    public async Task SymlinkPartialIsRejectedBeforeNetworkOrFileWrites()
    {
        LocalPaths.Ensure();
        var name = Guid.NewGuid().ToString("N") + ".gguf";
        var partial = Path.Combine(LocalPaths.Models, name + ".partial");
        var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        File.WriteAllText(outside, "synthetic preserved data");
        File.CreateSymbolicLink(partial, outside);
        using var client = new HttpClient(new Handler(_ => throw new InvalidOperationException("Network must not be called")));
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => ModelDownloader.DownloadWithClientAsync(client,
                "https://huggingface.co/test/model.gguf", name, Hash, null, CancellationToken.None));
            Assert.Equal("synthetic preserved data", File.ReadAllText(outside));
        }
        finally { File.Delete(partial); File.Delete(outside); }
    }
}
