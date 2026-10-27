using System.Text.RegularExpressions;

namespace Speecher.Configuration;

public sealed class ScriptRule
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    public string Pattern { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public ScriptShell Shell { get; set; } = ScriptShell.Cmd;

    public bool OutputText { get; set; }

    public Regex CreateRegex() =>
        new(Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
}
