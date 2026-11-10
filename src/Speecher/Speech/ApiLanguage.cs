namespace Speecher.Speech;

public sealed class ApiLanguage(string initial)
{
    private const string English = "en";
    private const string Russian = "ru";

    private readonly object sync = new();
    private string current = initial;

    public string Current
    {
        get
        {
            lock (sync)
            {
                return current;
            }
        }
    }

    public bool IsEnglish => IsEnglishCode(Current);

    public string Toggle()
    {
        lock (sync)
        {
            current = IsEnglishCode(current) ? Russian : English;
            return current;
        }
    }

    private static bool IsEnglishCode(string language) => string.Equals(language, English, StringComparison.OrdinalIgnoreCase);
}
