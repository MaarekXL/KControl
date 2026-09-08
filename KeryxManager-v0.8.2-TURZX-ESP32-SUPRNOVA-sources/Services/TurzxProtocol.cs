using KeryxControl.Models;

namespace KeryxControl.Services;

public static class TurzxProtocol
{
    public const int DisplayWidth = 480;
    public const int DisplayHeight = 320;
    public const int PortraitWidth = 320;
    public const int PortraitHeight = 480;
    public const int BaudRate = 115200;
    public const int TransferChunkSize = DisplayWidth * 8;

    private const byte HelloCommand = 69;
    private const byte ScreenOffCommand = 108;
    private const byte ScreenOnCommand = 109;
    private const byte SetBrightnessCommand = 110;
    private const byte SetOrientationCommand = 121;
    private const byte DisplayBitmapCommand = 197;
    private const byte LandscapeOrientation = 2;

    public static byte[] BuildHelloCommand() => [HelloCommand, HelloCommand, HelloCommand, HelloCommand, HelloCommand, HelloCommand];

    public static byte[] BuildScreenOnCommand() => BuildCommand(ScreenOnCommand, 0, 0, 0, 0);

    public static byte[] BuildScreenOffCommand() => BuildCommand(ScreenOffCommand, 0, 0, 0, 0);

    public static byte[] BuildBrightnessCommand(int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        var absolute = (int)(255 - clamped / 100d * 255);
        return BuildCommand(SetBrightnessCommand, absolute, 0, 0, 0);
    }

    public static byte[] BuildLandscapeOrientationCommand()
    {
        var command = new byte[16];
        command[5] = SetOrientationCommand;
        command[6] = LandscapeOrientation + 100;
        command[7] = DisplayWidth >> 8;
        command[8] = DisplayWidth & 0xff;
        command[9] = DisplayHeight >> 8;
        command[10] = DisplayHeight & 0xff;
        return command;
    }

    public static byte[] BuildDisplayCommand(TurzxRect region)
    {
        ValidateRegion(region);
        return BuildCommand(DisplayBitmapCommand, region.X, region.Y, region.Right, region.Bottom);
    }

    public static byte[] ConvertRegionToRgb565(TurzxRenderedFrame frame, TurzxRect region)
    {
        ValidateFrame(frame);
        ValidateRegion(region);
        var output = new byte[checked(region.Width * region.Height * 2)];
        var destination = 0;

        for (var y = region.Y; y <= region.Bottom; y++)
        {
            var source = checked(y * frame.Stride + region.X * 4);
            for (var x = 0; x < region.Width; x++)
            {
                var blue = frame.BgraPixels[source++];
                var green = frame.BgraPixels[source++];
                var red = frame.BgraPixels[source++];
                source++; // Alpha
                var rgb565 = (ushort)(((red >> 3) << 11) | ((green >> 2) << 5) | (blue >> 3));
                output[destination++] = (byte)(rgb565 & 0xff);
                output[destination++] = (byte)(rgb565 >> 8);
            }
        }

        return output;
    }

    public static bool RegionChanged(TurzxRenderedFrame current, TurzxRenderedFrame previous, TurzxRect region)
    {
        ValidateCompleteFrame(current);
        ValidateCompleteFrame(previous);
        ValidateRegion(region, current.Width, current.Height);
        if (current.Width != previous.Width || current.Height != previous.Height || current.Stride != previous.Stride) return true;

        var bytesPerRow = checked(region.Width * 4);
        for (var y = region.Y; y <= region.Bottom; y++)
        {
            var offset = checked(y * current.Stride + region.X * 4);
            if (!current.BgraPixels.AsSpan(offset, bytesPerRow).SequenceEqual(previous.BgraPixels.AsSpan(offset, bytesPerRow))) return true;
        }
        return false;
    }

    public static TurzxRect? FindChangedBounds(TurzxRenderedFrame current, TurzxRenderedFrame previous, TurzxRect searchArea)
    {
        ValidateCompleteFrame(current);
        ValidateCompleteFrame(previous);
        ValidateRegion(searchArea, current.Width, current.Height);
        if (current.Width != previous.Width || current.Height != previous.Height || current.Stride != previous.Stride) return searchArea;

        var minX = searchArea.Right;
        var minY = searchArea.Bottom;
        var maxX = searchArea.X - 1;
        var maxY = searchArea.Y - 1;
        for (var y = searchArea.Y; y <= searchArea.Bottom; y++)
        {
            var offset = checked(y * current.Stride + searchArea.X * 4);
            for (var x = searchArea.X; x <= searchArea.Right; x++, offset += 4)
            {
                if (current.BgraPixels[offset] == previous.BgraPixels[offset]
                    && current.BgraPixels[offset + 1] == previous.BgraPixels[offset + 1]
                    && current.BgraPixels[offset + 2] == previous.BgraPixels[offset + 2]
                    && current.BgraPixels[offset + 3] == previous.BgraPixels[offset + 3]) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY) return null;
        minX = Math.Max(searchArea.X, minX - 1);
        minY = Math.Max(searchArea.Y, minY - 1);
        maxX = Math.Min(searchArea.Right, maxX + 1);
        maxY = Math.Min(searchArea.Bottom, maxY + 1);
        return new TurzxRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static byte[] BuildCommand(byte command, int x, int y, int endX, int endY)
    {
        if (x is < 0 or > 1023 || y is < 0 or > 4095 || endX is < 0 or > 1023 || endY is < 0 or > 1023)
            throw new ArgumentOutOfRangeException(nameof(x), "TURZX command coordinates are out of range.");

        return
        [
            (byte)(x >> 2),
            (byte)(((x & 3) << 6) + (y >> 4)),
            (byte)(((y & 15) << 4) + (endX >> 6)),
            (byte)(((endX & 63) << 2) + (endY >> 8)),
            (byte)(endY & 255),
            command
        ];
    }

    private static void ValidateRegion(TurzxRect region)
    {
        ValidateRegion(region, DisplayWidth, DisplayHeight);
    }

    private static void ValidateRegion(TurzxRect region, int width, int height)
    {
        if (region.Width <= 0 || region.Height <= 0 || region.X < 0 || region.Y < 0
            || region.Right >= width || region.Bottom >= height)
            throw new ArgumentOutOfRangeException(nameof(region), "The region must fit inside the 480x320 TURZX display.");
    }

    private static void ValidateFrame(TurzxRenderedFrame frame)
    {
        ValidateCompleteFrame(frame);
        if (frame.Width != DisplayWidth || frame.Height != DisplayHeight)
            throw new ArgumentException("The rendered frame must be a complete 480x320 BGRA image.", nameof(frame));
    }

    private static void ValidateCompleteFrame(TurzxRenderedFrame frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || frame.Stride < frame.Width * 4
            || frame.BgraPixels.Length < frame.Stride * frame.Height)
            throw new ArgumentException("The rendered frame must be a complete BGRA image.", nameof(frame));
    }
}
