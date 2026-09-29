using NAudio.Wave;

namespace Speecher.Audio;

public sealed class MonoDownmixSampleProvider(ISampleProvider source) : ISampleProvider
{
    private readonly int channels = source.WaveFormat.Channels;
    private float[] sourceBuffer = [];

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

    public int Read(float[] buffer, int offset, int count)
    {
        var needed = count * channels;
        if (sourceBuffer.Length < needed)
        {
            sourceBuffer = new float[needed];
        }

        var read = source.Read(sourceBuffer, 0, needed);
        var frames = read / channels;
        for (var frame = 0; frame < frames; frame++)
        {
            float sum = 0;
            var baseIndex = frame * channels;
            for (var channel = 0; channel < channels; channel++)
            {
                sum += sourceBuffer[baseIndex + channel];
            }

            buffer[offset + frame] = sum / channels;
        }

        return frames;
    }
}
