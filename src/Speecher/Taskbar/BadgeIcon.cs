using System.Buffers.Binary;
using Speecher.Interop;

namespace Speecher.Taskbar;

internal static class BadgeIcon
{
    private const int HeaderSize = 40;
    private const int BytesPerPixel = 4;
    private const float CornerShare = 0.35f;
    private const float DotShare = 0.22f;
    private const float BottomBandShare = 1f / 3f;
    private const uint White = 0xFFFFFF;

    public static nint Create(int size, uint rgb, bool square, bool dot, uint? borderRgb, uint? bottomRgb)
    {
        var pixelBytes = size * size * BytesPerPixel;
        var maskBytes = (size + 31) / 32 * 4 * size;
        var image = new byte[HeaderSize + pixelBytes + maskBytes];
        WriteHeader(image, size);

        var half = size / 2f;
        var border = borderRgb is null ? Math.Max(1f, size / 16f) : Math.Max(2f, size / 8f);
        var ring = borderRgb ?? White;
        var dotRadius = size * DotShare;
        var bandTop = size * (1f - BottomBandShare);
        var band = bottomRgb ?? rgb;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - half;
                var dy = y + 0.5f - half;
                var outer = Coverage(ShapeDistance(dx, dy, half, square));
                var inner = Coverage(ShapeDistance(dx, dy, half - border, square));
                var hole = dot ? Coverage(CircleDistance(dx, dy, dotRadius)) : 0f;
                var body = 1f - hole;
                var inBand = bottomRgb is null ? 0f : Coverage(bandTop - (y + 0.5f));
                var offset = HeaderSize + (y * size + x) * BytesPerPixel;
                for (var channel = 0; channel < 3; channel++)
                {
                    var shift = channel * 8;
                    var fillValue = Blend(rgb >> shift & 0xFF, band >> shift & 0xFF, inBand);
                    var bodyValue = Blend(White >> shift & 0xFF, fillValue, body);
                    image[offset + channel] = (byte)MathF.Round(Blend(ring >> shift & 0xFF, bodyValue, inner));
                }

                image[offset + 3] = (byte)MathF.Round(outer * 255f);
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

    private static float ShapeDistance(float dx, float dy, float half, bool square) =>
        square ? RoundedSquareDistance(dx, dy, half, half * CornerShare) : CircleDistance(dx, dy, half);

    private static float CircleDistance(float dx, float dy, float radius) => MathF.Sqrt(dx * dx + dy * dy) - radius;

    private static float RoundedSquareDistance(float dx, float dy, float half, float corner)
    {
        var qx = MathF.Abs(dx) - (half - corner);
        var qy = MathF.Abs(dy) - (half - corner);
        var outside = MathF.Sqrt(MathF.Pow(MathF.Max(qx, 0f), 2) + MathF.Pow(MathF.Max(qy, 0f), 2));
        var inside = MathF.Min(MathF.Max(qx, qy), 0f);
        return outside + inside - corner;
    }

    private static float Coverage(float distance) => Math.Clamp(0.5f - distance, 0f, 1f);

    private static float Blend(float from, float to, float weight) => from + (to - from) * weight;
}
