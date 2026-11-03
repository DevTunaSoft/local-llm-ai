using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorkIntelligence.Services;

public sealed record ModelRuntimeOptions(int ContextSize = 4096, int Threads = 8, int MaxTokens = 1024, double Temperature = 0.7,
    int StartupTimeoutSeconds = 900, int ResponseTimeoutSeconds = 600, int? MemoryLimitMiB = null, string? VisionProjectorPath = null, string? VisionProjectorSha256 = null)
{
    public void Validate()
    {
        if((VisionProjectorPath is null)!=(VisionProjectorSha256 is null) ||
            VisionProjectorPath is not null && !Path.IsPathFullyQualified(VisionProjectorPath) ||
            VisionProjectorSha256 is not null && (VisionProjectorSha256.Length!=64 || !VisionProjectorSha256.All(Uri.IsHexDigit)))
            throw new ArgumentException("Vision requires an absolute local projector path and its trusted SHA-256.");
        if (MemoryLimitMiB is <256 or >1048576) throw new ArgumentOutOfRangeException(nameof(MemoryLimitMiB));
        if (StartupTimeoutSeconds is < 1 or > 86400) throw new ArgumentOutOfRangeException(nameof(StartupTimeoutSeconds));
        if (ResponseTimeoutSeconds is < 1 or > 86400) throw new ArgumentOutOfRangeException(nameof(ResponseTimeoutSeconds));
        if (ContextSize < 512 || ContextSize > 131072) throw new ArgumentOutOfRangeException(nameof(ContextSize));
        if (Threads < 1 || Threads > 256) throw new ArgumentOutOfRangeException(nameof(Threads));
        if (MaxTokens < 32 || MaxTokens > 32768) throw new ArgumentOutOfRangeException(nameof(MaxTokens));
        if (!double.IsFinite(Temperature) || Temperature < 0 || Temperature > 2) throw new ArgumentOutOfRangeException(nameof(Temperature));
    }
}

