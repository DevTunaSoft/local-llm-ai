using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ScheduledBackupTests
{
    [Fact]
    public void BackupCatchUpBecomesDueAfterTwentyFiveHours()
    {
        var now = new DateTimeOffset(2026, 11, 2, 19, 0, 0, TimeSpan.Zero);
        Assert.True(BackupRetention.IsOverdue(null, now));
        Assert.False(BackupRetention.IsOverdue(now.AddHours(-24), now));
        Assert.False(BackupRetention.IsOverdue(now.AddHours(-24.99), now));
        Assert.True(BackupRetention.IsOverdue(now.AddHours(-25), now));
        Assert.True(BackupRetention.IsOverdue(now.AddDays(-7), now));
    }
}
