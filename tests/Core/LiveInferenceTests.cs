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
            if (Environment.GetEnvironmentVariable("WI_REQUIRE_LIVE_INFERENCE") == "1")
                Assert.Fail("Live GGUF inference was required but model, SHA256, or native runtime directory was not provided.");
            Console.WriteLine("LIVE INFERENCE NOT EXECUTED: supply model, SHA256 and native runtime to run this optional integration test.");
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
        // A second start in the same process catches leaked ports, orphaned
        // workers, disposed HTTP clients, and lifecycle state that only works once.
        await runtime.StartAsync(model, expectedHash, new ModelRuntimeOptions(
            ContextSize: 1024, Threads: 4, MaxTokens: 48, Temperature: 0), timeout.Token);
        Assert.True(runtime.IsRunning);
        var repeated = new List<string>();
        await foreach (var token in runtime.StreamAsync(
            new[] { (role: "user", content: "Reply with the word AGAIN.") }, timeout.Token, maxTokens: 48))
            repeated.Add(token);
        Assert.False(string.IsNullOrWhiteSpace(string.Concat(repeated)), "Restarted model returned an empty completion.");
        // Persist the actual model output and an explicitly approved learning item,
        // reopen the production SQLite store, and verify workspace-scoped recall.
        var actualCompletion = string.Concat(repeated);
        long workspace;
        long chat;
        using (var store = new KnowledgeStore())
        {
            workspace = store.AddWorkspace("Real inference integration workspace");
            chat = store.CreateChat("GGUF live inference", workspace);
            store.AddMessage(chat, "user", "Reply with the word AGAIN.", workspace);
            store.AddMessage(chat, "assistant", actualCompletion, workspace);
            var proposal = store.ProposeMemory("Synthetic inference fixture approved", "live test approval", workspaceId: workspace);
            Assert.Empty(store.Memories(workspace));
            store.ResolveMemoryProposal(proposal, true);
        }
        using (var reopened = new KnowledgeStore())
        {
            var messages = reopened.Messages(chat, workspace);
            Assert.Equal(2, messages.Count);
            Assert.Equal(actualCompletion, messages[1].Content);
            Assert.Contains(reopened.Memories(workspace), m => m.Text == "Synthetic inference fixture approved");
            Assert.Empty(reopened.Memories(1));
        }
        await runtime.StopAsync();
        Assert.False(runtime.IsRunning);
        Assert.Null(runtime.ActiveModel);
    }
}
