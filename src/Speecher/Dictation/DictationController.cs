using Speecher.App;
using Speecher.Audio;
using Speecher.Configuration;
using Speecher.Hotkeys;
using Speecher.Input;
using Speecher.Speech;
using Speecher.Transcription;

namespace Speecher.Dictation;

public sealed class DictationController : IDisposable
{
    private readonly object sync = new();
    private readonly AppSettings settings;
    private readonly AudioRecorder recorder;
    private readonly ISpeechTranscriber transcriber;
    private readonly KeyboardTyper typer = new();
    private readonly VoiceCommandMatcher commandMatcher;
    private readonly ScriptTriggerMatcher scriptMatcher;
    private readonly HistoryWriter history = new();

    private DictationState state = DictationState.Idle;
    private long generation;
    private RecordingSession? session;
    private CancellationTokenSource? operation;
    private int lastInputLength;
    private bool typeTextArmed;
    private bool systemAudioArmed;
    private bool filePickerOpen;
    private AudioSource recordingSource;
    private bool disposed;

    public DictationController(AppSettings settings, ISpeechTranscriber transcriber)
    {
        this.settings = settings;
        this.transcriber = transcriber;
        recorder = new AudioRecorder(settings.MicrophoneName);
        commandMatcher = new VoiceCommandMatcher(settings.VoiceCommands);
        scriptMatcher = new ScriptTriggerMatcher(settings.ScriptTriggers);
    }

    public event Action<DictationIndicator>? IndicatorChanged;

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
                        StartRecording();
                        break;
                    case DictationState.Recording:
                        FinishRecording();
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
        }
    }

    private void StartRecording()
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
            FinishRecording();
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

    private void FinishRecording()
    {
        var recording = session!;
        session = null;
        var cancellation = new CancellationTokenSource();
        operation = cancellation;
        var typeText = TakeTypeTextFlag();
        state = DictationState.Transcribing;
        PublishIndicator();
        var id = generation;
        var voiceCommandsEnabled = recordingSource == AudioSource.Microphone;
        _ = Task.Run(() => ProcessRecordingAsync(recording, id, typeText, voiceCommandsEnabled, cancellation.Token));
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

    private async Task ProcessRecordingAsync(RecordingSession recording, long id, bool typeText, bool voiceCommandsEnabled, CancellationToken cancellationToken)
    {
        try
        {
            AudioClip clip;
            using (recording)
            {
                clip = await recording.StopAsync().ConfigureAwait(false);
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

            if (result.Text.Length == 0)
            {
                FinishOperation(id, "Nothing recognized");
                return;
            }

            ConsoleLog.Event($"Recognized [{result.Language}]: {result.Text}");
            history.Append(result.Text, result.Language, clip.DurationSeconds);

            var command = voiceCommandsEnabled ? commandMatcher.Match(result.Text) : null;
            if (command is not null)
            {
                ConsoleLog.Event($"Command: {command}");
            }

            var scriptRule = voiceCommandsEnabled && command is null ? scriptMatcher.Match(result.Text) : null;
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

            ExecuteInput(command, result.Text, typeText, cancellationToken);
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

    private void PublishIndicator() =>
        IndicatorChanged?.Invoke(new DictationIndicator(state, recordingSource, typeTextArmed, systemAudioArmed));
}
