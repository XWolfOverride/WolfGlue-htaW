using System.Runtime.InteropServices;

namespace WolfGlue.Platform.Windows;

/// <summary>P/Invoke de Win32 (user32, kernel32, ole32, dwmapi, shell32, shlwapi).</summary>
internal static unsafe partial class Win32
{
    // ------------------------------------------------------------- constantes
    public const uint WS_OVERLAPPED = 0x00000000, WS_CAPTION = 0x00C00000, WS_SYSMENU = 0x00080000,
        WS_THICKFRAME = 0x00040000, WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000,
        WS_CLIPCHILDREN = 0x02000000, WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const uint WS_EX_TOPMOST = 0x00000008, WS_EX_DLGMODALFRAME = 0x00000001;

    public const uint WM_DESTROY = 0x0002, WM_MOVE = 0x0003, WM_SIZE = 0x0005, WM_ACTIVATE = 0x0006,
        WM_SETFOCUS = 0x0007, WM_CLOSE = 0x0010, WM_GETMINMAXINFO = 0x0024, WM_SETTINGCHANGE = 0x001A,
        WM_DPICHANGED = 0x02E0, WM_APP = 0x8000, WM_MOVING = 0x0216, WM_TIMER = 0x0113, WM_ERASEBKGND = 0x0014;

    public const uint WS_CHILD = 0x40000000;
    public const int SW_SHOWNA = 8, SW_HIDE = 0, SW_SHOWNORMAL = 1, SW_SHOWMINIMIZED = 2, SW_SHOWMAXIMIZED = 3, SW_RESTORE = 9;
    public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
    public const int CW_USEDEFAULT = unchecked((int)0x80000000);
    public static readonly nint HWND_MESSAGE = -3;
    public const uint MONITOR_DEFAULTTONEAREST = 2, MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MB_OK = 0, MB_ICONERROR = 0x10;
    public const int IDC_ARROW = 32512;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
    public const uint COINIT_APARTMENTTHREADED = 0x2;
    public const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

