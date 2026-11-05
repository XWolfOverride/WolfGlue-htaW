using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Microsoft.Win32;
using WolfGlue.Native;

namespace WolfGlue.Platform.Windows;

/// <summary>Servicios nativos de Windows. Se llaman en el hilo de UI (STA).</summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class WinServices(WinPlatform platform) : IPlatformServices
{
    private static readonly StrategyBasedComWrappers Com = new();

    private static nint Hwnd(IPlatformWindow? owner) => owner is WinWindow w ? w.Hwnd : 0;

    // ------------------------------------------------------------- dialogos de archivo
    public string[]? OpenFile(IPlatformWindow? owner, FileDialogOptions o)
    {
        var dlg = Create<IFileOpenDialog>(ShellConst.CLSID_FileOpenDialog, ShellConst.IID_IFileOpenDialog);
        uint flags = ShellConst.FOS_FORCEFILESYSTEM | ShellConst.FOS_NOCHANGEDIR | ShellConst.FOS_PATHMUSTEXIST
                     | ShellConst.FOS_FILEMUSTEXIST;
        if (o.Multiple) flags |= ShellConst.FOS_ALLOWMULTISELECT;
        dlg.SetOptions(dlg.GetOptions() | flags);
        using var filters = Configure(dlg, o.Title, o.Filters, o.DefaultPath, null);
        if (!Show(dlg, owner)) return null;
        var items = dlg.GetResults();
        var count = items.GetCount();
        var result = new string[count];
        for (uint i = 0; i < count; i++) result[i] = PathOf(items.GetItemAt(i));
        return result;
    }

    public string? SaveFile(IPlatformWindow? owner, FileDialogOptions o)
    {
        var dlg = Create<IFileSaveDialog>(ShellConst.CLSID_FileSaveDialog, ShellConst.IID_IFileSaveDialog);
        dlg.SetOptions(dlg.GetOptions() | ShellConst.FOS_FORCEFILESYSTEM | ShellConst.FOS_NOCHANGEDIR
                       | ShellConst.FOS_PATHMUSTEXIST | ShellConst.FOS_OVERWRITEPROMPT);
        // DefaultPath puede ser una carpeta o una ruta de archivo completa.
        string? folder = o.DefaultPath, name = o.FileName;
        if (folder is not null && !Directory.Exists(folder))
        {
            name ??= Path.GetFileName(folder);
            folder = Path.GetDirectoryName(folder);
        }
        using var filters = Configure(dlg, o.Title, o.Filters, folder, name);
        if (!string.IsNullOrEmpty(o.DefaultExtension)) dlg.SetDefaultExtension(o.DefaultExtension.TrimStart('.'));
        if (!Show(dlg, owner)) return null;
        return PathOf(dlg.GetResult());
    }

    public string? ChooseFolder(IPlatformWindow? owner, FolderDialogOptions o)
    {
        var dlg = Create<IFileOpenDialog>(ShellConst.CLSID_FileOpenDialog, ShellConst.IID_IFileOpenDialog);
        dlg.SetOptions(dlg.GetOptions() | ShellConst.FOS_PICKFOLDERS | ShellConst.FOS_FORCEFILESYSTEM
                       | ShellConst.FOS_NOCHANGEDIR | ShellConst.FOS_PATHMUSTEXIST);
        using var filters = Configure(dlg, o.Title, [], o.DefaultPath, null);
        if (!Show(dlg, owner)) return null;
        return PathOf(dlg.GetResult());
    }

    private static T Create<T>(Guid clsid, Guid iid) where T : class
    {
        Marshal.ThrowExceptionForHR(Win32.CoCreateInstance(clsid, 0, Win32.CLSCTX_INPROC_SERVER, iid, out var p));
        try { return (T)Com.GetOrCreateObjectForComInstance(p, CreateObjectFlags.None); }
        finally { Marshal.Release(p); }
    }

    private static bool Show(IFileDialog dlg, IPlatformWindow? owner)
    {
        int hr = dlg.Show(Hwnd(owner));
        if (hr == ShellConst.ERROR_CANCELLED_HR) return false;
        Marshal.ThrowExceptionForHR(hr);
        return true;
    }

    private static string PathOf(IShellItem item)
    {
        var p = item.GetDisplayName(ShellConst.SIGDN_FILESYSPATH);
        try { return Marshal.PtrToStringUni(p) ?? ""; }
        finally { Marshal.FreeCoTaskMem(p); }
    }

    /// <summary>Titulo, filtros, carpeta y nombre iniciales. Devuelve la memoria de los filtros (liberar tras Show).</summary>
    private static FilterMemory Configure(IFileDialog dlg, string? title, FileFilter[] filters, string? folder, string? fileName)
    {
        if (!string.IsNullOrEmpty(title)) dlg.SetTitle(title);
        if (!string.IsNullOrEmpty(fileName)) dlg.SetFileName(fileName);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)
            && Win32.SHCreateItemFromParsingName(Path.GetFullPath(folder), 0, ShellConst.IID_IShellItem, out var p) >= 0)
        {
            try { dlg.SetFolder((IShellItem)Com.GetOrCreateObjectForComInstance(p, CreateObjectFlags.None)); }
            finally { Marshal.Release(p); }
        }
        var mem = new FilterMemory(filters);
        if (mem.Count > 0)
        {
            dlg.SetFileTypes((uint)mem.Count, mem.Pointer);
            dlg.SetFileTypeIndex(1);
        }
        return mem;
    }

    /// <summary>Array nativo de COMDLG_FILTERSPEC { LPCWSTR nombre; LPCWSTR patrones; }.</summary>
    private sealed class FilterMemory : IDisposable
    {
        private readonly List<nint> _strings = [];
        public nint Pointer { get; }
        public int Count { get; }

        public FilterMemory(FileFilter[] filters)
        {
            Count = filters.Length;
            if (Count == 0) return;
            Pointer = Marshal.AllocCoTaskMem(Count * 2 * sizeof(nint));
            var spec = (nint*)Pointer;
            for (int i = 0; i < Count; i++)
            {
                var f = filters[i];
                var pattern = f.Spec();
                spec[i * 2] = Str(string.IsNullOrEmpty(f.Name) ? pattern : f.Name);
                spec[i * 2 + 1] = Str(pattern);
            }
        }

        private nint Str(string s)
        {
            var p = Marshal.StringToCoTaskMemUni(s);
            _strings.Add(p);
            return p;
        }

        public void Dispose()
        {
            foreach (var s in _strings) Marshal.FreeCoTaskMem(s);
            if (Pointer != 0) Marshal.FreeCoTaskMem(Pointer);
        }
    }

    // ------------------------------------------------------------- mensajes
    public MessageResult Message(IPlatformWindow? owner, string title, MessageOptions o)
    {
        uint flags = o.Buttons switch
        {
            MessageButtons.OkCancel => Win32.MB_OKCANCEL,
            MessageButtons.YesNo => Win32.MB_YESNO,
            MessageButtons.YesNoCancel => Win32.MB_YESNOCANCEL,
            _ => Win32.MB_OK,
        };
        flags |= o.Kind switch
        {
            MessageKind.Warning => Win32.MB_ICONWARNING,
            MessageKind.Error => Win32.MB_ICONERROR,
            MessageKind.Question => Win32.MB_ICONQUESTION,
            _ => Win32.MB_ICONINFORMATION,
        };
        var hwnd = Hwnd(owner);
        if (hwnd == 0) flags |= Win32.MB_TASKMODAL;
        flags |= o.DefaultButton switch { 1 => Win32.MB_DEFBUTTON2, 2 => Win32.MB_DEFBUTTON3, _ => 0u };
        return Win32.MessageBoxW(hwnd, o.Text, title, flags) switch
        {
            Win32.IDOK => MessageResult.Ok,
            Win32.IDYES => MessageResult.Yes,
            Win32.IDNO => MessageResult.No,
            _ => MessageResult.Cancel,
        };
    }

    // ------------------------------------------------------------- portapapeles
    private bool OpenClipboardRetry()
    {
        // Otro proceso puede tenerlo abierto un instante.
        for (int i = 0; i < 10; i++)
        {
            if (Win32.OpenClipboard(platform.MessageWindow)) return true;
            Thread.Sleep(20);
        }
        return false;
    }

    public string? GetClipboardText()
    {
        if (!OpenClipboardRetry()) throw new IOException("No se pudo abrir el portapapeles.");
        try
        {
            var h = Win32.GetClipboardData(Win32.CF_UNICODETEXT);
            if (h == 0) return null;
            var p = Win32.GlobalLock(h);
            if (p == 0) return null;
            try { return Marshal.PtrToStringUni(p); }
            finally { Win32.GlobalUnlock(h); }
        }
        finally { Win32.CloseClipboard(); }
    }

    public void SetClipboardText(string text)
    {
        if (!OpenClipboardRetry()) throw new IOException("No se pudo abrir el portapapeles.");
        try
        {
            Win32.EmptyClipboard();
            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var h = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, bytes);
            if (h == 0) throw new OutOfMemoryException();
            var p = (char*)Win32.GlobalLock(h);
            text.AsSpan().CopyTo(new Span<char>(p, text.Length));
            p[text.Length] = '\0';
            Win32.GlobalUnlock(h);
            // Si SetClipboardData funciona, la memoria pasa a ser del sistema.
            if (Win32.SetClipboardData(Win32.CF_UNICODETEXT, h) == 0)
            {
                Win32.GlobalFree(h);
                throw new IOException("No se pudo escribir en el portapapeles.");
            }
        }
        finally { Win32.CloseClipboard(); }
    }

    // ------------------------------------------------------------- pantallas
    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var list = new List<ScreenInfo>();
        var handle = GCHandle.Alloc(list);
        try { Win32.EnumDisplayMonitors(0, 0, &MonitorCallback, GCHandle.ToIntPtr(handle)); }
        finally { handle.Free(); }
        return list;
    }

    [UnmanagedCallersOnly]
    private static int MonitorCallback(nint monitor, nint hdc, RECT* rect, nint data)
    {
        try
        {
            var list = (List<ScreenInfo>)GCHandle.FromIntPtr(data).Target!;
            var mi = new Win32.MONITORINFO { cbSize = (uint)sizeof(Win32.MONITORINFO) };
            if (!Win32.GetMonitorInfoW(monitor, ref mi)) return 1;
            double scale = Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI, out var dpi, out _) >= 0 && dpi > 0
                ? dpi / 96.0 : 1.0;
            list.Add(new ScreenInfo(ToRect(mi.rcMonitor), ToRect(mi.rcWork), scale,
                (mi.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0));
        }
        catch { /* se ignora ese monitor */ }
        return 1; // seguir enumerando
    }

    private static ScreenRect ToRect(RECT r) => new(r.Left, r.Top, r.Width, r.Height);

    // ------------------------------------------------------------- tema
    public ThemeInfo GetTheme()
    {
        var hc = new Win32.HIGHCONTRASTW { cbSize = (uint)sizeof(Win32.HIGHCONTRASTW) };
        bool highContrast = Win32.SystemParametersInfoW(Win32.SPI_GETHIGHCONTRAST, hc.cbSize, &hc, 0)
                            && (hc.dwFlags & Win32.HCF_HIGHCONTRASTON) != 0;

        string family = "Segoe UI";
        double size = 12;
        var ncm = new Win32.NONCLIENTMETRICSW { cbSize = (uint)sizeof(Win32.NONCLIENTMETRICSW) };
        // A 96 ppp, la altura de la fuente en pixeles equivale a pixeles CSS.
        if (Win32.SystemParametersInfoForDpi(Win32.SPI_GETNONCLIENTMETRICS, ncm.cbSize, &ncm, 0, 96))
        {
            var face = new string(ncm.lfMessageFont.lfFaceName);
            int nul = face.IndexOf('\0');
            if (nul >= 0) face = face[..nul];
            if (face.Length > 0) family = face;
            if (ncm.lfMessageFont.lfHeight != 0) size = Math.Abs(ncm.lfMessageFont.lfHeight);
        }
        // Windows 11 usa la variante "Variable" de Segoe UI en sus aplicaciones.
        if (family == "Segoe UI" && Environment.OSVersion.Version.Build >= 22000) family = "Segoe UI Variable Text";

        return new ThemeInfo(WinPlatform.ReadDarkMode(), ReadAccent(), family, size, highContrast);
    }

    private static string ReadAccent()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (k?.GetValue("AccentColor") is int abgr)
            {
                uint v = unchecked((uint)abgr);
                return $"#{v & 0xFF:X2}{(v >> 8) & 0xFF:X2}{(v >> 16) & 0xFF:X2}";
            }
        }
        catch { /* sin acceso al registro: valor por defecto */ }
        return "#0067C0";
    }

    // ------------------------------------------------------------- shell
    public void Open(string path) => Win32.ShellExecuteW(0, null, path, null, null, Win32.SW_SHOWNORMAL);

    public void Reveal(string path)
    {
        var pidl = Win32.ILCreateFromPathW(path);
        if (pidl == 0) throw new FileNotFoundException("No existe: " + path, path);
        try { Marshal.ThrowExceptionForHR(Win32.SHOpenFolderAndSelectItems(pidl, 0, 0, 0)); }
        finally { Win32.ILFree(pidl); }
    }
}
