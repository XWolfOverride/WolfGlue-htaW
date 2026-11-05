// Interfaces COM del shell para los dialogos de archivo (IFileOpenDialog / IFileSaveDialog), declaradas para
// Native AOT. El orden de los metodos reproduce la vtable de ShObjIdl_core.h: NO cambiar el orden.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

#pragma warning disable IDE1006

namespace WolfGlue.Platform.Windows;

internal static class ShellConst
{
    public static readonly Guid CLSID_FileOpenDialog = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
    public static readonly Guid CLSID_FileSaveDialog = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");
    public static readonly Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    public static readonly Guid IID_IFileOpenDialog = new("d57c7288-d4ad-4768-be02-9d969532d960");
    public static readonly Guid IID_IFileSaveDialog = new("84bccd23-5fde-4cdb-aea4-af64b83d78ab");

    public const uint FOS_OVERWRITEPROMPT = 0x2, FOS_NOCHANGEDIR = 0x8, FOS_PICKFOLDERS = 0x20,
        FOS_FORCEFILESYSTEM = 0x40, FOS_ALLOWMULTISELECT = 0x200, FOS_PATHMUSTEXIST = 0x800,
        FOS_FILEMUSTEXIST = 0x1000;
    public const uint SIGDN_FILESYSPATH = 0x80058000;
    public const int ERROR_CANCELLED_HR = unchecked((int)0x800704C7);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
internal partial interface IShellItem
{
    void _00(); // BindToHandler
    void _01(); // GetParent
    nint GetDisplayName(uint sigdn); // LPWSTR (CoTaskMemFree)
}

[GeneratedComInterface]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
internal partial interface IShellItemArray
{
    void _00(); void _01(); void _02(); void _03(); // BindToHandler, GetPropertyStore, GetPropertyDescriptionList, GetAttributes
    uint GetCount();
    IShellItem GetItemAt(uint index);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("b4db1657-70d7-485e-8e3e-6fcb5a5c1802")]
internal partial interface IModalWindow
{
    [PreserveSig] int Show(nint parent);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
internal partial interface IFileDialog : IModalWindow
{
    void SetFileTypes(uint count, nint filterSpecs);   // COMDLG_FILTERSPEC*
    void SetFileTypeIndex(uint index);
    void _02(); void _03(); void _04();                // GetFileTypeIndex, Advise, Unadvise
    void SetOptions(uint options);
    uint GetOptions();
    void _07();                                        // SetDefaultFolder
    void SetFolder(IShellItem item);
    void _09(); void _10();                            // GetFolder, GetCurrentSelection
    void SetFileName(string name);
    void _12();                                        // GetFileName
    void SetTitle(string title);
    void _14(); void _15();                            // SetOkButtonLabel, SetFileNameLabel
    IShellItem GetResult();
    void _17();                                        // AddPlace
    void SetDefaultExtension(string extension);
    void _19(); void _20(); void _21(); void _22();    // Close, SetClientGuid, ClearClientData, SetFilter
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
internal partial interface IFileOpenDialog : IFileDialog
{
    IShellItemArray GetResults();
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab")]
internal partial interface IFileSaveDialog : IFileDialog
{
}
