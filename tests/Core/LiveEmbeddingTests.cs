using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

/// <summary>Real native local embeddings smoke test; requires an actual GGUF and runtime.</summary>
public sealed class LiveEmbeddingTests
{
    [Fact]
    public async Task RealEmbeddingWorkerReturnsNormalizedDistinctVectors()
    {
        var model = Environment.GetEnvironmentVariable("WI_LIVE_EMBEDDING_GGUF");
        var runtimeDir = Environment.GetEnvironmentVariable("WI_DEV_RUNTIME_DIR");
        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(runtimeDir))
        {
            if (Environment.GetEnvironmentVariable("WI_REQUIRE_LIVE_EMBEDDING") == "1")
                Assert.Fail("Required live embedding fixture or runtime is missing.");
            return;
        }
        Assert.True(File.Exists(model), "Real embedding GGUF is missing.");
        Assert.True(File.Exists(Path.Combine(runtimeDir, "llama-server.exe")), "Native llama.cpp is missing.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        await using var worker = new EmbeddingRuntime();
        await worker.StartAsync(model, timeout.Token);
        Assert.True(worker.IsRunning);
        Assert.NotNull(worker.ModelKey);
        var first = await worker.EmbedAsync("ambulance dispatch routes and geofences", timeout.Token);
        var second = await worker.EmbedAsync("employee training reimbursement", timeout.Token);
        Assert.Equal(first.Length, second.Length);
        Assert.InRange(first.Length, 16, 8192);
        Assert.All(first, x => Assert.True(float.IsFinite(x)));
        Assert.InRange(first.Sum(x => (double)x * x), 0.99, 1.01);
        Assert.True(first.Zip(second).Any(pair => Math.Abs(pair.First - pair.Second) > 0.000001f),
            "Different inputs unexpectedly received identical embedding vectors.");
        await worker.StopAsync();
        Assert.False(worker.IsRunning);
        Assert.Null(worker.ModelKey);
    }
}
