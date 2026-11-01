using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorkIntelligence.Services;

public sealed class ModelRuntime : IAsyncDisposable
{
    private Process? _process;
    private HttpClient? _client;
    private string? _apiKey;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    public string? ActiveModel { get; private set; }
    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(string ggufPath, string expectedSha256, CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
        await StopCoreAsync();
        if (!File.Exists(ggufPath)) throw new FileNotFoundException("Model file missing.");
        HardwareProbe.ValidateModelMemory(ggufPath);
        await using (var verifyStream = File.OpenRead(ggufPath))
        {
            var actualSha = Convert.ToHexString(await SHA256.HashDataAsync(verifyStream, cancellationToken));
            if (!actualSha.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Model file was modified since import. Re-import and verify the trusted model file.");
        }
        var runtime = RuntimeBundle.Resolve("llama-server.exe");
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        _apiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var start = new ProcessStartInfo(runtime)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(runtime)!
        };
        foreach (var arg in new[] { "--model", ggufPath, "--host", "127.0.0.1", "--port", port.ToString(),
            "--ctx-size", "4096", "--threads", "8", "--n-gpu-layers", "0", "--no-webui", "--alias", "work-intelligence-local", "--cors-origins", "localhost" }) start.ArgumentList.Add(arg);
        start.Environment["LLAMA_API_KEY"] = _apiKey;
        _process = new Process { StartInfo = start, EnableRaisingEvents = true };
        if (!_process.Start()) throw new InvalidOperationException("Failed to start local AI engine.");
        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) AuditLog.Info("llama-stdout", "Native AI runtime emitted a diagnostic line (content redacted)."); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) AuditLog.Info("llama-stderr", "Native AI runtime emitted a diagnostic line (content redacted)."); };
        _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
        _client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
        { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        try
        {
            using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Larger CPU-only models can legitimately require longer than five minutes.
            startupTimeout.CancelAfter(TimeSpan.FromMinutes(15));
            while (!startupTimeout.IsCancellationRequested)
            {
                startupTimeout.Token.ThrowIfCancellationRequested();
                if (_process is null || _process.HasExited) throw new InvalidOperationException("AI engine exited before it became ready. Check diagnostics.");
                try
                {
                    using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(startupTimeout.Token);
                    requestTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                    using var response = await _client.GetAsync("health", requestTimeout.Token);
                    if (response.IsSuccessStatusCode) { ActiveModel = ggufPath; return; }
                }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) when (!startupTimeout.IsCancellationRequested) { }
                try { await Task.Delay(750, startupTimeout.Token); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { break; }
            }
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Model loading exceeded the fifteen-minute startup limit.");
        }
        catch { await StopCoreAsync(); throw; }
        }
        finally
        {
            // Covers failures before the readiness loop, including Process.Start().
            try { if (ActiveModel is null) await StopCoreAsync(); }
            finally { _lifecycle.Release(); }
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<(string role,string content)> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken, int maxTokens = 1024)
    {
        if (_client is null || !IsRunning) throw new InvalidOperationException("Select and load a local model first.");
        var messageList = messages.Select(x => new { role = x.role, content = x.content });
        var payload = JsonSerializer.Serialize(new { model = "work-intelligence-local", stream = true, messages = messageList, max_tokens = Math.Clamp(maxTokens, 32, 2048) });
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        await foreach (var fragment in CompletionStream.ReadAsync(reader, cancellationToken))
            yield return fragment;
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();
        try { await StopCoreAsync(); }
        finally { _lifecycle.Release(); }
    }

    private async Task StopCoreAsync()
    {
        _client?.Dispose(); _client=null; _apiKey=null; ActiveModel=null;
        if (_process is not null)
        {
            await ProcessTermination.StopAsync(_process);
            _process.Dispose();
            _process = null;
        }
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifecycle.Dispose();
    }
}
