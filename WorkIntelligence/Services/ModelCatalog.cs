using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkIntelligence.Services;

public sealed record CatalogPublisher(string Repository,string Description);
public sealed record CatalogModel(string Repository,string Revision,string FileName,string Sha256,long Bytes,string? License)
{
    public string DownloadUrl=>$"https://huggingface.co/{Repository}/resolve/{Revision}/"+string.Join("/",FileName.Split('/').Select(Uri.EscapeDataString))+"?download=true";
    public string LocalFileName=>Path.GetFileName(FileName);
    public string DisplayName=>$"{LocalFileName} — {Bytes/(1024d*1024*1024):F2} GiB — {Revision[..8]}";
}

/// <summary>Explicit publisher metadata lookup. Never sends work content or downloads weights automatically.</summary>
public static class ModelCatalog
{
    public static IReadOnlyList<CatalogPublisher> Publishers {get;} = [
        new("ggml-org/gpt-oss-20b-GGUF","GPT-OSS 20B — ggml-org engine-maintainer GGUF conversion; inspect upstream license"),
        new("unsloth/Qwen3.5-27B-GGUF","Qwen3.5 27B — Unsloth GGUF conversion of the Qwen model; review converter provenance and upstream license")];
    public static async Task<IReadOnlyList<CatalogModel>> FetchAsync(string repository,CancellationToken ct)
    {
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(60)};
        return await FetchWithClientAsync(client,repository,ct);
    }
    internal static async Task<IReadOnlyList<CatalogModel>> FetchWithClientAsync(HttpClient client,string repository,CancellationToken ct)
    {
        if(!Regex.IsMatch(repository,@"^[A-Za-z0-9_-]{1,80}/[A-Za-z0-9_.-]{1,120}$"))throw new ArgumentException("Enter a publisher/model repository, without URL, credentials or query parameters.");
        using var response=await client.GetAsync("https://huggingface.co/api/models/"+repository+"?blobs=true",HttpCompletionOption.ResponseHeadersRead,ct);
        if(response.StatusCode==HttpStatusCode.NotFound)throw new InvalidDataException("Publisher catalog was not found. No community repository was silently substituted.");
        response.EnsureSuccessStatusCode();
        const int maximum=4*1024*1024;
        if(response.Content.Headers.ContentLength>maximum)throw new InvalidDataException("Publisher catalog metadata is oversized.");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);
        using var bytes=new MemoryStream();var buffer=new byte[16384];int read;
        while((read=await stream.ReadAsync(buffer,ct))>0)
        {if(bytes.Length+read>maximum)throw new InvalidDataException("Publisher catalog metadata exceeds the safety budget.");bytes.Write(buffer,0,read);}
        using var json=JsonDocument.Parse(bytes.ToArray());var root=json.RootElement;
        var revision=root.TryGetProperty("sha",out var revisionNode)?revisionNode.GetString():null;
        if(revision is null || !Regex.IsMatch(revision,@"^[0-9a-fA-F]{40}$"))throw new InvalidDataException("Catalog lacks an immutable repository commit.");
        if(root.TryGetProperty("id",out var id) && !string.Equals(id.GetString(),repository,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Catalog publisher identity changed; review the source before downloading.");
        string? license=null;
        if(root.TryGetProperty("cardData",out var card) && card.ValueKind==JsonValueKind.Object && card.TryGetProperty("license",out var licenseNode) && licenseNode.ValueKind==JsonValueKind.String)
            license=licenseNode.GetString();
        if(!root.TryGetProperty("siblings",out var siblings) || siblings.ValueKind!=JsonValueKind.Array || siblings.GetArrayLength()>10000)
            throw new InvalidDataException("Publisher catalog has no bounded file listing.");
        var models=new List<CatalogModel>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in siblings.EnumerateArray())
        {
            var name=file.GetProperty("rfilename").GetString();
            if(name is null || !name.EndsWith(".gguf",StringComparison.OrdinalIgnoreCase))continue;
            if(name.Length>240 || !Regex.IsMatch(name,@"^(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.gguf$",RegexOptions.IgnoreCase)
                || name.Split('/').Any(x=>x is "." or "..") || !names.Add(name))throw new InvalidDataException("Catalog file paths are unsafe or ambiguous.");
            // Every shard must be hash-pinned as a set before native loading.
            // This catalog currently offers independently loadable single files.
            if(Regex.IsMatch(name,@"-\d{5}-of-\d{5}\.gguf$",RegexOptions.IgnoreCase))continue;
            if(!file.TryGetProperty("lfs",out var lfs) || lfs.ValueKind!=JsonValueKind.Object)continue;
            string? hash=null;
            if(lfs.TryGetProperty("sha256",out var hashNode))hash=hashNode.GetString();
            else if(lfs.TryGetProperty("oid",out var oid))hash=oid.GetString()?.Replace("sha256:","",StringComparison.Ordinal);
            if(hash is null || hash.Length!=64 || !hash.All(Uri.IsHexDigit))throw new InvalidDataException("Publisher GGUF has no trusted LFS SHA-256.");
            if(!lfs.TryGetProperty("size",out var sizeNode) || !sizeNode.TryGetInt64(out var size) || size<1024 || size>4L*1024*1024*1024*1024)
                throw new InvalidDataException("Catalog GGUF size is invalid.");
            models.Add(new(repository,revision.ToLowerInvariant(),name,hash.ToUpperInvariant(),size,license));
        }
        if(models.Count==0)throw new InvalidDataException("Publisher catalog has no independently loadable SHA-256-pinned GGUF files. Review its formats/shards before importing.");
        return models.OrderBy(x=>x.Bytes).ThenBy(x=>x.FileName,StringComparer.Ordinal).ToArray();
    }
}
