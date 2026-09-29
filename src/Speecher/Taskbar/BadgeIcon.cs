using System.Buffers.Binary;
using Speecher.Interop;

namespace Speecher.Taskbar;

internal static class BadgeIcon
{
    private const int HeaderSize = 40;
    private const int BytesPerPixel = 4;

    public static nint CreateCircle(int size, uint rgb)
    {
        var pixelBytes = size * size * BytesPerPixel;
        var maskBytes = (size + 31) / 32 * 4 * size;
        var image = new byte[HeaderSize + pixelBytes + maskBytes];
        WriteHeader(image, size);

        var radius = size / 2f;
        var fillRadius = radius - Math.Max(1f, size / 16f);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - radius;
                var dy = y + 0.5f - radius;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                var fill = Coverage(fillRadius, distance);
                var offset = HeaderSize + (y * size + x) * BytesPerPixel;
                image[offset] = TintFromWhite(rgb & 0xFF, fill);
                image[offset + 1] = TintFromWhite((rgb >> 8) & 0xFF, fill);
                image[offset + 2] = TintFromWhite((rgb >> 16) & 0xFF, fill);
                image[offset + 3] = (byte)MathF.Round(Coverage(radius, distance) * 255f);
            }
        }

        return User32.CreateIconFromResourceEx(image, (uint)image.Length, true, User32.IconResourceVersion, size, size, 0);
    }

    private static void WriteHeader(byte[] image, int size)
    {
        var header = image.AsSpan(0, HeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header, HeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], size);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], size * 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[12..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[14..], 32);
    }

    private static float Coverage(float radius, float distance) => Math.Clamp(radius - distance + 0.5f, 0f, 1f);

    private static byte TintFromWhite(uint channel, float weight) => (byte)MathF.Round(255f + (channel - 255f) * weight);
}
