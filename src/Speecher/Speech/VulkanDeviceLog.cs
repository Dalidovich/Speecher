using System.Globalization;
using System.Text.RegularExpressions;

namespace Speecher.Speech;

public static partial class VulkanDeviceLog
{
    public static IReadOnlyList<VulkanDevice> ParseDevices(string log)
    {
        var devices = new Dictionary<int, VulkanDevice>();
        foreach (Match match in DevicePattern().Matches(log))
        {
            var index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture);
            devices[index] = new VulkanDevice(
                index,
                match.Groups["name"].Value,
                match.Groups["uma"].Value != "0",
                match.Groups["fp16"].Value != "0");
        }

        return devices.Values.OrderBy(device => device.Index).ToList();
    }

    public static int? ParseActiveDevice(string log)
    {
        var matches = ActiveDevicePattern().Matches(log);
        return matches.Count == 0
            ? null
            : int.Parse(matches[^1].Groups["index"].Value, CultureInfo.InvariantCulture);
    }

    public static VulkanDevice? Choose(IReadOnlyList<VulkanDevice> devices, string gpuName)
    {
        return string.IsNullOrWhiteSpace(gpuName)
            ? devices.FirstOrDefault(device => !device.IsIntegrated && device.SupportsFp16)
            : devices.FirstOrDefault(device => device.Name.Contains(gpuName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"ggml_vulkan:\s*(?<index>\d+)\s*=\s*(?<name>[^|\r\n]+?)(?:\s+\([^()|\r\n]*\))?\s*\|\s*uma:\s*(?<uma>\d+)\s*\|\s*fp16:\s*(?<fp16>\d+)")]
    private static partial Regex DevicePattern();

    [GeneratedRegex(@"(?:using Vulkan(?<index>\d+) backend|Vulkan(?<index>\d+) total size)")]
    private static partial Regex ActiveDevicePattern();
}
