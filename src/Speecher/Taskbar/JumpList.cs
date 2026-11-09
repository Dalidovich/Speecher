using System.Runtime.InteropServices;
using Speecher.App;
using Speecher.Interop;

namespace Speecher.Taskbar;

public static class JumpList
{
    public static void Clear()
    {
        var thread = new Thread(ClearOnCurrentThread) { IsBackground = true, Name = "JumpList" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private static void ClearOnCurrentThread()
    {
        try
        {
            var list = (ICustomDestinationList)new DestinationListClass();
            list.DeleteList(null);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            ConsoleLog.Warning($"Jump list is unavailable: {ConsoleLog.Describe(ex)}");
        }
    }
}
