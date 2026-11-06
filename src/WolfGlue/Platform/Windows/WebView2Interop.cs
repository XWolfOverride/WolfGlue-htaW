// Interfaces COM de WebView2 declaradas a mano para Native AOT ([GeneratedComInterface]).
// El orden de los metodos reproduce la vtable de WebView2.h (SDK Microsoft.Web.WebView2). Los metodos que no se
// usan se declaran como "_nn" solo para ocupar su hueco: NO cambiar el orden ni borrar huecos.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

#pragma warning disable IDE1006 // nombres de los huecos

namespace WolfGlue.Platform.Windows;

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left, Top, Right, Bottom;
    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential)]
internal struct COREWEBVIEW2_COLOR
{
    public byte A, R, G, B;
}

internal static class WebView2Const
{
    public const int WEB_RESOURCE_CONTEXT_ALL = 0;
    public const int MOVE_FOCUS_REASON_PROGRAMMATIC = 0;
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("b96d755e-0319-4e92-a296-23436f46a1fc")]
internal partial interface ICoreWebView2Environment
{
    void CreateCoreWebView2Controller(nint parentWindow, ICoreWebView2CreateCoreWebView2ControllerCompletedHandler handler);
    ICoreWebView2WebResourceResponse CreateWebResourceResponse(nint content, int statusCode, string reasonPhrase, string headers);
    string get_BrowserVersionString();
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("4d00c0d1-9434-4eb6-8078-8697a560334f")]
internal partial interface ICoreWebView2Controller
{
    void _00(); // get_IsVisible
    void put_IsVisible([MarshalAs(UnmanagedType.Bool)] bool value);
    void _02(); // get_Bounds
    void put_Bounds(RECT bounds);
    void _04(); void _05(); void _06(); void _07(); void _08();
    void MoveFocus(int reason);
    void _10(); void _11(); void _12(); void _13(); void _14(); void _15(); void _16(); void _17();
    void _18(); void _19(); // get/put_ParentWindow
    void NotifyParentWindowPositionChanged();
    void Close();
    ICoreWebView2 get_CoreWebView2();
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("c979903e-d4ca-4228-92eb-47ee3fa96eab")]
internal partial interface ICoreWebView2Controller2 : ICoreWebView2Controller
{
    COREWEBVIEW2_COLOR get_DefaultBackgroundColor();
    void put_DefaultBackgroundColor(COREWEBVIEW2_COLOR value);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("76eceacb-0462-4d94-ac83-423a6793775e")]
internal partial interface ICoreWebView2
{
    ICoreWebView2Settings get_Settings();                                                    // 0
    string get_Source();                                                                     // 1
    void Navigate(string uri);                                                               // 2
    void NavigateToString(string html);                                                      // 3
    long add_NavigationStarting(ICoreWebView2NavigationStartingEventHandler handler);       // 4
    void _05(); void _06(); void _07(); void _08(); void _09(); void _10(); void _11();
    long add_NavigationCompleted(ICoreWebView2NavigationCompletedEventHandler handler);     // 12
    void _13(); void _14(); void _15(); void _16(); void _17(); void _18(); void _19();
    void _20(); void _21(); void _22(); void _23();
    void AddScriptToExecuteOnDocumentCreated(string javaScript,
        ICoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler? handler);          // 24
    void _25();
    void ExecuteScript(string javaScript, ICoreWebView2ExecuteScriptCompletedHandler? handler); // 26
    void _27();
    void Reload();                                                                           // 28
    void PostWebMessageAsJson(string json);                                                  // 29
    void PostWebMessageAsString(string message);                                             // 30
    long add_WebMessageReceived(ICoreWebView2WebMessageReceivedEventHandler handler);        // 31
    void _32(); void _33(); void _34(); void _35(); void _36(); void _37(); void _38(); void _39(); void _40();
    long add_NewWindowRequested(ICoreWebView2NewWindowRequestedEventHandler handler);        // 41
    void _42();
    long add_DocumentTitleChanged(ICoreWebView2DocumentTitleChangedEventHandler handler);    // 43
    void _44();
    string get_DocumentTitle();                                                              // 45
    void _46(); void _47();
    void OpenDevToolsWindow();                                                               // 48
    void _49(); void _50(); void _51();
    long add_WebResourceRequested(ICoreWebView2WebResourceRequestedEventHandler handler);    // 52
    void _53();
    void AddWebResourceRequestedFilter(string uri, int resourceContext);                     // 54
    void _55();
    long add_WindowCloseRequested(ICoreWebView2WindowCloseRequestedEventHandler handler);    // 56
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("e562e4f0-d7fa-43ac-8d71-c05150499f00")]
internal partial interface ICoreWebView2Settings
{
    void _00(); void _01(); void _02(); void _03(); void _04(); void _05(); void _06();
    void put_IsStatusBarEnabled([MarshalAs(UnmanagedType.Bool)] bool value);                 // 7
    void _08();
    void put_AreDevToolsEnabled([MarshalAs(UnmanagedType.Bool)] bool value);                 // 9
    void _10();
    void put_AreDefaultContextMenusEnabled([MarshalAs(UnmanagedType.Bool)] bool value);      // 11
    void _12(); void _13(); void _14();
    void put_IsZoomControlEnabled([MarshalAs(UnmanagedType.Bool)] bool value);               // 15
}

