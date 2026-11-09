using Speecher.Configuration;
using Speecher.Dictation;

namespace Speecher.Taskbar;

public static class WindowTitle
{
    private const string AppName = "Speecher";
    private const string Separator = " · ";

    public static void Show(DictationIndicator indicator)
    {
        try
        {
            Console.Title = Compose(indicator);
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
        }
    }

    private static string Compose(DictationIndicator indicator)
    {
        var parts = new List<string> { AppName, DescribeState(indicator) };
        if (indicator.TypeTextArmed)
        {
            parts.Add("[Type text]");
        }

        if (indicator.SystemAudioArmed)
        {
            parts.Add("[System audio]");
        }

        if (indicator.WakeWordListening)
        {
            parts.Add("[Listening]");
        }

        return string.Join(Separator, parts);
    }

    private static string DescribeState(DictationIndicator indicator) => indicator.State switch
    {
        DictationState.Recording when indicator.RecordingSource == AudioSource.System => "Recording system audio",
        DictationState.Recording => "Recording microphone",
        _ => indicator.State.ToString()
    };
}
