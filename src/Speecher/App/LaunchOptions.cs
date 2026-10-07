using Speecher.Configuration;

namespace Speecher.App;

public sealed record LaunchOptions(OutputMode? OutputMode, bool TranscribeFiles)
{
    public const string OutputArgument = "--output";
    public const string FilesArgument = "--files";

    public static LaunchOptions Parse(string[] args)
    {
        OutputMode? outputMode = null;
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
            if (!string.Equals(name, OutputArgument, StringComparison.OrdinalIgnoreCase))
            {
                throw new StartupException($"Unknown command line argument \"{argument}\".");
            }

            if (!Enum.TryParse<OutputMode>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new StartupException($"{OutputArgument} expects one of: {string.Join(", ", Enum.GetNames<OutputMode>())}.");
            }

            outputMode = parsed;
        }

        if (transcribeFiles && outputMode is not null)
        {
            throw new StartupException($"{FilesArgument} cannot be combined with {OutputArgument}.");
        }

        return new LaunchOptions(outputMode, transcribeFiles);
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
