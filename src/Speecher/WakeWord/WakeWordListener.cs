using Speecher.Audio;

namespace Speecher.WakeWord;

public sealed class WakeWordListener : IDisposable
{
    private readonly object sync = new();
    private readonly WakeWordDetector detector;
    private readonly float threshold;
    private readonly string microphoneName;
    private ListeningSession? session;
    private bool disposed;

    private WakeWordListener(WakeWordDetector detector, float threshold, string microphoneName)
    {
        this.detector = detector;
        this.threshold = threshold;
        this.microphoneName = microphoneName;
    }

    public event Action? Detected;

    public event Action<Exception>? Faulted;

    public float Threshold => threshold;

    public static WakeWordListener Load(WakeWordModel model, double? threshold, string microphoneName)
    {
        var detector = new WakeWordDetector(model);
        return new WakeWordListener(detector, (float)(threshold ?? model.Manifest.RecommendedThreshold), microphoneName);
    }

    public void Start()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (session is not null)
            {
                return;
            }

            var device = AudioDeviceCatalog.Resolve(microphoneName)
                ?? throw new InvalidOperationException("Recording device is not available.");
            ListeningSession? started = null;
            try
            {
                started = new ListeningSession(device, detector, threshold, OnDetected, exception => OnFaulted(started, exception));
                started.Start();
                session = started;
            }
            catch
            {
                if (started is null)
                {
                    device.Dispose();
                }
                else
                {
                    started.Dispose();
                }

                throw;
            }
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            session?.Dispose();
            session = null;
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            session?.Dispose();
            session = null;
            detector.Dispose();
        }
    }

    private void OnDetected()
    {
        lock (sync)
        {
            if (session is null)
            {
                return;
            }
        }

        Detected?.Invoke();
    }

    private void OnFaulted(ListeningSession? faulted, Exception exception)
    {
        lock (sync)
        {
            if (faulted is null || session != faulted)
            {
                return;
            }

            session = null;
        }

        faulted.Dispose();
        Faulted?.Invoke(exception);
    }
}
