namespace Speecher.App;

public sealed record LaunchOptions(bool TranscribeFiles)
{
    public const string FilesArgument = "--files";

    public static LaunchOptions Parse(string[] args)
    {
        var transcribeFiles = false;
        foreach (var argument in args)
        {
            if (!string.Equals(argument, FilesArgument, StringComparison.OrdinalIgnoreCase))
            {
                throw new StartupException($"Unknown command line argument \"{argument}\".");
            }

            transcribeFiles = true;
        }

        return new LaunchOptions(transcribeFiles);
    }
}
