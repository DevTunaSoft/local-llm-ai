using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class StartupOptionsTests
{
    [Fact]
    public void SourceDllScheduledCommandIncludesApplicationAndDataDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "Synthetic Work Data");
        var dotnet = Path.Combine(Path.GetTempPath(), "Program Files", "dotnet.exe");
        var assembly = Path.Combine(Path.GetTempPath(), "Work Intelligence", "WorkIntelligence.dll");
        Assert.Equal($"\"{dotnet}\" \"{assembly}\" --scheduled-backup --data-root \"{root}\"",
            StartupOptions.ScheduledCommand(dotnet, assembly, root));
        Assert.Equal(root, StartupOptions.DataRoot(["--scheduled-backup", "--data-root", root]));
    }

    [Fact]
    public void StandaloneCommandDoesNotTreatTheApplicationAsDotnet()
    {
        var executable = Path.Combine(Path.GetTempPath(), "WorkIntelligence.exe");
        var root = Path.Combine(Path.GetTempPath(), "Synthetic Data");
        Assert.Equal($"\"{executable}\" --scheduled-backup --data-root \"{root}\"",
            StartupOptions.ScheduledCommand(executable, null, root));
    }

    [Fact]
    public void AmbiguousOrRelativeDataRootsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => StartupOptions.DataRoot(["--data-root"]));
        Assert.Throws<ArgumentException>(() => StartupOptions.DataRoot(["--data-root", "relative"]));
        Assert.Throws<ArgumentException>(() => StartupOptions.DataRoot(["--data-root", Path.GetTempPath(), "--data-root", Path.GetTempPath()]));
        Assert.Null(StartupOptions.DataRoot([]));
    }
}
