using System.Net;
using System.Text;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ResponseTimeoutTests
{
    private sealed class NeverResponds : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class FragmentThenStall : Stream
    {
        private readonly MemoryStream _prefix = new(Encoding.UTF8.GetBytes("data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n"));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_prefix.Position < _prefix.Length) return await _prefix.ReadAsync(buffer, ct);
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _prefix.Dispose(); base.Dispose(disposing); }
    }

    private sealed class StallingBody : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new FragmentThenStall()) });
    }

    [Fact]
    public async Task NoResponseHeadersTimesOut()
    {
        using var client = new HttpClient(new NeverResponds()) { BaseAddress = new Uri("http://127.0.0.1/") };
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await foreach (var fragment in ModelRuntime.StreamResponseAsync(client,
                new ModelRuntimeOptions(ResponseTimeoutSeconds: 1), [("user", "synthetic")], CancellationToken.None)) { }
        });
    }

    [Fact]
    public async Task StalledResponsePreservesFragmentAndReportsIncompleteTimeout()
    {
        using var client = new HttpClient(new StallingBody()) { BaseAddress = new Uri("http://127.0.0.1/") };
        var fragments = new List<string>();
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await foreach (var fragment in ModelRuntime.StreamResponseAsync(client,
                new ModelRuntimeOptions(ResponseTimeoutSeconds: 1), [("user", "synthetic")], CancellationToken.None))
                fragments.Add(fragment);
        });
        Assert.Equal("partial", string.Concat(fragments));
    }

    [Fact]
    public async Task UserCancellationIsNotMisreportedAsTimeout()
    {
        using var client = new HttpClient(new NeverResponds()) { BaseAddress = new Uri("http://127.0.0.1/") };
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var fragment in ModelRuntime.StreamResponseAsync(client,
                new ModelRuntimeOptions(), [("user", "synthetic")], cancelled.Token)) { }
        });
    }
}
