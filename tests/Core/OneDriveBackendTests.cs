using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class OneDriveBackendTests
{
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
