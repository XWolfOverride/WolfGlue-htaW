using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using WolfGlue.Native;

namespace WolfGlue.Platform.Windows;

/// <summary>Menu contextual nativo (<c>TrackPopupMenu</c>) a partir del modelo comun.</summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class WinMenu
{
    private const uint MF_POPUP = 0x10, TPM_RIGHTBUTTON = 0x2;

    /// <summary>Muestra el menu y devuelve el id elegido (null si se cierra sin elegir).</summary>
    public static string? Popup(WinWindow? owner, nint fallbackOwner, IReadOnlyList<GlueMenuItem> items, (double X, double Y)? position)
    {
        var ids = new List<string>();
        var root = Win32.CreatePopupMenu();
        try
        {
            Fill(root, items, ids);
            nint hwnd = owner?.Hwnd ?? fallbackOwner;
            Win32.POINT p;
            if (position is { } pos && owner is not null)
            {
                double scale = owner.Dpi / 96.0;
                p = new Win32.POINT { X = (int)Math.Round(pos.X * scale), Y = (int)Math.Round(pos.Y * scale) + owner.ContentTop };
                Win32.ClientToScreen(owner.Hwnd, ref p);
            }
            else GetCursorPos(out p);
            // Sin ponerse al frente, el menu no se cierra al hacer clic fuera.
            Win32.SetForegroundWindow(hwnd);
            int cmd = Win32.TrackPopupMenu(root, Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY | TPM_RIGHTBUTTON, p.X, p.Y, 0, hwnd, 0);
            Win32.PostMessageW(hwnd, 0 /* WM_NULL */, 0, 0);
            return cmd > 0 && cmd <= ids.Count ? ids[cmd - 1] : null;
        }
        finally { Win32.DestroyMenu(root); } // destruye tambien los submenus
    }

    /// <summary>
    /// Lo que la barra de menus de la ventana necesita saber mientras un menu esta abierto (el bucle del menu es modal, asi que se
    /// le avisa desde un gancho de mensajes): el raton pasa por otra entrada, o se pulsan las flechas izquierda y derecha.
    /// </summary>
    internal interface IBarHook
    {
        /// <summary>Indice de la entrada de la barra bajo el punto de pantalla, o -1.</summary>
        int EntryAt(Win32.POINT screen);
        /// <summary>Pide cambiar a otra entrada (-1 = anterior, -2 = siguiente, >= 0 = esa); el menu actual se cierra.</summary>
        void SwitchTo(int entry, bool relative);
        int Current { get; }
    }

    private static IBarHook? s_hook;
    private static nint s_root, s_hookHandle;
    private static nint s_lastSelectMenu;
    private static bool s_lastSelectIsPopup;
    private static Win32.POINT s_lastPt;

    /// <summary>Se llama desde <c>WM_MENUSELECT</c> de la ventana duena: que menu y opcion estan resaltados.</summary>
    internal static void OnMenuSelect(nint wParam, nint lParam)
    {
        s_lastSelectMenu = lParam;
        s_lastSelectIsPopup = ((uint)wParam >> 16 & 0x10) != 0; // MF_POPUP
    }

    /// <summary>Menu en un punto de pantalla (fisico), p. ej. bajo una entrada del menu de ventana. Con <paramref name="hook"/>, la barra puede cambiar de menu.</summary>
    internal static unsafe string? PopupAt(nint hwnd, Win32.POINT screen, IReadOnlyList<GlueMenuItem> items, IBarHook? hook = null)
    {
        var ids = new List<string>();
        var root = Win32.CreatePopupMenu();
        try
        {
            Fill(root, items, ids);
            Win32.SetForegroundWindow(hwnd);
            s_hook = hook;
            s_root = root;
            s_lastSelectMenu = 0;
            s_lastSelectIsPopup = false;
            GetCursorPos(out s_lastPt);
            if (hook is not null) s_hookHandle = Win32.SetWindowsHookEx(Win32.WH_MSGFILTER, &MenuFilter, 0, Win32.GetCurrentThreadId());
            // Sin TPM_NONOTIFY: la ventana recibe WM_MENUSELECT, que dice si lo resaltado es un submenu.
            int cmd = Win32.TrackPopupMenu(root, Win32.TPM_RETURNCMD, screen.X, screen.Y, 0, hwnd, 0);
            Win32.PostMessageW(hwnd, 0 /* WM_NULL */, 0, 0);
            return cmd > 0 && cmd <= ids.Count ? ids[cmd - 1] : null;
        }
        finally
        {
            if (s_hookHandle != 0) { Win32.UnhookWindowsHookEx(s_hookHandle); s_hookHandle = 0; }
            s_hook = null;
            s_root = 0;
            Win32.DestroyMenu(root);
        }
    }

    [UnmanagedCallersOnly]
    private static nint MenuFilter(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code == Win32.MSGF_MENU && s_hook is { } hook && lParam != 0)
            {
                var msg = *(Win32.MSG*)lParam;
                // Como el menu del sistema: solo cuenta un movimiento real del raton. El sistema repite WM_MOUSEMOVE con el raton
                // quieto tras cambiar de menu con las flechas, y eso devolvia el menu al que esta bajo el puntero.
                if (msg.message == Win32.WM_MOUSEMOVE && (msg.pt.X != s_lastPt.X || msg.pt.Y != s_lastPt.Y))
                {
                    s_lastPt = msg.pt;
                    int entry = hook.EntryAt(msg.pt);
                    if (entry >= 0 && entry != hook.Current) { hook.SwitchTo(entry, false); Win32.EndMenu(); }
                }
                else if (msg.message == Win32.WM_KEYDOWN && (msg.wParam == 0x25 || msg.wParam == 0x27))
                {
                    // Izquierda/derecha: en el menu de primer nivel cambian de entrada (la derecha solo si no abre un submenu);
                    // dentro de un submenu el sistema los usa para cerrarlo o abrirlo.
                    bool top = s_lastSelectMenu == 0 || s_lastSelectMenu == s_root;
                    if (top && (msg.wParam == 0x25 || !s_lastSelectIsPopup))
                    {
                        hook.SwitchTo(msg.wParam == 0x25 ? -1 : -2, true);
                        Win32.EndMenu();
                    }
                }
            }
        }
        catch (Exception) { /* un fallo del gancho no debe tumbar el proceso */ }
        return Win32.CallNextHookEx(s_hookHandle, code, wParam, lParam);
    }

    private static void Fill(nint menu, IReadOnlyList<GlueMenuItem> items, List<string> ids)
    {
        foreach (var m in items)
        {
            if (m.IsSeparator) { Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, 0, null); continue; }
            var text = m.Shortcut is { Length: > 0 } s ? m.Text + "\t" + s : m.Text;
            uint flags = Win32.MF_STRING | (m.Enabled ? 0 : Win32.MF_GRAYED) | (m.Checked ? Win32.MF_CHECKED : 0);
            if (m.Items is { Count: > 0 } sub)
            {
                var h = Win32.CreatePopupMenu();
                Fill(h, sub, ids);
                Win32.AppendMenuW(menu, flags | MF_POPUP, h, text);
            }
            else
            {
                ids.Add(m.Id ?? "");
                Win32.AppendMenuW(menu, flags, ids.Count, text);
            }
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Win32.POINT p);
}

/// <summary>
/// Hace que los menus emergentes nativos (contextuales, de la bandeja y de la barra de titulo) sigan el tema oscuro o claro
/// de la app. Usa las funciones de uxtheme por ordinal (135 <c>SetPreferredAppMode</c>, 136 <c>FlushMenuThemes</c>), no
/// documentadas pero estables desde Windows 10 1903; si no estan, los menus quedan como siempre.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class WinDarkMenus
{
    public static void Apply(bool dark)
    {
        try
        {
            var lib = LoadLibraryW("uxtheme.dll");
            if (lib == 0) return;
            var setMode = GetProcAddress(lib, 135);
            var flush = GetProcAddress(lib, 136);
            if (setMode == 0) return;
            // 2 = ForceDark, 3 = ForceLight
            ((delegate* unmanaged[Stdcall]<int, int>)setMode)(dark ? 2 : 3);
            if (flush != 0) ((delegate* unmanaged[Stdcall]<void>)flush)();
        }
        catch (Exception) { /* sin esto, los menus se ven en claro */ }
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibraryW(string name);

    [LibraryImport("kernel32.dll", EntryPoint = "GetProcAddress")]
    private static partial nint GetProcAddress(nint module, nint ordinal);
}
