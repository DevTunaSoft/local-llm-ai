using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace WorkIntelligence.Services;

public static class ModelDownloader
{
    internal const long MaxDownloadBytes = 64L * 1024 * 1024 * 1024;

    internal static Uri ValidateDownloadRequest(string url, string filename, string expectedHash)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.Host is not ("huggingface.co" or "www.huggingface.co") ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.Port != 443)
            throw new ArgumentException("Only trusted HTTPS Hugging Face downloads without embedded credentials are permitted.");
        if (string.IsNullOrWhiteSpace(filename) ||
            !System.Text.RegularExpressions.Regex.IsMatch(filename,
                @"^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,119}\.gguf$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("Choose a safe single .gguf filename without folders.");
        var stem = filename[..^5].Split('.')[0];
        if (System.Text.RegularExpressions.Regex.IsMatch(stem,
            @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("Windows reserved device filenames cannot be used for models.");
        if (expectedHash is null ||
            !System.Text.RegularExpressions.Regex.IsMatch(expectedHash,
                @"^[0-9a-fA-F]{64}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("A trusted SHA-256 checksum is required for download verification.");
        return uri;
    }

    internal static void ValidateDownloadSize(long alreadyDownloaded, long newBytes)
    {
        if (alreadyDownloaded < 0 || newBytes < 0 || alreadyDownloaded > MaxDownloadBytes ||
            newBytes > MaxDownloadBytes - alreadyDownloaded)
            throw new InvalidDataException("GGUF download exceeds the configured safety size limit.");
    }

    public static async Task<string> DownloadAsync(string downloadUrl, string filename, string expectedSha256,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var uri = ValidateDownloadRequest(downloadUrl, filename, expectedSha256);
        LocalPaths.Ensure();
        var final=Path.Combine(LocalPaths.Models,filename);
        var partial=final+".partial";
        if (File.Exists(final)) throw new IOException("Model already exists. Select another filename or import the existing model.");
        using var client=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }) { Timeout=Timeout.InfiniteTimeSpan };
        var offset=File.Exists(partial) ? new FileInfo(partial).Length : 0;
        ValidateDownloadSize(offset, 0);
        using var request=new HttpRequestMessage(HttpMethod.Get,uri);
        if(offset>0)request.Headers.Range=new RangeHeaderValue(offset,null);
        using var response=await SendSafeWithRedirectsAsync(client,request,cancellationToken);
        response.EnsureSuccessStatusCode();
        if(response.StatusCode==HttpStatusCode.OK)offset=0;
        else if (response.StatusCode!=HttpStatusCode.PartialContent ||
                 response.Content.Headers.ContentRange?.From != offset)
            throw new InvalidDataException("Server returned an unexpected resume range.");
        if (response.Content.Headers.ContentLength is long contentLength)
            ValidateDownloadSize(offset, contentLength);
        var total=response.Content.Headers.ContentLength is long length ? length+offset : 0;
        await using(var destination=new FileStream(partial,offset==0?FileMode.Create:FileMode.Append,FileAccess.Write,FileShare.None,1024*1024,useAsync:true))
        await using(var source=await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            var buffer=new byte[1024*1024];int count;
            while((count=await source.ReadAsync(buffer,cancellationToken))>0)
            {
                ValidateDownloadSize(offset, count);
                await destination.WriteAsync(buffer.AsMemory(0,count),cancellationToken);
                offset+=count;
                if(total>0)progress?.Report(Math.Clamp((double)offset/total,0,1));
            }
            await destination.FlushAsync(cancellationToken);
        }
        var verified=false;
        try
        {
            await using(var stream=File.OpenRead(partial))
            {
                var actual=Convert.ToHexString(await SHA256.HashDataAsync(stream,cancellationToken));
                if(!actual.Equals(expectedSha256,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Downloaded model SHA-256 checksum failed validation.");
                stream.Position=0;
                var header=new byte[4];await stream.ReadExactlyAsync(header,cancellationToken);
                if(!header.AsSpan().SequenceEqual("GGUF"u8)) throw new InvalidDataException("Downloaded file is not a GGUF model.");
            }
            verified=true;
        }
        finally { if(!verified && File.Exists(partial)) File.Delete(partial); }
        File.Move(partial,final);
        return final;
    }
    private static async Task<HttpResponseMessage> SendSafeWithRedirectsAsync(HttpClient client, HttpRequestMessage original, CancellationToken ct)
    {
        var current = original.RequestUri ?? throw new ArgumentException("Missing model URL.");
        for (var redirects = 0; redirects <= 6; redirects++)
        {
            if (current.Scheme != Uri.UriSchemeHttps ||
                !(current.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase) ||
                  current.Host.EndsWith(".huggingface.co", StringComparison.OrdinalIgnoreCase) ||
                  current.Host.EndsWith(".hf.co", StringComparison.OrdinalIgnoreCase) ||
                  current.Host.EndsWith(".xethub.hf.co", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Model repository redirected outside permitted secure download domains.");
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.Range = original.Headers.Range;
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new HttpRequestException("Redirect without Location header.");
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                continue;
            }
            return response;
        }
        throw new HttpRequestException("Too many model download redirects.");
    }

}
