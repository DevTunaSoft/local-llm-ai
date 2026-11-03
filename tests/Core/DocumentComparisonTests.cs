using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class DocumentComparisonTests
{
    [Fact]
    public void ComparisonIdentifiesChangedEvidenceAndPreservesSourceLabels()
    {
        var result = DocumentComparison.Compare("procedure v1", "Shared line\nOld rule\nShared end",
            "procedure v2", "Shared line\nNew rule\nShared end");
        Assert.Contains("Before: procedure v1", result);
        Assert.Contains("After: procedure v2", result);
        Assert.Contains("- Before line 2: Old rule", result);
        Assert.Contains("+ After line 2: New rule", result);
        Assert.Contains("unchanged lines: 2", result);
    }

    [Fact]
    public void RepeatedLinesAndWhitespaceDoNotInventChanges()
    {
        var result = DocumentComparison.Compare("A", "repeat\nrepeat\nend", "B", " repeat \n\nrepeat\nend");
        Assert.Contains("Added lines: 0; removed lines: 0; unchanged lines: 3", result);
    }

    [Fact]
    public void ExcessiveComparisonFailsRatherThanSilentlyTruncating()
        => Assert.Throws<InvalidDataException>(() => DocumentComparison.Compare("A", string.Join("\n", Enumerable.Repeat("line", 2001)), "B", "line"));
}
