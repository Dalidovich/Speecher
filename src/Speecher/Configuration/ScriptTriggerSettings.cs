namespace Speecher.Configuration;

public sealed class ScriptTriggerSettings
{
    public bool Enabled { get; set; }

    public List<ScriptRule> Rules { get; set; } = [];

    public void FillMissingValues()
    {
        Rules ??= [];
        Rules.RemoveAll(rule => rule is null);
        foreach (var rule in Rules)
        {
            rule.Pattern ??= string.Empty;
            rule.Path ??= string.Empty;
            rule.Command ??= string.Empty;
        }
    }

    public string? FindInvalidField()
    {
        if (!Enabled)
        {
            return null;
        }

        for (var index = 0; index < Rules.Count; index++)
        {
            var rule = Rules[index];
            var prefix = $"{nameof(Rules)}[{index}]";
            if (!IsValidPattern(rule))
            {
                return $"{prefix}.{nameof(ScriptRule.Pattern)}";
            }

            var hasPath = !string.IsNullOrWhiteSpace(rule.Path);
            var hasCommand = !string.IsNullOrWhiteSpace(rule.Command);
            if (hasPath == hasCommand)
            {
                return $"{prefix}.{nameof(ScriptRule.Path)}/{nameof(ScriptRule.Command)}";
            }

            if (!Enum.IsDefined(rule.Shell))
            {
                return $"{prefix}.{nameof(ScriptRule.Shell)}";
            }
        }

        return null;
    }

    private static bool IsValidPattern(ScriptRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Pattern))
        {
            return false;
        }

        try
        {
            rule.CreateRegex();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
