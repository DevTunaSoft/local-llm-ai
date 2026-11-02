using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class OneDriveBackendTests
{
    [Fact]
    public void RestoreRequiresByteVerificationAgainstRemoteBeforeVaultPromotion()
    {
        var stage = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wi-stage-test"));
        var args = RemoteBackupService.BuildRestoreVerificationArgs("workonedrive:WorkBackups", stage);
        Assert.Equal(new[] { "check", "workonedrive:WorkBackups", stage, "--download", "--one-way" }, args);
        Assert.Throws<ArgumentException>(() =>
            RemoteBackupService.BuildRestoreVerificationArgs("localfolder:WorkBackups/../secret", stage));
        Assert.Throws<ArgumentException>(() =>
            RemoteBackupService.BuildRestoreVerificationArgs("workonedrive:WorkBackups", "relative-stage"));
    }

    [Theory]
    [InlineData("[workonedrive]\ntype = onedrive\ntoken = XXX\n", true)]
    [InlineData("[workonedrive]\ntype = s3\n", false)]
    [InlineData("[another]\ntype = onedrive\n[workonedrive]\ntype = s3\n", false)]
    [InlineData("[another]\ntype = onedrive\n", false)]
    [InlineData("[workonedrive]\ntype = onedrive-alias\n", false)]
    [InlineData("[workonedrive]\ntype = onedrive\n", true)]
    public void OnlyDirectNamedOneDriveBackendIsAccepted(string config, bool allowed)
        => Assert.Equal(allowed, RemoteBackupService.IsOneDriveConfig("workonedrive", config));
}
