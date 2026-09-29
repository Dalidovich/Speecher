using System.Globalization;
using Speecher.Hotkeys;
using Speecher.Interop;

namespace Speecher.Input;

public sealed class KeyboardTyper
{
    private static readonly VirtualKey[] Modifiers =
    [
        VirtualKey.ControlKey,
        VirtualKey.Menu,
        VirtualKey.ShiftKey,
        VirtualKey.LWin,
        VirtualKey.RWin
    ];

    private static readonly TimeSpan ModifierPollInterval = TimeSpan.FromMilliseconds(15);

    public void WaitForModifiersReleased(CancellationToken cancellationToken)
    {
        while (Modifiers.Any(IsPressed))
        {
            cancellationToken.WaitHandle.WaitOne(ModifierPollInterval);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public int TypeText(string text, CancellationToken cancellationToken)
    {
        var sent = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var element = elements.GetTextElement();
            var inputs = new User32.Input[element.Length * 2];
            for (var i = 0; i < element.Length; i++)
            {
                inputs[i * 2] = UnicodeInput(element[i], keyUp: false);
                inputs[i * 2 + 1] = UnicodeInput(element[i], keyUp: true);
            }

            Send(inputs);
            sent++;
        }

        return sent;
    }

    public int PressKey(VirtualKey key, int times, CancellationToken cancellationToken)
    {
        var sent = 0;
        while (sent < times && !cancellationToken.IsCancellationRequested)
        {
            Send([VirtualKeyInput(key, keyUp: false), VirtualKeyInput(key, keyUp: true)]);
            sent++;
        }

        return sent;
    }

    private static bool IsPressed(VirtualKey key) => (User32.GetAsyncKeyState((int)key) & 0x8000) != 0;

    private static void Send(User32.Input[] inputs) => User32.SendInput((uint)inputs.Length, inputs, User32.Input.Size);

    private static User32.Input UnicodeInput(char unit, bool keyUp) => KeyboardEvent(new User32.KeyboardInput
    {
        VirtualKey = 0,
        ScanCode = unit,
        Flags = User32.KeyEventFUnicode | (keyUp ? User32.KeyEventFKeyUp : 0)
    });

    private static User32.Input VirtualKeyInput(VirtualKey key, bool keyUp) => KeyboardEvent(new User32.KeyboardInput
    {
        VirtualKey = (ushort)key,
        ScanCode = (ushort)User32.MapVirtualKey((uint)key, User32.MapVkVkToVsc),
        Flags = keyUp ? User32.KeyEventFKeyUp : 0
    });

    private static User32.Input KeyboardEvent(User32.KeyboardInput keyboard) => new()
    {
        Type = User32.InputKeyboard,
        Data = new User32.InputUnion { Keyboard = keyboard }
    };
}
