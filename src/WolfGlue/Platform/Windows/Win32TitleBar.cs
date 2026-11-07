using System.Runtime.InteropServices;

namespace WolfGlue.Platform.Windows;

/// <summary>P/Invoke para la barra de titulo propia: area no cliente, raton, GDI, GDI+, menus y ayudas emergentes.</summary>
internal static unsafe partial class Win32
{
    public const uint WM_PAINT = 0x000F, WM_NCCALCSIZE = 0x0083, WM_NCHITTEST = 0x0084, WM_NCACTIVATE = 0x0086,
        WM_NCMOUSEMOVE = 0x00A0, WM_NCLBUTTONDOWN = 0x00A1, WM_NCLBUTTONUP = 0x00A2, WM_NCLBUTTONDBLCLK = 0x00A3,
        WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203,
        WM_CAPTURECHANGED = 0x0215, WM_NCMOUSELEAVE = 0x02A2, WM_MOUSELEAVE = 0x02A3, WM_USER = 0x0400;

    public const uint WM_GETICON = 0x007F;
    public const int HTNOWHERE = 0, HTCLIENT = 1, HTCAPTION = 2, HTMINBUTTON = 8, HTMAXBUTTON = 9, HTTOP = 12,
        HTTOPLEFT = 13, HTTOPRIGHT = 14, HTCLOSE = 20;

    public const int SM_CXFRAME = 32, SM_CYFRAME = 33, SM_CXPADDEDBORDER = 92;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const int WA_INACTIVE = 0;

