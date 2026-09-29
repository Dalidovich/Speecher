using System.Runtime.InteropServices;

namespace Speecher.Interop;

[ComImport]
[Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITaskbarList3
{
    void HrInit();

    void AddTab(nint hwnd);

    void DeleteTab(nint hwnd);

    void ActivateTab(nint hwnd);

    void SetActiveAlt(nint hwnd);

    void MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);

    void SetProgressValue(nint hwnd, ulong completed, ulong total);

    void SetProgressState(nint hwnd, int flags);

    void RegisterTab(nint tab, nint mdi);

    void UnregisterTab(nint tab);

    void SetTabOrder(nint tab, nint insertBefore);

    void SetTabActive(nint tab, nint mdi, uint reserved);

    void ThumbBarAddButtons(nint hwnd, uint count, nint buttons);

    void ThumbBarUpdateButtons(nint hwnd, uint count, nint buttons);

    void ThumbBarSetImageList(nint hwnd, nint imageList);

    void SetOverlayIcon(nint hwnd, nint icon, [MarshalAs(UnmanagedType.LPWStr)] string? description);

    void SetThumbnailTooltip(nint hwnd, [MarshalAs(UnmanagedType.LPWStr)] string? tip);

    void SetThumbnailClip(nint hwnd, nint clip);
}

[ComImport]
[Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
[ClassInterface(ClassInterfaceType.None)]
internal class TaskbarListClass
{
}
