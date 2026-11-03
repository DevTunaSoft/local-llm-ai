using System.Runtime.InteropServices;

namespace WorkIntelligence.Services;

public static class HardwareProbe
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    public static (ulong total, ulong available) GetPhysicalMemory()
    {
        var state = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref state)) throw new InvalidOperationException("Could not inspect available Windows physical memory.");
        return (state.TotalPhysical,state.AvailablePhysical);
    }
    internal static ulong EstimateModelBytes(long modelBytes)
    {
        if (modelBytes <= 0) throw new ArgumentOutOfRangeException(nameof(modelBytes));
        const ulong gib = 1024UL * 1024 * 1024;
        const ulong mib = 1024UL * 1024;
        var overhead = (ulong)modelBytes < gib ? 768UL * mib : 3UL * gib;
        return checked((ulong)Math.Ceiling(modelBytes * 1.10) + overhead);
    }

    public static void ValidateModelMemory(string gguf,int context=4096,int? memoryLimitMiB=null,string? projector=null)
    {
        var metadata=GgufMetadata.Read(gguf);
        var size = new FileInfo(gguf).Length;
        var (total, available)=GetPhysicalMemory();
        const ulong spare = 2UL * 1024 * 1024 * 1024;
        // A flat 3 GiB overhead rejected tiny test GGUF models on hosted Windows
        // agents even when they had ample memory to run them. Keep the larger
        // buffer for real models, but scale down for sub-1-GiB fixtures.
        var estimated = checked(EstimateModelBytes(size)+metadata.EstimateCacheBytes(context));
        if(projector is not null)
        {
            PathSafety.RejectReparsePoints(projector);
            estimated=checked(estimated+(ulong)Math.Ceiling(new FileInfo(projector).Length*1.10)+512UL*1024*1024);
        }
        if(memoryLimitMiB is int limit && estimated>(ulong)limit*1024*1024)
            throw new InvalidOperationException("Estimated model and context memory exceed your configured model budget. Reduce context, increase the approved budget or choose a smaller model.");
        if (estimated + spare > available)
            throw new InvalidOperationException($"Insufficient free physical RAM for safe model startup. Free: {available/(1024.0*1024*1024):F1} GiB; conservative model requirement: {estimated/(1024.0*1024*1024):F1} GiB plus 2 GiB Windows reserve. Close other applications or select a smaller model.");
        _=total;
    }
}
