namespace KeryxControl.Models;

public enum TurzxDisplayLevel
{
    Normal,
    Warning,
    Error
}

public enum TurzxConnectionState
{
    Disabled,
    Searching,
    Connecting,
    Connected,
    Error
}

public enum TurzxProtocolFamily
{
    SerialRevisionA,
    SerialRevisionC,
    NativeUsb
}

public enum TurzxValidationLevel
{
    HardwareValidated,
    ProtocolValidated
}

public sealed record TurzxDisplayProfile(
    string Id,
    string DisplayName,
    TurzxProtocolFamily Protocol,
    int LandscapeWidth,
    int LandscapeHeight,
    int PortraitWidth,
    int PortraitHeight,
    ushort VendorId,
    ushort ProductId,
    TurzxValidationLevel Validation);

public sealed record TurzxDetectedDevice(TurzxDisplayProfile Profile, string Endpoint, string? SerialNumber = null);

public sealed record TurzxDisplaySnapshot(
    string Language,
    string StateLabel,
    TurzxDisplayLevel Level,
    string Hashrate,
    string Temperature,
    string Power,
    string Utilization,
    string Uptime,
    long Accepted,
    long Rejected,
    int SelectedGpuCount);

public sealed record TurzxRenderedFrame(int Width, int Height, int Stride, byte[] BgraPixels);

public readonly record struct TurzxRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width - 1;
    public int Bottom => Y + Height - 1;
}

public sealed record TurzxConnectionStatus(
    TurzxConnectionState State,
    string? Endpoint = null,
    string? Model = null,
    string? Error = null);
