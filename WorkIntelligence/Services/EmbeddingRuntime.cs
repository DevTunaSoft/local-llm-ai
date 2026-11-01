using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorkIntelligence.Services;

/// <summary>Only talks to a local, authenticated llama-server embedding worker.
/// No cloud embedding endpoint and no plaintext index files.</summary>
public sealed class EmbeddingRuntime : IAsyncDisposable
{
    private Process? _process;
    private HttpClient? _client;
    public string? ModelKey { get; private set; }
    public bool IsRunning => _process is { HasExited: false } && _client is not null;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);

    public async Task StartAsync(string modelFile, CancellationToken ct)
    {
        await _lifecycle.WaitAsync(ct);
        try
        {
            await StopCoreAsync();
            modelFile = Path.GetFullPath(modelFile);
            if (!File.Exists(modelFile)) throw new FileNotFoundException("Local embedding model not found.");
            using var modelStream = File.OpenRead(modelFile);
            var header = new byte[4];
            modelStream.ReadExactly(header);
            if (!header.AsSpan().SequenceEqual("GGUF"u8)) throw new InvalidDataException("Embedding model is not GGUF.");
            modelStream.Position=0;
            // Hash identifies the exact embedding space; vectors from other models are never mixed.
            var hash = Convert.ToHexString(SHA256.HashData(modelStream));
            HardwareProbe.ValidateModelMemory(modelFile);
            var server = RuntimeBundle.Resolve("llama-server.exe");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var info = new ProcessStartInfo(server)
            {
                UseShellExecute=false, CreateNoWindow=true,
                RedirectStandardOutput=true, RedirectStandardError=true,
                WorkingDirectory=Path.GetDirectoryName(server)!
            };
            foreach(var arg in new[] {"--model",modelFile,"--host","127.0.0.1","--port",port.ToString(),
                "--embedding","--pooling","mean","--ctx-size","2048","--threads","4","--n-gpu-layers","0","--no-webui","--cors-origins","localhost"})
                info.ArgumentList.Add(arg);
            info.Environment["LLAMA_API_KEY"] = key;
            _process = new Process {StartInfo=info};
            if (!_process.Start()) throw new InvalidOperationException("Unable to start local embedding runtime.");
            _process.OutputDataReceived+=(_,e)=>{if(e.Data is not null) AuditLog.Info("EmbeddingRuntime","Local runtime diagnostic event (redacted).");};
            _process.ErrorDataReceived+=(_,e)=>{if(e.Data is not null) AuditLog.Info("EmbeddingRuntime","Local runtime error event (redacted).");};
            _process.BeginOutputReadLine();_process.BeginErrorReadLine();
            _client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            {BaseAddress=new Uri($"http://127.0.0.1:{port}/"),Timeout=TimeSpan.FromMinutes(5)};
            _client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",key);
            using var readiness = CancellationTokenSource.CreateLinkedTokenSource(ct);
            readiness.CancelAfter(TimeSpan.FromMinutes(5));
            while(true)
            {
                readiness.Token.ThrowIfCancellationRequested();
                if (_process.HasExited) throw new InvalidOperationException("Embedding worker exited before readiness.");
                try
                {
                    using var requestTimeout=CancellationTokenSource.CreateLinkedTokenSource(readiness.Token);
                    requestTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                    using var response=await _client.GetAsync("health",requestTimeout.Token);
                    if(response.IsSuccessStatusCode) {ModelKey=hash;return;}
                }
                catch(HttpRequestException) { }
                catch(OperationCanceledException) when (!readiness.IsCancellationRequested) { }
                await Task.Delay(500,readiness.Token);
            }
        }
        catch { await StopCoreAsync();throw; }
        finally { _lifecycle.Release(); }
    }

    public async Task<float[]> EmbedAsync(string input, CancellationToken ct)
    {
        if(!IsRunning || _client is null) throw new InvalidOperationException("Load a local embedding model first.");
        using var response=await _client.PostAsync("v1/embeddings",
            new StringContent(JsonSerializer.Serialize(new {input}), Encoding.UTF8,"application/json"),ct);
        response.EnsureSuccessStatusCode();
        using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var vector=document.RootElement.GetProperty("data")[0].GetProperty("embedding");
        if(vector.ValueKind!=JsonValueKind.Array || vector.GetArrayLength() is < 16 or > 8192)
            throw new InvalidDataException("Embedding vector has invalid dimensions.");
        var floats=new float[vector.GetArrayLength()];
        double squared=0;
        for (var i=0;i<floats.Length;i++)
        {
            floats[i]=vector[i].GetSingle();
            if(!float.IsFinite(floats[i])) throw new InvalidDataException("Nonfinite embedding component.");
            squared+=(double)floats[i]*floats[i];
        }
        if(squared<=1e-18) throw new InvalidDataException("Embedding vector has zero magnitude.");
        var inverse=1d/Math.Sqrt(squared);
        for(var i=0;i<floats.Length;i++) floats[i]=(float)(floats[i]*inverse);
        return floats;
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();try {await StopCoreAsync();}finally {_lifecycle.Release();}
    }
    private async Task StopCoreAsync()
    {
        _client?.Dispose();_client=null;ModelKey=null;
        if(_process is not null)
        {
            await ProcessTermination.StopAsync(_process);
            _process.Dispose();_process=null;
        }
    }
    public async ValueTask DisposeAsync() {await StopAsync();_lifecycle.Dispose();}
}
