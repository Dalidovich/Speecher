using Speecher.Localization;

namespace Speecher.Configuration;

public sealed class AppSettings
{
    public const string DefaultHotkey = "Ctrl+Alt+Space";
    public const string DefaultTypeTextHotkey = "Ctrl+Alt+T";
    public const string DefaultSystemAudioHotkey = "Ctrl+Alt+S";
    public const string DefaultTranscribeFilesHotkey = "Ctrl+Alt+F";
    public const string DefaultWakeWordHotkey = "Ctrl+Alt+W";
    public const string DefaultWakeWordModel = "fuck";

    private const double MaxWakeWordTrimSeconds = 5;

    public string Hotkey { get; set; } = DefaultHotkey;

    public string TypeTextHotkey { get; set; } = DefaultTypeTextHotkey;

    public string SystemAudioHotkey { get; set; } = DefaultSystemAudioHotkey;

    public string TranscribeFilesHotkey { get; set; } = DefaultTranscribeFilesHotkey;

    public string WakeWordHotkey { get; set; } = DefaultWakeWordHotkey;

    public string MicrophoneName { get; set; } = string.Empty;

    public double MaxRecordingSeconds { get; set; } = 120;

    public double MinRecordingSeconds { get; set; } = 0.5;

    public double SilenceThresholdDbfs { get; set; } = -45;

    public SpeechBackend Backend { get; set; } = SpeechBackend.Vulkan;

    public string GpuName { get; set; } = string.Empty;

    public int CpuThreads { get; set; }

    public ApiTranscriptionSettings Api { get; set; } = new();

    public Dictionary<VoiceCommandAction, List<string>> VoiceCommands { get; set; } = DefaultVoiceCommands.Create();

    public ScriptTriggerSettings ScriptTriggers { get; set; } = new();

    public bool WakeWordListening { get; set; }

    public string WakeWordModel { get; set; } = DefaultWakeWordModel;

    public double? WakeWordThreshold { get; set; }

    public double WakeWordTrimSeconds { get; set; } = 1;

    public List<string> WakeWordPhrases { get; set; } = DefaultWakeWordPhrases.Create();

    public void FillMissingValues()
    {
        Hotkey ??= DefaultHotkey;
        TypeTextHotkey ??= DefaultTypeTextHotkey;
        SystemAudioHotkey ??= DefaultSystemAudioHotkey;
        TranscribeFilesHotkey ??= DefaultTranscribeFilesHotkey;
        WakeWordHotkey ??= DefaultWakeWordHotkey;
        MicrophoneName ??= string.Empty;
        GpuName ??= string.Empty;
        Api ??= new();
        Api.FillMissingValues();
        VoiceCommands ??= DefaultVoiceCommands.Create();
        ScriptTriggers ??= new();
        ScriptTriggers.FillMissingValues();
        WakeWordModel ??= DefaultWakeWordModel;
        WakeWordPhrases ??= DefaultWakeWordPhrases.Create();
    }

    public string? FindInvalidField()
    {
        if (MaxRecordingSeconds <= 0 || double.IsNaN(MaxRecordingSeconds))
        {
            return nameof(MaxRecordingSeconds);
        }

        if (MinRecordingSeconds < 0 || MinRecordingSeconds >= MaxRecordingSeconds || double.IsNaN(MinRecordingSeconds))
        {
            return nameof(MinRecordingSeconds);
        }

        if (SilenceThresholdDbfs > 0 || double.IsNaN(SilenceThresholdDbfs))
        {
            return nameof(SilenceThresholdDbfs);
        }

        if (!Enum.IsDefined(Backend))
        {
            return nameof(Backend);
        }

        if (CpuThreads < 0)
        {
            return nameof(CpuThreads);
        }

        if (string.IsNullOrWhiteSpace(WakeWordModel) || WakeWordModel.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return nameof(WakeWordModel);
        }

        if (WakeWordThreshold is { } threshold && (threshold <= 0 || threshold > 1 || double.IsNaN(threshold)))
        {
            return nameof(WakeWordThreshold);
        }

        if (WakeWordTrimSeconds < 0 || WakeWordTrimSeconds > MaxWakeWordTrimSeconds || double.IsNaN(WakeWordTrimSeconds))
        {
            return nameof(WakeWordTrimSeconds);
        }

        if (Backend == SpeechBackend.Api)
        {
            var invalidApiField = Api.FindInvalidField();
            if (invalidApiField is not null)
            {
                return $"{nameof(Api)}.{invalidApiField}";
            }
        }

        var invalidTriggerField = ScriptTriggers.FindInvalidField();
        if (invalidTriggerField is not null)
        {
            return $"{nameof(ScriptTriggers)}.{invalidTriggerField}";
        }

        return null;
    }
}
