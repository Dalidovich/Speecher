using System.Runtime.InteropServices;

namespace Speecher.Interop;

[ComImport]
[Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOpenDialog
{
    [PreserveSig]
    int Show(nint parent);

    void SetFileTypes(uint count, [In, MarshalAs(UnmanagedType.LPArray)] FileFilterSpec[] filters);

    void SetFileTypeIndex(uint index);

    void GetFileTypeIndex(out uint index);

    void Advise(nint events, out uint cookie);

    void Unadvise(uint cookie);

    void SetOptions(uint options);

    void GetOptions(out uint options);

    void SetDefaultFolder(IShellItem item);

    void SetFolder(IShellItem item);

    void GetFolder(out IShellItem item);

    void GetCurrentSelection(out IShellItem item);

    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

    void GetFileName(out nint name);

    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);

    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

    void GetResult(out IShellItem item);

    void AddPlace(IShellItem item, int placement);

    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);

    void Close(int result);

    void SetClientGuid([In] ref Guid guid);

    void ClearClientData();

    void SetFilter(nint filter);

    void GetResults(out IShellItemArray items);

    void GetSelectedItems(out IShellItemArray items);
}

[ComImport]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(nint context, [In] ref Guid handler, [In] ref Guid riid, out nint result);

    void GetParent(out IShellItem parent);

    void GetDisplayName(uint form, out nint name);

    void GetAttributes(uint mask, out uint attributes);

    void Compare(IShellItem other, uint hint, out int order);
}

[ComImport]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    void BindToHandler(nint context, [In] ref Guid handler, [In] ref Guid riid, out nint result);

    void GetPropertyStore(int flags, [In] ref Guid riid, out nint store);

    void GetPropertyDescriptionList([In] ref Guid type, [In] ref Guid riid, out nint list);

    void GetAttributes(int flags, uint mask, out uint attributes);

    void GetCount(out uint count);

    void GetItemAt(uint index, out IShellItem item);

    void EnumItems(out nint items);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct FileFilterSpec(string name, string spec)
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string Name = name;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string Spec = spec;
}

[ComImport]
[Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
[ClassInterface(ClassInterfaceType.None)]
internal class FileOpenDialogClass
{
}
