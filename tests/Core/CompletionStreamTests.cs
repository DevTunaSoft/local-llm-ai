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

    [Theory]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{not-json}")]
    [InlineData("{\"choices\":[null]}")]
    [InlineData("{\"choices\":[42]}")]
    [InlineData("{\"choices\":[{\"delta\":42}]}")]
    public async Task MalformedStreamingFramesNeverBecomeSuccessfulAnswers(string frame)
    {
        var wrapped = "data: " + frame + "\n\ndata: [DONE]\n\n";
        if (frame == "{}")
            Assert.Equal("", await Read(wrapped));
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => Read(wrapped));
    }

    [Fact]
    public async Task FuzzMalformedAndNestedJsonFramesRemainBounded()
    {
        var random = new Random(4029);
        for (var i = 0; i < 48; i++)
        {
            var count = random.Next(1, 512);
            var noise = new string(Enumerable.Range(0, count).Select(_ =>
                (char)random.Next(32, 127)).ToArray());
            try
            {
                await Read("data: " + noise + "\n\ndata: [DONE]\n\n");
            }
            catch (Exception ex)
            {
                Assert.True(ex is InvalidDataException or EndOfStreamException or System.Text.Json.JsonException,
                    $"Unexpected streaming failure {ex.GetType().Name} on iteration {i}");
            }
        }
        var deep = new string('[', 65) + new string(']', 65);
        await Assert.ThrowsAsync<InvalidDataException>(() => Read("data: " + deep + "\n\n"));
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
