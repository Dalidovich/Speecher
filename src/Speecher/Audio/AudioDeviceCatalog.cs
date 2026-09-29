using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Speecher.Audio;

public static class AudioDeviceCatalog
{
    public static IReadOnlyList<AudioDeviceInfo> ListCaptureDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultId = TryGetDefault(enumerator) is { } defaultDevice ? ReadIdAndDispose(defaultDevice) : null;

        var devices = new List<AudioDeviceInfo>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (device)
            {
                devices.Add(new AudioDeviceInfo(device.FriendlyName, device.ID == defaultId));
            }
        }

        return devices;
    }

    public static MMDevice? Resolve(string microphoneName)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (string.IsNullOrEmpty(microphoneName))
        {
            return TryGetDefault(enumerator);
        }

        MMDevice? match = null;
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            if (match is null && string.Equals(device.FriendlyName, microphoneName, StringComparison.OrdinalIgnoreCase))
            {
                match = device;
            }
            else
            {
                device.Dispose();
            }
        }

        return match;
    }

    private static MMDevice? TryGetDefault(MMDeviceEnumerator enumerator)
    {
        try
        {
            return enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Console)
                ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)
                : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string ReadIdAndDispose(MMDevice device)
    {
        using (device)
        {
            return device.ID;
        }
    }
}
