using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace WorkIntelligence.Services;

/// <summary>Render a PDF page with a vetted, bundled PDFium library; OCR remains in memory.</summary>
public static class PdfPageOcr
{
    private const int Bgra = 4;
    private static readonly object Gate = new();
    private static bool _ready;
    static PdfPageOcr()
    {
        NativeLibrary.SetDllImportResolver(typeof(PdfPageOcr).Assembly, (name, assembly, path) =>
        {
            if (name == "wi_pdfium") return NativeLibrary.Load(RuntimeBundle.Resolve("pdfium.dll"));
            return IntPtr.Zero;
        });
    }
    public static string Extract(byte[] pdf, int pageIndex, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (pdf.Length is < 8 or > 25 * 1024 * 1024) throw new InvalidDataException("PDF size exceeds OCR limit.");
        lock (Gate) // PDFium document/render lifecycle is serialized for safety.
        {
            if (!_ready) { FPDF_InitLibrary(); _ready = true; }
            var data = GCHandle.Alloc(pdf, GCHandleType.Pinned);
            try
            {
                var doc = FPDF_LoadMemDocument(data.AddrOfPinnedObject(), pdf.Length, null);
                if (doc == IntPtr.Zero) throw new InvalidDataException("PDFium could not load scanned PDF.");
                try
                {
                    if (pageIndex < 0 || pageIndex >= FPDF_GetPageCount(doc)) throw new ArgumentOutOfRangeException(nameof(pageIndex));
                    var page = FPDF_LoadPage(doc, pageIndex);
                    if (page == IntPtr.Zero) throw new InvalidDataException("PDFium could not load selected page.");
                    try
                    {
                        var widthPts = FPDF_GetPageWidthF(page);
                        var heightPts = FPDF_GetPageHeightF(page);
                        if (widthPts <= 0 || heightPts <= 0 || widthPts > 5000 || heightPts > 5000)
                            throw new InvalidDataException("PDF page dimensions exceed safe range.");
                        const float scale = 2.0f; // 144 dpi from 72pt units
                        var width = checked((int)Math.Ceiling(widthPts * scale));
                        var height = checked((int)Math.Ceiling(heightPts * scale));
                        if (width is <= 0 or > 8000 || height is <= 0 or > 8000 || (long)width * height > 16_000_000)
                            throw new InvalidDataException("Rendered PDF page exceeds OCR memory budget.");
                        var bitmap = FPDFBitmap_CreateEx(width, height, Bgra, IntPtr.Zero, 0);
                        if (bitmap == IntPtr.Zero) throw new OutOfMemoryException("PDFium bitmap allocation failed.");
                        try
                        {
                            FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
                            FPDF_RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, 0);
                            var stride = FPDFBitmap_GetStride(bitmap);
                            if (stride != width * 4) throw new InvalidDataException("PDFium returned unsupported bitmap stride.");
                            var bytes = new byte[checked(stride * height)];
                            Marshal.Copy(FPDFBitmap_GetBuffer(bitmap), bytes, 0, bytes.Length);
                            try { return OcrService.ExtractImage(ToBmp(bytes, width, height), ct); }
                            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
                        }
                        finally { FPDFBitmap_Destroy(bitmap); }
                    }
                    finally { FPDF_ClosePage(page); }
                }
                finally { FPDF_CloseDocument(doc); }
            }
            finally { data.Free(); }
        }
    }
    // 32-bit uncompressed top-down BMP, avoids plaintext temporary documents and WPF STA dependencies.
    private static byte[] ToBmp(byte[] pixels, int width, int height)
    {
        var bmp = new byte[checked(54 + pixels.Length)];
        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(2), bmp.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), -height);
        BinaryPrimitives.WriteInt16LittleEndian(bmp.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bmp.AsSpan(28), 32);
        pixels.CopyTo(bmp, 54);
        return bmp;
    }
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern void FPDF_InitLibrary();
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr FPDF_LoadMemDocument(IntPtr data, int size, [MarshalAs(UnmanagedType.LPStr)] string? password);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern int FPDF_GetPageCount(IntPtr doc);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr FPDF_LoadPage(IntPtr doc, int index);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern float FPDF_GetPageWidthF(IntPtr page);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern float FPDF_GetPageHeightF(IntPtr page);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr FPDFBitmap_CreateEx(int width,int height,int format,IntPtr buffer,int stride);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern void FPDFBitmap_FillRect(IntPtr bitmap,int left,int top,int width,int height,uint color);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern void FPDF_RenderPageBitmap(IntPtr bitmap,IntPtr page,int x,int y,int width,int height,int rotate,int flags);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern int FPDFBitmap_GetStride(IntPtr bitmap);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern void FPDFBitmap_Destroy(IntPtr bitmap);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern void FPDF_ClosePage(IntPtr page);
    [DllImport("wi_pdfium", CallingConvention=CallingConvention.Cdecl)] private static extern void FPDF_CloseDocument(IntPtr document);
}
