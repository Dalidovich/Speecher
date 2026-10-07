using System.Text;
using Speecher.Audio;
using Speecher.Configuration;
using Speecher.Dictation;
using Speecher.Hotkeys;
using Speecher.Speech;
using Speecher.Taskbar;
using Speecher.Transcription;

namespace Speecher.App;

public sealed class SpeecherApp : IDisposable
{
    private readonly ShutdownSignal shutdown = new();
    private readonly FileDownloader downloader = new();
    private HotkeyListener? hotkeyListener;
    private SpeechTranscriber? transcriber;
    private DictationController? controller;
    private TaskbarBadge? taskbarBadge;

    public int Run(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            RunAsync(args, shutdown.Token).GetAwaiter().GetResult();
            return 0;
        }
        catch (OperationCanceledException) when (shutdown.Token.IsCancellationRequested)
        {
            return 0;
        }
        catch (StartupException ex)
        {
            ConsoleLog.Error(ex.Message);
            WaitForExitKey();
            return 1;
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"{ex.GetType().Name}: {ConsoleLog.Describe(ex)}");
            WaitForExitKey();
            return 1;
        }
    }

    public void Dispose()
    {
        controller?.Dispose();
        taskbarBadge?.Dispose();
        hotkeyListener?.Dispose();
        DisposeQuietly(transcriber);
        downloader.Dispose();
        shutdown.MarkCleanupCompleted();
        shutdown.Dispose();
    }

    private async Task RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = LaunchOptions.Parse(args);
        CudaRuntimeProvider.RedirectCompilationCache();
        if (options.TranscribeFiles)
        {
            await RunFileTranscriptionAsync(cancellationToken);
            return;
        }

        var settings = LoadSettings();
        JumpList.Register();
        var outputMode = options.OutputMode ?? settings.DefaultOutputMode;
        ConsoleLog.Info($"Output mode: {outputMode}");
        ListMicrophones(settings.MicrophoneName);
        var hotkey = RegisterHotkey(settings.Hotkey);

        var loadedTranscriber = await LoadTranscriberAsync(cancellationToken);

        controller = new DictationController(settings, outputMode, loadedTranscriber);
        taskbarBadge = TaskbarBadge.Attach();
        if (taskbarBadge is not null)
        {
            controller.StateChanged += taskbarBadge.Show;
            taskbarBadge.Show(DictationState.Idle);
        }

        hotkeyListener!.Pressed += controller.OnHotkey;
        ConsoleLog.Info($"Ready. Press {hotkey.DisplayName} to start or stop recording, Ctrl+C to exit.");

        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private async Task RunFileTranscriptionAsync(CancellationToken cancellationToken)
    {
        if (!Console.IsInputRedirected)
        {
            Console.InputEncoding = Encoding.Unicode;
        }

        JumpList.Register();
        ConsoleLog.Info("Mode: file transcription");
        var loadedTranscriber = await LoadTranscriberAsync(cancellationToken);
        ConsoleLog.Info("Ready. Enter a path to an audio file or a directory, Ctrl+C to exit.");
        await new FileTranscriptionConsole(loadedTranscriber).RunAsync(cancellationToken);
    }

    private async Task<SpeechTranscriber> LoadTranscriberAsync(CancellationToken cancellationToken)
    {
        var modelPath = await ModelProvider.EnsureModelAsync(downloader, cancellationToken);
        if (CudaRuntimeProvider.IsNvidiaDriverPresent())
        {
            await CudaRuntimeProvider.EnsureLibrariesAsync(downloader, cancellationToken);
            CudaRuntimeProvider.Activate();
        }

        cancellationToken.ThrowIfCancellationRequested();
        ConsoleLog.Info("Loading model...");
        transcriber = SpeechTranscriber.Load(modelPath);
        ConsoleLog.Info($"Backend: {transcriber.Backend}");
        return transcriber;
    }

    private static AppSettings LoadSettings()
    {
        var settings = SettingsStore.Load();
        var invalidField = settings.FindInvalidField();
        if (invalidField is not null)
        {
            throw new StartupException($"settings.json: {invalidField} has an invalid value.");
        }

        return settings;
    }

    private static void ListMicrophones(string microphoneName)
    {
        var devices = AudioDeviceCatalog.ListCaptureDevices();
        ConsoleLog.Info("Recording devices:");
        for (var i = 0; i < devices.Count; i++)
        {
            ConsoleLog.Info($"  {i + 1}. {devices[i].Name}{(devices[i].IsDefault ? " (default)" : string.Empty)}");
        }

        if (devices.Count == 0)
        {
            ConsoleLog.Info("  (none)");
        }

        if (!string.IsNullOrEmpty(microphoneName))
        {
            if (!devices.Any(d => string.Equals(d.Name, microphoneName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new StartupException($"Recording device \"{microphoneName}\" was not found.");
            }

            ConsoleLog.Info($"Using recording device: {microphoneName}");
            return;
        }

        if (devices.Count == 0)
        {
            throw new StartupException("No recording devices found.");
        }

        ConsoleLog.Info("Using the system default recording device.");
    }

    private HotkeyDefinition RegisterHotkey(string hotkeyText)
    {
        if (!HotkeyParser.TryParse(hotkeyText, out var hotkey))
        {
            throw new StartupException($"settings.json: Hotkey \"{hotkeyText}\" cannot be parsed. Expected format: Ctrl+Alt+Space.");
        }

        hotkeyListener = HotkeyListener.Register(hotkey, out var error);
        if (error is not null)
        {
            throw new StartupException($"Failed to register hotkey {hotkey.DisplayName}: {error}");
        }

        return hotkey;
    }

    private static void WaitForExitKey()
    {
        ConsoleLog.Info("Press any key to exit");
        if (Console.IsInputRedirected)
        {
            return;
        }

        try
        {
            Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void DisposeQuietly(IDisposable? disposable)
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception)
        {
        }
    }
}
