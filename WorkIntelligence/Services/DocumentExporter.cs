using System.IO.Compression;
using System.Text;
using System.Xml;

namespace WorkIntelligence.Services;

/// <summary>Standalone, local, source-independent document export. Produces Word Open XML.</summary>
public static class DocumentExporter
{
    public static void SaveDocx(string file,string title,string body)
    {
        if(title.Length>500 || body.Length>2_000_000) throw new InvalidDataException("Document exceeds export limit.");
        XmlConvert.VerifyXmlChars(title);
        XmlConvert.VerifyXmlChars(body);
        var path=Path.GetFullPath(file);
        PathSafety.RejectReparsePoints(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))
            {
                Write(zip,"[Content_Types].xml","""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                      <Default Extension="xml" ContentType="application/xml"/>
                      <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                    </Types>
                    """);
                Write(zip,"_rels/.rels","""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                    </Relationships>
                    """);
                var xml=new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>");
                foreach(var paragraph in (title+"\n\n"+body).Split('\n'))
                {
                    xml.Append("<w:p><w:r><w:t xml:space=\"preserve\">");
                    xml.Append(EscapeXml(paragraph.TrimEnd('\r')));
                    xml.Append("</w:t></w:r></w:p>");
                }
                xml.Append("<w:sectPr/></w:body></w:document>");
                Write(zip,"word/document.xml",xml.ToString());
            }
            File.Move(temp,path,true);
        }
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
    private static void Write(ZipArchive archive,string path,string content)
    {
        using var writer=new StreamWriter(archive.CreateEntry(path,CompressionLevel.Optimal).Open(),new UTF8Encoding(false));
        writer.Write(content);
    }
    private static string EscapeXml(string value)
    {
        var output=new StringBuilder(value.Length);
        foreach(var c in value)
        {
            if(c is '\t' || c is '\n' || c is '\r' || c>=0x20 && c is not '\uFFFE' and not '\uFFFF')
            {
                output.Append(c switch {'&'=>"&amp;",'<'=>"&lt;",'>'=>"&gt;",'"'=>"&quot;",'\''=>"&apos;",_=>c.ToString()});
            }
        }
        return output.ToString();
    }
}
