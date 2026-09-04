using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeryxControl.Models;

namespace KeryxControl.Services;

internal interface ITurzxDeviceDriver : IDisposable
{
    TurzxDisplayProfile Profile { get; }
    string Endpoint { get; }
    void Initialize(int brightness);
    void SetBrightness(int brightness);
    void DisplayFrame(TurzxRenderedFrame frame, TurzxRenderedFrame? previousFrame);
    void ScreenOff();
}

internal static class TurzxDeviceDriverFactory
{
    public static ITurzxDeviceDriver Open(TurzxDetectedDevice device) => device.Profile.Protocol switch
    {
        TurzxProtocolFamily.SerialRevisionA => new TurzxRevisionADriver(device.Profile, Win32SerialConnection.Open(device.Endpoint)),
        TurzxProtocolFamily.SerialRevisionC => new TurzxRevisionCDriver(device.Profile,
            Win32SerialConnection.Open(TurzxPortDetector.PrepareLegacyPort(device))),
        TurzxProtocolFamily.NativeUsb => new TurzxNativeUsbDriver(device.Profile,
            LibUsbConnection.Open(device.Profile.VendorId, device.Profile.ProductId)),
        _ => throw new NotSupportedException($"Protocole TURZX non pris en charge : {device.Profile.Protocol}")
    };
}

internal sealed class TurzxRevisionADriver : ITurzxDeviceDriver
{
    private static readonly TurzxRect[] UpdateAreas =
    [
        new(0, 0, 480, 50), new(12, 60, 290, 136), new(312, 60, 156, 136),
        new(12, 208, 105, 98), new(125, 208, 105, 98), new(238, 208, 105, 98), new(351, 208, 117, 98)
    ];
    private readonly Win32SerialConnection _connection;

    public TurzxRevisionADriver(TurzxDisplayProfile profile, Win32SerialConnection connection)
    {
        Profile = profile;
        _connection = connection;
    }

    public TurzxDisplayProfile Profile { get; }
    public string Endpoint => _connection.PortName;

    public void Initialize(int brightness)
    {
        _connection.Purge();
        _connection.Write(TurzxProtocol.BuildHelloCommand());
        var hello = _connection.ReadExact(6, TimeSpan.FromSeconds(1.5));
        if (hello.Length != 6 || hello.Any(value => value != 1))
            throw new IOException("Le périphérique n’a pas répondu comme un TURZX série A.");
        _connection.Write(TurzxProtocol.BuildScreenOnCommand());
        _connection.Write(TurzxProtocol.BuildLandscapeOrientationCommand());
        SetBrightness(brightness);
    }

    public void SetBrightness(int brightness) => _connection.Write(TurzxProtocol.BuildBrightnessCommand(brightness));

    public void DisplayFrame(TurzxRenderedFrame frame, TurzxRenderedFrame? previousFrame)
    {
        if (previousFrame is null)
        {
            SendRegion(frame, new TurzxRect(0, 0, Profile.LandscapeWidth, Profile.LandscapeHeight));
            return;
        }
        foreach (var area in UpdateAreas)
        {
            var changed = TurzxProtocol.FindChangedBounds(frame, previousFrame, area);
            if (changed is TurzxRect region) SendRegion(frame, region);
        }
    }

    public void ScreenOff()
    {
        try { _connection.Write(TurzxProtocol.BuildScreenOffCommand()); } catch { }
    }

    public void Dispose() => _connection.Dispose();

    private void SendRegion(TurzxRenderedFrame frame, TurzxRect region)
    {
        _connection.Write(TurzxProtocol.BuildDisplayCommand(region));
        var pixels = TurzxProtocol.ConvertRegionToRgb565(frame, region);
        for (var offset = 0; offset < pixels.Length; offset += TurzxProtocol.TransferChunkSize)
        {
            var length = Math.Min(TurzxProtocol.TransferChunkSize, pixels.Length - offset);
            _connection.Write(pixels.AsSpan(offset, length));
        }
    }
}

