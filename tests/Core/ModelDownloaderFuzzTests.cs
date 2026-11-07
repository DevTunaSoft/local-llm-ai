using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ModelDownloaderFuzzTests
{
    private static readonly string ValidHash = new('A', 64);
    private const string ValidUrl = "https://huggingface.co/approved/model/resolve/main/model.gguf";

    [Theory]
    [InlineData("../escape.gguf")]
    [InlineData("/absolute.gguf")]
    [InlineData("CON.gguf")]
    [InlineData("con.synthetic.gguf")]
    [InlineData("NUL.gguf")]
    [InlineData("COM1.gguf")]
    [InlineData("LPT9.gguf")]
    [InlineData(".hidden.gguf")]
    [InlineData("a\\b.gguf")]
    [InlineData("a/b.gguf")]
    [InlineData("a:stream.gguf")]
    [InlineData("a.gguf ")]
    [InlineData("a.exe")]
    public void DangerousModelNamesAreRejectedBeforeNetworkAccess(string filename)
        => Assert.Throws<ArgumentException>(() =>
            ModelDownloader.ValidateDownloadRequest(ValidUrl, filename, ValidHash));

    [Theory]
    [InlineData("http://huggingface.co/a.gguf")]
    [InlineData("https://huggingface.co.evil.example/a.gguf")]
    [InlineData("https://user:pass@huggingface.co/a.gguf")]
    [InlineData("https://huggingface.co:8443/a.gguf")]
    [InlineData("https://127.0.0.1/a.gguf")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://evil.example/a.gguf")]
    public void UnsafeModelOriginsAreRejectedBeforeNetworkAccess(string url)
        => Assert.Throws<ArgumentException>(() =>
            ModelDownloader.ValidateDownloadRequest(url, "safe.gguf", ValidHash));

    [Fact]
    public void RandomizedUrlAndNameMutationsRejectPathEscapes()
    {
        var random = new Random(991727);
        const string unsafeChars = "/\\:*?<>|";
        for (var i = 0; i < 150; i++)
        {
            var prefix = "model" + (char)unsafeChars[random.Next(unsafeChars.Length)] + i + ".gguf";
            Assert.Throws<ArgumentException>(() =>
                ModelDownloader.ValidateDownloadRequest(ValidUrl, prefix, ValidHash));
        }
    }

    [Fact]
    public void SizeBoundaryArithmeticCannotOverflowOrFillDiskWithoutLimit()
    {
        var cap = ModelDownloader.MaxDownloadBytes;
        ModelDownloader.ValidateDownloadSize(0, cap);
        ModelDownloader.ValidateDownloadSize(cap - 1, 1);
        Assert.Throws<InvalidDataException>(() => ModelDownloader.ValidateDownloadSize(cap, 1));
        Assert.Throws<InvalidDataException>(() => ModelDownloader.ValidateDownloadSize(long.MaxValue, 1));
        Assert.Throws<InvalidDataException>(() => ModelDownloader.ValidateDownloadSize(1, long.MaxValue));
        Assert.Throws<InvalidDataException>(() => ModelDownloader.ValidateDownloadSize(-1, 0));
        var random = new Random(77123);
        for (var i = 0; i < 150; i++)
        {
            var offset = random.NextInt64(0, cap);
            var excess = random.NextInt64(1, 1_000_000);
            Assert.Throws<InvalidDataException>(() =>
                ModelDownloader.ValidateDownloadSize(offset, cap - offset + excess));
        }
    }

    [Fact]
    public void ValidTrustedDownloadDescriptionPassesValidation()
    {
        Assert.Equal("huggingface.co",
            ModelDownloader.ValidateDownloadRequest(ValidUrl, "qwen-27b-q4_k_m.gguf", ValidHash).Host);
        Assert.Throws<ArgumentException>(() =>
            ModelDownloader.ValidateDownloadRequest(ValidUrl, "safe.gguf", "not-a-digest"));
    }
}
