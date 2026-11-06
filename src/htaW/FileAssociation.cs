using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace HtaW;

/// <summary>Estado de la asociacion: sin asociar, a este htaw, a otra copia de htaw, a otro programa.</summary>
public enum AssociationState { None, This, Other, Foreign, Unsupported }

/// <summary>Asociacion de la extension .htaw, siempre por usuario (sin administrador).</summary>
internal static partial class FileAssociation
{
    private const string ProgId = "htaW.App";

    public static bool Register(string exe)
    {
        if (!OperatingSystem.IsWindows()) return false;
        RegisterWindows(exe);
        return true;
    }

    public static bool Unregister()
    {
        if (!OperatingSystem.IsWindows()) return false;
        UnregisterWindows();
        return true;
    }

    /// <summary>Estado de la asociacion de .htaw para este usuario.</summary>
    public static (AssociationState state, string? exe) Status(string exe)
    {
        if (!OperatingSystem.IsWindows()) return (AssociationState.Unsupported, null);
        return StatusWindows(exe);
    }

    [SupportedOSPlatform("windows")]
    private static (AssociationState, string?) StatusWindows(string exe)
    {
        using var ext = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.htaw");
        var prog = ext?.GetValue("") as string;
        if (prog is null) return (AssociationState.None, null);
        if (prog != ProgId) return (AssociationState.Foreign, prog);
        using var cmd = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ProgId + @"\shell\open\command");
        var target = CommandExe(cmd?.GetValue("") as string);
        if (target is null) return (AssociationState.None, null);
        return string.Equals(Path.GetFullPath(target), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase)
            ? (AssociationState.This, target) : (AssociationState.Other, target);
    }

    /// <summary>Ejecutable de una orden <c>"ruta" "%1" %*</c> (o sin comillas).</summary>
    internal static string? CommandExe(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = command.Trim();
        if (command[0] == '"')
        {
            var end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }
        var sp = command.IndexOf(' ');
        return sp < 0 ? command : command[..sp];
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows(string exe)
    {
        using (var ext = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.htaw"))
        {
            ext.SetValue("", ProgId);
            ext.SetValue("Content Type", "application/x-htaw");
        }
        using (var prog = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
        {
            prog.SetValue("", Strings.ChooseFilter);
            using (var icon = prog.CreateSubKey("DefaultIcon")) icon.SetValue("", $"\"{exe}\",0");
            using (var cmd = prog.CreateSubKey(@"shell\open\command")) cmd.SetValue("", $"\"{exe}\" \"%1\" %*");
        }
        NotifyShell();
    }

    [SupportedOSPlatform("windows")]
    private static void UnregisterWindows()
    {
        using (var ext = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.htaw", writable: true))
        {
            // Solo se quita si apunta a htaW (otra aplicacion podria haberla tomado).
            if (ext?.GetValue("") as string == ProgId) Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.htaw", false);
        }
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ProgId, false);
        NotifyShell();
    }

    private static void NotifyShell() => SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0, 0, 0);

    [LibraryImport("shell32.dll")]
    private static partial void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);
}
