using System.Text.RegularExpressions;
using Speecher.App;
using Speecher.Configuration;

namespace Speecher.Dictation;

public sealed class ScriptTriggerMatcher
{
    private readonly List<(Regex Regex, ScriptRule Rule)> rules = [];

    public ScriptTriggerMatcher(ScriptTriggerSettings settings)
    {
        if (!settings.Enabled)
        {
            return;
        }

        foreach (var rule in settings.Rules)
        {
            rules.Add((rule.CreateRegex(), rule));
        }
    }

    public ScriptRule? Match(string text)
    {
        foreach (var (regex, rule) in rules)
        {
            try
            {
                if (regex.IsMatch(text))
                {
                    return rule;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                ConsoleLog.Warning($"Script trigger pattern timed out: {rule.Pattern}");
            }
        }

        return null;
    }
}
