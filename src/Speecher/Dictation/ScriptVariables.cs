using System.Text.RegularExpressions;

namespace Speecher.Dictation;

public static class ScriptVariables
{
    public const string TextName = "SPEECHER_TEXT";

    private const string Prefix = "SPEECHER_";
    private static readonly char[] TrimmedPunctuation = ['.', ',', '!', '?', ';', ':', '…'];

    public static IReadOnlyDictionary<string, string> Build(string text, Match match)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TextName] = text.Trim()
        };

        foreach (Group group in match.Groups)
        {
            if (!group.Success || IsUnnamed(group.Name))
            {
                continue;
            }

            var value = Clean(group.Value);
            if (value.Length > 0)
            {
                variables[Prefix + group.Name.ToUpperInvariant()] = value;
            }
        }

        return variables;
    }

    private static bool IsUnnamed(string name)
    {
        return name.All(char.IsAsciiDigit);
    }

    private static string Clean(string value)
    {
        return value
            .Replace("\"", string.Empty)
            .Trim()
            .TrimStart(TrimmedPunctuation)
            .TrimEnd(TrimmedPunctuation)
            .Trim();
    }
}
