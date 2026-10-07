using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Speecher.Audio;

public sealed class RecordingSession : IDisposable
{
    private readonly MMDevice device;
    private readonly WasapiCapture capture;
    private readonly MemoryStream buffer = new();
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly long maxBytes;
    private readonly Action onMaxDurationReached;
    private readonly Action<Exception> onFaulted;
    private int maxDurationSignaled;
    private int disposed;
    private volatile bool stopRequested;

    public RecordingSession(MMDevice device, bool loopback, double maxSeconds, Action onMaxDurationReached, Action<Exception> onFaulted)
    {
        this.device = device;
        this.onMaxDurationReached = onMaxDurationReached;
        this.onFaulted = onFaulted;
        capture = loopback ? new WasapiLoopbackCapture(device) : new WasapiCapture(device);

        var format = capture.WaveFormat;
        var maxFrames = (long)(maxSeconds * format.SampleRate);
        maxBytes = Math.Max(1, maxFrames) * format.BlockAlign;

        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;
    }

    public void Start() => capture.StartRecording();

    public async Task<AudioClip> StopAsync()
    {
        stopRequested = true;
        capture.StopRecording();
        await stopped.Task.ConfigureAwait(false);

        byte[] data;
        lock (buffer)
        {
            data = buffer.ToArray();
        }

        return AudioConverter.ToWhisperFormat(data, capture.WaveFormat);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        stopRequested = true;
        try
        {
            capture.Dispose();
        }
        catch (Exception)
        {
        }

        device.Dispose();
        lock (buffer)
        {
            buffer.Dispose();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        bool limitReached;
        lock (buffer)
        {
            if (!buffer.CanWrite)
            {
                return;
            }

            var remaining = maxBytes - buffer.Length;
            var toWrite = (int)Math.Min(e.BytesRecorded, Math.Max(0, remaining));
            buffer.Write(e.Buffer, 0, toWrite);
            limitReached = buffer.Length >= maxBytes;
        }

        if (limitReached && Interlocked.Exchange(ref maxDurationSignaled, 1) == 0)
        {
            ThreadPool.QueueUserWorkItem(_ => onMaxDurationReached());
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is null)
        {
            stopped.TrySetResult();
            return;
        }

        stopped.TrySetException(e.Exception);
        if (!stopRequested)
        {
            var exception = e.Exception;
            ThreadPool.QueueUserWorkItem(_ => onFaulted(exception));
        }
    }
}
