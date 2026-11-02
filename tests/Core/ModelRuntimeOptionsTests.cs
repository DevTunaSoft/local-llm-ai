using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ModelRuntimeOptionsTests
{
    [Fact]
    public void DefaultsAreValid() => new ModelRuntimeOptions().Validate();

    [Theory]
    [InlineData(511, 8, 1024, 0.7)]
    [InlineData(131073, 8, 1024, 0.7)]
    [InlineData(4096, 0, 1024, 0.7)]
    [InlineData(4096, 257, 1024, 0.7)]
    [InlineData(4096, 8, 31, 0.7)]
    [InlineData(4096, 8, 32769, 0.7)]
    [InlineData(4096, 8, 1024, -0.1)]
    [InlineData(4096, 8, 1024, 2.1)]
    public void RejectsInvalidOptions(int context, int threads, int tokens, double temperature)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ModelRuntimeOptions(context, threads, tokens, temperature).Validate());

    [Theory]
    [InlineData(0, 600)]
    [InlineData(86401, 600)]
    [InlineData(900, 0)]
    [InlineData(900, 86401)]
    public void RejectsUnboundedTimeouts(int startup, int response)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ModelRuntimeOptions(
            StartupTimeoutSeconds: startup, ResponseTimeoutSeconds: response).Validate());

    [Fact]
    public void LegacySettingsReceiveFiniteDefaultTimeouts()
    {
        var options = System.Text.Json.JsonSerializer.Deserialize<ModelRuntimeOptions>(
            "{\"ContextSize\":4096,\"Threads\":8,\"MaxTokens\":1024,\"Temperature\":0.7}")!;
        Assert.Equal(900, options.StartupTimeoutSeconds);
        Assert.Equal(600, options.ResponseTimeoutSeconds);
        options.Validate();
    }

    [Fact]
    public void RejectsNonFiniteTemperature()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ModelRuntimeOptions(Temperature: double.NaN).Validate());
}
