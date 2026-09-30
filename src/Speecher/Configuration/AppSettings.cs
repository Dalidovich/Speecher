using Speecher.Localization;

namespace Speecher.Configuration;

public sealed class AppSettings
{
    public const string DefaultHotkey = "Ctrl+Alt+Space";

    public string Hotkey { get; set; } = DefaultHotkey;

    public string MicrophoneName { get; set; } = string.Empty;

    public OutputMode DefaultOutputMode { get; set; } = OutputMode.Type;

    public double MaxRecordingSeconds { get; set; } = 120;

    public double MinRecordingSeconds { get; set; } = 0.5;

    public double SilenceThresholdDbfs { get; set; } = -45;

    public Dictionary<VoiceCommandAction, List<string>> VoiceCommands { get; set; } = DefaultVoiceCommands.Create();

    public void FillMissingValues()
    {
        Hotkey ??= DefaultHotkey;
        MicrophoneName ??= string.Empty;
        VoiceCommands ??= DefaultVoiceCommands.Create();
    }

    public string? FindInvalidField()
    {
        if (!Enum.IsDefined(DefaultOutputMode))
        {
            return nameof(DefaultOutputMode);
        }

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

        return null;
    }
}
