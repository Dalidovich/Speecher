using NAudio.CoreAudioApi;
using Speecher.Configuration;

namespace Speecher.Audio;

public sealed class AudioRecorder(AudioSource source, string microphoneName)
{
    public RecordingSession Start(double maxSeconds, Action onMaxDurationReached, Action<Exception> onFaulted)
    {
        var loopback = source == AudioSource.System;
        var device = ResolveDevice(loopback);

        RecordingSession? session = null;
        try
        {
            session = new RecordingSession(device, loopback, maxSeconds, onMaxDurationReached, onFaulted);
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

    private MMDevice ResolveDevice(bool loopback)
    {
        if (loopback)
        {
            return AudioDeviceCatalog.ResolveDefaultOutput()
                ?? throw new InvalidOperationException("Output device is not available.");
        }

        return AudioDeviceCatalog.Resolve(microphoneName)
            ?? throw new InvalidOperationException("Recording device is not available.");
    }
}
