using System.Security.Cryptography;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

/// <summary>
/// Runs an actual llama-server process and streams a completion when a real GGUF
/// and native runtime are supplied. Never substitutes a mock HTTP endpoint.
/// </summary>
public sealed class LiveInferenceTests
{
    [Fact]
    public async Task RealLocalGgufProducesStreamingCompletion()
    {
        var model = Environment.GetEnvironmentVariable("WI_LIVE_TEST_GGUF");
        var expectedHash = Environment.GetEnvironmentVariable("WI_LIVE_TEST_GGUF_SHA256");
        var runtimeDir = Environment.GetEnvironmentVariable("WI_DEV_RUNTIME_DIR");
        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(expectedHash) ||
            string.IsNullOrWhiteSpace(runtimeDir))
        {
            // Default source CI has no vetted native runtime or model. This is not a pass
            // proving inference; the live test must be explicitly enabled by its inputs.
            Console.WriteLine("LIVE INFERENCE NOT EXECUTED: set WI_LIVE_TEST_GGUF, WI_LIVE_TEST_GGUF_SHA256, and WI_DEV_RUNTIME_DIR.");
            return;
        }
        Assert.True(File.Exists(model), "Live inference GGUF is missing.");
        Assert.True(File.Exists(Path.Combine(runtimeDir, "llama-server.exe")), "Native llama-server.exe is missing.");
        Assert.True(File.Exists(Path.Combine(runtimeDir, "SHA256SUMS.txt")), "Runtime hash manifest is missing.");
        Assert.Equal(64, expectedHash.Length);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        await using var runtime = new ModelRuntime();
        await runtime.StartAsync(model, expectedHash, new ModelRuntimeOptions(
            ContextSize: 1024, Threads: 4, MaxTokens: 48, Temperature: 0), timeout.Token);
        Assert.True(runtime.IsRunning);
        Assert.Equal(Path.GetFullPath(model), runtime.ActiveModel);
        var tokens = new List<string>();
        await foreach (var token in runtime.StreamAsync(
            new[] { (role: "user", content: "Reply with the word READY.") }, timeout.Token, maxTokens: 48))
        {
            tokens.Add(token);
        }
        Assert.False(string.IsNullOrWhiteSpace(string.Concat(tokens)), "Real model returned an empty completion.");
        await runtime.StopAsync();
        Assert.False(runtime.IsRunning);
        Assert.Null(runtime.ActiveModel);
    }
}
