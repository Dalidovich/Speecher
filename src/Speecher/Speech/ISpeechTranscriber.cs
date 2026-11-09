namespace Speecher.Speech;

public interface ISpeechTranscriber : IDisposable
{
    string Backend { get; }

    Task<TranscriptionResult> TranscribeAsync(float[] samples, CancellationToken cancellationToken);
}
