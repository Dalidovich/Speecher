using System.Runtime.InteropServices;

namespace Speecher.Interop;

internal static partial class Kernel32
{
    public const uint CtrlCEvent = 0;
    public const uint CtrlBreakEvent = 1;
    public const uint CtrlCloseEvent = 2;

    public const uint GmemMoveable = 0x0002;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public delegate bool ConsoleCtrlHandler(uint ctrlType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler? handler, [MarshalAs(UnmanagedType.Bool)] bool add);

    [LibraryImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetDllDirectory(string? pathName);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll")]
    public static partial nint GetConsoleWindow();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalFree(nint hMem);
}
