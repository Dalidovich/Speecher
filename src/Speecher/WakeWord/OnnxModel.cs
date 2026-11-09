using Microsoft.ML.OnnxRuntime;

namespace Speecher.WakeWord;

public sealed class OnnxModel : IDisposable
{
    private readonly InferenceSession session;
    private readonly RunOptions runOptions = new();
    private readonly string[] inputNames;
    private readonly string[] outputNames;

    public OnnxModel(string modelPath)
    {
        using var options = new SessionOptions
        {
            InterOpNumThreads = 1,
            IntraOpNumThreads = 1,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR,
        };
        session = new InferenceSession(modelPath, options);
        inputNames = [session.InputMetadata.Keys.First()];
        outputNames = [session.OutputMetadata.Keys.First()];
        InputDimensions = session.InputMetadata[inputNames[0]].Dimensions;
    }

    public IReadOnlyList<int> InputDimensions { get; }

    public float[] Run(float[] input, long[] shape)
    {
        using var inputValue = OrtValue.CreateTensorValueFromMemory(input, shape);
        using var outputs = session.Run(runOptions, inputNames, [inputValue], outputNames);
        return outputs[0].GetTensorDataAsSpan<float>().ToArray();
    }

    public void Dispose()
    {
        runOptions.Dispose();
        session.Dispose();
    }
}