internal sealed class TurzxRevisionCDriver : ITurzxDeviceDriver
{
    private static readonly byte[] Hello = [0x01, 0xef, 0x69, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xc5, 0xd3];
    private static readonly byte[] Options = [0x7d, 0xef, 0x69, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x2d];
    private static readonly byte[] TurnOff = [0x83, 0xef, 0x69, 0x00, 0x00, 0x00, 0x01];
    private static readonly byte[] SetBrightnessCommand = [0x7b, 0xef, 0x69, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00];
    private static readonly byte[] StopVideo = [0x79, 0xef, 0x69, 0x00, 0x00, 0x00, 0x01];
    private static readonly byte[] StopMedia = [0x96, 0xef, 0x69, 0x00, 0x00, 0x00, 0x01];
    private static readonly byte[] QueryStatus = [0xcf, 0xef, 0x69, 0x00, 0x00, 0x00, 0x01];
    private static readonly byte[] PreUpdateBitmap = [0x86, 0xef, 0x69, 0x00, 0x00, 0x00, 0x01];
    private static readonly byte[] UpdateBitmap = [0xcc, 0xef, 0x69, 0x00];
    private readonly Win32SerialConnection _connection;
    private int _romVersion = 87;
    private int _updateCount;

    public TurzxRevisionCDriver(TurzxDisplayProfile profile, Win32SerialConnection connection)
    {
        Profile = profile;
        _connection = connection;
    }

    public TurzxDisplayProfile Profile { get; }
    public string Endpoint => _connection.PortName;

