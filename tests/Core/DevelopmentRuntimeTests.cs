using System.Security.Cryptography;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class DevelopmentRuntimeTests
{
    [Fact]
    public void HashPinnedDevelopmentRuntimeAcceptsValidFileAndRejectsTampering()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previous = Environment.GetEnvironmentVariable("WI_DEV_RUNTIME_DIR");
        try
        {
            var runtime = Path.Combine(root, "llama-server.exe");
            File.WriteAllBytes(runtime, [1, 2, 3, 4]);
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(runtime)));
            File.WriteAllText(Path.Combine(root, "SHA256SUMS.txt"), hash + "  llama-server.exe" + Environment.NewLine);
            Environment.SetEnvironmentVariable("WI_DEV_RUNTIME_DIR", root);
            Assert.Equal(runtime, RuntimeBundle.Resolve("llama-server.exe"));
            File.AppendAllText(runtime, "tampered");
            Assert.Throws<InvalidDataException>(() => RuntimeBundle.Resolve("llama-server.exe"));
            Assert.Throws<ArgumentException>(() => RuntimeBundle.Resolve("../llama-server.exe"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WI_DEV_RUNTIME_DIR", previous);
            Directory.Delete(root, true);
        }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TransitiveDllTamperingOrAdditionIsRejected(bool listed)
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previous = Environment.GetEnvironmentVariable("WI_DEV_RUNTIME_DIR");
        try
        {
            var executable = Path.Combine(root, "llama-server.exe");
            var library = Path.Combine(root, "ggml.dll");
            File.WriteAllBytes(executable, [1, 2, 3, 4]);
            File.WriteAllBytes(library, [5, 6, 7, 8]);
            var manifest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable))) + "  llama-server.exe\n";
            if (listed) manifest += Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(library))) + "  ggml.dll\n";
            File.WriteAllText(Path.Combine(root, "SHA256SUMS.txt"), manifest);
            Environment.SetEnvironmentVariable("WI_DEV_RUNTIME_DIR", root);
            if (listed)
            {
                Assert.Equal(executable, RuntimeBundle.Resolve("llama-server.exe"));
                File.AppendAllText(library, "tampered");
            }
            Assert.Throws<InvalidDataException>(() => RuntimeBundle.Resolve("llama-server.exe"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WI_DEV_RUNTIME_DIR", previous);
            Directory.Delete(root, true);
        }
    }

}
