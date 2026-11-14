using System.Text.RegularExpressions;
using Speecher.App;
using Speecher.Configuration;

namespace Speecher.Dictation;

public sealed record ScriptMatch(ScriptRule Rule, Match Match);

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

    public ScriptMatch? Match(string text)
    {
        foreach (var (regex, rule) in rules)
        {
            try
            {
                var match = regex.Match(text);
                if (match.Success)
                {
                    return new ScriptMatch(rule, match);
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
