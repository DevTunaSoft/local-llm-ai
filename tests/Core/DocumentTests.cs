using System.IO.Compression;
using System.Text;
using WorkIntelligence.Services;
using Xunit;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Core;

namespace WorkIntelligence.Tests;

public sealed class DocumentTests
{
    [Fact]
    public void ShortSearchablePdfDoesNotRequireOcr()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(200, 200);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Hello", 12, new PdfPoint(20, 100), font);
        Assert.Contains("Hello", DocumentExtractor.Extract("short.pdf", builder.Build()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Utf32BomIsDecodedBeforeUtf16(bool bigEndian)
    {
        var encoding = new UTF32Encoding(bigEndian, true, true);
        const string text = "Synthetic Unicode 🧪";
        Assert.Equal(text, DocumentExtractor.Extract("utf32.txt", encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray()));
    }

    [Fact]
    public void MalformedUnicodeIsRejected()
    {
        Assert.Throws<DecoderFallbackException>(() => DocumentExtractor.Extract("bad.txt", [0xFF, 0xFE, 0x41]));
        Assert.Throws<DecoderFallbackException>(() => DocumentExtractor.Extract("bad.txt", [0xC3, 0x28]));
    }

    [Fact]
    public void EmptyPdfPageDoesNotRequireNativeOcr()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(200, 200);
        Assert.True(string.IsNullOrWhiteSpace(DocumentExtractor.Extract("blank.pdf", builder.Build())));
    }

    [Fact]
    public void Utf8BomIsNotIndexedAsContent()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Synthetic work text")).ToArray();
        Assert.Equal("Synthetic work text", DocumentExtractor.Extract("bom.txt", bytes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WindowsUtf16WithBomIsDecoded(bool bigEndian)
    {
        var encoding = new UnicodeEncoding(bigEndian, byteOrderMark: true, throwOnInvalidBytes: true);
        const string content = "Synthetic café text 🧪";
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(content)).ToArray();
        Assert.Equal(content, DocumentExtractor.Extract("utf16.txt", bytes));
    }

    [Fact]
    public void DocxTabsAndBreaksDoNotMergeWords()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
            writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>alpha</w:t><w:tab/><w:t>beta</w:t><w:br/><w:t>gamma</w:t></w:r></w:p></w:body></w:document>");
        Assert.Equal("alpha\tbeta\ngamma" + Environment.NewLine,
            DocumentExtractor.Extract("format.docx", buffer.ToArray()));
    }

    [Fact]
    public void OfficeXmlRejectsExternalEntities()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
            writer.Write("<!DOCTYPE x [<!ENTITY payload SYSTEM 'file:///should-never-be-read'>]><x>&payload;</x>");
        Assert.Throws<System.Xml.XmlException>(() => DocumentExtractor.Extract("attack.docx", buffer.ToArray()));
    }

    [Fact]
    public void DocxExportRoundTripsUnicodeAndXmlMetacharacters()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wi-docx-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "report.docx");
        try
        {
            const string content = "Synthetic café & <markup> 🧪\nNext line";
            DocumentExporter.SaveDocx(file, "Fixture", content);
            var text = DocumentExtractor.Extract(file, File.ReadAllBytes(file));
            Assert.Contains(content.Replace("\n", Environment.NewLine), text);
            Assert.Single(Directory.GetFiles(folder));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
