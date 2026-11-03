using System.Text;
using System.Text.RegularExpressions;

namespace WorkIntelligence.Services;

/// <summary>Deterministic local line comparison. Imported text remains data, never instructions.</summary>
public static class DocumentComparison
{
    public static string Compare(string firstLabel, string firstText, string secondLabel, string secondText)
    {
        if (firstText.Length > 2_000_000 || secondText.Length > 2_000_000)
            throw new InvalidDataException("Compare documents of at most two million text characters each.");
        static string[] Lines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(x => Regex.Replace(x, @"[ \t]+", " ").Trim()).Where(x => x.Length != 0).ToArray();
        var first = Lines(firstText);
        var second = Lines(secondText);
        if (first.Length > 2000 || second.Length > 2000)
            throw new InvalidDataException("Compare documents of at most 2,000 nonempty lines each.");
        var lengths = new ushort[first.Length + 1, second.Length + 1];
        for (var i = first.Length - 1; i >= 0; i--)
            for (var j = second.Length - 1; j >= 0; j--)
                lengths[i, j] = (ushort)(first[i] == second[j] ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]));
        var output = new StringBuilder($"SOURCE COMPARISON\nBefore: {firstLabel}\nAfter: {secondLabel}\n" +
            "Whitespace within lines is normalized; blank lines are ignored. This is a text comparison, not policy approval.\n\n");
        var a = 0;
        var b = 0;
        var added = 0;
        var removed = 0;
        while (a < first.Length || b < second.Length)
        {
            if (a < first.Length && b < second.Length && first[a] == second[b]) { a++; b++; continue; }
            if (b < second.Length && (a == first.Length || lengths[a, b + 1] >= lengths[a + 1, b]))
            { output.AppendLine($"+ After line {b + 1}: {second[b++]}"); added++; }
            else { output.AppendLine($"- Before line {a + 1}: {first[a++]}"); removed++; }
        }
        output.AppendLine($"\nAdded lines: {added}; removed lines: {removed}; unchanged lines: {lengths[0, 0]}.");
        return output.ToString();
    }
}
