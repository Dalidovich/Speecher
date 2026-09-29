using System.Globalization;
using System.Resources;
using Speecher.Configuration;

namespace Speecher.Localization;

public static class DefaultVoiceCommands
{
    private static readonly ResourceManager Phrases = new("Speecher.Localization.VoicePhrases", typeof(DefaultVoiceCommands).Assembly);

    private static readonly CultureInfo[] PhraseCultures = [CultureInfo.GetCultureInfo("ru"), CultureInfo.InvariantCulture];

    public static Dictionary<VoiceCommandAction, List<string>> Create()
    {
        var commands = new Dictionary<VoiceCommandAction, List<string>>();
        foreach (var action in Enum.GetValues<VoiceCommandAction>())
        {
            commands[action] = PhraseCultures
                .Select(culture => Phrases.GetString(action.ToString(), culture))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return commands;
    }
}
