using Speecher.App;
using Speecher.Audio;
using Speecher.Configuration;
using Speecher.Hotkeys;
using Speecher.Input;
using Speecher.Speech;
using Speecher.Transcription;
using Speecher.WakeWord;

namespace Speecher.Dictation;

public sealed class DictationController : IDisposable
{
    private readonly object sync = new();
    private readonly AppSettings settings;
    private readonly AudioRecorder recorder;
    private readonly ISpeechTranscriber transcriber;
    private readonly ApiLanguage apiLanguage;
    private readonly KeyboardTyper typer = new();
    private readonly VoiceCommandMatcher commandMatcher;
    private readonly ScriptTriggerMatcher scriptMatcher;
    private readonly HistoryWriter history = new();
    private readonly WakeWordListener? wakeWordListener;
    private readonly WakeWordPhraseStripper wakeWordStripper;

    private DictationState state = DictationState.Idle;
    private long generation;
    private RecordingSession? session;
    private CancellationTokenSource? operation;
    private int lastInputLength;
    private bool typeTextArmed;
    private bool systemAudioArmed;
    private bool filePickerOpen;
    private AudioSource recordingSource;
    private bool recordingStartedByWakeWord;
    private bool listening;
    private bool disposed;

    public DictationController(AppSettings settings, ISpeechTranscriber transcriber, ApiLanguage apiLanguage, WakeWordListener? wakeWordListener)
    {
        this.settings = settings;
        this.transcriber = transcriber;
        this.apiLanguage = apiLanguage;
        this.wakeWordListener = wakeWordListener;
        recorder = new AudioRecorder(settings.MicrophoneName);
        commandMatcher = new VoiceCommandMatcher(settings.VoiceCommands);
        scriptMatcher = new ScriptTriggerMatcher(settings.ScriptTriggers);
        wakeWordStripper = new WakeWordPhraseStripper(settings.WakeWordPhrases);
        if (wakeWordListener is not null)
        {
            wakeWordListener.Detected += OnWakeWord;
            wakeWordListener.Faulted += OnWakeWordFaulted;
        }
    }

    public event Action<DictationIndicator>? IndicatorChanged;

    public DictationIndicator Indicator
    {
        get
        {
            lock (sync)
            {
                return CreateIndicator();
            }
        }
    }

