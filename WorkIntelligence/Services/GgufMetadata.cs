using System.Text;

namespace WorkIntelligence.Services;

public sealed record GgufMetadata(string Architecture,uint Version,ulong TensorCount,IReadOnlyDictionary<string,ulong> Numbers)
{
    public ulong? Number(string suffix)=>Numbers.TryGetValue(Architecture+suffix,out var number)?number:null;
    /// <summary>Conservative f16 KV-cache estimate. Hybrid models may use less; native memory usage is not guaranteed by this estimate.</summary>
    public ulong EstimateCacheBytes(int context)
    {
        var layers=Number(".block_count");var embedding=Number(".embedding_length");
        var heads=Number(".attention.head_count");var kvHeads=Number(".attention.head_count_kv") ?? heads;
        if(layers is >0 && embedding is >0 && heads is >0 && kvHeads is >0)
        {
            var key=Number(".attention.key_length") ?? embedding.Value/heads.Value;
            var value=Number(".attention.value_length") ?? embedding.Value/heads.Value;
            if(key>0 && value>0)return checked((ulong)context*layers.Value*kvHeads.Value*(key+value)*2);
        }
        return checked((ulong)context*256*1024); // 1 GiB at 4K when architecture dimensions are unavailable
    }

    public static GgufMetadata Read(string file,CancellationToken ct=default)
    {
        PathSafety.RejectReparsePoints(file);
        using var stream=File.OpenRead(file);
        using var reader=new BinaryReader(stream,new UTF8Encoding(false,true),true);
        const long budget=64L*1024*1024;
        void Check(long bytes)
        {
            ct.ThrowIfCancellationRequested();
            if(bytes<0 || bytes>stream.Length-stream.Position || stream.Position+bytes>budget)
                throw new InvalidDataException("GGUF metadata is truncated or exceeds the safe metadata budget.");
        }
        void Skip(ulong bytes){if(bytes>long.MaxValue)throw new InvalidDataException("Invalid GGUF metadata length.");Check((long)bytes);stream.Position+=(long)bytes;}
        uint U32(){Check(4);return reader.ReadUInt32();}
        ulong U64(){Check(8);return reader.ReadUInt64();}
        string Text(int max)
        {
            var length=U64();
            if(length>(ulong)max)throw new InvalidDataException("GGUF metadata string exceeds its safety limit.");
            Check((long)length);return new UTF8Encoding(false,true).GetString(reader.ReadBytes((int)length));
        }
        void Value(uint type,int depth=0)
        {
            if(depth>4)throw new InvalidDataException("GGUF metadata array nesting is unsupported.");
            switch(type)
            {
                case 0: case 1: case 7: Skip(1);break;
                case 2: case 3: Skip(2);break;
                case 4: case 5: case 6: Skip(4);break;
                case 10: case 11: case 12: Skip(8);break;
                case 8: Skip(U64());break;
                case 9:
                    var element=U32();var count=U64();
                    if(element>12)throw new InvalidDataException("Unknown GGUF array element type.");
                    if(count>2_000_000)throw new InvalidDataException("GGUF metadata array is oversized.");
                    var width=element switch{0 or 1 or 7=>1UL,2 or 3=>2UL,4 or 5 or 6=>4UL,10 or 11 or 12=>8UL,_=>0UL};
                    if(width>0)Skip(checked(count*width));
                    else for(ulong i=0;i<count;i++)Value(element,depth+1);
                    break;
                default:throw new InvalidDataException("Unknown GGUF metadata value type.");
            }
        }
        try
        {
            Check(4);
            if(!reader.ReadBytes(4).AsSpan().SequenceEqual("GGUF"u8))throw new InvalidDataException("Model is not GGUF.");
            var version=U32();
            if(version is not (2 or 3))throw new InvalidDataException("Only little-endian GGUF v2/v3 is supported by this build.");
            var tensors=U64();var metadata=U64();
            if(tensors is <1 or >1_000_000 || metadata is <1 or >100_000)throw new InvalidDataException("GGUF tensor/metadata counts are invalid.");
            var keys=new HashSet<string>(StringComparer.Ordinal);var numbers=new Dictionary<string,ulong>(StringComparer.Ordinal);
            string? architecture=null;
            for(ulong i=0;i<metadata;i++)
            {
                var key=Text(1024);if(!keys.Add(key))throw new InvalidDataException("GGUF has duplicate metadata keys.");
                var type=U32();
                if(key=="general.architecture")
                {
                    if(type!=8)throw new InvalidDataException("GGUF architecture must be a string.");
                    architecture=Text(128);
                }
                else if(type==4)numbers[key]=U32();
                else if(type==10)numbers[key]=U64();
                else Value(type);
            }
            if(string.IsNullOrWhiteSpace(architecture)||architecture.Any(x=>!char.IsAsciiLetterOrDigit(x)&&x!='_'&&x!='-'))
                throw new InvalidDataException("GGUF architecture is missing or malformed.");
            if(numbers.GetValueOrDefault("split.count",1UL)>1)
                throw new InvalidDataException("Split GGUF sets require a verified manifest for every shard. Select a single-file merged GGUF in this build.");
            var alignment=numbers.GetValueOrDefault("general.alignment",32UL);
            if(alignment is <1 or >4096 || (alignment&(alignment-1))!=0)throw new InvalidDataException("GGUF alignment is invalid.");
            var names=new HashSet<string>(StringComparer.Ordinal);var offsets=new List<ulong>();
            for(ulong i=0;i<tensors;i++)
            {
                if(!names.Add(Text(1024)))throw new InvalidDataException("GGUF tensor names are duplicated.");
                var dimensions=U32();if(dimensions is <1 or >4)throw new InvalidDataException("GGUF tensor dimensions are invalid.");
                ulong elements=1;
                for(var d=0;d<dimensions;d++){var size=U64();if(size==0)throw new InvalidDataException("GGUF tensor is empty.");elements=checked(elements*size);}
                _=U32(); // ggml tensor type: compatibility is resolved by the vetted native engine.
                var offset=U64();if(offset%alignment!=0)throw new InvalidDataException("GGUF tensor offset is misaligned.");offsets.Add(offset);
            }
            var data=checked(((ulong)stream.Position+alignment-1)/alignment*alignment);
            if(data>=(ulong)stream.Length || offsets.Any(x=>x>=(ulong)stream.Length-data))
                throw new InvalidDataException("GGUF tensor data is missing or truncated.");
            return new(architecture,version,tensors,numbers);
        }
        catch(Exception ex) when(ex is EndOfStreamException or OverflowException or DecoderFallbackException)
        {throw new InvalidDataException("GGUF structure is malformed or exceeds safe numeric limits.",ex);}
    }
}
