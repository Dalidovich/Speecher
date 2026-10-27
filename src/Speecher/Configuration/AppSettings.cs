using Speecher.Localization;

namespace Speecher.Configuration;

public sealed class AppSettings
{
    public const string DefaultHotkey = "Ctrl+Alt+Space";

    public string Hotkey { get; set; } = DefaultHotkey;

    public string MicrophoneName { get; set; } = string.Empty;

    public OutputMode DefaultOutputMode { get; set; } = OutputMode.Clipboard;

    public double MaxRecordingSeconds { get; set; } = 120;

    public double MinRecordingSeconds { get; set; } = 0.5;

    public double SilenceThresholdDbfs { get; set; } = -45;

    public SpeechBackend Backend { get; set; } = SpeechBackend.Vulkan;

    public string GpuName { get; set; } = string.Empty;

    public int CpuThreads { get; set; }

    public Dictionary<VoiceCommandAction, List<string>> VoiceCommands { get; set; } = DefaultVoiceCommands.Create();

    public ScriptTriggerSettings ScriptTriggers { get; set; } = new();

    public void FillMissingValues()
    {
        Hotkey ??= DefaultHotkey;
        MicrophoneName ??= string.Empty;
        GpuName ??= string.Empty;
        VoiceCommands ??= DefaultVoiceCommands.Create();
        ScriptTriggers ??= new();
        ScriptTriggers.FillMissingValues();
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

        if (!Enum.IsDefined(Backend))
        {
            return nameof(Backend);
        }

        if (CpuThreads < 0)
        {
            return nameof(CpuThreads);
        }

        var invalidTriggerField = ScriptTriggers.FindInvalidField();
        if (invalidTriggerField is not null)
        {
            return $"{nameof(ScriptTriggers)}.{invalidTriggerField}";
        }

        return null;
    }
}
