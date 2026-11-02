using System.Buffers.Binary;
using System.Security.Cryptography;

namespace WorkIntelligence.Services;

/// <summary>Explicitly selected local raster image. Remote URLs and active image formats are never accepted.</summary>
public sealed class VisionAttachment
{
    public string Name {get;}
    public string Sha256 {get;}
    public string DataUrl {get;}
    private VisionAttachment(string name,string hash,string data){Name=name;Sha256=hash;DataUrl=data;}
    public static VisionAttachment FromFile(string file)
    {
        PathSafety.RejectReparsePoints(file);
        using var stream=File.OpenRead(file);
        if(stream.Length is <24 or >10*1024*1024)throw new InvalidDataException("Vision images must be between 24 bytes and 10 MiB.");
        var bytes=new byte[(int)stream.Length];stream.ReadExactly(bytes);
        return FromBytes(Path.GetFileName(file),bytes);
    }
    internal static VisionAttachment FromBytes(string name,ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length is <24 or >10*1024*1024)throw new InvalidDataException("Vision image exceeds its size budget.");
        string media;uint width=0,height=0;
        if(bytes.StartsWith(new byte[]{137,80,78,71,13,10,26,10}) && bytes.Slice(12,4).SequenceEqual("IHDR"u8))
        {media="image/png";width=BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]);height=BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]);}
        else if(bytes[0]==255 && bytes[1]==216)
        {
            media="image/jpeg";var position=2;
            while(position+4<=bytes.Length)
            {
                if(bytes[position++]!=255)throw new InvalidDataException("JPEG marker structure is invalid.");
                while(position<bytes.Length && bytes[position]==255)position++;
                if(position>=bytes.Length)break;
                var marker=bytes[position++];
                if(marker is 217 or 218)break;
                if(marker is 216 or >=208 and <=215)continue;
                if(position+2>bytes.Length)break;
                var length=BinaryPrimitives.ReadUInt16BigEndian(bytes[position..]);
                if(length<2 || position+length>bytes.Length)throw new InvalidDataException("JPEG segment is truncated.");
                if(marker is 192 or 193 or 194 or 195 or 197 or 198 or 199 or 201 or 202 or 203 or 205 or 206 or 207)
                {
                    if(length<8)throw new InvalidDataException("JPEG frame is truncated.");
                    height=BinaryPrimitives.ReadUInt16BigEndian(bytes[(position+3)..]);width=BinaryPrimitives.ReadUInt16BigEndian(bytes[(position+5)..]);break;
                }
                position+=length;
            }
        }
        else throw new InvalidDataException("Only local PNG or JPEG raster images are supported for vision.");
        if(width is <1 or >8192 || height is <1 or >8192 || (ulong)width*height>16_000_000)
            throw new InvalidDataException("Vision image dimensions exceed the 16-megapixel budget or are invalid.");
        return new(name,Convert.ToHexString(SHA256.HashData(bytes)),"data:"+media+";base64,"+Convert.ToBase64String(bytes));
    }
}
