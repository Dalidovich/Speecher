using System.Runtime.InteropServices;
using Speecher.App;
using Speecher.Configuration;
using Speecher.Interop;

namespace Speecher.Taskbar;

public static class JumpList
{
    private static readonly (string Title, string Arguments)[] Tasks =
    [
        ("Type text", $"{LaunchOptions.OutputArgument} {OutputMode.Type}"),
        ("Copy to clipboard", $"{LaunchOptions.OutputArgument} {OutputMode.Clipboard}"),
        ("System audio to clipboard", $"{LaunchOptions.SourceArgument} {AudioSource.System} {LaunchOptions.OutputArgument} {OutputMode.Clipboard}"),
        ("Transcribe files", LaunchOptions.FilesArgument)
    ];

    public static void Register()
    {
        var thread = new Thread(RegisterOnCurrentThread) { IsBackground = true, Name = "JumpList" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private static void RegisterOnCurrentThread()
    {
        try
        {
            var list = (ICustomDestinationList)new DestinationListClass();
            var objectArrayId = typeof(IObjectArray).GUID;
            list.BeginList(out _, ref objectArrayId);
            var tasks = (IObjectCollection)new EnumerableObjectCollectionClass();
            foreach (var (title, arguments) in Tasks)
            {
                tasks.AddObject(CreateTask(title, arguments));
            }

            list.AddUserTasks(tasks);
            list.CommitList();
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            ConsoleLog.Warning($"Jump list is unavailable: {ConsoleLog.Describe(ex)}");
        }
    }

    private static IShellLinkW CreateTask(string title, string arguments)
    {
        var link = (IShellLinkW)new ShellLinkClass();
        link.SetPath(PortablePaths.ExecutablePath);
        link.SetArguments(arguments);
        link.SetWorkingDirectory(PortablePaths.Root);
        link.SetIconLocation(PortablePaths.ExecutablePath, 0);
        link.SetDescription(title);
        SetTitle((IPropertyStore)link, title);
        return link;
    }

    private static void SetTitle(IPropertyStore store, string title)
    {
        var key = PropertyKey.Title;
        var value = new PropVariant { VarType = PropVariant.VtLpwstr, Pointer = Marshal.StringToCoTaskMemUni(title) };
        try
        {
            store.SetValue(ref key, ref value);
            store.Commit();
        }
        finally
        {
            Marshal.FreeCoTaskMem(value.Pointer);
        }
    }
}