/// <summary>
/// Solo el miembro que se usa de Settings8, con la vtable aplanada: 18 de Settings + 2 (Settings2) + 2 (3) + 4 (4)
/// + 2 (5) + 2 (6) + 2 (7) = 32 huecos antes de IsReputationCheckingRequired.
/// </summary>
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("9e6b0e8f-86ad-4e81-8147-a9b5edb68650")]
internal partial interface ICoreWebView2Settings8
{
    void _00(); void _01(); void _02(); void _03(); void _04(); void _05(); void _06(); void _07();
    void _08(); void _09(); void _10(); void _11(); void _12(); void _13(); void _14(); void _15();
    void _16(); void _17(); void _18(); void _19(); void _20(); void _21(); void _22(); void _23();
    void _24(); void _25(); void _26(); void _27(); void _28(); void _29(); void _30(); void _31();
    void _32();
    void put_IsReputationCheckingRequired([MarshalAs(UnmanagedType.Bool)] bool value);      // 33
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0f99a40c-e962-4207-9e92-e3d542eff849")]
internal partial interface ICoreWebView2WebMessageReceivedEventArgs
{
    string get_Source();
    string get_WebMessageAsJson();
    [PreserveSig] int TryGetWebMessageAsString(out nint message);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("453e667f-12c7-49d4-be6d-ddbe7956f57a")]
internal partial interface ICoreWebView2WebResourceRequestedEventArgs
{
    ICoreWebView2WebResourceRequest get_Request();
    void _01();
    void put_Response(ICoreWebView2WebResourceResponse response);
    ICoreWebView2Deferral GetDeferral();
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("97055cd4-512c-4264-8b5f-e3f446cea6a5")]
internal partial interface ICoreWebView2WebResourceRequest
{
    string get_Uri();
    void _01();
    string get_Method();
    void _03();
    nint get_Content(); // IStream* (puede ser 0)
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("aafcc94f-fa27-48fd-97df-830ef75aaec9")]
internal partial interface ICoreWebView2WebResourceResponse { }

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("34acb11c-fc37-4418-9132-f9c21d1eafb9")]
internal partial interface ICoreWebView2NewWindowRequestedEventArgs
{
    string get_Uri();
    void _01(); void _02();
    void put_Handled([MarshalAs(UnmanagedType.Bool)] bool value);
    void _04();
    [return: MarshalAs(UnmanagedType.Bool)] bool get_IsUserInitiated();
    void _06();
    ICoreWebView2WindowFeatures get_WindowFeatures();
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("5eaf559f-b46e-4397-8860-e422f287ff1e")]
internal partial interface ICoreWebView2WindowFeatures
{
    void _00();
    [return: MarshalAs(UnmanagedType.Bool)] bool get_HasSize();
    void _02(); void _03();
    uint get_Height();
    uint get_Width();
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("5b495469-e119-438a-9b18-7604f25f2e49")]
internal partial interface ICoreWebView2NavigationStartingEventArgs
{
    string get_Uri();
    [return: MarshalAs(UnmanagedType.Bool)] bool get_IsUserInitiated();
    void _02(); void _03(); void _04();
    void put_Cancel([MarshalAs(UnmanagedType.Bool)] bool value);
}

[GeneratedComInterface]
[Guid("c10e7f7b-b585-46f0-a623-8befbf3e4ee0")]
internal partial interface ICoreWebView2Deferral
{
    void Complete();
}

// ------------------------------------------------------------------ handlers (implementados en C#)

[GeneratedComInterface]
[Guid("4e8a3389-c9d8-4bd2-b6b5-124fee6cc14d")]
internal partial interface ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler
{
    [PreserveSig] int Invoke(int errorCode, ICoreWebView2Environment? result);
}

[GeneratedComInterface]
[Guid("6c4819f3-c9b7-4260-8127-c9f5bde7f68c")]
internal partial interface ICoreWebView2CreateCoreWebView2ControllerCompletedHandler
{
    [PreserveSig] int Invoke(int errorCode, ICoreWebView2Controller? result);
}

[GeneratedComInterface]
[Guid("57213f19-00e6-49fa-8e07-898ea01ecbd2")]
internal partial interface ICoreWebView2WebMessageReceivedEventHandler
{
    [PreserveSig] int Invoke(nint sender, ICoreWebView2WebMessageReceivedEventArgs args);
}

[GeneratedComInterface]
[Guid("ab00b74c-15f1-4646-80e8-e76341d25d71")]
internal partial interface ICoreWebView2WebResourceRequestedEventHandler
{
    [PreserveSig] int Invoke(nint sender, ICoreWebView2WebResourceRequestedEventArgs args);
}

[GeneratedComInterface]
[Guid("d4c185fe-c81c-4989-97af-2d3fa7ab5651")]
internal partial interface ICoreWebView2NewWindowRequestedEventHandler
{
    [PreserveSig] int Invoke(nint sender, ICoreWebView2NewWindowRequestedEventArgs args);
}

