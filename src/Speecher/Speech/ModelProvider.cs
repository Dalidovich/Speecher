using Speecher.App;

namespace Speecher.Speech;

public static class ModelProvider
{
    public const string ModelFileName = "ggml-large-v3-turbo.bin";

    private const string ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/" + ModelFileName;

    public static async Task<string> EnsureModelAsync(FileDownloader downloader, CancellationToken cancellationToken)
    {
        var modelPath = Path.Combine(PortablePaths.ModelsDirectory, ModelFileName);
        if (File.Exists(modelPath))
        {
            return modelPath;
        }

        ConsoleLog.Info($"Downloading model {ModelFileName}...");
        try
        {
            Directory.CreateDirectory(PortablePaths.ModelsDirectory);
            FileDownloader.DeletePartial(modelPath);
            await downloader.DownloadAsync(ModelUrl, modelPath, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StartupException($"Failed to download model {ModelFileName}: {ConsoleLog.Describe(ex)}");
        }

        ConsoleLog.Info("Model downloaded.");
        return modelPath;
    }
}
