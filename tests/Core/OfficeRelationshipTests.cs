using System.IO.Compression;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class OfficeRelationshipTests
{
    private static byte[] Archive(params (string path, string text)[] entries)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var (path, text) in entries)
                using (var writer = new StreamWriter(zip.CreateEntry(path).Open())) writer.Write(text);
        return output.ToArray();
    }

    [Fact]
    public void PresentationUsesSlideListRatherThanFileNamesAndExcludesOrphanedSlides()
    {
        const string drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
        var bytes = Archive(
            ("ppt/presentation.xml", """
            <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
            <p:sldIdLst><p:sldId id="256" r:id="second"/><p:sldId id="257" r:id="first"/></p:sldIdLst></p:presentation>
            """),
            ("ppt/_rels/presentation.xml.rels", """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            <Relationship Id="first" Target="slides/slide1.xml"/><Relationship Id="second" Target="slides/slide2.xml"/></Relationships>
            """),
            ("ppt/slides/slide1.xml", $"<s xmlns:a=\"{drawing}\"><a:p><a:t>Last content</a:t></a:p></s>"),
            ("ppt/slides/slide2.xml", $"<s xmlns:a=\"{drawing}\"><a:p><a:t>First content</a:t></a:p></s>"),
            ("ppt/slides/slide3.xml", $"<s xmlns:a=\"{drawing}\"><a:p><a:t>Orphaned secret</a:t></a:p></s>"));
        var text = DocumentExtractor.Extract("fixture.pptx", bytes);
        Assert.True(text.IndexOf("First content", StringComparison.Ordinal) < text.IndexOf("Last content", StringComparison.Ordinal));
        Assert.Contains("[Slide 1]", text);
        Assert.DoesNotContain("Orphaned secret", text);
    }

    [Fact]
    public void WorkbookUsesActualSheetNamesAndRelationshipTargets()
    {
        var bytes = Archive(
            ("xl/workbook.xml", """
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
            <sheets><sheet name="Incident Register" sheetId="9" r:id="custom"/></sheets></workbook>
            """),
            ("xl/_rels/workbook.xml.rels", """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="custom" Target="worksheets/custom.xml"/></Relationships>
            """),
            ("xl/worksheets/custom.xml", """
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row><c r="C7" t="inlineStr"><is><t>Synthetic incident</t></is></c></row></sheetData></worksheet>
            """));
        var text = DocumentExtractor.Extract("fixture.xlsx", bytes);
        Assert.Contains("[Sheet Incident Register]", text);
        Assert.Contains("C7: Synthetic incident", text);
    }

    [Theory]
    [InlineData("../outside.xml", "")]
    [InlineData("https://example.com/content", "External")]
    [InlineData("worksheets/missing.xml", "")]
    public void UnsafeOrMissingReferencedPartsFailClosed(string target, string mode)
    {
        var bytes = Archive(
            ("xl/workbook.xml", """
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Test" r:id="r1"/></sheets></workbook>
            """),
            ("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r1\" Target=\"{target}\" TargetMode=\"{mode}\"/></Relationships>"));
        Assert.Throws<InvalidDataException>(() => DocumentExtractor.Extract("fixture.xlsx", bytes));
    }

    [Fact]
    public void InvalidSharedStringReferenceCannotMasqueradeAsNumericContent()
    {
        var bytes = Archive(("xl/worksheets/sheet1.xml", """
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row><c r="A1" t="s"><v>999</v></c></row></sheetData></worksheet>
            """));
        Assert.Throws<InvalidDataException>(() => DocumentExtractor.Extract("fixture.xlsx", bytes));
    }
}