    public const uint TME_LEAVE = 0x2, TME_NONCLIENT = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    public struct TRACKMOUSEEVENT
    {
        public uint cbSize, dwFlags;
        public nint hwndTrack;
        public uint dwHoverTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NCCALCSIZE_PARAMS
    {
        public RECT rgrc0, rgrc1, rgrc2;
        public nint lppos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PAINTSTRUCT
    {
        public nint hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore, fIncUpdate;
        public fixed byte rgbReserved[32];
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TrackMouseEvent(ref TRACKMOUSEEVENT tme);

    [LibraryImport("user32.dll")] public static partial nint SetCapture(nint hwnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool ReleaseCapture();
    [LibraryImport("user32.dll")] public static partial nint GetCapture();
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool ClientToScreen(nint hwnd, ref POINT p);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool ScreenToClient(nint hwnd, ref POINT p);
    [LibraryImport("user32.dll")] public static partial nint BeginPaint(nint hwnd, out PAINTSTRUCT ps);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool EndPaint(nint hwnd, ref PAINTSTRUCT ps);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool InvalidateRect(nint hwnd, ref RECT r, [MarshalAs(UnmanagedType.Bool)] bool erase);

    // ------------------------------------------------------------- GDI
    public const int TRANSPARENT = 1;
    public const uint SRCCOPY = 0x00CC0020;
    public const uint DT_CENTER = 0x1, DT_VCENTER = 0x4, DT_SINGLELINE = 0x20, DT_CALCRECT = 0x400, DT_NOPREFIX = 0x800,
        DT_END_ELLIPSIS = 0x8000, DT_LEFT = 0x0;
    public const uint DI_NORMAL = 0x3;

    [LibraryImport("gdi32.dll")] public static partial nint CreateCompatibleDC(nint hdc);
    [LibraryImport("gdi32.dll")] public static partial nint CreateCompatibleBitmap(nint hdc, int w, int h);
    [LibraryImport("gdi32.dll")] public static partial nint SelectObject(nint hdc, nint obj);
    [LibraryImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DeleteDC(nint hdc);
    [LibraryImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool BitBlt(nint dst, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);
    [LibraryImport("gdi32.dll")] public static partial int SetBkMode(nint hdc, int mode);
    [LibraryImport("gdi32.dll")] public static partial uint SetTextColor(nint hdc, uint color);
    [LibraryImport("gdi32.dll")] public static partial nint CreateFontIndirectW(LOGFONTW* lf);
    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)] public static partial int GetTextFaceW(nint hdc, int count, char* name);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)] public static partial int DrawTextW(nint hdc, string text, int len, ref RECT r, uint format);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DrawIconEx(nint hdc, int x, int y, nint icon, int w, int h, uint step, nint brush, uint flags);
    [LibraryImport("user32.dll")] public static partial nint GetDC(nint hwnd);
    [LibraryImport("user32.dll")] public static partial int ReleaseDC(nint hwnd, nint hdc);

    // ------------------------------------------------------------- menus
    public const uint MF_STRING = 0x0, MF_GRAYED = 0x1, MF_CHECKED = 0x8, MF_SEPARATOR = 0x800;
    public const uint TPM_LEFTALIGN = 0x0, TPM_TOPALIGN = 0x0, TPM_RETURNCMD = 0x100, TPM_NONOTIFY = 0x80;

    [LibraryImport("user32.dll")] public static partial nint CreatePopupMenu();
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)] [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendMenuW(nint menu, uint flags, nint id, string? text);
    [LibraryImport("user32.dll")] public static partial int TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool DestroyMenu(nint menu);

    // ------------------------------------------------------------- ayudas emergentes (tooltips)
    public const uint TTS_ALWAYSTIP = 0x1, TTS_NOPREFIX = 0x2, TTF_SUBCLASS = 0x10;
    public const uint TTM_ADDTOOLW = WM_USER + 50, TTM_DELTOOLW = WM_USER + 51, TTM_SETMAXTIPWIDTH = WM_USER + 24;
    public const uint ICC_WIN95_CLASSES = 0xFF;

    [StructLayout(LayoutKind.Sequential)]
    public struct TOOLINFOW
    {
        public uint cbSize, uFlags;
        public nint hwnd, uId;
        public RECT rect;
        public nint hinst, lpszText, lParam, lpReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INITCOMMONCONTROLSEX { public uint dwSize, dwICC; }

    [LibraryImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool InitCommonControlsEx(ref INITCOMMONCONTROLSEX icc);

    // ------------------------------------------------------------- fuentes propias en memoria
    [LibraryImport("gdi32.dll")] public static partial nint AddFontMemResourceEx(byte* font, uint size, nint reserved, out uint count);
    [LibraryImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool RemoveFontMemResourceEx(nint handle);

    // ------------------------------------------------------------- campo de busqueda (EDIT nativo)
    public const uint ES_AUTOHSCROLL = 0x0080, WS_VISIBLE = 0x10000000, WS_TABSTOP = 0x00010000;
    public const uint WM_SETFONT = 0x0030, WM_COMMAND = 0x0111, WM_CTLCOLOREDIT = 0x0133, WM_KEYDOWN = 0x0100, WM_CHAR = 0x0102,
        WM_GETDLGCODE = 0x0087, EM_SETSEL = 0x00B1, EM_SETCUEBANNER = 0x1501;
    public const int EN_SETFOCUS = 0x0100, EN_KILLFOCUS = 0x0200, EN_CHANGE = 0x0300;
    public const int VK_RETURN = 0x0D, VK_ESCAPE = 0x1B;

    [LibraryImport("gdi32.dll")] public static partial uint SetBkColor(nint hdc, uint color);
    [LibraryImport("user32.dll")] public static partial nint SetFocus(nint hwnd);
    [LibraryImport("user32.dll")] public static partial nint GetFocus();
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)] public static partial int GetWindowTextW(nint hwnd, char* text, int max);
    [LibraryImport("user32.dll")] public static partial int GetWindowTextLengthW(nint hwnd);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, EntryPoint = "SendMessageW")]
    public static partial nint SendMessageText(nint hwnd, uint msg, nint wParam, string lParam);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static partial bool MoveWindow(nint hwnd, int x, int y, int w, int h, [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [LibraryImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowSubclass(nint hwnd, delegate* unmanaged<nint, uint, nint, nint, nint, nint, nint> proc, nint id, nint data);
    [LibraryImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RemoveWindowSubclass(nint hwnd, delegate* unmanaged<nint, uint, nint, nint, nint, nint, nint> proc, nint id);
    [LibraryImport("comctl32.dll")] public static partial nint DefSubclassProc(nint hwnd, uint msg, nint wParam, nint lParam);

    public static string GetText(nint hwnd)
    {
        int n = GetWindowTextLengthW(hwnd);
        if (n <= 0) return "";
        var buf = new char[n + 1];
        fixed (char* p = buf) { n = GetWindowTextW(hwnd, p, buf.Length); }
        return new string(buf, 0, Math.Max(0, n));
    }
}


/// <summary>GDI+ (API plana), solo para rellenos y bordes con esquinas suavizadas.</summary>
internal static unsafe partial class Gdip
{
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInput { public uint Version; public nint DebugCallback; public int SuppressThread, SuppressCodecs; }

    private static nint _token;

    public static bool Start()
    {
        if (_token != 0) return true;
        var input = new StartupInput { Version = 1 };
        try { return GdiplusStartup(out _token, ref input, 0) == 0; }
        catch (DllNotFoundException) { return false; }
    }

    /// <summary>Rellena (y opcionalmente bordea) un rectangulo redondeado. Colores ARGB; 0 = no pintar.</summary>
    public static void RoundRect(nint hdc, float x, float y, float w, float h, float r, uint fill, uint border, float borderWidth)
    {
        if (GdipCreateFromHDC(hdc, out var g) != 0) return;
        try
        {
            GdipSetSmoothingMode(g, 4); // antialias
            GdipSetPixelOffsetMode(g, 4); // calidad
            if (borderWidth > 0) { float k = borderWidth / 2; x += k; y += k; w -= borderWidth; h -= borderWidth; }
            GdipCreatePath(0, out var path);
            try
            {
                r = Math.Min(r, Math.Min(w, h) / 2);
                if (r <= 0.5f) GdipAddPathRectangle(path, x, y, w, h);
                else
                {
                    float d = r * 2;
                    GdipAddPathArc(path, x, y, d, d, 180, 90);
                    GdipAddPathArc(path, x + w - d, y, d, d, 270, 90);
                    GdipAddPathArc(path, x + w - d, y + h - d, d, d, 0, 90);
                    GdipAddPathArc(path, x, y + h - d, d, d, 90, 90);
                    GdipClosePathFigure(path);
                }
                if (fill != 0 && GdipCreateSolidFill(fill, out var brush) == 0)
                {
                    GdipFillPath(g, brush, path);
                    GdipDeleteBrush(brush);
                }
                if (border != 0 && borderWidth > 0 && GdipCreatePen1(border, borderWidth, 2, out var pen) == 0)
                {
                    GdipDrawPath(g, pen, path);
                    GdipDeletePen(pen);
                }
            }
            finally { GdipDeletePath(path); }
        }
        finally { GdipDeleteGraphics(g); }
    }

    // ------------------------------------------------------------- imagenes
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapData { public uint Width, Height; public int Stride, PixelFormat; public nint Scan0, Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    private struct GpRect { public int X, Y, Width, Height; }

    private const int PixelFormat32bppPARGB = 0x000E200B, ImageLockModeReadWrite = 3;

    /// <summary>Bitmap de GDI+ a partir de los bytes de un PNG o ICO (0 si no se puede leer). Se libera con <see cref="Dispose"/>.</summary>
    public static nint Load(byte[] data)
    {
        nint stream;
        fixed (byte* p = data) stream = Win32.SHCreateMemStream(p, (uint)data.Length);
        if (stream == 0) return 0;
        try { return GdipCreateBitmapFromStream(stream, out var bmp) == 0 ? bmp : 0; }
        finally { Marshal.Release(stream); }
    }

    public static (int w, int h) Size(nint image)
    {
        GdipGetImageWidth(image, out var w);
        GdipGetImageHeight(image, out var h);
        return ((int)w, (int)h);
    }

    /// <summary>
    /// Copia escalada a <paramref name="w"/>x<paramref name="h"/>. Con <paramref name="tint"/> (0xRRGGBB) se usa solo la
    /// transparencia como silueta, pintada de ese color (icono monocromo que sigue el tema).
    /// </summary>
    public static nint Scaled(nint source, int w, int h, uint? tint)
    {
        if (GdipCreateBitmapFromScan0(w, h, 0, PixelFormat32bppPARGB, 0, out var bmp) != 0) return 0;
        if (GdipGetImageGraphicsContext(bmp, out var g) == 0)
        {
            GdipSetInterpolationMode(g, 7); // bicubica de alta calidad
            GdipSetPixelOffsetMode(g, 4);
            GdipDrawImageRectI(g, source, 0, 0, w, h);
            GdipDeleteGraphics(g);
        }
        if (tint is { } c)
        {
            var rect = new GpRect { Width = w, Height = h };
            if (GdipBitmapLockBits(bmp, ref rect, ImageLockModeReadWrite, PixelFormat32bppPARGB, out var bd) == 0)
            {
                uint r = (c >> 16) & 0xFF, gg = (c >> 8) & 0xFF, b = c & 0xFF;
                for (int y = 0; y < h; y++)
                {
                    var row = (uint*)(bd.Scan0 + y * bd.Stride);
                    for (int x = 0; x < w; x++)
                    {
                        uint a = row[x] >> 24;
                        row[x] = a << 24 | (r * a / 255) << 16 | (gg * a / 255) << 8 | (b * a / 255);
                    }
                }
                GdipBitmapUnlockBits(bmp, ref bd);
            }
        }
        return bmp;
    }

    /// <summary>Pinta una imagen de GDI+ en un DC con su transparencia.</summary>
    public static void Draw(nint hdc, nint image, int x, int y, int w, int h)
    {
        if (GdipCreateFromHDC(hdc, out var g) != 0) return;
        GdipSetInterpolationMode(g, 7);
        GdipDrawImageRectI(g, image, x, y, w, h);
        GdipDeleteGraphics(g);
    }

    public static void Dispose(nint image) { if (image != 0) GdipDisposeImage(image); }

    /// <summary>Pixeles BGRA (alfa sin premultiplicar) de arriba abajo, con filas de <c>w * 4</c> bytes.</summary>
    public static (int w, int h, byte[] bgra) Pixels(nint image)
    {
        var (w, h) = Size(image);
        var px = new byte[w * h * 4];
        var rect = new GpRect { Width = w, Height = h };
        if (GdipBitmapLockBits(image, ref rect, 1 /* lectura */, 0x0026200A /* 32bppARGB */, out var bd) != 0) return (0, 0, []);
        fixed (byte* dst = px)
            for (int y = 0; y < h; y++)
                Buffer.MemoryCopy((void*)(bd.Scan0 + y * bd.Stride), dst + y * w * 4, w * 4, w * 4);
        GdipBitmapUnlockBits(image, ref bd);
        return (w, h, px);
    }

    [LibraryImport("gdiplus.dll")] private static partial int GdipCreateBitmapFromStream(nint stream, out nint bitmap);
    [LibraryImport("gdiplus.dll")] private static partial int GdipCreateBitmapFromScan0(int w, int h, int stride, int format, nint scan0, out nint bitmap);
    [LibraryImport("gdiplus.dll")] private static partial int GdipGetImageGraphicsContext(nint image, out nint graphics);
    [LibraryImport("gdiplus.dll")] private static partial int GdipSetInterpolationMode(nint graphics, int mode);
    [LibraryImport("gdiplus.dll")] private static partial int GdipDrawImageRectI(nint graphics, nint image, int x, int y, int w, int h);
    [LibraryImport("gdiplus.dll")] private static partial int GdipBitmapLockBits(nint bitmap, ref GpRect rect, int flags, int format, out BitmapData data);
    [LibraryImport("gdiplus.dll")] private static partial int GdipBitmapUnlockBits(nint bitmap, ref BitmapData data);
    [LibraryImport("gdiplus.dll")] private static partial int GdipGetImageWidth(nint image, out uint width);
    [LibraryImport("gdiplus.dll")] private static partial int GdipGetImageHeight(nint image, out uint height);
    [LibraryImport("gdiplus.dll")] private static partial int GdipDisposeImage(nint image);

    [LibraryImport("gdiplus.dll")] private static partial int GdiplusStartup(out nint token, ref StartupInput input, nint output);
    [LibraryImport("gdiplus.dll")] private static partial int GdipCreateFromHDC(nint hdc, out nint graphics);
    [LibraryImport("gdiplus.dll")] private static partial int GdipDeleteGraphics(nint graphics);
    [LibraryImport("gdiplus.dll")] private static partial int GdipSetSmoothingMode(nint graphics, int mode);
    [LibraryImport("gdiplus.dll")] private static partial int GdipSetPixelOffsetMode(nint graphics, int mode);
    [LibraryImport("gdiplus.dll")] private static partial int GdipCreatePath(int fillMode, out nint path);
    [LibraryImport("gdiplus.dll")] private static partial int GdipDeletePath(nint path);
    [LibraryImport("gdiplus.dll")] private static partial int GdipAddPathArc(nint path, float x, float y, float w, float h, float start, float sweep);
    [LibraryImport("gdiplus.dll")] private static partial int GdipAddPathRectangle(nint path, float x, float y, float w, float h);
    [LibraryImport("gdiplus.dll")] private static partial int GdipClosePathFigure(nint path);
    [LibraryImport("gdiplus.dll")] private static partial int GdipCreateSolidFill(uint argb, out nint brush);
    [LibraryImport("gdiplus.dll")] private static partial int GdipDeleteBrush(nint brush);
    [LibraryImport("gdiplus.dll")] private static partial int GdipFillPath(nint graphics, nint brush, nint path);
    [LibraryImport("gdiplus.dll")] private static partial int GdipCreatePen1(uint argb, float width, int unit, out nint pen);
    [LibraryImport("gdiplus.dll")] private static partial int GdipDeletePen(nint pen);
    [LibraryImport("gdiplus.dll")] private static partial int GdipDrawPath(nint graphics, nint pen, nint path);
}
