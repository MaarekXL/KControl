using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace KeryxControl.Services;

public static class TurzxNativeUsbProtocol
{
    public const int CommandDataLength = 500;
    public const int EncryptedPacketLength = 512;
    private static readonly byte[] DesKey = Encoding.ASCII.GetBytes("slv3tuzx");

    public static byte[] BuildSyncCommand(DateTime? localNow = null) => BuildCommand(10, null, localNow);

    public static byte[] BuildBrightnessCommand(int percent, DateTime? localNow = null)
    {
        var value = (byte)(Math.Clamp(percent, 0, 100) / 100d * 102);
        return BuildCommand(14, header => header[8] = value, localNow);
    }

    public static byte[] BuildPngCommand(int byteLength, DateTime? localNow = null)
    {
        if (byteLength <= 0 || byteLength > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(byteLength));
        return BuildCommand(102, header => BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(8, 4), byteLength), localNow);
    }

    public static byte[] BuildCommand(byte commandId, Action<byte[]>? customize = null, DateTime? localNow = null)
    {
        var header = new byte[CommandDataLength];
        header[0] = commandId;
        header[2] = 0x1a;
        header[3] = 0x6d;
        var now = localNow ?? DateTime.Now;
        var milliseconds = checked((uint)Math.Clamp(now.TimeOfDay.TotalMilliseconds, 0, uint.MaxValue));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), milliseconds);
        customize?.Invoke(header);
        return Encrypt(header);
    }

    public static byte[] Encrypt(ReadOnlySpan<byte> commandHeader)
    {
        if (commandHeader.Length != CommandDataLength) throw new ArgumentException("A TURZX USB command header must be 500 bytes.", nameof(commandHeader));
        var padded = new byte[504];
        commandHeader.CopyTo(padded);
        using var des = DES.Create();
        des.Mode = CipherMode.CBC;
        des.Padding = PaddingMode.None;
        using var encryptor = des.CreateEncryptor(DesKey, DesKey);
        var encrypted = encryptor.TransformFinalBlock(padded, 0, padded.Length);
        var packet = new byte[EncryptedPacketLength];
        encrypted.CopyTo(packet, 0);
        packet[510] = 0xa1;
        packet[511] = 0x1a;
        return packet;
    }
}
