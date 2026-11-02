using System.Globalization;
using System.Text;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace WorkIntelligence.Services;

/// <summary>Local searchable PDF export, with an embedded TrueType font and atomic replacement.</summary>
public static class PdfExporter
{
    public static void Save(string file, string title, string body, string fontPath)
    {
        if (title.Length > 500 || body.Length > 2_000_000)
            throw new InvalidDataException("Document exceeds export limit.");
        PathSafety.RejectReparsePoints(fontPath);
        if (!File.Exists(fontPath) || new FileInfo(fontPath).Length > 20 * 1024 * 1024)
            throw new InvalidDataException("A local TrueType font (at most 20 MB) is required for PDF export.");
        var path = Path.GetFullPath(file);
        PathSafety.RejectReparsePoints(path);
        var builder = new PdfDocumentBuilder();
        var font = builder.AddTrueTypeFont(File.ReadAllBytes(fontPath));
        var page = builder.AddPage(595, 842); // A4, 48-point margins
        var y = 794d;
        var pages = 1;
        void Line(string line)
        {
            if (y < 48)
            {
                if (++pages > 1000) throw new InvalidDataException("PDF export exceeds 1,000 pages.");
                page = builder.AddPage(595, 842);
                y = 794;
            }
            if (line.Length > 0) page.AddText(line, 11, new PdfPoint(48, y), font);
            y -= 16;
        }
        foreach (var paragraph in (title + "\n\n" + body).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            // Wrap at text elements, so surrogate pairs and combining marks stay
            // together. Measure actual glyphs rather than truncating wide lines.
            var line = new StringBuilder();
            var elements = StringInfo.GetTextElementEnumerator(paragraph.Replace("\t", "    "));
            while (elements.MoveNext())
            {
                var element = elements.GetTextElement();
                if (element.Any(char.IsControl)) throw new InvalidDataException("PDF text contains unsupported control characters.");
                var candidate = line.ToString() + element;
                var letters = page.MeasureText(candidate, 11, new PdfPoint(48, y), font);
                if (letters.Any(x => x.GlyphRectangle.Right > 547) && line.Length > 0)
                {
                    Line(line.ToString());
                    line.Clear();
                }
                line.Append(element);
            }
            Line(line.ToString());
        }
        var bytes = builder.Build();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
