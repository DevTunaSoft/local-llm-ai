using System.Text;
using System.Text.Json;

namespace WorkIntelligence.Services;

internal static class CompletionStream
{
    private const int MaximumEventCharacters = 1_048_576;

    internal static async IAsyncEnumerable<string> ReadAsync(TextReader reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
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
            JsonDocument document;
            try { document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 32 }); }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Malformed local AI streaming event.", ex);
            }
            using (document)
            {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Local AI streaming event must be a JSON object.");
            if (root.TryGetProperty("error", out _))
                throw new InvalidDataException("The local AI engine reported a response error. Any partial answer is incomplete.");
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                continue;
            var choice = choices[0];
            if (choice.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Local AI event contains an invalid choice.");
            if (choice.TryGetProperty("delta", out var delta) &&
                delta.ValueKind == JsonValueKind.Object &&
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
}
