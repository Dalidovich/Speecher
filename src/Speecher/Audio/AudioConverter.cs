using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Speecher.Audio;

public static class AudioConverter
{
    public static AudioClip ToWhisperFormat(byte[] data, WaveFormat format)
    {
        using var stream = new RawSourceWaveStream(new MemoryStream(data), format);
        return ToWhisperFormat(stream.ToSampleProvider());
    }

    public static AudioClip ToWhisperFormat(ISampleProvider provider)
    {
        if (provider.WaveFormat.Channels > 1)
        {
            provider = new MonoDownmixSampleProvider(provider);
        }

        if (provider.WaveFormat.SampleRate != AudioClip.SampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, AudioClip.SampleRate);
        }

        return new AudioClip(ReadAll(provider));
    }

    private static float[] ReadAll(ISampleProvider provider)
    {
        var samples = new List<float>();
        var buffer = new float[AudioClip.SampleRate];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            samples.AddRange(buffer.AsSpan(0, read));
        }

        return samples.ToArray();
    }
}
