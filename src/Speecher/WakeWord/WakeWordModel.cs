using System.Text.Json;

namespace Speecher.WakeWord;

public sealed record WakeWordModel(
    string Name,
    ModelManifest Manifest,
    string MelspectrogramPath,
    string EmbeddingPath,
    string ClassifierPath)
{
    private const string ManifestFileName = "model.json";
    private const int SupportedSampleRate = 16000;
    private const int SupportedChunkSamples = 1280;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static WakeWordModel Load(string directory)
    {
        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Wake word model not found: {manifestPath}", manifestPath);
        }

        ModelManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ModelManifest>(File.ReadAllText(manifestPath), SerializerOptions)
                ?? throw new InvalidDataException($"Wake word manifest is empty: {manifestPath}");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Wake word manifest is not valid JSON: {manifestPath} ({ex.Message})", ex);
        }

        Validate(manifest, manifestPath);

        return new WakeWordModel(
            Path.GetFileName(directory),
            manifest,
            RequireFile(directory, manifest.MelspectrogramFile),
            RequireFile(directory, manifest.EmbeddingFile),
            RequireFile(directory, manifest.ClassifierFile));
    }

    private static void Validate(ModelManifest manifest, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.ClassifierFile))
        {
            throw new InvalidDataException($"\"classifierFile\" is missing in {manifestPath}");
        }

        if (manifest.SampleRate != SupportedSampleRate)
        {
            throw new InvalidDataException($"Unsupported sampleRate {manifest.SampleRate} in {manifestPath}; only {SupportedSampleRate} is supported.");
        }

        if (manifest.ChunkSamples != SupportedChunkSamples)
        {
            throw new InvalidDataException($"Unsupported chunkSamples {manifest.ChunkSamples} in {manifestPath}; only {SupportedChunkSamples} is supported.");
        }

        if (manifest.EmbeddingWindow <= 0)
        {
            throw new InvalidDataException($"\"embeddingWindow\" must be positive in {manifestPath}");
        }
    }

    private static string RequireFile(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Wake word model file not found: {path}", path);
        }

        return path;
    }
}
