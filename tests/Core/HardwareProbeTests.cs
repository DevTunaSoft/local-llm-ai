using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class HardwareProbeTests
{
    [Fact]
    public void SmallModelsHaveScaledMemoryOverhead()
    {
        const long mib = 1024L * 1024;
        const long gib = 1024L * mib;
        var tiny = HardwareProbe.EstimateModelBytes(105 * mib);
        Assert.InRange(tiny, 768UL * (ulong)mib, 2UL * (ulong)gib);
        var large = HardwareProbe.EstimateModelBytes(20 * gib);
        Assert.True(large >= 25UL * (ulong)gib);
        Assert.True(large > tiny);
    }

    [Fact]
    public void InvalidModelSizeIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HardwareProbe.EstimateModelBytes(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => HardwareProbe.EstimateModelBytes(-1));
    }
}
