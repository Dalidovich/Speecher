using Speecher.Configuration;

namespace Speecher.Dictation;

public sealed record DictationIndicator(DictationState State, AudioSource RecordingSource, bool TypeTextArmed, bool SystemAudioArmed, bool WakeWordListening, bool EnglishApiLanguage)
{
    public bool ShowsSystemAudio => State == DictationState.Recording ? RecordingSource == AudioSource.System : SystemAudioArmed;
}
