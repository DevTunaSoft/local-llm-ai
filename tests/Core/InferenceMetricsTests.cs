using System.Text;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class InferenceMetricsTests
{
    [Fact]
    public async Task FinalEmptyChoiceUsageChunkRecordsActualTokensAndTimings()
    {
        var sse="data: {\"choices\":[{\"delta\":{\"content\":\"Synthetic\"}}]}\n\n"+
            "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":5},\"timings\":{\"prompt_ms\":10,\"predicted_ms\":250}}\n\n"+
            "data: [DONE]\n\n";
        CompletionUsage? usage=null;var answer=new StringBuilder();
        await foreach(var part in CompletionStream.ReadAsync(new StringReader(sse),CancellationToken.None,value=>usage=value))answer.Append(part);
        Assert.Equal("Synthetic",answer.ToString());Assert.Equal(new CompletionUsage(12,5,10,250),usage);
        var metrics=new InferenceMetrics(usage,500,200,1024);
        Assert.Equal(10,metrics.EndToEndTokensPerSecond);Assert.Equal(20,metrics.GenerationTokensPerSecond);
    }
    [Fact]
    public async Task NegativeUsageIsRejectedInsteadOfInventingBenchmarks()
    {
        var sse="data: {\"choices\":[],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":-1}}\n\n";
        await Assert.ThrowsAsync<InvalidDataException>(async()=>{await foreach(var part in CompletionStream.ReadAsync(new StringReader(sse),CancellationToken.None)){} });
    }
    [Fact]
    public void MissingServerCountsRemainUnknown()=>Assert.Null(new InferenceMetrics(null,100,100,0).EndToEndTokensPerSecond);
}
