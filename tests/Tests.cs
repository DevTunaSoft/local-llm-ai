using System.IO.Compression;
using System.Text;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class Tests
{
    [Fact]
    public void ChunkingPreservesContentBoundaries()
    {
        var text = new string('a', 5000);
        var parts = KnowledgeStore.Chunk(text).ToArray();
        Assert.True(parts.Length >= 4);
        Assert.All(parts, part => Assert.InRange(part.Length, 1, 1400));
        Assert.Equal(new string('a', 1400), parts[0]);
    }
    [Fact]
    public void ChunkingRejectsImpossibleOverlap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KnowledgeStore.Chunk("hello", 100, 100).ToArray());
    }
    [Theory]
    [InlineData("doc.txt")]
    [InlineData("doc.md")]
    [InlineData("doc.json")]
    [InlineData("doc.csv")]
    public void ExtractsUtf8Documents(string filename)
    {
        Assert.Equal("sample work content", DocumentExtractor.Extract(filename, Encoding.UTF8.GetBytes("sample work content")));
    }
    [Fact]
    public void RejectsBinaryAsText()
    {
        Assert.Throws<InvalidDataException>(() => DocumentExtractor.Extract("binary.txt", new byte[1000]));
    }
    [Fact]
    public void ExtractsDocxParagraph()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen:true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open(), Encoding.UTF8);
            writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Test document text</w:t></w:r></w:p></w:body></w:document>");
        }
        var text=DocumentExtractor.Extract("file.docx",buffer.ToArray());
        Assert.Contains("Test document text",text);
    }
    [Fact]
    public void ExtractsXlsxSharedStringsAndNumbers()
    {
        using var buffer=new MemoryStream();
        using(var archive=new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen:true))
        {
            using(var writer=new StreamWriter(archive.CreateEntry("xl/sharedStrings.xml").Open()))
                writer.Write("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>Ambulance</t></si></sst>");
            using(var writer=new StreamWriter(archive.CreateEntry("xl/worksheets/sheet1.xml").Open()))
                writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\"><v>5</v></c></row></sheetData></worksheet>");
        }
        var result=DocumentExtractor.Extract("data.xlsx",buffer.ToArray());
        Assert.Contains("Ambulance",result);Assert.Contains("B1: 5",result);
    }
    [Fact]
    public void RejectsUnsupportedFormat()
    {
        Assert.Throws<NotSupportedException>(()=>DocumentExtractor.Extract("picture.heic",[0,1,2]));
    }
    [Fact]
    public void OcrRejectsShortImageBeforeLaunchingBinary()
    {
        Assert.Throws<InvalidDataException>(() => DocumentExtractor.Extract("picture.png", new byte[7]));
    }
    [Fact]
    public async Task DownloaderRejectsUntrustedEndpointWithoutNetwork()
    {
        await Assert.ThrowsAsync<ArgumentException>(()=>ModelDownloader.DownloadAsync("http://127.0.0.1/private","demo.gguf",new string('a',64),null,CancellationToken.None));
    }
    [Fact]
    public void BundledRuntimeHasMatchingHashManifest()
    {
        var resources = typeof(RuntimeBundle).Assembly.GetManifestResourceNames();
        var archive = resources.Contains("WorkIntelligence.runtime.zip");
        var manifest = resources.Contains("WorkIntelligence.runtime.sha256");
        Assert.Equal(archive, manifest);
    }

    [Fact]
    public void RuntimeBundleFailsClosedWhenNotPackaged()
    {
        // Source-only build intentionally has no embedded native components.
        if (!typeof(RuntimeBundle).Assembly.GetManifestResourceNames().Contains("WorkIntelligence.runtime.zip"))
            Assert.Throws<FileNotFoundException>(()=>RuntimeBundle.Resolve("llama-server.exe"));
    }
}
