using Speecher.App;
using Speecher.Audio;
using Speecher.Configuration;
using Speecher.Hotkeys;
using Speecher.Input;
using Speecher.Speech;

namespace Speecher.Dictation;

public sealed class DictationController : IDisposable
{
    private readonly object sync = new();
    private readonly AppSettings settings;
    private readonly OutputMode outputMode;
    private readonly bool voiceCommandsEnabled;
    private readonly AudioRecorder recorder;
    private readonly SpeechTranscriber transcriber;
    private readonly KeyboardTyper typer = new();
    private readonly VoiceCommandMatcher commandMatcher;
    private readonly HistoryWriter history = new();

    private DictationState state = DictationState.Idle;
    private long generation;
    private RecordingSession? session;
    private CancellationTokenSource? operation;
    private int lastInputLength;
    private bool disposed;

    public DictationController(AppSettings settings, OutputMode outputMode, AudioSource audioSource, SpeechTranscriber transcriber)
    {
        this.settings = settings;
        this.outputMode = outputMode;
        this.transcriber = transcriber;
        voiceCommandsEnabled = audioSource == AudioSource.Microphone;
        recorder = new AudioRecorder(audioSource, settings.MicrophoneName);
        commandMatcher = new VoiceCommandMatcher(settings.VoiceCommands);
    }

    public event Action<DictationState>? StateChanged;

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
        session = recorder.Start(
            settings.MaxRecordingSeconds,
            () => OnMaxDurationReached(id),
            exception => OnRecordingFaulted(id, exception));
        generation = id;
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
        state = DictationState.Transcribing;
        StateChanged?.Invoke(state);
        var id = generation;
        _ = Task.Run(() => ProcessRecordingAsync(recording, id, cancellation.Token));
    }

    private void CancelOperation()
    {
        ConsoleLog.Event("Cancelled");
        ResetToIdle();
    }

    private void ResetToIdle()
    {
        generation++;
        operation?.Cancel();
        operation = null;
        session?.Dispose();
        session = null;
        SetState(DictationState.Idle);
    }

    private async Task ProcessRecordingAsync(RecordingSession recording, long id, CancellationToken cancellationToken)
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

            if (!TryAdvance(id, DictationState.Typing))
            {
                return;
            }

            ExecuteInput(command, result.Text, cancellationToken);
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

    private void ExecuteInput(VoiceCommandAction? command, string text, CancellationToken cancellationToken)
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
                OutputText(text, cancellationToken);
                break;
        }
    }

    private void OutputText(string text, CancellationToken cancellationToken)
    {
        if (outputMode == OutputMode.Clipboard)
        {
            ClipboardWriter.SetText(text, cancellationToken);
            ConsoleLog.Event("Copied to clipboard");
            return;
        }

        var typed = typer.TypeText(text, cancellationToken);
        RememberInput(typed + typer.PressKey(VirtualKey.Enter, 1, cancellationToken));
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
        StateChanged?.Invoke(next);
    }
}
