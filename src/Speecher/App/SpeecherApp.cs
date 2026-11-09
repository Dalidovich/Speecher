using System.Text;
using Speecher.Audio;
using Speecher.Configuration;
using Speecher.Dictation;
using Speecher.Hotkeys;
using Speecher.Speech;
using Speecher.Taskbar;

namespace Speecher.App;

public sealed class SpeecherApp : IDisposable
{
    private readonly ShutdownSignal shutdown = new();
    private readonly FileDownloader downloader = new();
    private HotkeyListener? hotkeyListener;
    private HotkeyListener? typeTextHotkeyListener;
    private HotkeyListener? systemAudioHotkeyListener;
    private HotkeyListener? transcribeFilesHotkeyListener;
    private ISpeechTranscriber? transcriber;
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
        typeTextHotkeyListener?.Dispose();
        systemAudioHotkeyListener?.Dispose();
        transcribeFilesHotkeyListener?.Dispose();
        DisposeQuietly(transcriber);
        downloader.Dispose();
        shutdown.MarkCleanupCompleted();
        shutdown.Dispose();
    }

    private async Task RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 0)
        {
            throw new StartupException($"Unknown command line argument \"{args[0]}\".");
        }

        var settings = LoadSettings();
        ListMicrophones(settings.MicrophoneName);

        var hotkey = ParseHotkey(nameof(AppSettings.Hotkey), settings.Hotkey);
        var typeTextHotkey = ParseHotkey(nameof(AppSettings.TypeTextHotkey), settings.TypeTextHotkey);
        var systemAudioHotkey = ParseHotkey(nameof(AppSettings.SystemAudioHotkey), settings.SystemAudioHotkey);
        var transcribeFilesHotkey = ParseHotkey(nameof(AppSettings.TranscribeFilesHotkey), settings.TranscribeFilesHotkey);
        EnsureDistinct(
            (nameof(AppSettings.Hotkey), hotkey),
            (nameof(AppSettings.TypeTextHotkey), typeTextHotkey),
            (nameof(AppSettings.SystemAudioHotkey), systemAudioHotkey),
            (nameof(AppSettings.TranscribeFilesHotkey), transcribeFilesHotkey));

        hotkeyListener = RegisterHotkey(hotkey);
        typeTextHotkeyListener = RegisterHotkey(typeTextHotkey);
        systemAudioHotkeyListener = RegisterHotkey(systemAudioHotkey);
        transcribeFilesHotkeyListener = RegisterHotkey(transcribeFilesHotkey);

        var loadedTranscriber = await LoadTranscriberAsync(settings, cancellationToken);

        controller = new DictationController(settings, loadedTranscriber);
        controller.IndicatorChanged += WindowTitle.Show;
        WindowTitle.Show(DictationIndicator.Initial);
        taskbarBadge = TaskbarBadge.Attach();
        if (taskbarBadge is not null)
        {
            controller.IndicatorChanged += taskbarBadge.Show;
            taskbarBadge.Show(DictationIndicator.Initial);
        }

        hotkeyListener.Pressed += controller.OnHotkey;
        typeTextHotkeyListener.Pressed += controller.OnTypeTextHotkey;
        systemAudioHotkeyListener.Pressed += controller.OnSystemAudioHotkey;
        transcribeFilesHotkeyListener.Pressed += controller.OnTranscribeFilesHotkey;
        ConsoleLog.Info($"Ready. Press {hotkey.DisplayName} to start or stop recording, {typeTextHotkey.DisplayName} to type the next phrase instead of copying it, {systemAudioHotkey.DisplayName} to record system audio next time, {transcribeFilesHotkey.DisplayName} to transcribe audio files, Ctrl+C to exit.");

        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private async Task<ISpeechTranscriber> LoadTranscriberAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Backend == SpeechBackend.Api)
        {
            ConsoleLog.Info("Checking transcription API...");
            transcriber = await ApiSpeechTranscriber.CreateAsync(settings.Api, cancellationToken).ConfigureAwait(false);
            ConsoleLog.Info($"Backend: {transcriber.Backend}");
            return transcriber;
        }

        var modelPath = await ModelProvider.EnsureModelAsync(downloader, cancellationToken);
        if (settings.Backend == SpeechBackend.Cuda && CudaRuntimeProvider.IsNvidiaDriverPresent())
        {
            CudaRuntimeProvider.RedirectCompilationCache();
            await CudaRuntimeProvider.EnsureLibrariesAsync(downloader, cancellationToken);
            CudaRuntimeProvider.Activate();
        }

        cancellationToken.ThrowIfCancellationRequested();
        ConsoleLog.Info("Loading model...");
        transcriber = SpeechTranscriber.Load(modelPath, settings);
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

    private static HotkeyDefinition ParseHotkey(string settingName, string hotkeyText)
    {
        if (!HotkeyParser.TryParse(hotkeyText, out var hotkey))
        {
            throw new StartupException($"settings.json: {settingName} \"{hotkeyText}\" cannot be parsed. Expected format: Ctrl+Alt+Space.");
        }

        return hotkey;
    }

    private static void EnsureDistinct(params (string Name, HotkeyDefinition Hotkey)[] hotkeys)
    {
        for (var i = 0; i < hotkeys.Length; i++)
        {
            for (var j = i + 1; j < hotkeys.Length; j++)
            {
                if (hotkeys[i].Hotkey.Modifiers == hotkeys[j].Hotkey.Modifiers && hotkeys[i].Hotkey.Key == hotkeys[j].Hotkey.Key)
                {
                    throw new StartupException($"settings.json: {hotkeys[i].Name} and {hotkeys[j].Name} must differ.");
                }
            }
        }
    }

    private static HotkeyListener RegisterHotkey(HotkeyDefinition hotkey)
    {
        var listener = HotkeyListener.Register(hotkey, out var error);
        if (error is not null)
        {
            listener.Dispose();
            throw new StartupException($"Failed to register hotkey {hotkey.DisplayName}: {error}");
        }

        return listener;
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
