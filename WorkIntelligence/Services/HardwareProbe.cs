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
    public static void ValidateModelMemory(string gguf)
    {
        var size = new FileInfo(gguf).Length;
        var (total, available)=GetPhysicalMemory();
        const ulong spare = 2UL * 1024 * 1024 * 1024;
        // Conservative estimate; actual loaded model, KV cache and runtime buffers must be measured.
        var estimated = checked((ulong)(size * 1.10) + 3UL * 1024 * 1024 * 1024);
        if (estimated + spare > available)
            throw new InvalidOperationException($"Insufficient free physical RAM for safe model startup. Free: {available/(1024.0*1024*1024):F1} GiB; conservative model requirement: {estimated/(1024.0*1024*1024):F1} GiB plus 2 GiB Windows reserve. Close other applications or select a smaller model.");
        _=total;
    }
}
