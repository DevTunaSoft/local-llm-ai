using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class BoundedProcessOutputTests
{
    [Fact]
    public async Task ExactBoundarySucceedsAndOverflowCancelsOwner()
    {
        using var exact = new CancellationTokenSource();
        Assert.Equal("example", await BoundedProcessOutput.ReadAsync(new StringReader("example"), 7, exact));
        Assert.False(exact.IsCancellationRequested);
        using var overflow = new CancellationTokenSource();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            BoundedProcessOutput.ReadAsync(new StringReader("example!"), 7, overflow));
        Assert.True(overflow.IsCancellationRequested);
    }

    [Fact]
    public async Task SeededOutputFuzzEnforcesCharacterBudget()
    {
        var random = new Random(17017);
        for (var i = 0; i < 160; i++)
        {
            var limit = random.Next(1, 500);
            var length = random.Next(0, 1000);
            var output = new string((char)('a' + i % 26), length);
            using var owner = new CancellationTokenSource();
            if (length <= limit)
            {
                Assert.Equal(output, await BoundedProcessOutput.ReadAsync(new StringReader(output), limit, owner));
                Assert.False(owner.IsCancellationRequested);
            }
            else
            {
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    BoundedProcessOutput.ReadAsync(new StringReader(output), limit, owner));
                Assert.True(owner.IsCancellationRequested);
            }
        }
    }

    [Fact]
    public async Task CancellationInterruptsInputAndRejectsInvalidBounds()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BoundedProcessOutput.ReadAsync(new StringReader("fixture"), 10, cancelled));
        using var owner = new CancellationTokenSource();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            BoundedProcessOutput.ReadAsync(new StringReader("fixture"), 0, owner));
    }
}
