using System.Buffers.Binary;
using Speecher.Interop;

namespace Speecher.Taskbar;

internal static class BadgeIcon
{
    private const int HeaderSize = 40;
    private const int BytesPerPixel = 4;
    private const float CornerShare = 0.35f;
    private const float DotShare = 0.22f;

    public static nint Create(int size, uint rgb, bool square, bool dot)
    {
        var pixelBytes = size * size * BytesPerPixel;
        var maskBytes = (size + 31) / 32 * 4 * size;
        var image = new byte[HeaderSize + pixelBytes + maskBytes];
        WriteHeader(image, size);

        var half = size / 2f;
        var border = Math.Max(1f, size / 16f);
        var dotRadius = size * DotShare;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - half;
                var dy = y + 0.5f - half;
                var outer = Coverage(ShapeDistance(dx, dy, half, square));
                var inner = Coverage(ShapeDistance(dx, dy, half - border, square));
                var hole = dot ? Coverage(CircleDistance(dx, dy, dotRadius)) : 0f;
                var fill = inner * (1f - hole);
                var offset = HeaderSize + (y * size + x) * BytesPerPixel;
                image[offset] = TintFromWhite(rgb & 0xFF, fill);
                image[offset + 1] = TintFromWhite((rgb >> 8) & 0xFF, fill);
                image[offset + 2] = TintFromWhite((rgb >> 16) & 0xFF, fill);
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

    private static byte TintFromWhite(uint channel, float weight) => (byte)MathF.Round(255f + (channel - 255f) * weight);
}
