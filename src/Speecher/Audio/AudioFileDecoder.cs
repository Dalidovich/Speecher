using System.Runtime.InteropServices;
using Concentus;
using Concentus.Oggfile;
using NAudio.Wave;

namespace Speecher.Audio;

public static class AudioFileDecoder
{
    private const int HeaderProbeLength = 64;

    private static readonly byte[] OggSignature = "OggS"u8.ToArray();
    private static readonly byte[] OpusSignature = "OpusHead"u8.ToArray();
    private static readonly WaveFormat OpusFormat = new(48000, 16, 1);

    public static AudioClip Decode(string path)
    {
        return IsOggOpus(path) ? DecodeOggOpus(path) : DecodeWithMediaFoundation(path);
    }

    private static bool IsOggOpus(string path)
    {
        var header = new byte[HeaderProbeLength];
        using var stream = File.OpenRead(path);
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        var probe = header.AsSpan(0, read);
        return probe.StartsWith(OggSignature) && probe.IndexOf(OpusSignature) >= 0;
    }

    private static AudioClip DecodeOggOpus(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = OpusCodecFactory.CreateDecoder(OpusFormat.SampleRate, OpusFormat.Channels);
        var reader = new OpusOggReadStream(decoder, stream);
        using var pcm = new MemoryStream();
        while (reader.HasNextPacket)
        {
            var packet = reader.DecodeNextPacket();
            if (packet is not null)
            {
                pcm.Write(MemoryMarshal.AsBytes(packet.AsSpan()));
            }
        }

        return AudioConverter.ToWhisperFormat(pcm.ToArray(), OpusFormat);
    }

    private static AudioClip DecodeWithMediaFoundation(string path)
    {
        try
        {
            using var reader = new MediaFoundationReader(path);
            return AudioConverter.ToWhisperFormat(reader.ToSampleProvider());
        }
        catch (COMException ex)
        {
            throw new InvalidDataException($"Not a supported audio file (0x{ex.ErrorCode:X8}).", ex);
        }
    }
}