    public void Initialize(int brightness)
    {
        _connection.Purge();
        string response = "";
        for (var attempt = 0; attempt < 3 && !response.StartsWith("chs_", StringComparison.OrdinalIgnoreCase); attempt++)
        {
            SendCommand(Hello);
            response = Encoding.ASCII.GetString(_connection.ReadExact(23, TimeSpan.FromSeconds(2)))
                .Trim('\0', '\r', '\n', ' ');
        }
        if (!response.StartsWith("chs_", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Le périphérique n’a pas répondu comme un TURZX série C.");
        var versionText = response.Split('.').ElementAtOrDefault(2);
        if (int.TryParse(versionText, out var version) && version is >= 80 and <= 100) _romVersion = version;
        SendCommand(StopVideo);
        SendCommand(StopMedia);
        _ = _connection.ReadExact(1024, TimeSpan.FromSeconds(1));
        SetBrightness(brightness);
        SendCommand(Options, [0x00, 0x00, 0x00, 0x00]);
    }

    public void SetBrightness(int brightness)
    {
        var value = (byte)(Math.Clamp(brightness, 0, 100) / 100d * 255);
        SendCommand(SetBrightnessCommand, [value]);
    }

    public void DisplayFrame(TurzxRenderedFrame frame, TurzxRenderedFrame? previousFrame)
    {
        ValidateFrame(frame);
        if (previousFrame is null)
        {
            SendFullFrame(frame);
            return;
        }
        var changed = TurzxProtocol.FindChangedBounds(frame, previousFrame, new TurzxRect(0, 0, frame.Width, frame.Height));
        if (changed is TurzxRect region) SendChangedRegion(frame, region);
    }

    public void ScreenOff()
    {
        try
        {
            SendCommand(StopVideo);
            SendCommand(StopMedia);
            _ = _connection.ReadExact(1024, TimeSpan.FromSeconds(1));
            SendCommand(TurnOff);
        }
        catch { }
    }

    public void Dispose() => _connection.Dispose();

    private void SendFullFrame(TurzxRenderedFrame frame)
    {
        SendCommand(PreUpdateBitmap);
        SendCommand([0x2c], padding: 0x2c);
        var prefix = Profile.PortraitHeight switch
        {
            480 => new byte[] { 0xc8, 0xef, 0x69, 0x00, 0x0e, 0x10 },
            800 => new byte[] { 0xc8, 0xef, 0x69, 0x00, 0x17, 0x70 },
            1920 => new byte[] { 0xc8, 0xef, 0x69, 0x00, 0x38, 0x40 },
            _ => throw new NotSupportedException($"Résolution TURZX série C non prise en charge : {Profile.PortraitWidth}×{Profile.PortraitHeight}")
        };
        var physicalWidthFactor = (Profile.PortraitWidth * Profile.PortraitWidth / 64);
        Span<byte> factor = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(factor, checked((ushort)physicalWidthFactor));
        SendCommand(prefix, factor.ToArray());

        var oriented = Profile.PortraitHeight == 1920 ? TurzxFrameTransforms.RotateClockwise(frame) : frame;
        var pixels = EncodePixels(oriented, includeAlpha: true);
        SendRaw(InsertSeparatorEvery249Bytes(pixels, appendTerminator: false));
        _ = _connection.ReadExact(1024, TimeSpan.FromSeconds(3));
        SendCommand(QueryStatus);
        _ = _connection.ReadExact(1024, TimeSpan.FromSeconds(2));
    }

    private void SendChangedRegion(TurzxRenderedFrame frame, TurzxRect region)
    {
        var cropped = TurzxFrameTransforms.Crop(frame, region);
        int x0;
        int y0;
        TurzxRenderedFrame oriented;
        if (Profile.PortraitHeight == 1920)
        {
            oriented = TurzxFrameTransforms.RotateClockwise(cropped);
            x0 = region.X;
            y0 = Profile.LandscapeHeight - region.Y - oriented.Width;
        }
        else
        {
            oriented = cropped;
            x0 = region.Y;
            y0 = region.X;
        }

        var includeAlpha = Profile.PortraitHeight != 480 && _romVersion > 88;
        var pixelSize = includeAlpha ? 4 : 3;
        var raw = new List<byte>(oriented.Width * oriented.Height * pixelSize + oriented.Height * 5);
        for (var row = 0; row < oriented.Height; row++)
        {
            var address = Profile.PortraitHeight == 1920
                ? (x0 + row) * Profile.PortraitWidth + y0
                : (x0 + row) * Profile.PortraitHeight + y0;
            raw.Add((byte)(address >> 16)); raw.Add((byte)(address >> 8)); raw.Add((byte)address);
            raw.Add((byte)(oriented.Width >> 8)); raw.Add((byte)oriented.Width);
            AppendPixelRow(raw, oriented, row, includeAlpha);
        }

        var rawLength = raw.Count + 2;
        var payload = new byte[10];
        payload[0] = (byte)(rawLength >> 16); payload[1] = (byte)(rawLength >> 8); payload[2] = (byte)rawLength;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(6, 4), _updateCount++);
        SendCommand(UpdateBitmap, payload);
        SendRaw(InsertSeparatorEvery249Bytes(raw.ToArray(), appendTerminator: true));
        SendCommand(QueryStatus);
        _ = _connection.ReadExact(1024, TimeSpan.FromSeconds(2));
    }

    private void SendCommand(ReadOnlySpan<byte> command, ReadOnlySpan<byte> payload = default, byte padding = 0)
    {
        var length = command.Length + payload.Length;
        var paddedLength = ((length + 249) / 250) * 250;
        var message = new byte[paddedLength];
        if (padding != 0) Array.Fill(message, padding);
        command.CopyTo(message);
        payload.CopyTo(message.AsSpan(command.Length));
        _connection.Write(message);
    }

    private void SendRaw(ReadOnlySpan<byte> payload)
    {
        var paddedLength = ((payload.Length + 249) / 250) * 250;
        var message = new byte[paddedLength];
        payload.CopyTo(message);
        _connection.Write(message);
    }

    private static byte[] InsertSeparatorEvery249Bytes(byte[] bytes, bool appendTerminator)
    {
        using var stream = new MemoryStream(bytes.Length + bytes.Length / 249 + 4);
        for (var offset = 0; offset < bytes.Length; offset += 249)
        {
            var length = Math.Min(249, bytes.Length - offset);
            stream.Write(bytes, offset, length);
            if (offset + length < bytes.Length) stream.WriteByte(0);
        }
        if (appendTerminator) { stream.WriteByte(0xef); stream.WriteByte(0x69); }
        return stream.ToArray();
    }

    private static byte[] EncodePixels(TurzxRenderedFrame frame, bool includeAlpha)
    {
        var output = new List<byte>(frame.Width * frame.Height * (includeAlpha ? 4 : 3));
        for (var row = 0; row < frame.Height; row++) AppendPixelRow(output, frame, row, includeAlpha);
        return output.ToArray();
    }

    private static void AppendPixelRow(List<byte> output, TurzxRenderedFrame frame, int row, bool includeAlpha)
    {
        var offset = row * frame.Stride;
        for (var column = 0; column < frame.Width; column++, offset += 4)
        {
            output.Add(frame.BgraPixels[offset]);
            output.Add(frame.BgraPixels[offset + 1]);
            output.Add(frame.BgraPixels[offset + 2]);
            if (includeAlpha) output.Add(frame.BgraPixels[offset + 3]);
        }
    }

    private void ValidateFrame(TurzxRenderedFrame frame)
    {
        if (frame.Width != Profile.LandscapeWidth || frame.Height != Profile.LandscapeHeight)
            throw new ArgumentException($"L’image doit mesurer {Profile.LandscapeWidth}×{Profile.LandscapeHeight}.", nameof(frame));
    }
}

internal sealed class TurzxNativeUsbDriver : ITurzxDeviceDriver
{
    private readonly LibUsbConnection _connection;

