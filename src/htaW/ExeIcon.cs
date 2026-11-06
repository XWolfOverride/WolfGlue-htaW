using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using WolfGlue;

namespace HtaW;

/// <summary>
/// Sustituye el icono de un ejecutable de Windows (recursos <c>RT_GROUP_ICON</c> y <c>RT_ICON</c>) con
/// <c>UpdateResourceW</c>. Se conserva el nombre e idioma del grupo existente, para que el icono de la clase de
/// ventana (que se carga por ese nombre) tambien cambie.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class ExeIcon
{
    private const nint RT_ICON = 3, RT_GROUP_ICON = 14;
    private const ushort DefaultGroup = 32512; // IDI_APPLICATION
    private const ushort LangNeutral = 0;

    /// <summary>Recurso: nombre numerico (o texto) e idioma.</summary>
    internal readonly record struct ResName(ushort Id, string? Name, ushort Lang);

    /// <summary>Grupos de iconos e iconos del ejecutable.</summary>
    internal static (List<ResName> groups, List<ResName> icons) List(string exe)
    {
        var lib = LoadLibraryExW(exe, 0, 0x2 /* AS_DATAFILE */ | 0x20 /* AS_IMAGE_RESOURCE */);
        if (lib == 0) throw new IOException($"No se puede leer {exe} ({Marshal.GetLastPInvokeError()})");
        try { return (Enumerate(lib, RT_GROUP_ICON), Enumerate(lib, RT_ICON)); }
        finally { FreeLibrary(lib); }
    }

    /// <summary>Pone <paramref name="icon"/> (.ico o .png) como icono de <paramref name="exe"/>.</summary>
    public static void Replace(string exe, byte[] icon)
    {
        var images = Images(icon);
        var (groups, icons) = List(exe);
        var group = groups.Count > 0 ? groups[0] : new ResName(DefaultGroup, null, LangNeutral);

        var h = BeginUpdateResourceW(exe, false);
        if (h == 0) throw new IOException($"BeginUpdateResource: {Marshal.GetLastPInvokeError()}");
        bool ok = false;
        try
        {
            foreach (var i in icons) Update(h, RT_ICON, i, null);
            foreach (var g in groups) Update(h, RT_GROUP_ICON, g, null);
            var dir = new byte[6 + images.Count * 14];
            BinaryPrimitives.WriteUInt16LittleEndian(dir.AsSpan(2), 1); // tipo: icono
            BinaryPrimitives.WriteUInt16LittleEndian(dir.AsSpan(4), (ushort)images.Count);
            for (int n = 0; n < images.Count; n++)
            {
                var img = images[n];
                ushort id = (ushort)(n + 1);
                Update(h, RT_ICON, new ResName(id, null, group.Lang), img.Data);
                var e = dir.AsSpan(6 + n * 14, 14);
                e[0] = (byte)(img.Width >= 256 ? 0 : img.Width);
                e[1] = (byte)(img.Height >= 256 ? 0 : img.Height);
                e[2] = img.Colors;
                BinaryPrimitives.WriteUInt16LittleEndian(e[4..], img.Planes);
                BinaryPrimitives.WriteUInt16LittleEndian(e[6..], img.BitCount);
                BinaryPrimitives.WriteInt32LittleEndian(e[8..], img.Data.Length);
                BinaryPrimitives.WriteUInt16LittleEndian(e[12..], id);
            }
            Update(h, RT_GROUP_ICON, group, dir);
            ok = true;
        }
        finally
        {
            if (!EndUpdateResourceW(h, !ok) && ok) throw new IOException($"EndUpdateResource: {Marshal.GetLastPInvokeError()}");
        }
    }

    private readonly record struct Image(int Width, int Height, byte Colors, ushort Planes, ushort BitCount, byte[] Data);

    /// <summary>Imagenes del icono: las de un .ico tal cual, o un PNG como unica imagen (Windows admite PNG en iconos).</summary>
    private static List<Image> Images(byte[] icon)
    {
        switch (GlueIcon.Detect(icon))
        {
            case GlueIconFormat.Png:
                var (w, hgt) = GlueIcon.PngSize(icon);
                return [new Image(w, hgt, 0, 1, 32, icon)];
            case GlueIconFormat.Ico:
                return [.. GlueIcon.IcoImages(icon).Select(i =>
                    new Image(i.Width, i.Height, i.ColorCount, i.Planes == 0 ? (ushort)1 : i.Planes, i.BitCount == 0 ? (ushort)32 : i.BitCount,
                        icon.AsSpan(i.Offset, i.Length).ToArray()))];
            default:
                throw new FormatException("El icono no es .png ni .ico");
        }
    }

    private static void Update(nint h, nint type, ResName r, byte[]? data)
    {
        fixed (char* name = r.Name)
        fixed (byte* p = data)
        {
            var n = r.Name is null ? (nint)r.Id : (nint)name;
            if (!UpdateResourceW(h, type, n, r.Lang, p, (uint)(data?.Length ?? 0)))
                throw new IOException($"UpdateResource: {Marshal.GetLastPInvokeError()}");
        }
    }

    // ------------------------------------------------------------- enumeracion (un solo hilo)
    private static List<ResName>? _found;
    private static (ushort id, string? name) _current;

    private static List<ResName> Enumerate(nint lib, nint type)
    {
        _found = [];
        EnumResourceNamesW(lib, type, &OnName, 0);
        var r = _found;
        _found = null;
        return r;
    }

    [UnmanagedCallersOnly]
    private static int OnName(nint module, nint type, nint name, nint param)
    {
        _current = name >>> 16 == 0 ? ((ushort)name, null) : ((ushort)0, Marshal.PtrToStringUni(name));
        EnumResourceLanguagesW(module, type, name, &OnLanguage, 0);
        return 1;
    }

    [UnmanagedCallersOnly]
    private static int OnLanguage(nint module, nint type, nint name, ushort lang, nint param)
    {
        _found!.Add(new ResName(_current.id, _current.name, lang));
        return 1;
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibraryExW(string file, nint reserved, uint flags);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeLibrary(nint module);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumResourceNamesW(nint module, nint type,
        delegate* unmanaged<nint, nint, nint, nint, int> callback, nint param);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumResourceLanguagesW(nint module, nint type, nint name,
        delegate* unmanaged<nint, nint, nint, ushort, nint, int> callback, nint param);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint BeginUpdateResourceW(string file, [MarshalAs(UnmanagedType.Bool)] bool deleteExisting);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateResourceW(nint update, nint type, nint name, ushort lang, void* data, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EndUpdateResourceW(nint update, [MarshalAs(UnmanagedType.Bool)] bool discard);
}
