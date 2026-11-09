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

    private readonly BlockingCollection<DictationIndicator> pending = new();
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

    public void Show(DictationIndicator indicator)
    {
        try
        {
            pending.TryAdd(indicator);
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
        var icons = new Dictionary<BadgeLook, nint>();
        ITaskbarList3? taskbar = null;
        try
        {
            taskbar = (ITaskbarList3)new TaskbarListClass();
            taskbar.HrInit();
            foreach (var indicator in pending.GetConsumingEnumerable())
            {
                var look = BadgeLook.Of(indicator);
                if (!icons.TryGetValue(look, out var icon))
                {
                    icon = BadgeIcon.Create(size, ColorOf(look.State), look.Square, look.Dot);
                    icons[look] = icon;
                }

                taskbar.SetOverlayIcon(window, icon, indicator.State.ToString());
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

    private readonly record struct BadgeLook(DictationState State, bool Square, bool Dot)
    {
        public static BadgeLook Of(DictationIndicator indicator) =>
            new(indicator.State, indicator.ShowsSystemAudio, indicator.TypeTextArmed);
    }
}
