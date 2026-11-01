using System.Text;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class CompletionStreamTests
{
    private static async Task<string> Read(string events, CancellationToken ct = default)
    {
        using var reader = new StringReader(events);
        var answer = new StringBuilder();
        await foreach (var text in CompletionStream.ReadAsync(reader, ct)) answer.Append(text);
        return answer.ToString();
    }

    [Fact]
    public async Task UnicodeContentAndCommentsAreHandled()
    {
        Assert.Equal("café 🧪", await Read(": ping\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"café 🧪\"}}]}\n\ndata: [DONE]\n\n"));
    }

    [Fact]
    public async Task NetworkEofWithoutDoneIsNotReportedAsSuccess()
    {
        await Assert.ThrowsAsync<EndOfStreamException>(() => Read("data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n"));
    }

    [Fact]
    public async Task ServerErrorEventIsNotReportedAsEmptySuccess()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Read("data: {\"error\":{\"message\":\"synthetic error\"}}\n\ndata: [DONE]\n\n"));
    }

    [Fact]
    public async Task MultilineSseDataIsAssembledBeforeJsonParsing()
    {
        Assert.Equal("joined", await Read("data: {\"choices\":\ndata: [{\"delta\":{\"content\":\"joined\"}}]}\n\ndata: [DONE]\n\n"));
    }

    [Fact]
    public async Task TokenLimitDoesNotLookLikeCompleteAnswer()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Read("data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"length\"}]}\n\ndata: [DONE]\n\n"));
    }

    [Fact]
    public async Task CancellationIsObservedEvenWithBufferedData()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Read("data: [DONE]\n\n", cts.Token));
    }

    [Fact]
    public async Task OversizedEventIsRejected()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Read("data: " + new string('x', 1_048_577) + "\n\n"));
    }
}