public sealed class ModelRuntime : IAsyncDisposable
{
    private ModelRuntimeOptions _options = new();
    private Process? _process;
    private HttpClient? _client;
    private string? _apiKey;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    public InferenceMetrics? LastMetrics {get;private set;}
    public double? LastLoadMilliseconds {get;private set;}
    public ulong PeakResidentBytes {get;private set;}
    public bool SupportsVision=>IsRunning && _options.VisionProjectorPath is not null;
    public string? ActiveModel { get; private set; }
    public bool IsRunning => _process is { HasExited: false };
    public ulong ResidentBytes
    {
        get{try
            {
                if(_process is not {HasExited:false})return 0;
                _process.Refresh();PeakResidentBytes=Math.Max(PeakResidentBytes,(ulong)Math.Max(0,_process.PeakWorkingSet64));
                return (ulong)Math.Max(0,_process.WorkingSet64);
            }
            catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception){return 0;}}
    }

    public Task StartAsync(string ggufPath, string expectedSha256, CancellationToken cancellationToken)
        => StartAsync(ggufPath, expectedSha256, new ModelRuntimeOptions(), cancellationToken);

    public async Task StartAsync(string ggufPath, string expectedSha256, ModelRuntimeOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
        await StopCoreAsync();
        _options = options;
        ggufPath = Path.GetFullPath(ggufPath);
        if (!File.Exists(ggufPath)) throw new FileNotFoundException("Model file missing.");
        if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("Expected SHA-256 must be 64 hexadecimal characters.", nameof(expectedSha256));
        HardwareProbe.ValidateModelMemory(ggufPath,options.ContextSize,options.MemoryLimitMiB,options.VisionProjectorPath);
        if(options.VisionProjectorPath is string projector)
        {
            _=GgufMetadata.Read(projector,cancellationToken);
            await using var projectorBytes=File.OpenRead(projector);
            var hash=Convert.ToHexString(await SHA256.HashDataAsync(projectorBytes,cancellationToken));
            if(!hash.Equals(options.VisionProjectorSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Vision projector changed since verification; reselect a trusted projector.");
        }
        await using (var verifyStream = File.OpenRead(ggufPath))
        {
            var header = new byte[4];
            verifyStream.ReadExactly(header);
            if (!header.AsSpan().SequenceEqual("GGUF"u8)) throw new InvalidDataException("Model is not a GGUF file.");
            verifyStream.Position = 0;
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
            "--ctx-size", options.ContextSize.ToString(System.Globalization.CultureInfo.InvariantCulture), "--threads", options.Threads.ToString(System.Globalization.CultureInfo.InvariantCulture), "--n-gpu-layers", "0", "--no-webui", "--alias", "work-intelligence-local", "--cors-origins", "localhost" }) start.ArgumentList.Add(arg);
        if(options.VisionProjectorPath is string visionProjector)
            foreach(var arg in new[]{"--mmproj",visionProjector,"--no-mmproj-offload"})start.ArgumentList.Add(arg);
        start.Environment["LLAMA_API_KEY"] = _apiKey;
        _process = new Process { StartInfo = start, EnableRaisingEvents = true };
        var loadTimer=Stopwatch.StartNew();
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
            startupTimeout.CancelAfter(TimeSpan.FromSeconds(options.StartupTimeoutSeconds));
            while (!startupTimeout.IsCancellationRequested)
            {
                startupTimeout.Token.ThrowIfCancellationRequested();
                if (_process is null || _process.HasExited) throw new InvalidOperationException("AI engine exited before it became ready. Check diagnostics.");
                try
                {
                    using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(startupTimeout.Token);
                    requestTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                    using var response = await _client.GetAsync("health", requestTimeout.Token);
                    _=ResidentBytes;
                    if (response.IsSuccessStatusCode) { ActiveModel = ggufPath;LastLoadMilliseconds=loadTimer.Elapsed.TotalMilliseconds;return; }
                }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) when (!startupTimeout.IsCancellationRequested) { }
                try { await Task.Delay(750, startupTimeout.Token); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { break; }
            }
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Model loading exceeded its configured startup timeout.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await StopCoreAsync();
            throw new TimeoutException("Model loading exceeded its configured startup timeout.");
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
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken, int maxTokens = 1024, VisionAttachment? image = null)
    {
        if(image is not null && !SupportsVision)throw new InvalidOperationException("Load a compatible model with a verified vision projector before sending an image.");
        if (_client is null || !IsRunning) throw new InvalidOperationException("Select and load a local model first.");
        LastMetrics=null;CompletionUsage? usage=null;var timer=Stopwatch.StartNew();
        await foreach (var fragment in StreamResponseAsync(_client, _options, messages, cancellationToken, maxTokens,image,value=>usage=value))
        {_=ResidentBytes;yield return fragment;}
        _=ResidentBytes;timer.Stop();LastMetrics=new(usage,timer.Elapsed.TotalMilliseconds,LastLoadMilliseconds,PeakResidentBytes);
    }

    internal static async IAsyncEnumerable<string> StreamResponseAsync(HttpClient client, ModelRuntimeOptions options,
        IReadOnlyList<(string role, string content)> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken, int maxTokens = 1024, VisionAttachment? image = null,Action<CompletionUsage>? usageReceived=null)
    {
        options.Validate();
        using var responseTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        responseTimeout.CancelAfter(TimeSpan.FromSeconds(options.ResponseTimeoutSeconds));
        var token = responseTimeout.Token;
        var lastUser=-1;
        for(var i=0;i<messages.Count;i++)if(messages[i].role=="user")lastUser=i;
        if(image is not null && lastUser<0)throw new ArgumentException("An image requires a user message.");
        var messageList=messages.Select((x,index)=>new{role=x.role,content=image is not null && index==lastUser
            ? (object)new object[]{new{type="text",text=x.content},new{type="image_url",image_url=new{url=image.DataUrl}}}
            : x.content});
        var payload = JsonSerializer.Serialize(new { model = "work-intelligence-local", stream = true, stream_options=new{include_usage=true}, messages = messageList, max_tokens = Math.Clamp(maxTokens == 1024 ? options.MaxTokens : maxTokens, 32, options.MaxTokens), temperature = options.Temperature });
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        HttpResponseMessage response;
        try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("Local response exceeded its configured timeout."); }
        using (response)
        {
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);
            await using var fragments = CompletionStream.ReadAsync(reader, token,usageReceived).GetAsyncEnumerator(token);
            while (true)
            {
                bool next;
                try { next = await fragments.MoveNextAsync(); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                { throw new TimeoutException("Local response exceeded its configured timeout; any partial answer is incomplete."); }
                if (!next) break;
                yield return fragments.Current;
            }
        }
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();
        try { await StopCoreAsync(); }
        finally { _lifecycle.Release(); }
    }

    private async Task StopCoreAsync()
    {
        _client?.Dispose(); _client=null; _apiKey=null; ActiveModel=null;LastMetrics=null;LastLoadMilliseconds=null;PeakResidentBytes=0;
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
