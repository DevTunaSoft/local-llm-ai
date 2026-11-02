using System.Text;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class GgufMetadataTests
{
    internal static byte[] Fixture(bool missingArchitecture=false,bool duplicate=false,ulong offset=0)
    {
        using var output=new MemoryStream();using var w=new BinaryWriter(output,Encoding.UTF8,true);
        void Text(string s){var b=Encoding.UTF8.GetBytes(s);w.Write((ulong)b.Length);w.Write(b);}
        w.Write("GGUF"u8);w.Write(3u);w.Write(1UL);w.Write(missingArchitecture?1UL:duplicate?2UL:6UL);
        if(missingArchitecture){Text("general.name");w.Write(8u);Text("synthetic");}
        else if(duplicate){for(var i=0;i<2;i++){Text("general.architecture");w.Write(8u);Text("llama");}}
        else
        {
            Text("general.architecture");w.Write(8u);Text("llama");
            foreach(var (key,value) in new[]{("block_count",32u),("embedding_length",4096u),("attention.head_count",32u),("attention.head_count_kv",8u),("context_length",8192u)})
            {Text("llama."+key);w.Write(4u);w.Write(value);}
        }
        Text("synthetic.weight");w.Write(1u);w.Write(1UL);w.Write(0u);w.Write(offset);
        while(output.Position%32!=0)w.Write((byte)0);
        w.Write(new byte[2048]);return output.ToArray();
    }
    private static string Save(byte[] bytes){var f=Path.Combine(Path.GetTempPath(),"wi-gguf-"+Guid.NewGuid().ToString("N")+".gguf");File.WriteAllBytes(f,bytes);return f;}
    [Fact]
    public void ReadsArchitectureAndContextDependentKvMemory()
    {
        var file=Save(Fixture());try
        {
            var metadata=GgufMetadata.Read(file);Assert.Equal("llama",metadata.Architecture);
            Assert.Equal(8192UL,metadata.Number(".context_length"));
            Assert.Equal(512UL*1024*1024,metadata.EstimateCacheBytes(4096));
            Assert.Equal(1024UL*1024*1024,metadata.EstimateCacheBytes(8192));
        }finally{File.Delete(file);}
    }
    [Theory]
    [InlineData(true,false,0)]
    [InlineData(false,true,0)]
    [InlineData(false,false,1)]
    [InlineData(false,false,1048576)]
    public void InvalidMetadataAndTensorOffsetsAreRejected(bool missing,bool duplicate,int offset)
    {
        var file=Save(Fixture(missing,duplicate,(ulong)offset));
        try{Assert.Throws<InvalidDataException>(()=>GgufMetadata.Read(file));}finally{File.Delete(file);}
    }
    [Fact]
    public void HeaderOnlyModelCannotBeImported()
    {
        var file=Save("GGUF"u8.ToArray().Concat(new byte[2048]).ToArray());
        try{using var store=new KnowledgeStore();Assert.Throws<InvalidDataException>(()=>store.ImportModel(file));}
        finally{File.Delete(file);}
    }
    [Fact]
    public void TruncatedMetadataFailsBeforeNativeExecution()
    {
        var file=Save(Fixture()[..40]);try{Assert.Throws<InvalidDataException>(()=>GgufMetadata.Read(file));}finally{File.Delete(file);}
    }
}
