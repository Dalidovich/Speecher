using System.Text;
using Speecher.App;
using Speecher.Configuration;
using Whisper.net;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

namespace Speecher.Speech;

public sealed class SpeechTranscriber : IDisposable
{
    private const string Russian = "ru";
    private const string English = "en";
    private const string AutoDetect = "auto";
    private const int MaxVulkanLoadAttempts = 2;

    private readonly WhisperFactory factory;
    private readonly int? cpuThreads;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SpeechTranscriber(WhisperFactory factory, string backend, int? cpuThreads)
    {
        this.factory = factory;
        this.cpuThreads = cpuThreads;
        Backend = backend;
    }

    public string Backend { get; }

    public static SpeechTranscriber Load(string modelPath, AppSettings settings)
    {
        RuntimeOptions.LibraryPath = PortablePaths.ExecutablePath;
        switch (settings.Backend)
        {
            case SpeechBackend.Vulkan:
                RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];
                return LoadVulkan(modelPath, settings);
            case SpeechBackend.Cuda:
                RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cuda12, RuntimeLibrary.Cpu];
                return LoadCuda(modelPath, settings);
            default:
                RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cpu];
                return LoadCpu(modelPath, settings);
        }
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

    private static SpeechTranscriber LoadVulkan(string modelPath, AppSettings settings)
    {
        var log = new StringBuilder();
        using var logger = LogProvider.AddLogger((_, message) =>
        {
            lock (log)
            {
                log.Append(message);
            }
        });

        var deviceIndex = 0;
        for (var attempt = 0; attempt < MaxVulkanLoadAttempts; attempt++)
        {
            var attemptStart = ReadLog(log).Length;
            var factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = true, GpuDevice = deviceIndex });
            if (RuntimeOptions.LoadedLibrary != RuntimeLibrary.Vulkan)
            {
                factory.Dispose();
                ConsoleLog.Warning("Vulkan runtime is not available, falling back to CPU.");
                return LoadCpu(modelPath, settings);
            }

            var text = ReadLog(log);
            var wanted = VulkanDeviceLog.Choose(VulkanDeviceLog.ParseDevices(text), settings.GpuName);
            if (wanted is null)
            {
                factory.Dispose();
                ConsoleLog.Warning(string.IsNullOrWhiteSpace(settings.GpuName)
                    ? "No suitable Vulkan GPU found, falling back to CPU."
                    : $"Vulkan GPU \"{settings.GpuName}\" was not found, falling back to CPU.");
                return LoadCpu(modelPath, settings);
            }

            if (VulkanDeviceLog.ParseActiveDevice(text[attemptStart..]) == wanted.Index)
            {
                return new SpeechTranscriber(factory, $"Vulkan ({wanted.Name})", null);
            }

            factory.Dispose();
            deviceIndex = wanted.Index;
        }

        ConsoleLog.Warning("Could not confirm the selected Vulkan GPU, falling back to CPU.");
        return LoadCpu(modelPath, settings);
    }

    private static SpeechTranscriber LoadCuda(string modelPath, AppSettings settings)
    {
        var factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = true, UseFlashAttention = true });
        if (RuntimeOptions.LoadedLibrary == RuntimeLibrary.Cuda12)
        {
            return new SpeechTranscriber(factory, "CUDA", null);
        }

        ConsoleLog.Warning("CUDA runtime is not available, falling back to CPU.");
        return OnCpu(factory, settings);
    }

    private static SpeechTranscriber LoadCpu(string modelPath, AppSettings settings)
    {
        var factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = false });
        return OnCpu(factory, settings);
    }

    private static SpeechTranscriber OnCpu(WhisperFactory factory, AppSettings settings)
    {
        var threads = settings.CpuThreads > 0 ? settings.CpuThreads : Environment.ProcessorCount;
        return new SpeechTranscriber(factory, $"CPU ({threads} threads)", threads);
    }

    private static string ReadLog(StringBuilder log)
    {
        lock (log)
        {
            return log.ToString();
        }
    }

    private WhisperProcessorBuilder CreateBuilder()
    {
        var builder = factory.CreateBuilder();
        return cpuThreads is { } threads ? builder.WithThreads(threads) : builder;
    }

    private string DetectRussianOrEnglish(float[] samples)
    {
        using var processor = CreateBuilder().Build();
        var (detected, _) = processor.DetectLanguageWithProbability(samples, Russian, English);
        return detected == Russian ? Russian : English;
    }

    private async Task<(string Text, string? Language)> RunAsync(float[] samples, string language, CancellationToken cancellationToken)
    {
        await using var processor = CreateBuilder()
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
