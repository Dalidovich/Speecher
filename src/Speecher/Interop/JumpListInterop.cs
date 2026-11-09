using System.Runtime.InteropServices;

namespace Speecher.Interop;

[ComImport]
[Guid("6332debf-87b5-4670-90c0-5e57b408a49e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICustomDestinationList
{
    void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);

    [return: MarshalAs(UnmanagedType.Interface)]
    object BeginList(out uint minSlots, [In] ref Guid riid);

    void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string category, [MarshalAs(UnmanagedType.Interface)] object items);

    void AppendKnownCategory(int category);

    void AddUserTasks([MarshalAs(UnmanagedType.Interface)] object tasks);

    void CommitList();

    [return: MarshalAs(UnmanagedType.Interface)]
    object GetRemovedDestinations([In] ref Guid riid);

    void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string? appId);

    void AbortList();
}

[ComImport]
[Guid("77f10cf0-3db5-4966-b520-b7c54fd35ed6")]
[ClassInterface(ClassInterfaceType.None)]
internal class DestinationListClass
{
}
