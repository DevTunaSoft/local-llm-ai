using WorkIntelligence.Services;
using Xunit;
using UglyToad.PdfPig;

namespace WorkIntelligence.Tests;

public sealed class PdfExportTests
{
    private static string Font => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf")
        : "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

    [Fact]
    public void PdfRoundTripsTextAndPaginatesWithoutLosingLines()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wi-pdf-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "report.pdf");
        try
        {
            var body = string.Join("\n", Enumerable.Range(1, 110).Select(x => $"Synthetic row {x:D3} café & <source>"));
            PdfExporter.Save(file, "Synthetic report", body, Font);
            using var document = PdfDocument.Open(file);
            Assert.True(document.NumberOfPages >= 3);
            var text = string.Join("\n", document.GetPages().Select(x => x.Text));
            Assert.Contains("Synthetic report", text);
            foreach (var i in Enumerable.Range(1, 110)) Assert.Contains($"Synthetic row {i:D3} café & <source>", text);
            Assert.Single(Directory.GetFiles(folder));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void FailedExportPreservesPriorFile()
    {
        var file = Path.Combine(Path.GetTempPath(), "wi-pdf-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            File.WriteAllText(file, "existing report");
            Assert.Throws<InvalidDataException>(() => PdfExporter.Save(file, "Report", "data", file + ".missing.ttf"));
            Assert.Equal("existing report", File.ReadAllText(file));
        }
        finally { File.Delete(file); }
    }
}
