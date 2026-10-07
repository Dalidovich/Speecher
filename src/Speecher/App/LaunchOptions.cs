using Speecher.Configuration;

namespace Speecher.App;

public sealed record LaunchOptions(OutputMode? OutputMode, AudioSource AudioSource, bool TranscribeFiles)
{
    public const string OutputArgument = "--output";
    public const string SourceArgument = "--source";
    public const string FilesArgument = "--files";

    public static LaunchOptions Parse(string[] args)
    {
        OutputMode? outputMode = null;
        AudioSource? audioSource = null;
        var transcribeFiles = false;
        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            if (string.Equals(argument, FilesArgument, StringComparison.OrdinalIgnoreCase))
            {
                transcribeFiles = true;
                continue;
            }

            var (name, value) = SplitArgument(args, ref i);
            if (string.Equals(name, OutputArgument, StringComparison.OrdinalIgnoreCase))
            {
                outputMode = ParseEnum<OutputMode>(OutputArgument, value);
            }
            else if (string.Equals(name, SourceArgument, StringComparison.OrdinalIgnoreCase))
            {
                audioSource = ParseEnum<AudioSource>(SourceArgument, value);
            }
            else
            {
                throw new StartupException($"Unknown command line argument \"{argument}\".");
            }
        }

        if (transcribeFiles && outputMode is not null)
        {
            throw new StartupException($"{FilesArgument} cannot be combined with {OutputArgument}.");
        }

        if (transcribeFiles && audioSource is not null)
        {
            throw new StartupException($"{FilesArgument} cannot be combined with {SourceArgument}.");
        }

        return new LaunchOptions(outputMode, audioSource ?? AudioSource.Microphone, transcribeFiles);
    }

    private static T ParseEnum<T>(string argumentName, string? value) where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new StartupException($"{argumentName} expects one of: {string.Join(", ", Enum.GetNames<T>())}.");
        }

        return parsed;
    }

    private static (string Name, string? Value) SplitArgument(string[] args, ref int index)
    {
        var argument = args[index];
        var separator = argument.IndexOf('=');
        if (separator >= 0)
        {
            return (argument[..separator], argument[(separator + 1)..]);
        }

        if (index + 1 < args.Length)
        {
            index++;
            return (argument, args[index]);
        }

        return (argument, null);
    }
}
