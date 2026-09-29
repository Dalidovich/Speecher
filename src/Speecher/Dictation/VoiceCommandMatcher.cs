using System.Text;
using Speecher.Configuration;

namespace Speecher.Dictation;

public sealed class VoiceCommandMatcher
{
    private readonly Dictionary<string, VoiceCommandAction> phrases = new(StringComparer.Ordinal);

    public VoiceCommandMatcher(IReadOnlyDictionary<VoiceCommandAction, List<string>> commands)
    {
        foreach (var (action, actionPhrases) in commands)
        {
            foreach (var phrase in actionPhrases ?? [])
            {
                var normalized = Normalize(phrase ?? string.Empty);
                if (normalized.Length > 0)
                {
                    phrases.TryAdd(normalized, action);
                }
            }
        }
    }

    public VoiceCommandAction? Match(string text)
    {
        return phrases.TryGetValue(Normalize(text), out var action) ? action : null;
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text.ToLowerInvariant())
        {
            if (char.IsPunctuation(character))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
