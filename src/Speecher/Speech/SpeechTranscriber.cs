using System.Text;
using Speecher.App;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Speecher.Speech;

public sealed class SpeechTranscriber : IDisposable
{
    private const string Russian = "ru";
    private const string English = "en";
    private const string AutoDetect = "auto";

    private readonly WhisperFactory factory;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SpeechTranscriber(WhisperFactory factory)
    {
        this.factory = factory;
    }

    public string Backend => RuntimeOptions.LoadedLibrary == RuntimeLibrary.Cuda12 ? "CUDA" : "CPU";

    public static SpeechTranscriber Load(string modelPath)
    {
        RuntimeOptions.LibraryPath = PortablePaths.ExecutablePath;
        RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cuda12, RuntimeLibrary.Cpu];

        var factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = true, UseFlashAttention = true });
        return new SpeechTranscriber(factory);
    }

    public async Task<TranscriptionResult> TranscribeAsync(float[] samples, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (text, language) = await RunAsync(samples, AutoDetect, cancellationToken).ConfigureAwait(false);
            if (text.Length == 0 || language is Russian or English)
            {
                return new TranscriptionResult(text, language ?? English);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var restricted = DetectRussianOrEnglish(samples);
            var (restrictedText, _) = await RunAsync(samples, restricted, cancellationToken).ConfigureAwait(false);
            return new TranscriptionResult(restrictedText, restricted);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        if (gate.Wait(TimeSpan.FromSeconds(2)))
        {
            factory.Dispose();
        }
    }

    private string DetectRussianOrEnglish(float[] samples)
    {
        using var processor = factory.CreateBuilder().Build();
        var (detected, _) = processor.DetectLanguageWithProbability(samples, Russian, English);
        return detected == Russian ? Russian : English;
    }

    private async Task<(string Text, string? Language)> RunAsync(float[] samples, string language, CancellationToken cancellationToken)
    {
        await using var processor = factory.CreateBuilder()
            .WithLanguage(language)
            .Build();

        var text = new StringBuilder();
        string? detectedLanguage = null;
        await foreach (var segment in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
        {
            text.Append(segment.Text);
            detectedLanguage ??= segment.Language;
        }

        return (text.ToString().Trim(), detectedLanguage);
    }
}
