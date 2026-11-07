using System.IO.Compression;
using System.Text;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class RuntimeArchiveTests
{
    [Theory]
    [InlineData("../outside.dll")]
    [InlineData("..\\outside.dll")]
    [InlineData("C:\\outside.dll")]
    public void ComponentResolutionCannotEscapeBundle(string name)
        => Assert.Throws<ArgumentException>(() => RuntimeBundle.Resolve(name));

    private static MemoryStream Archive(params string[] names)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var name in names)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write("Synthetic inert test asset");
            }
        stream.Position = 0;
        return stream;
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/absolute.txt")]
    [InlineData("tool.exe:alternate-stream")]
    public void ArchiveTraversalAndWindowsAlternateStreamsAreRejected(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-archive-" + Guid.NewGuid().ToString("N"));
        using var archive = Archive(name);
        try { Assert.Throws<InvalidDataException>(() => RuntimeBundle.ExtractArchive(archive, root)); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("../late-traversal.dll")]
    [InlineData("C:/late-drive-escape.dll")]
    [InlineData("valid.dll:alternate")]
    [InlineData("./invalid.dll")]
    public void InvalidLateEntryCannotPartiallyInstallEarlierNativeComponents(string maliciousEntry)
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-preflight-" + Guid.NewGuid().ToString("N"));
        using var archive = Archive("safe/llama-server.exe", "safe/pdfium.dll", maliciousEntry);
        try
        {
            Assert.Throws<InvalidDataException>(() => RuntimeBundle.ExtractArchive(archive, root));
            Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void DuplicateCaseInsensitiveArchivePathsAreRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-archive-" + Guid.NewGuid().ToString("N"));
        using var archive = Archive("test.bin", "TEST.bin");
        try { Assert.Throws<InvalidDataException>(() => RuntimeBundle.ExtractArchive(archive, root)); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ReparseDirectoryCannotRedirectRuntimeExtraction()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-archive-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(root, "outside");
        var destination = Path.Combine(root, "bundle");
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(destination);
        Directory.CreateSymbolicLink(Path.Combine(destination, "assets"), outside);
        using var archive = Archive("assets/test.bin");
        try
        {
            Assert.Throws<InvalidDataException>(() => RuntimeBundle.ExtractArchive(archive, destination));
            Assert.Empty(Directory.GetFiles(outside));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ExtractionIsRepeatableAndRepairsTamperedAssets()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-archive-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var archive = Archive("assets/test.bin")) RuntimeBundle.ExtractArchive(archive, root);
            var file = Path.Combine(root, "assets", "test.bin");
            Assert.Equal("Synthetic inert test asset", File.ReadAllText(file));
            File.WriteAllText(file, "tampered");
            using (var archive = Archive("assets/test.bin")) RuntimeBundle.ExtractArchive(archive, root);
            Assert.Equal("Synthetic inert test asset", File.ReadAllText(file));
            using (var archive = Archive("assets/test.bin")) RuntimeBundle.ExtractArchive(archive, root);
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
