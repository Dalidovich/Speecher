using NAudio.Wave;
using Speecher.App;
using Speecher.Audio;

namespace Speecher.Dictation;

public static class RecordingExporter
{
    private const string FileNameFormat = "yyyyMMdd-HHmmss";

    public static void Save(AudioClip clip)
    {
        try
        {
            Directory.CreateDirectory(PortablePaths.ExportDirectory);
            var path = CreateUniquePath();
            using var writer = new WaveFileWriter(path, new WaveFormat(AudioClip.SampleRate, 16, 1));
            writer.WriteSamples(ToPcm16(clip.Samples), 0, clip.Samples.Length);
            ConsoleLog.Event($"Saved recording: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ConsoleLog.Warning($"Recording was not saved: {ConsoleLog.Describe(ex)}");
        }
    }

    private static string CreateUniquePath()
    {
        var name = DateTime.Now.ToString(FileNameFormat);
        var path = Path.Combine(PortablePaths.ExportDirectory, $"{name}.wav");
        for (var suffix = 2; File.Exists(path); suffix++)
        {
            path = Path.Combine(PortablePaths.ExportDirectory, $"{name}-{suffix}.wav");
        }

        return path;
    }

    private static short[] ToPcm16(float[] samples)
    {
        var pcm = new short[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            pcm[i] = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
        }

        return pcm;
    }
}
