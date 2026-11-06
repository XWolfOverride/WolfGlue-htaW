using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WolfGlue.Platform.Windows;

/// <summary>HICON a partir de los bytes de un .ico o .png, al tamano pedido.</summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class WinIcon
{
    private const uint IconVersion = 0x00030000;

    /// <summary>Devuelve 0 si los datos no son un icono valido. El llamador destruye el HICON.</summary>
    public static nint Create(byte[] data, int size)
    {
        try
        {
            switch (GlueIcon.Detect(data))
            {
                case GlueIconFormat.Png:
                    // CreateIconFromResourceEx acepta PNG (como las imagenes PNG de los .ico) y lo escala a cx/cy.
                    return FromResource(data, 0, data.Length, size);
                case GlueIconFormat.Ico:
                    var img = GlueIcon.Best(GlueIcon.IcoImages(data), size);
                    return FromResource(data, img.Offset, img.Length, size);
                default:
                    return 0;
            }
        }
        catch (FormatException) { return 0; }
    }

    private static nint FromResource(byte[] data, int offset, int length, int size)
    {
        fixed (byte* p = data) return CreateIconFromResourceEx(p + offset, (uint)length, true, IconVersion, size, size, 0);
    }

    /// <summary>Tamano real (ancho) de un HICON, para pruebas.</summary>
    public static int Width(nint icon)
    {
        if (!GetIconInfo(icon, out var info)) return 0;
        try
        {
            BITMAP bm;
            return GetObjectW(info.hbmColor != 0 ? info.hbmColor : info.hbmMask, sizeof(BITMAP), &bm) != 0 ? bm.bmWidth : 0;
        }
        finally
        {
            if (info.hbmColor != 0) Win32.DeleteObject(info.hbmColor);
            if (info.hbmMask != 0) Win32.DeleteObject(info.hbmMask);
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint CreateIconFromResourceEx(byte* bits, uint size, [MarshalAs(UnmanagedType.Bool)] bool icon,
        uint version, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint icon);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public int fIcon;
        public int xHotspot, yHotspot;
        public nint hbmMask, hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public nint bmBits;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetIconInfo(nint icon, out ICONINFO info);

    [LibraryImport("gdi32.dll")]
    private static partial int GetObjectW(nint obj, int size, void* buffer);
}
