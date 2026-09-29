using Speecher.Interop;

namespace Speecher.App;

public sealed class ShutdownSignal : IDisposable
{
    private static readonly TimeSpan CloseCleanupTimeout = TimeSpan.FromSeconds(4);

    private readonly CancellationTokenSource source = new();
    private readonly ManualResetEventSlim cleanupCompleted = new(false);
    private readonly Kernel32.ConsoleCtrlHandler handler;

    public ShutdownSignal()
    {
        handler = OnConsoleControl;
        Kernel32.SetConsoleCtrlHandler(handler, true);
    }

    public CancellationToken Token => source.Token;

    public void MarkCleanupCompleted() => cleanupCompleted.Set();

    public void Dispose()
    {
        Kernel32.SetConsoleCtrlHandler(handler, false);
        GC.KeepAlive(handler);
    }

    private bool OnConsoleControl(uint controlType)
    {
        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        if (controlType is not (Kernel32.CtrlCEvent or Kernel32.CtrlBreakEvent))
        {
            cleanupCompleted.Wait(CloseCleanupTimeout);
        }

        return true;
    }
}
