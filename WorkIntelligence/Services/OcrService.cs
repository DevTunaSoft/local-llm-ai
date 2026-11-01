using System.Diagnostics;
using System.Text;

namespace WorkIntelligence.Services;

/// <summary>Local image OCR using a vetted, bundled Tesseract executable.
/// stdin/stdout avoid writing the document image or OCR text to temporary plaintext files.</summary>
public static class OcrService
{
    public static string ExtractImage(ReadOnlySpan<byte> image)
    {
        if (image.Length is < 8 or > 25 * 1024 * 1024)
            throw new InvalidDataException("OCR image exceeds permitted size or is empty.");
        // Copy is necessary because redirected stdin requires a stream; no local temp image is created.
        var payload = image.ToArray();
        try { return ExtractCoreAsync(payload, CancellationToken.None).GetAwaiter().GetResult(); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload); }
    }

    private static async Task<string> ExtractCoreAsync(byte[] image, CancellationToken cancellationToken)
    {
        var tesseract = RuntimeBundle.Resolve("tesseract.exe");
        var dataDir = Path.Combine(Path.GetDirectoryName(tesseract)!, "tessdata");
        if (!File.Exists(Path.Combine(dataDir, "eng.traineddata")))
            throw new FileNotFoundException("Bundled English OCR model is missing.");
        var info = new ProcessStartInfo(tesseract)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(tesseract)!,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (var arg in new[] { "stdin", "stdout", "--tessdata-dir", dataDir, "-l", "eng", "--psm", "3" })
            info.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("Bundled local OCR engine did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        using var registration = timeout.Token.Register(() => ProcessTermination.RequestStop(process));
        // Start draining both output channels before writing stdin, avoiding pipe deadlock.
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(image, timeout.Token);
            await process.StandardInput.BaseStream.FlushAsync(timeout.Token);
            process.StandardInput.Close();
            await ProcessTermination.WaitForExitAsync(process,timeout.Token);
            var result = await stdout;
            _ = await stderr; // Never log possibly confidential OCR diagnostics.
            if (process.ExitCode != 0) throw new InvalidDataException($"Local OCR failed (exit code {process.ExitCode}).");
            if (result.Length > 12_000_000) throw new InvalidDataException("OCR result exceeds size limit.");
            return result;
        }
        catch (OperationCanceledException)
        {
            await ProcessTermination.StopAsync(process);
            if (!cancellationToken.IsCancellationRequested)
                throw new TimeoutException("Local OCR exceeded its two-minute processing limit.");
            throw;
        }
    }
}
