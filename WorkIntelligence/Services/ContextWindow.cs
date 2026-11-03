using System.Text;
using System.Text.Json;

namespace WorkIntelligence.Services;

/// <summary>Uses the local engine's actual template/tokenizer, preserving system evidence and the latest user turn.</summary>
internal static class ContextWindow
{
    internal static async Task<IReadOnlyList<(string role,string content)>> FitAsync(HttpClient client,
        IReadOnlyList<(string role,string content)> messages,int context,int outputTokens,CancellationToken ct)
    {
        var result=messages.ToList();
        if(!result.Any(x=>x.role=="user"))throw new ArgumentException("A conversation requires a user turn.");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(60));
        async Task<JsonDocument> Post(string endpoint,object body)
        {
            var json=JsonSerializer.Serialize(body);
            if(Encoding.UTF8.GetByteCount(json)>1024*1024)throw new InvalidDataException("Conversation exceeds the local template input safety budget.");
            using var request=new HttpRequestMessage(HttpMethod.Post,endpoint){Content=new StringContent(json,Encoding.UTF8,"application/json")};
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);response.EnsureSuccessStatusCode();
            await using var input=await response.Content.ReadAsStreamAsync(deadline.Token);using var buffer=new MemoryStream();var bytes=new byte[16384];int count;
            while((count=await input.ReadAsync(bytes,deadline.Token))>0)
            {if(buffer.Length+count>16*1024*1024)throw new InvalidDataException("Local tokenizer response exceeds safety limits.");buffer.Write(bytes,0,count);}
            return JsonDocument.Parse(buffer.ToArray());
        }
        try
        {
            while(true)
            {
                ct.ThrowIfCancellationRequested();
                using var template=await Post("apply-template",new{messages=result.Select(x=>new{role=x.role,content=x.content})});
                if(!template.RootElement.TryGetProperty("prompt",out var prompt)||prompt.ValueKind!=JsonValueKind.String)throw new InvalidDataException("Local engine did not return a chat template prompt.");
                using var tokenized=await Post("tokenize",new{content=prompt.GetString(),add_special=false,parse_special=true});
                if(!tokenized.RootElement.TryGetProperty("tokens",out var tokens)||tokens.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Local engine did not return token counts.");
                if(tokens.GetArrayLength()+outputTokens+32<=context)return result;
                var lastUser=result.FindLastIndex(x=>x.role=="user");
                var remove=result.FindIndex(x=>x.role!="system");
                if(remove<0 || remove>=lastUser)throw new InvalidOperationException("The question and captured evidence exceed this model context budget. Increase context/reduce output tokens or narrow the question; saved history was not deleted.");
                result.RemoveAt(remove);
                while(remove<result.Count && result[remove].role=="assistant")result.RemoveAt(remove);
            }
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested)
        {throw new TimeoutException("Local template/tokenizer preflight exceeded its one-minute limit.");}
    }
}
