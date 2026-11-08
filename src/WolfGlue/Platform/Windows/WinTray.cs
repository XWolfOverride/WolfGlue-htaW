using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using WolfGlue.Native;

namespace WolfGlue.Platform.Windows;

/// <summary>
/// Icono del area de notificacion (<c>Shell_NotifyIconW</c>). Los avisos del icono llegan a la ventana de mensajes de
/// la plataforma (<see cref="WinPlatform.MessageWindow"/>) como <see cref="WM_TRAY"/>. El menu se pinta con un menu
/// emergente nativo; el clic izquierdo hace la opcion <c>default</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe partial class WinTray
{
    internal const uint WM_TRAY = Win32.WM_APP + 2;
    private const uint IconId = 1;
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
    private const uint WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203, WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B;
    private const uint TPM_RIGHTBUTTON = 0x2, TPM_BOTTOMALIGN = 0x20;
    private const int SM_CXSMICON = 49;

    private readonly WinPlatform _platform;
    private GlueTray? _tray;
    private nint _icon;
    private bool _added;
    private long _lastDefault;

    /// <summary>Mensaje que el explorador envia cuando se reinicia (hay que volver a poner el icono).</summary>
    internal static readonly uint TaskbarCreated = RegisterWindowMessageW("TaskbarCreated");

    public WinTray(WinPlatform platform) => _platform = platform;

    public void Set(GlueTray? tray)
    {
        _tray = tray;
        if (tray is null) { Remove(); return; }
        Apply();
    }

    /// <summary>Vuelve a poner el icono (explorador reiniciado o cambio del tema de la barra de tareas).</summary>
    public void Refresh()
    {
        if (_tray is null) return;
        _added = false;
        Apply();
    }

    private void Apply()
    {
        var tray = _tray!;
        var newIcon = BuildIcon(tray);
        var data = Data();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        data.uCallbackMessage = WM_TRAY;
        data.hIcon = newIcon;
        var tip = tray.Tooltip ?? "";
        if (tip.Length > 127) tip = tip[..127];
        for (int i = 0; i < tip.Length; i++) data.szTip[i] = tip[i];
        if (!Shell_NotifyIconW(_added ? NIM_MODIFY : NIM_ADD, &data) && _added)
            Shell_NotifyIconW(NIM_ADD, &data); // el icono habia desaparecido
        _added = true;
        if (_icon != 0) WinIcon.DestroyIcon(_icon);
        _icon = newIcon;
    }

    private void Remove()
    {
        if (_added)
        {
            var data = Data();
            Shell_NotifyIconW(NIM_DELETE, &data);
            _added = false;
        }
        if (_icon != 0) { WinIcon.DestroyIcon(_icon); _icon = 0; }
    }

    private NOTIFYICONDATAW Data()
    {
        var d = new NOTIFYICONDATAW { hWnd = _platform.MessageWindow, uID = IconId };
        d.cbSize = (uint)sizeof(NOTIFYICONDATAW);
        return d;
    }

    // ------------------------------------------------------------- icono
    /// <summary>true si la barra de tareas es oscura (el icono monocromo se pinta de blanco).</summary>
    internal static bool TaskbarIsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("SystemUsesLightTheme") is not int v || v == 0;
        }
        catch { return true; }
    }

    private nint BuildIcon(GlueTray tray)
    {
        int size = GetSystemMetrics(SM_CXSMICON);
        if (size <= 0) size = 16;
        var data = tray.Icon ?? _platform.App.Options.Icon;
        nint icon = 0;
        if (data is not null)
        {
            bool tint = !tray.Color && tray.Icon is not null;
            if (tint)
            {
                Gdip.Start();
                var source = Gdip.Load(data);
                if (source != 0)
                {
                    // Una silueta del color que contrasta con la barra de tareas (blanco sobre oscuro, casi negro sobre claro).
                    var bmp = Gdip.Scaled(source, size, size, TaskbarIsDark() ? 0xFFFFFFu : 0x202020u);
                    if (bmp != 0) { icon = Gdip.ToHIcon(bmp); Gdip.Dispose(bmp); }
                    Gdip.Dispose(source);
                }
            }
            else icon = WinIcon.Create(data, size);
        }
        if (icon == 0) icon = Win32.LoadIconW(_platform.HInstance, 32512);
        if (icon == 0) icon = Win32.LoadIconW(0, 32512);
        // El de la clase / sistema es compartido: se copia para poder destruirlo igual que los demas.
        return CopyIcon(icon);
    }

    // ------------------------------------------------------------- avisos del icono
    /// <summary>Mensaje <see cref="WM_TRAY"/> recibido: <paramref name="mouse"/> es el mensaje de raton original.</summary>
    public void OnMessage(nint mouse)
    {
        switch ((uint)mouse)
        {
            case WM_LBUTTONUP:
                RunDefault();
                break;
            case WM_LBUTTONDBLCLK:
                break; // el segundo clic ya hizo lo suyo
            case WM_RBUTTONUP:
            case WM_CONTEXTMENU:
                ShowMenu();
                break;
        }
    }

    private void RunDefault()
    {
        var item = _tray?.Menu.FirstOrDefault(m => m.Default && m.Enabled);
        if (item is null) return;
        // Un doble clic produce dos subidas del boton: solo cuenta la primera.
        long now = Environment.TickCount64;
        if (now - _lastDefault < GetDoubleClickTime()) return;
        _lastDefault = now;
        _platform.App.Native.Tray.Raise(item.Id);
    }

    private void ShowMenu()
    {
        if (_tray is not { } tray) return;
        var h = Win32.CreatePopupMenu();
        var ids = new List<string>();
        try
        {
            uint position = 0;
            foreach (var m in tray.Menu)
            {
                if (m.IsSeparator) { Win32.AppendMenuW(h, Win32.MF_SEPARATOR, 0, null); position++; continue; }
                ids.Add(m.Id);
                Win32.AppendMenuW(h, Win32.MF_STRING | (m.Enabled ? 0 : Win32.MF_GRAYED) | (m.Checked ? Win32.MF_CHECKED : 0),
                    ids.Count, m.Text);
                if (m.Default) SetMenuDefaultItem(h, position, 1);
                position++;
            }
            GetCursorPos(out var p);
            // Sin ponerse al frente, el menu no se cierra al hacer clic fuera (comportamiento documentado de la bandeja).
            Win32.SetForegroundWindow(_platform.MessageWindow);
            int cmd = Win32.TrackPopupMenu(h, Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN,
                p.X, p.Y, 0, _platform.MessageWindow, 0);
            Win32.PostMessageW(_platform.MessageWindow, 0 /* WM_NULL */, 0, 0);
            if (cmd > 0 && cmd <= ids.Count) _platform.App.Native.Tray.Raise(ids[cmd - 1]);
        }
        finally { Win32.DestroyMenu(h); }
    }

    // ------------------------------------------------------------- P/Invoke
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID, uFlags, uCallbackMessage;
        public nint hIcon;
        public fixed char szTip[128];
        public uint dwState, dwStateMask;
        public fixed char szInfo[256];
        public uint uTimeoutOrVersion;
        public fixed char szInfoTitle[64];
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Shell_NotifyIconW(uint message, NOTIFYICONDATAW* data);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessageW(string name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetMenuDefaultItem(nint menu, uint item, uint byPosition);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Win32.POINT p);

    [LibraryImport("user32.dll")]
    private static partial nint CopyIcon(nint icon);

    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();
}
