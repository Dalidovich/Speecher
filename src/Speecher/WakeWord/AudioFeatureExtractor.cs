namespace Speecher.WakeWord;

public sealed class AudioFeatureExtractor : IDisposable
{
    public const int ChunkSamples = 1280;
    public const int MelBins = 32;
    public const int EmbeddingDimension = 96;
    public const int EmbeddingWindowFrames = 76;
    public const int EmbeddingStepFrames = 8;

    private const int SampleRate = 16000;
    private const int RawBufferMaxSamples = SampleRate * 10;
    private const int MelContextSamples = 160 * 3;
    private const int MelBufferMaxFrames = 10 * 97;
    private const int FeatureBufferMaxFrames = 120;
    private const int WarmupNoiseSamples = SampleRate * 4;
    private const int WarmupNoiseAmplitude = 1000;
    private const int WarmupNoiseSeed = 1234;

    private readonly OnnxModel melspectrogramModel;
    private readonly OnnxModel embeddingModel;
    private readonly List<short> rawBuffer = new(RawBufferMaxSamples * 2);
    private readonly List<float[]> melBuffer = new(MelBufferMaxFrames + 64);
    private readonly List<float[]> featureBuffer = new(FeatureBufferMaxFrames + 16);
    private short[] rawRemainder = [];
    private int accumulatedSamples;

    public AudioFeatureExtractor(string melspectrogramPath, string embeddingPath)
    {
        melspectrogramModel = new OnnxModel(melspectrogramPath);
        embeddingModel = new OnnxModel(embeddingPath);
        Reset();
    }

    public int FeatureFrameCount => featureBuffer.Count;

    public void Reset()
    {
        rawBuffer.Clear();
        melBuffer.Clear();
        for (var frame = 0; frame < EmbeddingWindowFrames; frame++)
        {
            melBuffer.Add(Enumerable.Repeat(1f, MelBins).ToArray());
        }

        accumulatedSamples = 0;
        rawRemainder = [];
        featureBuffer.Clear();
        featureBuffer.AddRange(ComputeClipEmbeddings(CreateWarmupNoise()));
    }

    public int Process(ReadOnlySpan<short> samples)
    {
        var processedSamples = 0;
        var input = PrependRemainder(samples);

        if (accumulatedSamples + input.Length >= ChunkSamples)
        {
            var remainder = (accumulatedSamples + input.Length) % ChunkSamples;
            var evenLength = input.Length - remainder;
            AppendRaw(input.AsSpan(0, evenLength));
            accumulatedSamples += evenLength;
            rawRemainder = remainder == 0 ? [] : input.AsSpan(evenLength).ToArray();
        }
        else
        {
            accumulatedSamples += input.Length;
            AppendRaw(input);
        }

        if (accumulatedSamples >= ChunkSamples && accumulatedSamples % ChunkSamples == 0)
        {
            UpdateMelBuffer(accumulatedSamples);

            for (var chunkOffset = accumulatedSamples / ChunkSamples - 1; chunkOffset >= 0; chunkOffset--)
            {
                var windowEnd = melBuffer.Count - EmbeddingStepFrames * chunkOffset;
                var windowStart = windowEnd - EmbeddingWindowFrames;
                if (windowStart >= 0)
                {
                    featureBuffer.Add(ComputeEmbedding(melBuffer, windowStart));
                }
            }

            processedSamples = accumulatedSamples;
            accumulatedSamples = 0;
        }

        TrimFront(featureBuffer, FeatureBufferMaxFrames);

        return processedSamples != 0 ? processedSamples : accumulatedSamples;
    }

