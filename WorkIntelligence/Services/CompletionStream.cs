using System.Text;
using System.Text.Json;

namespace WorkIntelligence.Services;

internal static class CompletionStream
{
    private const int MaximumEventCharacters = 1_048_576;

    internal static async IAsyncEnumerable<string> ReadAsync(TextReader reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken,Action<CompletionUsage>? usageReceived=null)
    {
        var data = new StringBuilder();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new EndOfStreamException("Local AI connection ended before response completion. The partial answer has been preserved.");
            if (line.Length > MaximumEventCharacters)
                throw new InvalidDataException("Local AI event exceeds the response size limit.");
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var value = line.AsMemory(5);
                if (value.Span.StartsWith(" ")) value = value[1..];
                if (data.Length + value.Length + 1 > MaximumEventCharacters)
                    throw new InvalidDataException("Local AI event exceeds the response size limit.");
                data.Append(value.Span).Append('\n');
                continue;
            }
            // A blank line terminates an event; comments and other SSE fields do not.
            if (line.Length != 0 || data.Length == 0) continue;
            var content = data.ToString().TrimEnd('\n');
            data.Clear();
            if (content == "[DONE]") yield break;
            if (content.Length == 0) continue;
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if(root.TryGetProperty("usage",out var usage) && usage.ValueKind==JsonValueKind.Object)
            {
                if(!usage.TryGetProperty("prompt_tokens",out var promptNode)||!promptNode.TryGetInt32(out var prompt)||prompt<0 ||
                    !usage.TryGetProperty("completion_tokens",out var completionNode)||!completionNode.TryGetInt32(out var completion)||completion<0)
                    throw new InvalidDataException("Local model reported invalid token usage.");
                double? promptMs=null,generationMs=null;
                if(root.TryGetProperty("timings",out var timings) && timings.ValueKind==JsonValueKind.Object)
                {
                    double? Timing(string key)
                    {
                        if(!timings.TryGetProperty(key,out var node))return null;
                        if(!node.TryGetDouble(out var value)||!double.IsFinite(value)||value<0)throw new InvalidDataException("Local model timing data is invalid.");
                        return value;
                    }
                    promptMs=Timing("prompt_ms");generationMs=Timing("predicted_ms");
                }
                usageReceived?.Invoke(new(prompt,completion,promptMs,generationMs));
            }
            if (root.TryGetProperty("error", out _))
                throw new InvalidDataException("The local AI engine reported a response error. Any partial answer is incomplete.");
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                continue;
            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("content", out var deltaContent) && deltaContent.ValueKind == JsonValueKind.String &&
                deltaContent.GetString() is { Length: > 0 } text)
                yield return text;
            if (choice.TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String &&
                reason.GetString() is { } finish && finish != "stop")
                throw new InvalidDataException(finish == "length"
                    ? "The local AI response reached its token limit. The partial answer is incomplete."
                    : "The local AI response did not finish normally. The partial answer is incomplete.");
        }
    }
}
