using System.Globalization;
using System.Resources;

namespace Speecher.Localization;

public static class LocalizedPhrases
{
    private static readonly ResourceManager Phrases = new("Speecher.Localization.VoicePhrases", typeof(LocalizedPhrases).Assembly);

    private static readonly CultureInfo[] PhraseCultures = [CultureInfo.GetCultureInfo("ru"), CultureInfo.InvariantCulture];

    public static List<string> GetAll(string key) =>
        PhraseCultures
            .Select(culture => Phrases.GetString(key, culture))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