    public float[] GetFeatures(int frameCount, int endOffsetFromLatest = 0)
    {
        var end = featureBuffer.Count - endOffsetFromLatest;
        var start = end - frameCount;
        if (start < 0)
        {
            throw new InvalidOperationException(
                $"Feature buffer holds {featureBuffer.Count} frames, {frameCount + endOffsetFromLatest} required.");
        }

        var features = new float[frameCount * EmbeddingDimension];
        for (var frame = 0; frame < frameCount; frame++)
        {
            featureBuffer[start + frame].CopyTo(features, frame * EmbeddingDimension);
        }

        return features;
    }

    public void Dispose()
    {
        melspectrogramModel.Dispose();
        embeddingModel.Dispose();
    }

    private short[] PrependRemainder(ReadOnlySpan<short> samples)
    {
        if (rawRemainder.Length == 0)
        {
            return samples.ToArray();
        }

        var combined = new short[rawRemainder.Length + samples.Length];
        rawRemainder.CopyTo(combined, 0);
        samples.CopyTo(combined.AsSpan(rawRemainder.Length));
        rawRemainder = [];
        return combined;
    }

    private void AppendRaw(ReadOnlySpan<short> samples)
    {
        foreach (var sample in samples)
        {
            rawBuffer.Add(sample);
        }

        if (rawBuffer.Count > RawBufferMaxSamples)
        {
            rawBuffer.RemoveRange(0, rawBuffer.Count - RawBufferMaxSamples);
        }
    }

    private void UpdateMelBuffer(int newSamples)
    {
        var takeCount = Math.Min(rawBuffer.Count, newSamples + MelContextSamples);
        var window = new short[takeCount];
        rawBuffer.CopyTo(rawBuffer.Count - takeCount, window, 0, takeCount);

        melBuffer.AddRange(ComputeMelspectrogram(window));
        TrimFront(melBuffer, MelBufferMaxFrames);
    }

    private List<float[]> ComputeMelspectrogram(short[] samples)
    {
        var input = new float[samples.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            input[index] = samples[index];
        }

        var output = melspectrogramModel.Run(input, [1, samples.Length]);
        var frameCount = output.Length / MelBins;
        var frames = new List<float[]>(frameCount);
        for (var frame = 0; frame < frameCount; frame++)
        {
            var row = new float[MelBins];
            for (var bin = 0; bin < MelBins; bin++)
            {
                row[bin] = output[frame * MelBins + bin] / 10f + 2f;
            }

            frames.Add(row);
        }

        return frames;
    }

    private float[] ComputeEmbedding(List<float[]> melFrames, int windowStart)
    {
        var input = new float[EmbeddingWindowFrames * MelBins];
        for (var frame = 0; frame < EmbeddingWindowFrames; frame++)
        {
            melFrames[windowStart + frame].CopyTo(input, frame * MelBins);
        }

        var output = embeddingModel.Run(input, [1, EmbeddingWindowFrames, MelBins, 1]);
        if (output.Length != EmbeddingDimension)
        {
            throw new InvalidDataException($"Embedding model returned {output.Length} values, expected {EmbeddingDimension}.");
        }

        return output;
    }

    private List<float[]> ComputeClipEmbeddings(short[] samples)
    {
        var melFrames = ComputeMelspectrogram(samples);
        var embeddings = new List<float[]>();
        for (var windowStart = 0; windowStart + EmbeddingWindowFrames <= melFrames.Count; windowStart += EmbeddingStepFrames)
        {
            embeddings.Add(ComputeEmbedding(melFrames, windowStart));
        }

        return embeddings;
    }

    private static short[] CreateWarmupNoise()
    {
        var random = new Random(WarmupNoiseSeed);
        var noise = new short[WarmupNoiseSamples];
        for (var index = 0; index < noise.Length; index++)
        {
            noise[index] = (short)random.Next(-WarmupNoiseAmplitude, WarmupNoiseAmplitude);
        }

        return noise;
    }

    private static void TrimFront(List<float[]> buffer, int maxCount)
    {
        if (buffer.Count > maxCount)
        {
            buffer.RemoveRange(0, buffer.Count - maxCount);
        }
    }
}
