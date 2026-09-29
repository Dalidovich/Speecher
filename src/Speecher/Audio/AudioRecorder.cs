namespace Speecher.Audio;

public sealed class AudioRecorder(string microphoneName)
{
    public RecordingSession Start(double maxSeconds, Action onMaxDurationReached, Action<Exception> onFaulted)
    {
        var device = AudioDeviceCatalog.Resolve(microphoneName)
            ?? throw new InvalidOperationException("Recording device is not available.");

        RecordingSession? session = null;
        try
        {
            session = new RecordingSession(device, maxSeconds, onMaxDurationReached, onFaulted);
            session.Start();
            return session;
        }
        catch
        {
            if (session is null)
            {
                device.Dispose();
            }
            else
            {
                session.Dispose();
            }

            throw;
        }
    }
}