    public void OnHotkey()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            try
            {
                switch (state)
                {
                    case DictationState.Idle:
                        StartRecording(startedByWakeWord: false);
                        break;
                    case DictationState.Recording:
                        FinishRecording(stoppedByWakeWord: false);
                        break;
                    default:
                        CancelOperation();
                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleLog.Error(ConsoleLog.Describe(ex));
                ResetToIdle();
            }
        }
    }

    public void OnTypeTextHotkey()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            typeTextArmed = !typeTextArmed;
            ConsoleLog.Event(typeTextArmed ? "Type text armed for the next recording" : "Type text disarmed");
            PublishIndicator();
        }
    }

    public void OnSystemAudioHotkey()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            systemAudioArmed = !systemAudioArmed;
            ConsoleLog.Event(systemAudioArmed ? "System audio armed for the next recording" : "System audio disarmed");
            PublishIndicator();
        }
    }

    public void OnWakeWordHotkey()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            if (wakeWordListener is null)
            {
                ConsoleLog.Event("Wake word listening is unavailable: the wake word model was not loaded");
                return;
            }

            if (listening)
            {
                wakeWordListener.Stop();
                listening = false;
                ConsoleLog.Event("Wake word listening off");
            }
            else if (!TryStartListening())
            {
                return;
            }

            settings.WakeWordListening = listening;
            SettingsStore.Save(settings, "wake word listening state was not saved");
            PublishIndicator();
        }
    }

    public void OnApiLanguageHotkey()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            if (settings.Backend != SpeechBackend.Api)
            {
                ConsoleLog.Warning($"API language can be switched only with {nameof(AppSettings.Backend)} \"{SpeechBackend.Api}\".");
                return;
            }

            if (settings.Api.LanguageParameter.Length == 0)
            {
                ConsoleLog.Warning($"API language cannot be switched: {nameof(AppSettings.Api)}.{nameof(ApiTranscriptionSettings.LanguageParameter)} is empty.");
                return;
            }

            ConsoleLog.Event($"API language: {apiLanguage.Toggle()}");
            PublishIndicator();
        }
    }

    public void RestoreWakeWordListening()
    {
        lock (sync)
        {
            if (disposed || listening || !settings.WakeWordListening)
            {
                return;
            }

            if (wakeWordListener is null)
            {
                ConsoleLog.Warning("Wake word listening was on, but the wake word model was not loaded.");
                return;
            }

            if (TryStartListening())
            {
                PublishIndicator();
            }
        }
    }

    public void OnTranscribeFilesHotkey()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            if (state != DictationState.Idle || filePickerOpen)
            {
                ConsoleLog.Event("Busy: finish the current operation before transcribing files");
                return;
            }

            filePickerOpen = true;
        }

        _ = Task.Run(PickAndTranscribeFilesAsync);
    }

    public void Dispose()
    {
        lock (sync)
        {
            disposed = true;
            generation++;
            operation?.Cancel();
            session?.Dispose();
            session = null;
            wakeWordListener?.Stop();
            listening = false;
        }
    }

    private bool TryStartListening()
    {
        try
        {
            wakeWordListener!.Start();
            listening = true;
            ConsoleLog.Event($"Wake word listening on, threshold {wakeWordListener.Threshold:0.##}");
            return true;
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"Wake word listening failed to start: {ConsoleLog.Describe(ex)}");
            return false;
        }
    }

    private void OnWakeWord()
    {
        lock (sync)
        {
            if (disposed || !listening)
            {
                return;
            }

            ConsoleLog.Event("Wake word detected");
            try
            {
                switch (state)
                {
                    case DictationState.Idle:
                        StartRecording(startedByWakeWord: true);
                        break;
                    case DictationState.Recording:
                        FinishRecording(stoppedByWakeWord: true);
                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleLog.Error(ConsoleLog.Describe(ex));
                ResetToIdle();
            }
        }
    }

    private void OnWakeWordFaulted(Exception exception)
    {
        lock (sync)
        {
            if (disposed || !listening)
            {
                return;
            }

            listening = false;
            ConsoleLog.Error($"Wake word listening stopped: {ConsoleLog.Describe(exception)}");
            PublishIndicator();
        }
    }

    private void StartRecording(bool startedByWakeWord)
    {
        var id = generation + 1;
        var source = systemAudioArmed ? AudioSource.System : AudioSource.Microphone;
        session = recorder.Start(
            source,
            settings.MaxRecordingSeconds,
            () => OnMaxDurationReached(id),
            exception => OnRecordingFaulted(id, exception));
        generation = id;
        systemAudioArmed = false;
        recordingSource = source;
        recordingStartedByWakeWord = startedByWakeWord;
        if (source == AudioSource.System)
        {
            ConsoleLog.Event($"Capturing system audio from: {AudioDeviceCatalog.FindDefaultOutputName()}");
        }

        SetState(DictationState.Recording);
    }

    private void OnMaxDurationReached(long id)
    {
        lock (sync)
        {
            if (state != DictationState.Recording || generation != id || disposed)
            {
                return;
            }

            ConsoleLog.Event("Maximum recording duration reached.");
            FinishRecording(stoppedByWakeWord: false);
        }
    }

    private void OnRecordingFaulted(long id, Exception exception)
    {
        lock (sync)
        {
            if (state != DictationState.Recording || generation != id || disposed)
            {
                return;
            }

            ConsoleLog.Error($"Recording device failed: {ConsoleLog.Describe(exception)}");
            ResetToIdle();
        }
    }

    private void FinishRecording(bool stoppedByWakeWord)
    {
        var recording = session!;
        session = null;
        var cancellation = new CancellationTokenSource();
        operation = cancellation;
        var typeText = TakeTypeTextFlag();
        state = DictationState.Transcribing;
        PublishIndicator();
        var id = generation;
        var fromMicrophone = recordingSource == AudioSource.Microphone;
        var wakeWordEdges = fromMicrophone ? new WakeWordEdges(recordingStartedByWakeWord, stoppedByWakeWord) : WakeWordEdges.None;
        _ = Task.Run(() => ProcessRecordingAsync(recording, id, typeText, fromMicrophone, wakeWordEdges, cancellation.Token));
    }

    private void CancelOperation()
    {
        ConsoleLog.Event("Cancelled");
        ResetToIdle();
    }

    private bool TakeTypeTextFlag()
    {
        var armed = typeTextArmed;
        typeTextArmed = false;
        return armed;
    }

    private void ResetToIdle()
    {
        if (state == DictationState.Recording && TakeTypeTextFlag())
        {
            ConsoleLog.Event("Type text disarmed");
        }

        generation++;
        operation?.Cancel();
        operation = null;
        session?.Dispose();
        session = null;
        SetState(DictationState.Idle);
    }

    private async Task ProcessRecordingAsync(RecordingSession recording, long id, bool typeText, bool voiceCommandsEnabled, WakeWordEdges wakeWordEdges, CancellationToken cancellationToken)
    {
        try
        {
            AudioClip clip;
            using (recording)
            {
                clip = await recording.StopAsync().ConfigureAwait(false);
            }

            if (wakeWordEdges.AtEnd)
            {
                clip = clip.WithoutTail(settings.WakeWordTrimSeconds);
            }

            var skipReason = GetSkipReason(clip);
            if (skipReason is not null)
            {
                FinishOperation(id, $"Skipped: {skipReason}");
                return;
            }

            if (!TryAdvance(id, DictationState.Transcribing))
            {
                return;
            }

            var result = await transcriber.TranscribeAsync(clip.Samples, cancellationToken).ConfigureAwait(false);
            if (!IsCurrent(id))
            {
                return;
            }

            var text = wakeWordEdges.Any ? wakeWordStripper.Strip(result.Text, wakeWordEdges) : result.Text;
            if (text.Length == 0)
            {
                FinishOperation(id, "Nothing recognized");
                return;
            }

            ConsoleLog.Event($"Recognized [{result.Language}]: {text}");
            history.Append(text, result.Language, clip.DurationSeconds);

            var command = voiceCommandsEnabled ? commandMatcher.Match(text) : null;
            if (command is not null)
            {
                ConsoleLog.Event($"Command: {command}");
            }

            var scriptRule = voiceCommandsEnabled && command is null ? scriptMatcher.Match(text) : null;
            if (scriptRule is not null)
            {
                ScriptLauncher.Launch(scriptRule);
                if (!scriptRule.OutputText)
                {
                    FinishOperation(id, null);
                    return;
                }
            }

            if (!TryAdvance(id, DictationState.Typing))
            {
                return;
            }

            ExecuteInput(command, text, typeText, cancellationToken);
            FinishOperation(id, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ConsoleLog.Error(ConsoleLog.Describe(ex));
            FinishOperation(id, null);
        }
    }

    private async Task PickAndTranscribeFilesAsync()
    {
        var files = await PickFilesAsync().ConfigureAwait(false);
        long id;
        bool typeText;
        CancellationToken cancellationToken;
        lock (sync)
        {
            filePickerOpen = false;
            if (disposed || files.Count == 0)
            {
                return;
            }

            if (state != DictationState.Idle)
            {
                ConsoleLog.Event("Busy: selected files ignored");
                return;
            }

            id = ++generation;
            var cancellation = new CancellationTokenSource();
            operation = cancellation;
            cancellationToken = cancellation.Token;
            typeText = TakeTypeTextFlag();
            SetState(DictationState.Transcribing);
        }

        await TranscribeFilesAsync(files, id, typeText, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        try
        {
            var files = await AudioFilePicker.PickAsync().ConfigureAwait(false);
            if (files.Count == 0)
            {
                ConsoleLog.Event("No files selected");
            }

            return files;
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"File dialog failed: {ConsoleLog.Describe(ex)}");
            return [];
        }
    }

    private async Task TranscribeFilesAsync(IReadOnlyList<string> files, long id, bool typeText, CancellationToken cancellationToken)
    {
        try
        {
            var texts = new List<string>();
            foreach (var file in files)
            {
                ConsoleLog.Event($"File: {file}");
                var text = await TranscribeFileAsync(file, cancellationToken).ConfigureAwait(false);
                if (!IsCurrent(id))
                {
                    return;
                }

                if (text is null)
                {
                    continue;
                }

                if (text.Length == 0)
                {
                    ConsoleLog.Event("Nothing recognized");
                    continue;
                }

                ConsoleLog.Event($"Recognized: {text}");
                texts.Add(text);
            }

            if (texts.Count == 0)
            {
                FinishOperation(id, null);
                return;
            }

            if (!TryAdvance(id, DictationState.Typing))
            {
                return;
            }

            typer.WaitForModifiersReleased(cancellationToken);
            OutputText(texts, typeText, cancellationToken);
            FinishOperation(id, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ConsoleLog.Error(ConsoleLog.Describe(ex));
            FinishOperation(id, null);
        }
    }

    private async Task<string?> TranscribeFileAsync(string file, CancellationToken cancellationToken)
    {
        try
        {
            var clip = AudioFileDecoder.Decode(file);
            if (clip.Samples.Length == 0)
            {
                return string.Empty;
            }

            var result = await transcriber.TranscribeAsync(clip.Samples, cancellationToken).ConfigureAwait(false);
            return result.Text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ConsoleLog.Error(ConsoleLog.Describe(ex));
            return null;
        }
    }

    private void ExecuteInput(VoiceCommandAction? command, string text, bool typeText, CancellationToken cancellationToken)
    {
        typer.WaitForModifiersReleased(cancellationToken);

        switch (command)
        {
            case VoiceCommandAction.Enter:
                RememberInput(typer.PressKey(VirtualKey.Enter, 1, cancellationToken));
                break;
            case VoiceCommandAction.Tab:
                RememberInput(typer.PressKey(VirtualKey.Tab, 1, cancellationToken));
                break;
            case VoiceCommandAction.Undo:
                var toErase = Volatile.Read(ref lastInputLength);
                var erased = typer.PressKey(VirtualKey.Back, toErase, cancellationToken);
                Interlocked.Add(ref lastInputLength, -erased);
                break;
            default:
                OutputText([text], typeText, cancellationToken);
                break;
        }
    }

    private void OutputText(IReadOnlyList<string> texts, bool typeText, CancellationToken cancellationToken)
    {
        if (!typeText)
        {
            ClipboardWriter.SetText(string.Join(Environment.NewLine, texts), cancellationToken);
            ConsoleLog.Event("Copied to clipboard");
            return;
        }

        var sent = 0;
        foreach (var text in texts)
        {
            sent += typer.TypeText(text, cancellationToken);
            sent += typer.PressKey(VirtualKey.Enter, 1, cancellationToken);
        }

        RememberInput(sent);
    }

    private void RememberInput(int sent)
    {
        if (sent > 0)
        {
            Volatile.Write(ref lastInputLength, sent);
        }
    }

    private string? GetSkipReason(AudioClip clip)
    {
        if (clip.DurationSeconds < settings.MinRecordingSeconds)
        {
            return "too short";
        }

        return clip.PeakWindowLevelDbfs() < settings.SilenceThresholdDbfs ? "silence" : null;
    }

    private bool IsCurrent(long id)
    {
        lock (sync)
        {
            return generation == id && !disposed;
        }
    }

    private bool TryAdvance(long id, DictationState next)
    {
        lock (sync)
        {
            if (generation != id || disposed)
            {
                return false;
            }

            SetState(next);
            return true;
        }
    }

    private void FinishOperation(long id, string? message)
    {
        lock (sync)
        {
            if (generation != id || disposed)
            {
                return;
            }

            if (message is not null)
            {
                ConsoleLog.Event(message);
            }

            operation = null;
            SetState(DictationState.Idle);
        }
    }

    private void SetState(DictationState next)
    {
        state = next;
        ConsoleLog.Event(next == DictationState.Idle ? "Idle" : $"{next}...");
        PublishIndicator();
    }

    private void PublishIndicator() => IndicatorChanged?.Invoke(CreateIndicator());

    private DictationIndicator CreateIndicator() =>
        new(state, recordingSource, typeTextArmed, systemAudioArmed, listening, settings.Backend == SpeechBackend.Api && apiLanguage.IsEnglish);
}
