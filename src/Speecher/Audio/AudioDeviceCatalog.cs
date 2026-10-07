using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Speecher.Audio;

public static class AudioDeviceCatalog
{
    public static IReadOnlyList<AudioDeviceInfo> ListCaptureDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultId = TryGetDefault(enumerator, DataFlow.Capture, Role.Console) is { } defaultDevice ? ReadIdAndDispose(defaultDevice) : null;

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

    public static string? FindDefaultOutputName()
    {
        using var device = ResolveDefaultOutput();
        return device?.FriendlyName;
    }

    public static MMDevice? ResolveDefaultOutput()
    {
        using var enumerator = new MMDeviceEnumerator();
        return TryGetDefault(enumerator, DataFlow.Render, Role.Multimedia);
    }

    public static MMDevice? Resolve(string microphoneName)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (string.IsNullOrEmpty(microphoneName))
        {
            return TryGetDefault(enumerator, DataFlow.Capture, Role.Console);
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

    private static MMDevice? TryGetDefault(MMDeviceEnumerator enumerator, DataFlow flow, Role role)
    {
        try
        {
            return enumerator.HasDefaultAudioEndpoint(flow, role)
                ? enumerator.GetDefaultAudioEndpoint(flow, role)
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