    public TurzxNativeUsbDriver(TurzxDisplayProfile profile, LibUsbConnection connection)
    {
        Profile = profile;
        _connection = connection;
    }

    public TurzxDisplayProfile Profile { get; }
    public string Endpoint => $"USB {Profile.VendorId:X4}:{Profile.ProductId:X4}";

    public void Initialize(int brightness)
    {
        SendCommand(TurzxNativeUsbProtocol.BuildSyncCommand());
        Thread.Sleep(200);
        SetBrightness(brightness);
    }

    public void SetBrightness(int brightness) => SendCommand(TurzxNativeUsbProtocol.BuildBrightnessCommand(brightness));

    public void DisplayFrame(TurzxRenderedFrame frame, TurzxRenderedFrame? previousFrame)
    {
        if (frame.Width != Profile.LandscapeWidth || frame.Height != Profile.LandscapeHeight)
            throw new ArgumentException($"L’image doit mesurer {Profile.LandscapeWidth}×{Profile.LandscapeHeight}.", nameof(frame));
        if (previousFrame is not null && TurzxProtocol.FindChangedBounds(frame, previousFrame,
                new TurzxRect(0, 0, frame.Width, frame.Height)) is null) return;
        var portrait = TurzxFrameTransforms.RotateClockwise(frame);
        var png = TurzxFrameTransforms.EncodePng(portrait);
        if (png.Length > 1024 * 1024) throw new IOException("L’image TURZX dépasse la limite USB de 1 Mio.");
        var command = TurzxNativeUsbProtocol.BuildPngCommand(png.Length);
        var payload = new byte[command.Length + png.Length];
        command.CopyTo(payload, 0);
        png.CopyTo(payload, command.Length);
        SendCommand(payload);
    }

    public void ScreenOff()
    {
        try { SetBrightness(0); } catch { }
    }

    public void Dispose() => _connection.Dispose();

    private void SendCommand(byte[] payload)
    {
        _connection.Write(payload);
        _ = _connection.Read();
        _connection.Drain();
    }
}

internal static class TurzxFrameTransforms
{
    public static TurzxRenderedFrame Crop(TurzxRenderedFrame source, TurzxRect region)
    {
        var stride = region.Width * 4;
        var output = new byte[stride * region.Height];
        for (var row = 0; row < region.Height; row++)
            Buffer.BlockCopy(source.BgraPixels, (region.Y + row) * source.Stride + region.X * 4,
                output, row * stride, stride);
        return new TurzxRenderedFrame(region.Width, region.Height, stride, output);
    }

    public static TurzxRenderedFrame RotateClockwise(TurzxRenderedFrame source)
    {
        var width = source.Height;
        var height = source.Width;
        var stride = width * 4;
        var output = new byte[stride * height];
        for (var sourceY = 0; sourceY < source.Height; sourceY++)
        for (var sourceX = 0; sourceX < source.Width; sourceX++)
        {
            var destinationX = source.Height - 1 - sourceY;
            var destinationY = sourceX;
            Buffer.BlockCopy(source.BgraPixels, sourceY * source.Stride + sourceX * 4,
                output, destinationY * stride + destinationX * 4, 4);
        }
        return new TurzxRenderedFrame(width, height, stride, output);
    }

    public static byte[] EncodePng(TurzxRenderedFrame frame)
    {
        var bitmap = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32,
            null, frame.BgraPixels, frame.Stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
