using WorkIntelligence.Services;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class NativeOcrFactAttribute : FactAttribute
{
    public NativeOcrFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WI_REQUIRE_NATIVE_OCR") != "1" &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WI_NATIVE_TEST_ROOT")))
            Skip = "NOT EXECUTED: requires real Tesseract/PDFium and synthetic image fixtures.";
    }
}

public sealed class NativeOcrTests : IDisposable
{
    private readonly string? _previous = Environment.GetEnvironmentVariable("WI_DEV_RUNTIME_DIR");
    private readonly string _root = Environment.GetEnvironmentVariable("WI_NATIVE_TEST_ROOT") ?? "";
    public NativeOcrTests()
    {
        if (_root.Length != 0) Environment.SetEnvironmentVariable("WI_DEV_RUNTIME_DIR", Path.Combine(_root,"runtime"));
    }

    [NativeOcrFact]
    public void RealTesseractExtractsPngJpegBmpAndTiff()
    {
        Assert.True(Directory.Exists(_root), "Native OCR was required but its fixture directory is missing.");
        foreach (var extension in new[] {"png","jpeg","bmp","tiff"})
        {
            var file = Path.Combine(_root,"fixtures","synthetic."+extension);
            var text = DocumentExtractor.Extract(file, File.ReadAllBytes(file));
            Assert.Contains("SYNTHETIC RECOVERY 427",text);
        }
    }

    [NativeOcrFact]
    public void RealPdfiumAndTesseractExtractMixedPdfWithPageProvenance()
    {
        Assert.True(Directory.Exists(_root), "Native OCR was required but its fixture directory is missing.");
        var builder = new PdfDocumentBuilder();
        var searchable = builder.AddPage(600,300);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        searchable.AddText("Synthetic searchable first page",14,new PdfPoint(25,250),font);
        var scanned = builder.AddPage(600,300);
        scanned.AddPng(File.ReadAllBytes(Path.Combine(_root,"fixtures","synthetic.png")),new PdfRectangle(20,100,580,205));
        var bytes = builder.Build();
        var text = DocumentExtractor.Extract("mixed.pdf",bytes);
        Assert.Contains("[Page 1] Synthetic searchable first page",text);
        Assert.Contains("[Page 2] SYNTHETIC RECOVERY 427",text);
    }

    [Fact]
    public void CancelledExtractionDoesNotStartANativeProcess()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => DocumentExtractor.Extract("synthetic.png",new byte[16],cancelled.Token));
    }

    public void Dispose() => Environment.SetEnvironmentVariable("WI_DEV_RUNTIME_DIR",_previous);
}
