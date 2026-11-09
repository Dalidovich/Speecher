using Speecher.App;

namespace Speecher.WakeWord;

public sealed class WakeWordDetector : IDisposable
{
    private const int PredictionHistoryLength = 30;
    private const int WarmupPredictionCount = 5;

    private readonly AudioFeatureExtractor featureExtractor;
    private readonly OnnxModel classifier;
    private readonly int embeddingWindow;
    private readonly Queue<float> predictionHistory = new(PredictionHistoryLength + 1);

    public WakeWordDetector(WakeWordModel model)
    {
        featureExtractor = new AudioFeatureExtractor(model.MelspectrogramPath, model.EmbeddingPath);
        classifier = new OnnxModel(model.ClassifierPath);
        embeddingWindow = ResolveEmbeddingWindow(model);

        if (embeddingWindow > featureExtractor.FeatureFrameCount)
        {
            featureExtractor.Dispose();
            classifier.Dispose();
            throw new InvalidDataException(
                $"embeddingWindow {embeddingWindow} exceeds the {featureExtractor.FeatureFrameCount} warm-up feature frames.");
        }
    }

    public int EmbeddingWindow => embeddingWindow;

    public float Process(ReadOnlySpan<short> samples)
    {
        var preparedSamples = featureExtractor.Process(samples);
        var chunkSamples = AudioFeatureExtractor.ChunkSamples;

        float score;
        if (preparedSamples > chunkSamples)
        {
            score = 0f;
            for (var chunkOffset = preparedSamples / chunkSamples - 1; chunkOffset >= 0; chunkOffset--)
            {
                score = Math.Max(score, Classify(featureExtractor.GetFeatures(embeddingWindow, chunkOffset)));
            }
        }
        else if (preparedSamples == chunkSamples)
        {
            score = Classify(featureExtractor.GetFeatures(embeddingWindow));
        }
        else
        {
            score = predictionHistory.Count > 0 ? predictionHistory.Last() : 0f;
        }

        if (predictionHistory.Count < WarmupPredictionCount)
        {
            score = 0f;
        }

        predictionHistory.Enqueue(score);
        if (predictionHistory.Count > PredictionHistoryLength)
        {
            predictionHistory.Dequeue();
        }

        return score;
    }

    public void Reset()
    {
        predictionHistory.Clear();
        featureExtractor.Reset();
    }

    public void Dispose()
    {
        featureExtractor.Dispose();
        classifier.Dispose();
    }

    private float Classify(float[] features)
    {
        var output = classifier.Run(features, [1, embeddingWindow, AudioFeatureExtractor.EmbeddingDimension]);
        return output.Length > 0 ? output[0] : 0f;
    }

    private int ResolveEmbeddingWindow(WakeWordModel model)
    {
        var dimensions = classifier.InputDimensions;
        if (dimensions.Count >= 2 && dimensions[1] > 0)
        {
            if (dimensions[1] != model.Manifest.EmbeddingWindow)
            {
                ConsoleLog.Warning(
                    $"model.json embeddingWindow={model.Manifest.EmbeddingWindow} but classifier expects {dimensions[1]}; using {dimensions[1]}.");
            }

            return dimensions[1];
        }

        return model.Manifest.EmbeddingWindow;
    }
}
