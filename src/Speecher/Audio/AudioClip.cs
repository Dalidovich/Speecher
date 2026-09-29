namespace Speecher.Audio;

public sealed class AudioClip(float[] samples)
{
    public const int SampleRate = 16000;

    private const int LevelWindowSamples = SampleRate / 20;

    public float[] Samples { get; } = samples;

    public double DurationSeconds => (double)Samples.Length / SampleRate;

    public double PeakWindowLevelDbfs()
    {
        double maxMeanSquare = 0;
        for (var start = 0; start < Samples.Length; start += LevelWindowSamples)
        {
            var end = Math.Min(start + LevelWindowSamples, Samples.Length);
            double sum = 0;
            for (var i = start; i < end; i++)
            {
                sum += Samples[i] * (double)Samples[i];
            }

            maxMeanSquare = Math.Max(maxMeanSquare, sum / (end - start));
        }

        return maxMeanSquare > 0 ? 10 * Math.Log10(maxMeanSquare) : double.NegativeInfinity;
    }
}