[GeneratedComInterface]
[Guid("9adbe429-f36d-432b-9ddc-f8881fbd76e3")]
internal partial interface ICoreWebView2NavigationStartingEventHandler
{
    [PreserveSig] int Invoke(nint sender, ICoreWebView2NavigationStartingEventArgs args);
}

[GeneratedComInterface]
[Guid("d33a35bf-1c49-4f98-93ab-006e0533fe1c")]
internal partial interface ICoreWebView2NavigationCompletedEventHandler
{
    [PreserveSig] int Invoke(nint sender, nint args);
}

[GeneratedComClass]
internal sealed partial class NavigationCompletedHandler(Action cb) : ICoreWebView2NavigationCompletedEventHandler
{
    public int Invoke(nint sender, nint args) => Safe.Run(cb);
}

[GeneratedComInterface]
[Guid("b99369f3-9b11-47b5-bc6f-8e7895fcea17")]
internal partial interface ICoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler
{
    [PreserveSig] int Invoke(int errorCode, nint id);
}

[GeneratedComInterface]
[Guid("49511172-cc67-4bca-9923-137112f4c4cc")]
internal partial interface ICoreWebView2ExecuteScriptCompletedHandler
{
    [PreserveSig] int Invoke(int errorCode, nint resultObjectAsJson);
}

[GeneratedComInterface]
[Guid("f5f2b923-953e-4042-9f95-f3a118e1afd4")]
internal partial interface ICoreWebView2DocumentTitleChangedEventHandler
{
    [PreserveSig] int Invoke(nint sender, nint args);
}

[GeneratedComInterface]
[Guid("5c19e9e0-092f-486b-affa-ca8231913039")]
internal partial interface ICoreWebView2WindowCloseRequestedEventHandler
{
    [PreserveSig] int Invoke(nint sender, nint args);
}

// Implementaciones: cada una envuelve un delegado. Los errores no deben volver a WebView2 como excepcion.

[GeneratedComClass]
internal sealed partial class EnvironmentCompletedHandler(Action<int, ICoreWebView2Environment?> cb)
    : ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler
{
    public int Invoke(int errorCode, ICoreWebView2Environment? result) => Safe.Run(() => cb(errorCode, result));
}

[GeneratedComClass]
internal sealed partial class ControllerCompletedHandler(Action<int, ICoreWebView2Controller?> cb)
    : ICoreWebView2CreateCoreWebView2ControllerCompletedHandler
{
    public int Invoke(int errorCode, ICoreWebView2Controller? result) => Safe.Run(() => cb(errorCode, result));
}

[GeneratedComClass]
internal sealed partial class WebMessageReceivedHandler(Action<ICoreWebView2WebMessageReceivedEventArgs> cb)
    : ICoreWebView2WebMessageReceivedEventHandler
{
    public int Invoke(nint sender, ICoreWebView2WebMessageReceivedEventArgs args) => Safe.Run(() => cb(args));
}

[GeneratedComClass]
internal sealed partial class WebResourceRequestedHandler(Action<ICoreWebView2WebResourceRequestedEventArgs> cb)
    : ICoreWebView2WebResourceRequestedEventHandler
{
    public int Invoke(nint sender, ICoreWebView2WebResourceRequestedEventArgs args) => Safe.Run(() => cb(args));
}

[GeneratedComClass]
internal sealed partial class NewWindowRequestedHandler(Action<ICoreWebView2NewWindowRequestedEventArgs> cb)
    : ICoreWebView2NewWindowRequestedEventHandler
{
    public int Invoke(nint sender, ICoreWebView2NewWindowRequestedEventArgs args) => Safe.Run(() => cb(args));
}

[GeneratedComClass]
internal sealed partial class NavigationStartingHandler(Action<ICoreWebView2NavigationStartingEventArgs> cb)
    : ICoreWebView2NavigationStartingEventHandler
{
    public int Invoke(nint sender, ICoreWebView2NavigationStartingEventArgs args) => Safe.Run(() => cb(args));
}

[GeneratedComClass]
internal sealed partial class AddScriptCompletedHandler : ICoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler
{
    public int Invoke(int errorCode, nint id)
    {
        // id es [in] (LPCWSTR): pertenece a WebView2, no se libera.
        return 0;
    }
}

[GeneratedComClass]
internal sealed partial class DocumentTitleChangedHandler(Action cb) : ICoreWebView2DocumentTitleChangedEventHandler
{
    public int Invoke(nint sender, nint args) => Safe.Run(cb);
}

[GeneratedComClass]
internal sealed partial class WindowCloseRequestedHandler(Action cb) : ICoreWebView2WindowCloseRequestedEventHandler
{
    public int Invoke(nint sender, nint args) => Safe.Run(cb);
}

/// <summary>Ejecuta un callback nativo sin dejar escapar excepciones (se reenvian al bucle de mensajes).</summary>
internal static class Safe
{
    public static int Run(Action a)
    {
        try { a(); }
        catch (Exception e) { if (OperatingSystem.IsWindows()) WinPlatform.Instance?.Report(e); }
        return 0;
    }
}
