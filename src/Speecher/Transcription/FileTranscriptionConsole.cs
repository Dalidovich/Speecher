using Speecher.App;
using Speecher.Audio;
using Speecher.Input;
using Speecher.Speech;

namespace Speecher.Transcription;

public sealed class FileTranscriptionConsole(SpeechTranscriber transcriber)
{
    private const string NothingRecognized = "(nothing recognized)";

    private static readonly string Separator = new('-', 60);

    private static readonly EnumerationOptions RecursiveEnumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true
    };

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var input = await PromptAsync("File or directory path: ", cancellationToken).ConfigureAwait(false);
            if (input is null)
            {
                return;
            }

            var path = NormalizePath(input);
            if (path.Length == 0)
            {
                continue;
            }

            try
            {
                await ProcessPathAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ConsoleLog.Error(ConsoleLog.Describe(ex));
            }
        }
    }

    private async Task ProcessPathAsync(string path, CancellationToken cancellationToken)
    {
        if (Directory.Exists(path))
        {
            await TranscribeDirectoryAsync(path, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (File.Exists(path))
        {
            await TranscribeToClipboardAsync(path, cancellationToken).ConfigureAwait(false);
            return;
        }

        ConsoleLog.Error($"Path \"{path}\" was not found.");
    }

    private async Task TranscribeToClipboardAsync(string file, CancellationToken cancellationToken)
    {
        var text = await TranscribeAsync(file, cancellationToken).ConfigureAwait(false);
        if (text.Length == 0)
        {
            ConsoleLog.Info(NothingRecognized);
            return;
        }

        ConsoleLog.Info(text);
        ClipboardWriter.SetText(text, cancellationToken);
        ConsoleLog.Info("Copied to clipboard");
    }

    private async Task TranscribeDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        var files = Directory.GetFiles(directory, "*", RecursiveEnumeration);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        if (files.Length == 0)
        {
            ConsoleLog.Info($"Directory \"{directory}\" contains no files.");
            return;
        }

        var answer = await PromptAsync(
            $"\"{directory}\" is a directory with {files.Length} file(s) including subfolders. Press Enter to transcribe all of them, or type anything to cancel: ",
            cancellationToken).ConfigureAwait(false);
        if (answer is null || answer.Trim().Length > 0)
        {
            ConsoleLog.Info("Cancelled");
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConsoleLog.Info(Separator);
            ConsoleLog.Info(file);
            ConsoleLog.Info(await DescribeTranscriptionAsync(file, cancellationToken).ConfigureAwait(false));
        }

        ConsoleLog.Info(Separator);
    }

    private async Task<string> DescribeTranscriptionAsync(string file, CancellationToken cancellationToken)
    {
        try
        {
            var text = await TranscribeAsync(file, cancellationToken).ConfigureAwait(false);
            return text.Length == 0 ? NothingRecognized : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Error: {ConsoleLog.Describe(ex)}";
        }
    }

    private async Task<string> TranscribeAsync(string file, CancellationToken cancellationToken)
    {
        var clip = AudioFileDecoder.Decode(file);
        if (clip.Samples.Length == 0)
        {
            return string.Empty;
        }

        var result = await transcriber.TranscribeAsync(clip.Samples, cancellationToken).ConfigureAwait(false);
        return result.Text;
    }

    private static async Task<string?> PromptAsync(string prompt, CancellationToken cancellationToken)
    {
        Console.Write(prompt);
        var line = await Task.Run(Console.ReadLine, CancellationToken.None).WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return line;
    }

    private static string NormalizePath(string input)
    {
        var path = input.Trim().Trim('"').Trim();
        return path.Length == 0 ? path : Path.GetFullPath(path);
    }
}
