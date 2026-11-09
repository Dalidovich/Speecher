namespace Speecher.WakeWord;

public sealed class ModelManifest
{
    public string Name { get; set; } = "";

    public string WakePhrase { get; set; } = "";

    public string ClassifierFile { get; set; } = "";

    public string MelspectrogramFile { get; set; } = "melspectrogram.onnx";

    public string EmbeddingFile { get; set; } = "embedding_model.onnx";

    public int SampleRate { get; set; } = 16000;

    public int ChunkSamples { get; set; } = 1280;

    public int EmbeddingWindow { get; set; } = 16;

    public double RecommendedThreshold { get; set; } = 0.5;

    public string TrainedAtUtc { get; set; } = "";
}
