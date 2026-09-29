using System.Runtime.InteropServices;
using Speecher.Interop;

namespace Speecher.Hotkeys;

public sealed class HotkeyListener : IDisposable
{
    private const int HotkeyId = 1;
    private const uint PeekNoRemove = 0;
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly HotkeyDefinition hotkey;
    private readonly TaskCompletionSource<string?> registration = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread thread;
    private uint threadId;

    private HotkeyListener(HotkeyDefinition hotkey)
    {
        this.hotkey = hotkey;
        thread = new Thread(RunMessageLoop) { IsBackground = true, Name = "Hotkey" };
    }

    public event Action? Pressed;

    public static HotkeyListener Register(HotkeyDefinition hotkey, out string? error)
    {
        var listener = new HotkeyListener(hotkey);
        listener.thread.Start();
        error = listener.registration.Task.GetAwaiter().GetResult();
        return listener;
    }

    public void Dispose()
    {
        if (threadId != 0)
        {
            User32.PostThreadMessage(threadId, User32.WmQuit, 0, 0);
        }

        thread.Join(TimeSpan.FromSeconds(2));
    }

    private void RunMessageLoop()
    {
        User32.PeekMessage(out _, 0, User32.WmUser, User32.WmUser, PeekNoRemove);

        if (!User32.RegisterHotKey(0, HotkeyId, hotkey.Modifiers | User32.ModNoRepeat, (uint)hotkey.Key))
        {
            var errorCode = Marshal.GetLastPInvokeError();
            registration.SetResult(errorCode == ErrorHotkeyAlreadyRegistered
                ? "the key combination is already registered by another application"
                : $"Win32 error {errorCode}");
            return;
        }

        threadId = Kernel32.GetCurrentThreadId();
        registration.SetResult(null);

        try
        {
            while (User32.GetMessage(out var message, 0, 0, 0) > 0)
            {
                if (message.Message == User32.WmHotkey && message.WParam == HotkeyId)
                {
                    Pressed?.Invoke();
                }
            }
        }
        finally
        {
            User32.UnregisterHotKey(0, HotkeyId);
        }
    }
}