    // ------------------------------------------------------------- estructuras
    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public delegate* unmanaged<nint, uint, nint, nint, nint> lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground;
        public char* lpszMenuName, lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam, lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
    }

    // ------------------------------------------------------------- user32
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial ushort RegisterClassExW(WNDCLASSEXW* wc);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(uint exStyle, string className, string? windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    public static partial nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hwnd, int cmd);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowTextW(nint hwnd, string text);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(nint hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessageW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll")]
    public static partial int GetMessageW(out MSG msg, nint hwnd, uint min, uint max);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(in MSG msg);

    [LibraryImport("user32.dll")]
    public static partial nint DispatchMessageW(in MSG msg);

    [LibraryImport("user32.dll")]
    public static partial nint LoadCursorW(nint instance, nint name);

    public const uint WM_SETICON = 0x0080;
    public const int SM_CXICON = 11, SM_CXSMICON = 49;

    [LibraryImport("user32.dll")]
    public static partial nint SendMessageW(nint hwnd, uint msg, nint wParam, nint lParam);
    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetricsForDpi(int index, uint dpi);
    [LibraryImport("user32.dll")]
    public static partial nint LoadIconW(nint instance, nint name);
    [LibraryImport("user32.dll")]
    public static partial nint GetClassLongPtrW(nint hwnd, int index);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessDpiAwarenessContext(nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AdjustWindowRectExForDpi(ref RECT rect, uint style,
        [MarshalAs(UnmanagedType.Bool)] bool menu, uint exStyle, uint dpi);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfoW(nint monitor, ref MONITORINFO info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnableWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsZoomed(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial nuint SetTimer(nint hwnd, nint id, uint elapseMs, nint timerProc);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool KillTimer(nint hwnd, nint id);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBoxW(nint hwnd, string text, string caption, uint type);

    // ------------------------------------------------------------- otros
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? name);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("ole32.dll")]
    public static partial int CoInitializeEx(nint reserved, uint coInit);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int attr, int* value, int size);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint ShellExecuteW(nint hwnd, string? op, string file, string? parameters, string? dir, int show);

    [LibraryImport("shlwapi.dll")]
    public static partial nint SHCreateMemStream(byte* data, uint size);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateSolidBrush(uint colorRef);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint obj);

    [LibraryImport("user32.dll")]
    public static partial int FillRect(nint hdc, ref RECT rect, nint brush);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InvalidateRect(nint hwnd, nint rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

    // ------------------------------------------------------------- COM y shell
    public const uint CLSCTX_INPROC_SERVER = 1;

    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint obj);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SHCreateItemFromParsingName(string path, nint bindCtx, in Guid iid, out nint item);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint ILCreateFromPathW(string path);

    [LibraryImport("shell32.dll")]
    public static partial void ILFree(nint pidl);

    [LibraryImport("shell32.dll")]
    public static partial int SHOpenFolderAndSelectItems(nint pidlFolder, uint count, nint children, uint flags);

    // ------------------------------------------------------------- portapapeles
    public const uint CF_UNICODETEXT = 13, CF_DIB = 8, CF_DIBV5 = 17, GMEM_MOVEABLE = 0x0002;
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterClipboardFormatW(string name);
    [LibraryImport("kernel32.dll")]
    public static partial nuint GlobalSize(nint mem);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenClipboard(nint owner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    public static partial nint GetClipboardData(uint format);

    [LibraryImport("user32.dll")]
    public static partial nint SetClipboardData(uint format, nint mem);

    [LibraryImport("kernel32.dll")]
    public static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    public static partial nint GlobalFree(nint mem);

    [LibraryImport("kernel32.dll")]
    public static partial nint GlobalLock(nint mem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(nint mem);

    // ------------------------------------------------------------- pantallas
    public const uint MONITORINFOF_PRIMARY = 1;
    public const int MDT_EFFECTIVE_DPI = 0;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayMonitors(nint hdc, nint clip,
        delegate* unmanaged<nint, nint, RECT*, nint, int> callback, nint data);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    // ------------------------------------------------------------- ajustes del sistema
    public const uint SPI_GETHIGHCONTRAST = 0x0042, SPI_GETNONCLIENTMETRICS = 0x0029;
    public const uint HCF_HIGHCONTRASTON = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    public struct HIGHCONTRASTW
    {
        public uint cbSize, dwFlags;
        public nint lpszDefaultScheme;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct LOGFONTW
    {
        public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
        public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;
        public fixed char lfFaceName[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NONCLIENTMETRICSW
    {
        public uint cbSize;
        public int iBorderWidth, iScrollWidth, iScrollHeight, iCaptionWidth, iCaptionHeight;
        public LOGFONTW lfCaptionFont;
        public int iSmCaptionWidth, iSmCaptionHeight;
        public LOGFONTW lfSmCaptionFont;
        public int iMenuWidth, iMenuHeight;
        public LOGFONTW lfMenuFont, lfStatusFont, lfMessageFont;
        public int iPaddedBorderWidth;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SystemParametersInfoW(uint action, uint param, void* data, uint winIni);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SystemParametersInfoForDpi(uint action, uint param, void* data, uint winIni, uint dpi);

    // ------------------------------------------------------------- MessageBox
    public const uint MB_OKCANCEL = 0x1, MB_YESNOCANCEL = 0x3, MB_YESNO = 0x4,
        MB_ICONQUESTION = 0x20, MB_ICONWARNING = 0x30, MB_ICONINFORMATION = 0x40, MB_TASKMODAL = 0x2000;
    public const uint MB_DEFBUTTON2 = 0x100, MB_DEFBUTTON3 = 0x200;
    public const int IDOK = 1, IDCANCEL = 2, IDYES = 6, IDNO = 7;

    // ------------------------------------------------------------- WebView2Loader
    // En AOT se enlaza estaticamente (WebView2LoaderStatic.lib, ver WolfGlue.targets); con dotnet run se carga la DLL.
    [LibraryImport("WebView2Loader", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CreateCoreWebView2EnvironmentWithOptions(string? browserExecutableFolder,
        string? userDataFolder, nint environmentOptions, nint environmentCreatedHandler);

    // ------------------------------------------------------------- IStream (solo lectura, por vtable)
    /// <summary>Lee un IStream completo y libera la referencia.</summary>
    public static byte[] ReadAndReleaseStream(nint stream)
    {
        if (stream == 0) return [];
        try
        {
            var vtbl = *(nint**)stream;
            // ISequentialStream::Read esta en la posicion 3 (tras QueryInterface, AddRef, Release).
            var read = (delegate* unmanaged[Stdcall]<nint, byte*, uint, uint*, int>)vtbl[3];
            using var ms = new MemoryStream();
            var buf = new byte[64 * 1024];
            while (true)
            {
                uint n;
                int hr;
                fixed (byte* p = buf) hr = read(stream, p, (uint)buf.Length, &n);
                if (n > 0) ms.Write(buf, 0, (int)n);
                if (hr != 0 || n == 0) break; // S_FALSE o fin
            }
            return ms.ToArray();
        }
        finally { Marshal.Release(stream); }
    }

    public static int LOWORD(nint v) => (int)(v & 0xFFFF);
    public static int HIWORD(nint v) => (int)((v >> 16) & 0xFFFF);
}
