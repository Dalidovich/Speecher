using System.ComponentModel;
using System.Runtime.InteropServices;
using Speecher.Interop;

namespace Speecher.Input;

public static class ClipboardWriter
{
    private const int OpenAttempts = 20;
    private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(25);

    public static void SetText(string text, CancellationToken cancellationToken)
    {
        OpenClipboard(cancellationToken);
        try
        {
            if (!User32.EmptyClipboard())
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            var memory = CopyToGlobalMemory(text);
            if (User32.SetClipboardData(User32.CfUnicodeText, memory) == 0)
            {
                var error = Marshal.GetLastPInvokeError();
                Kernel32.GlobalFree(memory);
                throw new Win32Exception(error);
            }
        }
        finally
        {
            User32.CloseClipboard();
        }
    }

    private static void OpenClipboard(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (User32.OpenClipboard(0))
            {
                return;
            }

            if (attempt == OpenAttempts)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Clipboard is locked by another application.");
            }

            cancellationToken.WaitHandle.WaitOne(OpenRetryDelay);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static nint CopyToGlobalMemory(string text)
    {
        var byteCount = (text.Length + 1) * sizeof(char);
        var memory = Kernel32.GlobalAlloc(Kernel32.GmemMoveable, (nuint)byteCount);
        if (memory == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var target = Kernel32.GlobalLock(memory);
        if (target == 0)
        {
            var error = Marshal.GetLastPInvokeError();
            Kernel32.GlobalFree(memory);
            throw new Win32Exception(error);
        }

        try
        {
            Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
            Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
        }
        finally
        {
            Kernel32.GlobalUnlock(memory);
        }

        return memory;
    }
}
