namespace Speecher.Localization;

public static class DefaultWakeWordPhrases
{
    private const string ResourceKey = "WakeWord";

    public static List<string> Create() => LocalizedPhrases.GetAll(ResourceKey);
}
