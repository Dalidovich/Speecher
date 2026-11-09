using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Speecher.Audio;

namespace Speecher.WakeWord;

internal sealed class ListeningSession : IDisposable
{
    private const int IdleWaitMilliseconds = 200;
    private const int ReadBufferSamples = AudioFeatureExtractor.ChunkSamples * 4;
    private static readonly TimeSpan BufferDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(1.5);

    private readonly MMDevice device;
    private readonly WasapiCapture capture;
    private readonly BufferedWaveProvider captured;
    private readonly ISampleProvider converted;
    private readonly WakeWordDetector detector;
    private readonly float threshold;
    private readonly Action onDetected;
    private readonly Action<Exception> onFaulted;
    private readonly AutoResetEvent dataReady = new(false);
    private readonly CancellationTokenSource stop = new();
    private readonly Thread worker;
    private int disposed;

    public ListeningSession(MMDevice device, WakeWordDetector detector, float threshold, Action onDetected, Action<Exception> onFaulted)
    {
        this.device = device;
        this.detector = detector;
        this.threshold = threshold;
        this.onDetected = onDetected;
        this.onFaulted = onFaulted;
        capture = new WasapiCapture(device);
        captured = new BufferedWaveProvider(capture.WaveFormat)
        {
            ReadFully = false,
            BufferDuration = BufferDuration,
            DiscardOnBufferOverflow = true
        };
        converted = AudioConverter.ToWhisperSampleProvider(captured.ToSampleProvider());
        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;
        worker = new Thread(RunDetection) { IsBackground = true, Name = "WakeWord" };
    }

    public void Start()
    {
        detector.Reset();
        worker.Start();
        capture.StartRecording();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        stop.Cancel();
        try
        {
            capture.Dispose();
        }
        catch (Exception)
        {
        }

        if (worker.IsAlive)
        {
            worker.Join(TimeSpan.FromSeconds(2));
        }

        device.Dispose();
        dataReady.Dispose();
        stop.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (stop.IsCancellationRequested)
        {
            return;
        }

        captured.AddSamples(e.Buffer, 0, e.BytesRecorded);
        dataReady.Set();
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null && !stop.IsCancellationRequested)
        {
            var exception = e.Exception;
            ThreadPool.QueueUserWorkItem(_ => onFaulted(exception));
        }
    }

    private void RunDetection()
    {
        try
        {
            DetectUntilStopped();
        }
        catch (Exception ex)
        {
            if (!stop.IsCancellationRequested)
            {
                ThreadPool.QueueUserWorkItem(_ => onFaulted(ex));
            }
        }
    }

    private void DetectUntilStopped()
    {
        var samples = new float[ReadBufferSamples];
        var chunk = new short[AudioFeatureExtractor.ChunkSamples];
        var chunkFill = 0;
        var sinceTrigger = new Stopwatch();
        var waitHandles = new[] { dataReady, stop.Token.WaitHandle };

        while (!stop.IsCancellationRequested)
        {
            WaitHandle.WaitAny(waitHandles, IdleWaitMilliseconds);
            int read;
            while (!stop.IsCancellationRequested && (read = converted.Read(samples, 0, samples.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    chunk[chunkFill++] = ToPcm16(samples[i]);
                    if (chunkFill < chunk.Length)
                    {
                        continue;
                    }

                    chunkFill = 0;
                    var score = detector.Process(chunk);
                    if (score >= threshold && (!sinceTrigger.IsRunning || sinceTrigger.Elapsed >= Cooldown))
                    {
                        sinceTrigger.Restart();
                        ThreadPool.QueueUserWorkItem(_ => onDetected());
                    }
                }
            }
        }
    }

    private static short ToPcm16(float sample) => (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
}
