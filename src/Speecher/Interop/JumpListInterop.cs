using System.Runtime.InteropServices;
using System.Text;

namespace Speecher.Interop;

[ComImport]
[Guid("6332debf-87b5-4670-90c0-5e57b408a49e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICustomDestinationList
{
    void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);

    [return: MarshalAs(UnmanagedType.Interface)]
    object BeginList(out uint minSlots, [In] ref Guid riid);

    void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string category, IObjectArray items);

    void AppendKnownCategory(int category);

    void AddUserTasks(IObjectArray tasks);

    void CommitList();

    [return: MarshalAs(UnmanagedType.Interface)]
    object GetRemovedDestinations([In] ref Guid riid);

    void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string? appId);

    void AbortList();
}

[ComImport]
[Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectArray
{
    uint GetCount();

    [return: MarshalAs(UnmanagedType.Interface)]
    object GetAt(uint index, [In] ref Guid riid);
}

[ComImport]
[Guid("5632b1a4-e38a-400a-928a-d4cd63230295")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectCollection : IObjectArray
{
    new uint GetCount();

    [return: MarshalAs(UnmanagedType.Interface)]
    new object GetAt(uint index, [In] ref Guid riid);

    void AddObject([MarshalAs(UnmanagedType.Interface)] object item);

    void AddFromArray(IObjectArray source);

    void RemoveObjectAt(uint index);

    void Clear();
}

[ComImport]
[Guid("000214F9-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, nint findData, uint flags);

    nint GetIDList();

    void SetIDList(nint idList);

    void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);

    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

    void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);

    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);

    void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);

    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);

    ushort GetHotkey();

    void SetHotkey(ushort hotkey);

    int GetShowCmd();

    void SetShowCmd(int showCmd);

    void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxIconPath, out int iconIndex);

    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);

    void Resolve(nint hwnd, uint flags);

    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}

[ComImport]
[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    uint GetCount();

    void GetAt(uint index, out PropertyKey key);

    void GetValue([In] ref PropertyKey key, out PropVariant value);

    void SetValue([In] ref PropertyKey key, [In] ref PropVariant value);

    void Commit();
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey(Guid formatId, uint propertyId)
{
    public static readonly PropertyKey Title = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);

    public Guid FormatId = formatId;
    public uint PropertyId = propertyId;
}

[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropVariant
{
    public const ushort VtLpwstr = 31;

    [FieldOffset(0)]
    public ushort VarType;

    [FieldOffset(8)]
    public nint Pointer;
}

[ComImport]
[Guid("77f10cf0-3db5-4966-b520-b7c54fd35ed6")]
[ClassInterface(ClassInterfaceType.None)]
internal class DestinationListClass
{
}

[ComImport]
[Guid("2d3468c1-36a7-43b6-ac24-d3f02fd9607a")]
[ClassInterface(ClassInterfaceType.None)]
internal class EnumerableObjectCollectionClass
{
}

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
[ClassInterface(ClassInterfaceType.None)]
internal class ShellLinkClass
{
}
