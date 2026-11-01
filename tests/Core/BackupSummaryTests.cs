using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class BackupSummaryTests
{
    [Fact]
    public void VerificationUsesTheExactCreatedSnapshot()
    {
        var id = new string('a', 64);
        Assert.Equal(id, BackupService.ReadSnapshotId("{\"message_type\":\"status\"}\n{\"message_type\":\"summary\",\"snapshot_id\":\"" + id + "\"}\n"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"message_type\":\"summary\"}")]
    [InlineData("{\"message_type\":\"summary\",\"snapshot_id\":\"short\"}")]
    public void NoSnapshotMustNotBePresentedAsAVerifiedBackup(string output)
        => Assert.Throws<InvalidDataException>(() => BackupService.ReadSnapshotId(output));
}
