using Speecher.Configuration;

namespace Speecher.Localization;

public static class DefaultVoiceCommands
{
    public static Dictionary<VoiceCommandAction, List<string>> Create() =>
        Enum.GetValues<VoiceCommandAction>().ToDictionary(action => action, action => LocalizedPhrases.GetAll(action.ToString()));
}
