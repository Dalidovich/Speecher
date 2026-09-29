using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Speecher.App;
using Speecher.Dictation;
using Speecher.Interop;

namespace Speecher.Taskbar;

public sealed class TaskbarBadge : IDisposable
{
    private const uint Green = 0x43A047;
    private const uint Red = 0xE53935;
    private const uint Yellow = 0xFDD835;

    private readonly BlockingCollection<DictationState> pending = new();
    private readonly Thread thread;

    private TaskbarBadge(nint window)
    {
        thread = new Thread(() => Run(window)) { IsBackground = true, Name = "TaskbarBadge" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public static TaskbarBadge? Attach()
    {
        var window = Kernel32.GetConsoleWindow();
        return window == 0 ? null : new TaskbarBadge(window);
    }

    public void Show(DictationState state)
    {
        try
        {
            pending.TryAdd(state);
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        pending.CompleteAdding();
        thread.Join(TimeSpan.FromSeconds(2));
    }

    private void Run(nint window)
    {
        var size = User32.GetSystemMetrics(User32.SmCxSmIcon);
        var icons = Enum.GetValues<DictationState>().ToDictionary(state => state, state => BadgeIcon.CreateCircle(size, ColorOf(state)));
        ITaskbarList3? taskbar = null;
        try
        {
            taskbar = (ITaskbarList3)new TaskbarListClass();
            taskbar.HrInit();
            foreach (var state in pending.GetConsumingEnumerable())
            {
                taskbar.SetOverlayIcon(window, icons[state], state.ToString());
            }

            taskbar.SetOverlayIcon(window, 0, null);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            ConsoleLog.Warning($"Taskbar badge is unavailable: {ConsoleLog.Describe(ex)}");
        }
        finally
        {
            if (taskbar is not null)
            {
                Marshal.ReleaseComObject(taskbar);
            }

            foreach (var icon in icons.Values)
            {
                User32.DestroyIcon(icon);
            }
        }
    }

    private static uint ColorOf(DictationState state) => state switch
    {
        DictationState.Idle => Green,
        DictationState.Recording => Red,
        _ => Yellow
    };
}
