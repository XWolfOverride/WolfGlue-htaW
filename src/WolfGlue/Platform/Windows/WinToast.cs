using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using WolfGlue.Native;

namespace WolfGlue.Platform.Windows;

/// <summary>
/// Notificaciones «toast» de Windows 10/11 usando las clases de WinRT por COM, sin proyeccion de WinRT (Native AOT):
/// se piden las fabricas con <c>RoGetActivationFactory</c> y se llaman por su tabla virtual (las posiciones son las del
/// orden de los metodos en cada interfaz, tras los 6 de IUnknown + IInspectable). El AppUserModelID se registra en
/// <c>HKCU\Software\Classes\AppUserModelId</c> con nombre e icono; las notificaciones se retiran al cerrar la app.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe partial class WinToast
{
    // IIDs de Windows.UI.Notifications / Windows.Data.Xml.Dom.
    private static readonly Guid IidManagerStatics = new("50ac103f-d235-4598-bbef-98fe4d1a3ad4");
    private static readonly Guid IidToastFactory = new("04124b20-82c6-4229-b109-fd9ed4662b53");
    private static readonly Guid IidToast2 = new("9dfb9fd1-143a-490e-90bf-b9fba7132de7");
    private static readonly Guid IidXmlDocumentIO = new("6cd0e74e-ee65-4489-9ebf-ca43e87ba637");
    private static readonly Guid IidXmlDocument = new("f7f3a506-1e87-42d6-bcfb-b8c809fa5494");
    private static readonly Guid IidActivatedArgs = new("e3bf92f3-c197-436f-8265-0625824f8dac");
    private static readonly Guid IidUnknown = new("00000000-0000-0000-c000-000000000046");
    private static readonly Guid IidAgile = new("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90");

    /// <summary>IID de <c>TypedEventHandler&lt;ToastNotification, IInspectable&gt;</c>.</summary>
    internal static readonly Guid IidActivatedHandler = PInterfaceId(
        "{9de1c534-6ae1-11e0-84e1-18a905bcc53f}",
        "rc(Windows.UI.Notifications.ToastNotification;{997e2675-059e-4e60-8b06-1760917c8b80})",
        "cinterface(IInspectable)");

    private const string Group = "WolfGlue";
    private const int MaxTag = 64;

    private static WinToast? s_current;

    private readonly GlueApp _app;
    private readonly string _aumid;
    private nint _notifier;
    private nint _factory;
    private nint _handler;
    private bool _failed;
    private readonly Dictionary<string, nint> _shown = [];

    public WinToast(GlueApp app)
    {
        _app = app;
        _aumid = app.Options.AppUserModelId ?? app.Options.AppId;
    }

    // ------------------------------------------------------------- API
    public void Show(string id, GlueNotification n)
    {
        EnsureInit();
        var xml = BuildXml(id, n);
        nint doc = 0, io = 0, xmlDoc = 0, toast = 0, toast2 = 0;
        try
        {
            Check(Activate("Windows.Data.Xml.Dom.XmlDocument", out doc));
            Check(QueryInterface(doc, IidXmlDocumentIO, out io));
            Check(CallHs(io, 6, xml));
            Check(QueryInterface(doc, IidXmlDocument, out xmlDoc));
            Check(CallOut(_factory, 6, xmlDoc, out toast));
            if (QueryInterface(toast, IidToast2, out toast2) >= 0)
            {
                CallHs(toast2, 6, Tag(id)); // put_Tag
                CallHs(toast2, 8, Group); // put_Group
            }
            Check(CallOut(toast, 11, _handler, out long _)); // add_Activated
            if (_shown.Remove(id, out var old)) Release(old);
            Check(Call(_notifier, 6, toast)); // Show (si hay otra con el mismo tag y grupo, la sustituye)
            _shown[id] = toast;
            toast = 0;
        }
        finally
        {
            Release(toast2);
            Release(toast);
            Release(xmlDoc);
            Release(io);
            Release(doc);
        }
    }

    public void Close(string id)
    {
        if (_notifier == 0 || !_shown.Remove(id, out var toast)) return;
        Call(_notifier, 7, toast); // Hide
        Release(toast);
    }

    public void CloseAll()
    {
        if (_notifier == 0) return;
        foreach (var t in _shown.Values)
        {
            Call(_notifier, 7, t);
            Release(t);
        }
        _shown.Clear();
    }

    public NotificationPermission Permission()
    {
        try
        {
            EnsureInit();
            // get_Setting: 0 = Enabled; el resto, desactivadas (por la app, el usuario, directiva o manifiesto).
            return CallOut(_notifier, 8, out int setting) >= 0 && setting == 0 ? NotificationPermission.Granted : NotificationPermission.Denied;
        }
        catch (Exception e) when (e is COMException or InvalidOperationException) { return NotificationPermission.Denied; }
    }

    // ------------------------------------------------------------- arranque
    private void EnsureInit()
    {
        if (_failed) throw new InvalidOperationException("Las notificaciones no estan disponibles en este sistema.");
        if (_notifier != 0) return;
        try
        {
            RegisterAppId();
            nint statics = 0;
            try
            {
                Check(GetFactory("Windows.UI.Notifications.ToastNotificationManager", IidManagerStatics, out statics));
                Check(CallOutHs(statics, 7, _aumid, out _notifier)); // CreateToastNotifierWithId
            }
            finally { Release(statics); }
            Check(GetFactory("Windows.UI.Notifications.ToastNotification", IidToastFactory, out _factory));
            _handler = CreateHandler(this);
            s_current = this;
        }
        catch
        {
            _failed = true;
            throw;
        }
    }

    /// <summary>Nombre e icono de la app para el sistema (sin esto, los toasts de una app sin instalar no salen).</summary>
    private void RegisterAppId()
    {
        using var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + _aumid);
        k.SetValue("DisplayName", _app.Options.AppName ?? _app.Options.AppId);
        if (_app.Options.Icon is { } icon && icon.Length > 4 && icon[0] == 0x89 && icon[1] == 'P')
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WolfGlue", "toast");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(_aumid)))[..16] + ".png");
                if (!File.Exists(file) || new FileInfo(file).Length != icon.Length) File.WriteAllBytes(file, icon);
                k.SetValue("IconUri", file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* sin icono */ }
        }
    }

    // ------------------------------------------------------------- XML
    internal static string BuildXml(string id, GlueNotification n)
    {
        var sb = new StringBuilder();
        sb.Append("<toast launch=\"").Append(Esc(Args("c", id, null))).Append("\" activationType=\"foreground\"><visual><binding template=\"ToastGeneric\"><text>")
          .Append(Esc(n.Title)).Append("</text>");
        if (!string.IsNullOrEmpty(n.Body)) sb.Append("<text>").Append(Esc(n.Body)).Append("</text>");
        sb.Append("</binding></visual>");
        if (n.Buttons is { Length: > 0 })
        {
            sb.Append("<actions>");
            foreach (var b in n.Buttons)
                sb.Append("<action content=\"").Append(Esc(b.Text)).Append("\" arguments=\"").Append(Esc(Args("b", id, b.Id)))
                  .Append("\" activationType=\"foreground\"/>");
            sb.Append("</actions>");
        }
        if (n.Silent) sb.Append("<audio silent=\"true\"/>");
        sb.Append("</toast>");
        return sb.ToString();
    }

    private static string Esc(string s) => SecurityElement.Escape(s) ?? "";

    internal static string Args(string kind, string id, string? button) =>
        kind + "|" + Uri.EscapeDataString(id) + (button is null ? "" : "|" + Uri.EscapeDataString(button));

    /// <summary>Interpreta los argumentos de una activacion: id de la notificacion y, si fue un boton, el suyo.</summary>
    internal static bool TryParseArgs(string? args, out string id, out string? button)
    {
        id = ""; button = null;
        if (string.IsNullOrEmpty(args)) return false;
        var p = args.Split('|');
        if (p.Length == 2 && p[0] == "c") { id = Uri.UnescapeDataString(p[1]); return true; }
        if (p.Length == 3 && p[0] == "b") { id = Uri.UnescapeDataString(p[1]); button = Uri.UnescapeDataString(p[2]); return true; }
        return false;
    }

    private static string Tag(string id) => id.Length <= MaxTag ? id : id[..MaxTag];

    /// <summary>IID de una interfaz parametrizada de WinRT: UUID v5 (SHA-1) sobre su firma.</summary>
    internal static Guid PInterfaceId(string pinterface, params string[] args)
    {
        var sig = "pinterface(" + pinterface + ";" + string.Join(';', args) + ")";
        var ns = new Guid("11f47ad5-7b73-42c0-abae-878b1e16adee");
        Span<byte> nsBytes = stackalloc byte[16];
        ns.TryWriteBytes(nsBytes, bigEndian: true, out _);
        var data = new byte[16 + Encoding.UTF8.GetByteCount(sig)];
        nsBytes.CopyTo(data);
        Encoding.UTF8.GetBytes(sig, data.AsSpan(16));
        var hash = SHA1.HashData(data);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    // ------------------------------------------------------------- manejador del evento Activated (objeto COM nativo)
    // Estructura: [puntero a la tabla][contador]. La tabla (QueryInterface, AddRef, Release, Invoke) es comun.
    private static nint* s_vtbl;

    private static nint CreateHandler(WinToast owner)
    {
        if (s_vtbl == null)
        {
            var v = (nint*)NativeMemory.Alloc((nuint)(4 * sizeof(nint)));
            v[0] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)&HandlerQueryInterface;
            v[1] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&HandlerAddRef;
            v[2] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&HandlerRelease;
            v[3] = (nint)(delegate* unmanaged[Stdcall]<nint, nint, nint, int>)&HandlerInvoke;
            s_vtbl = v;
        }
        var obj = (nint*)NativeMemory.Alloc((nuint)(2 * sizeof(nint)));
        obj[0] = (nint)s_vtbl;
        obj[1] = 1;
        return (nint)obj;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int HandlerQueryInterface(nint self, Guid* iid, nint* result)
    {
        if (*iid == IidUnknown || *iid == IidAgile || *iid == IidActivatedHandler)
        {
            *result = self;
            Interlocked.Increment(ref ((int*)((nint*)self + 1))[0]);
            return 0;
        }
        *result = 0;
        return unchecked((int)0x80004002); // E_NOINTERFACE
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint HandlerAddRef(nint self) => (uint)Interlocked.Increment(ref ((int*)((nint*)self + 1))[0]);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint HandlerRelease(nint self) => (uint)Interlocked.Decrement(ref ((int*)((nint*)self + 1))[0]);

    // Se llama en un hilo del sistema (el objeto es agil): se lee el argumento y se pasa al hilo de UI.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int HandlerInvoke(nint self, nint sender, nint args)
    {
        try
        {
            if (s_current is { } t && args != 0 && QueryInterface(args, IidActivatedArgs, out var a) >= 0)
            {
                try
                {
                    if (CallOut(a, 6, out nint hs) >= 0) // get_Arguments
                    {
                        var text = ReadAndFree(hs);
                        if (TryParseArgs(text, out var id, out var button)) t._app.Native.Notifications.Raise(id, button);
                    }
                }
                finally { Release(a); }
            }
        }
        catch (Exception) { /* un error en el aviso no debe tumbar el proceso */ }
        return 0;
    }

    // ------------------------------------------------------------- llamadas por la tabla virtual
    private static nint Slot(nint obj, int index) => (*(nint**)obj)[index];

    private static int Call(nint obj, int slot, nint a) =>
        ((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(obj, slot))(obj, a);

    private static int CallOut(nint obj, int slot, out nint result)
    {
        nint r = 0;
        int hr = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(obj, slot))(obj, &r);
        result = r;
        return hr;
    }

    private static int CallOut(nint obj, int slot, out int result)
    {
        int r = 0;
        int hr = ((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(obj, slot))(obj, &r);
        result = r;
        return hr;
    }

    private static int CallOut(nint obj, int slot, nint a, out nint result)
    {
        nint r = 0;
        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Slot(obj, slot))(obj, a, &r);
        result = r;
        return hr;
    }

    private static int CallOut(nint obj, int slot, nint a, out long result)
    {
        long r = 0;
        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, long*, int>)Slot(obj, slot))(obj, a, &r);
        result = r;
        return hr;
    }

    private static int QueryInterface(nint obj, Guid iid, out nint result)
    {
        nint r = 0;
        int hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(obj, 0))(obj, &iid, &r);
        result = r;
        return hr;
    }

    private static void Release(nint obj)
    {
        if (obj != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(obj, 2))(obj);
    }

    private static void Check(int hr)
    {
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }

    // ------------------------------------------------------------- HSTRING
    /// <summary>Crea un HSTRING (queda en una cola que se libera al terminar la llamada que lo usa: son cortos y pocos).</summary>
    private static nint Hs(string s)
    {
        nint h;
        fixed (char* p = s) Check(WindowsCreateString(p, (uint)s.Length, out h));
        return h;
    }

    private static string ReadAndFree(nint hs)
    {
        if (hs == 0) return "";
        uint len;
        var p = WindowsGetStringRawBuffer(hs, &len);
        var s = new string(p, 0, (int)len);
        WindowsDeleteString(hs);
        return s;
    }

    private static int CallHs(nint obj, int slot, string s)
    {
        var h = Hs(s);
        try { return Call(obj, slot, h); }
        finally { WindowsDeleteString(h); }
    }

    private static int CallOutHs(nint obj, int slot, string s, out nint result)
    {
        var h = Hs(s);
        try { return CallOut(obj, slot, h, out result); }
        finally { WindowsDeleteString(h); }
    }

    private static int Activate(string className, out nint instance)
    {
        var h = Hs(className);
        try { return RoActivateInstance(h, out instance); }
        finally { WindowsDeleteString(h); }
    }

    private static int GetFactory(string className, Guid iid, out nint factory)
    {
        var h = Hs(className);
        try
        {
            nint f = 0;
            int hr = RoGetActivationFactory(h, &iid, &f);
            factory = f;
            return hr;
        }
        finally { WindowsDeleteString(h); }
    }

    [LibraryImport("combase.dll")]
    private static partial int WindowsCreateString(char* source, uint length, out nint hstring);

    [LibraryImport("combase.dll")]
    private static partial int WindowsDeleteString(nint hstring);

    [LibraryImport("combase.dll")]
    private static partial char* WindowsGetStringRawBuffer(nint hstring, uint* length);

    [LibraryImport("combase.dll")]
    private static partial int RoGetActivationFactory(nint classId, Guid* iid, nint* factory);

    [LibraryImport("combase.dll")]
    private static partial int RoActivateInstance(nint classId, out nint instance);
}
