using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace WorkIntelligence.Services;

public static class DocumentExtractor
{
    public static string Extract(string path, byte[] bytes)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".log" or ".ps1" or ".cs" or ".sql" or ".yml" or ".yaml" => DecodeText(bytes),
            ".pdf" => Pdf(bytes),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" => OcrService.ExtractImage(bytes),
            ".docx" or ".xlsx" or ".pptx" => Office(bytes, extension),
            _ => throw new NotSupportedException($"{extension} is not yet supported. Import an extractable text, PDF, DOCX, XLSX, or PPTX file.")
        };
    }
    private static string DecodeText(byte[] bytes)
    {
        ReadOnlySpan<byte> data = bytes;
        string text;
        // Test UTF-32 before UTF-16: the little-endian BOM shares its first two bytes.
        if (data.StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 }))
            text = new UTF32Encoding(false, false, true).GetString(data[4..]);
        else if (data.StartsWith(new byte[] { 0, 0, 0xFE, 0xFF }))
            text = new UTF32Encoding(true, false, true).GetString(data[4..]);
        else if (data.StartsWith(new byte[] { 0xFF, 0xFE }))
            text = new UnicodeEncoding(false, false, true).GetString(data[2..]);
        else if (data.StartsWith(new byte[] { 0xFE, 0xFF }))
            text = new UnicodeEncoding(true, false, true).GetString(data[2..]);
        else
        {
            if (data.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) data = data[3..];
            text = new UTF8Encoding(false, true).GetString(data);
        }
        if (text.Contains('\0')) throw new InvalidDataException("Binary file not supported.");
        return text;
    }
    private static string Pdf(byte[] bytes)
    {
        using var document = PdfDocument.Open(bytes);
        if (document.NumberOfPages > 1000) throw new InvalidDataException("PDF exceeds 1,000-page safety limit.");
        var output = new StringBuilder();
        var ocrPages = 0;
        foreach (var page in document.GetPages())
        {
            var pageText = ContentOrderTextExtractor.GetText(page);
            if (page.NumberOfImages > 0 && (string.IsNullOrWhiteSpace(pageText) || pageText.Trim().Length < 12))
            {
                // OCR each image-only page, including mixed searchable/scanned PDFs.
                // Failure aborts ingestion; never claim a partial document was completely indexed.
                pageText = PdfPageOcr.Extract(bytes, page.Number - 1);
                ocrPages++;
                if (string.IsNullOrWhiteSpace(pageText))
                    throw new InvalidDataException($"OCR returned no readable text for PDF page {page.Number}.");
            }
            if (!string.IsNullOrWhiteSpace(pageText))
                output.Append("[Page ").Append(page.Number).Append("] ").AppendLine(pageText);
            if (output.Length > 12_000_000) throw new InvalidDataException("Extracted PDF text is too large.");
        }
        _ = ocrPages;
        return output.ToString();
    }
    private static string Office(byte[] bytes, string extension)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
        if (zip.Entries.Count > 3000 || zip.Entries.Sum(e => e.Length) > 100 * 1024 * 1024)
            throw new InvalidDataException("Office document exceeds safe archive limits.");
        var output = new StringBuilder();
        if (extension == ".docx")
        {
            var entry = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException("Invalid DOCX document.");
            var doc = ReadXml(entry);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            foreach (var paragraph in doc.Descendants(w + "p"))
            {
                foreach (var node in paragraph.Descendants())
                {
                    if (node.Name == w + "t") output.Append(node.Value);
                    else if (node.Name == w + "tab") output.Append('\t');
                    else if (node.Name == w + "br" || node.Name == w + "cr") output.Append('\n');
                }
                output.AppendLine();
            }
        }
        else if (extension == ".pptx")
        {
            XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
            foreach (var entry in zip.Entries.Where(e => System.Text.RegularExpressions.Regex.IsMatch(e.FullName, @"^ppt/slides/slide\d+\.xml$"))
                .OrderBy(e => int.Parse(System.Text.RegularExpressions.Regex.Match(e.Name, @"\d+").Value)))
            {
                output.AppendLine("[" + entry.Name + "]");
                var doc = ReadXml(entry);
                foreach (var paragraph in doc.Descendants(a + "p"))
                    output.AppendLine(string.Join(" ",paragraph.Descendants(a + "t").Select(x => x.Value)));
            }
        }
        else
        {
            XNamespace s = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var stringsEntry = zip.GetEntry("xl/sharedStrings.xml");
            string[] shared = stringsEntry is null ? Array.Empty<string>() : ReadXml(stringsEntry).Descendants(s + "si")
                .Select(si => string.Concat(si.Descendants(s + "t").Select(t => t.Value))).ToArray();
            foreach (var entry in zip.Entries.Where(e => System.Text.RegularExpressions.Regex.IsMatch(e.FullName, @"^xl/worksheets/sheet\d+\.xml$"))
                .OrderBy(e => int.Parse(System.Text.RegularExpressions.Regex.Match(e.Name, @"\d+").Value)))
            {
                output.AppendLine("[" + entry.Name + "]");
                var xml = ReadXml(entry);
                foreach (var row in xml.Descendants(s + "row"))
                {
                    var cells = new List<string>();
                    foreach (var cell in row.Elements(s + "c"))
                    {
                        var raw = cell.Element(s + "v")?.Value ?? string.Join("", cell.Descendants(s + "t").Select(x => x.Value));
                        if (cell.Attribute("t")?.Value == "s" && int.TryParse(raw, out var index) && index >= 0 && index < shared.Length) raw = shared[index];
                        cells.Add((cell.Attribute("r")?.Value ?? "cell") + ": " + raw);
                    }
                    output.AppendLine(string.Join(" | ", cells));
                    if (output.Length > 12_000_000) throw new InvalidDataException("Extracted spreadsheet too large.");
                }
            }
        }
        if (output.Length > 12_000_000) throw new InvalidDataException("Extracted document text too large.");
        return output.ToString();
    }
    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        if (entry.Length > 30 * 1024 * 1024) throw new InvalidDataException("Oversized archive entry.");
        using var entryStream = entry.Open();
        using var reader = XmlReader.Create(entryStream, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 30_000_000 });
        return XDocument.Load(reader);
    }
}
