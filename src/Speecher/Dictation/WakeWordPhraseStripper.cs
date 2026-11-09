using System.Text.RegularExpressions;

namespace Speecher.Dictation;

public sealed class WakeWordPhraseStripper
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly char[] DanglingSeparators = [' ', ',', ';', ':', '-', '–', '—'];

    private readonly Regex? leading;
    private readonly Regex? trailing;

    public WakeWordPhraseStripper(IEnumerable<string> phrases)
    {
        var alternatives = phrases
            .Select(phrase => (phrase ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(words => words.Length > 0)
            .Select(words => string.Join(@"\s+", words.Select(Regex.Escape)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (alternatives.Count == 0)
        {
            return;
        }

        var phrase = $@"(?<!\w)(?:{string.Join('|', alternatives)})(?!\w)";
        leading = new Regex($@"^\W*{phrase}\W*", Options);
        trailing = new Regex($@"\s*{phrase}\W*$", Options);
    }

    public string Strip(string text, WakeWordEdges edges)
    {
        if (edges.AtStart && leading is not null)
        {
            text = RemoveRepeatedly(leading, text);
        }

        if (edges.AtEnd && trailing is not null)
        {
            text = RemoveRepeatedly(trailing, text).TrimEnd(DanglingSeparators);
        }

        return text.Trim();
    }

    private static string RemoveRepeatedly(Regex pattern, string text)
    {
        string previous;
        do
        {
            previous = text;
            text = pattern.Replace(text, string.Empty);
        }
        while (text.Length != previous.Length);

        return text;
    }
}
