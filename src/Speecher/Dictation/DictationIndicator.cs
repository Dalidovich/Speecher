using Speecher.Configuration;

namespace Speecher.Dictation;

public sealed record DictationIndicator(DictationState State, AudioSource RecordingSource, bool TypeTextArmed, bool SystemAudioArmed, bool WakeWordListening)
{
    public static DictationIndicator Initial { get; } = new(DictationState.Idle, AudioSource.Microphone, false, false, false);

    public bool ShowsSystemAudio => State == DictationState.Recording ? RecordingSource == AudioSource.System : SystemAudioArmed;
}
