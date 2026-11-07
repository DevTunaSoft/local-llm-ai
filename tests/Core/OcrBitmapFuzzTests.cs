using System.Buffers.Binary;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class OcrBitmapFuzzTests
{
    private static byte[] SyntheticBmp(int size)
    {
        var bytes = new byte[size];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2, 4), size);
        return bytes;
    }

    [Fact]
    public void InternalPdfRendererCanUseBitmapsLargerThanExternalImageLimit()
    {
        // Previously the 25 MiB external image gate blocked legitimate
        // high-resolution scanned PDFs after PDFium had rendered them.
        var rendered = SyntheticBmp(26 * 1024 * 1024 + 54);
        OcrService.ValidateRenderedBmp(rendered);
    }

    [Fact]
    public void OversizedBmpAndMismatchedHeadersAreRejected()
    {
        OcrService.ValidateRenderedBmp(SyntheticBmp(58));
        Assert.Throws<InvalidDataException>(() => OcrService.ValidateRenderedBmp(new byte[0]));
        var badLength = SyntheticBmp(58);
        BinaryPrimitives.WriteInt32LittleEndian(badLength.AsSpan(2, 4), 600);
        Assert.Throws<InvalidDataException>(() => OcrService.ValidateRenderedBmp(badLength));
        var badMagic = SyntheticBmp(58);
        badMagic[0] = 0;
        Assert.Throws<InvalidDataException>(() => OcrService.ValidateRenderedBmp(badMagic));
        Assert.Throws<InvalidDataException>(() =>
            OcrService.ValidateRenderedBmp(new byte[54 + 64 * 1024 * 1024 + 1]));
    }

    [Fact]
    public void SeededHeaderFuzzDoesNotAcceptArbitraryPayloads()
    {
        var random = new Random(20261107);
        for (var i = 0; i < 128; i++)
        {
            var size = random.Next(0, 400);
            var bytes = new byte[size];
            random.NextBytes(bytes);
            Assert.Throws<InvalidDataException>(() => OcrService.ValidateRenderedBmp(bytes));
        }
    }
}
