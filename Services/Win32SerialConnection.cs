using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace KeryxControl.Services;

internal sealed class Win32SerialConnection : IDisposable
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private const uint PurgeTxAbort = 0x0001;
    private const uint PurgeRxAbort = 0x0002;
    private const uint PurgeTxClear = 0x0004;
    private const uint PurgeRxClear = 0x0008;
    private const uint DcbBinary = 1u << 0;
    private const uint DcbOutCtsFlow = 1u << 2;
    private const uint DcbDtrEnable = 1u << 4;
    private const uint DcbRtsHandshake = 2u << 12;

    private readonly SafeFileHandle _handle;
    private bool _disposed;

    private Win32SerialConnection(SafeFileHandle handle, string portName)
    {
        _handle = handle;
        PortName = portName;
    }

    public string PortName { get; }

    public static Win32SerialConnection Open(string portName)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The TURZX driver requires Windows.");
        if (string.IsNullOrWhiteSpace(portName) || !portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid serial port name.", nameof(portName));

        var normalized = portName.Trim().ToUpperInvariant();
        var handle = CreateFile($@"\\.\{normalized}", GenericRead | GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException($"Impossible d'ouvrir {normalized} ({new Win32Exception(error).Message}).");
        }

        try
        {
            Configure(handle);
            return new Win32SerialConnection(handle, normalized);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public void Purge()
    {
        ThrowIfDisposed();
        if (!PurgeComm(_handle, PurgeTxAbort | PurgeRxAbort | PurgeTxClear | PurgeRxClear)) ThrowLastWin32("Impossible de vider le port série");
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();
        if (data.IsEmpty) return;
        var remaining = data.ToArray();
        while (remaining.Length > 0)
        {
            if (!WriteFile(_handle, remaining, remaining.Length, out var written, IntPtr.Zero)) ThrowLastWin32("Écriture TURZX impossible");
            if (written <= 0) throw new IOException("Écriture TURZX interrompue.");
            remaining = remaining.AsSpan(written).ToArray();
        }
    }

    public byte[] ReadExact(int count, TimeSpan timeout)
    {
        ThrowIfDisposed();
        var buffer = new byte[count];
        var offset = 0;
        var deadline = DateTime.UtcNow + timeout;
        while (offset < count && DateTime.UtcNow < deadline)
        {
            var chunk = new byte[count - offset];
            if (!ReadFile(_handle, chunk, chunk.Length, out var read, IntPtr.Zero)) ThrowLastWin32("Lecture TURZX impossible");
            if (read == 0) continue;
            Buffer.BlockCopy(chunk, 0, buffer, offset, read);
            offset += read;
        }
        return offset == count ? buffer : buffer.AsSpan(0, offset).ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handle.Dispose();
    }

    private static void Configure(SafeFileHandle handle)
    {
        if (!SetupComm(handle, 1024 * 1024, 1024 * 1024)) ThrowLastWin32("Configuration des tampons série impossible");

        var dcb = new Dcb { Length = (uint)Marshal.SizeOf<Dcb>() };
        if (!GetCommState(handle, ref dcb)) ThrowLastWin32("Lecture de la configuration série impossible");
        dcb.BaudRate = TurzxProtocol.BaudRate;
        dcb.Flags = DcbBinary | DcbOutCtsFlow | DcbDtrEnable | DcbRtsHandshake;
        dcb.ByteSize = 8;
        dcb.Parity = 0;
        dcb.StopBits = 0;
        dcb.XonChar = 0x11;
        dcb.XoffChar = 0x13;
        if (!SetCommState(handle, ref dcb)) ThrowLastWin32("Configuration du port série impossible");

        var timeouts = new CommTimeouts
        {
            ReadIntervalTimeout = uint.MaxValue,
            ReadTotalTimeoutMultiplier = 0,
            ReadTotalTimeoutConstant = 100,
            WriteTotalTimeoutMultiplier = 0,
            WriteTotalTimeoutConstant = 5000
        };
        if (!SetCommTimeouts(handle, ref timeouts)) ThrowLastWin32("Configuration des délais série impossible");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void ThrowLastWin32(string message)
    {
        var error = Marshal.GetLastWin32Error();
        throw new IOException($"{message} ({new Win32Exception(error).Message}).");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Dcb
    {
        public uint Length;
        public uint BaudRate;
        public uint Flags;
        public ushort Reserved;
        public ushort XonLimit;
        public ushort XoffLimit;
        public byte ByteSize;
        public byte Parity;
        public byte StopBits;
        public byte XonChar;
        public byte XoffChar;
        public byte ErrorChar;
        public byte EofChar;
        public byte EventChar;
        public ushort Reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CommTimeouts
    {
        public uint ReadIntervalTimeout;
        public uint ReadTotalTimeoutMultiplier;
        public uint ReadTotalTimeoutConstant;
        public uint WriteTotalTimeoutMultiplier;
        public uint WriteTotalTimeoutConstant;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupComm(SafeFileHandle file, int inputBufferSize, int outputBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCommState(SafeFileHandle file, ref Dcb dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCommState(SafeFileHandle file, ref Dcb dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCommTimeouts(SafeFileHandle file, ref CommTimeouts timeouts);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PurgeComm(SafeFileHandle file, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteFile(SafeFileHandle file, byte[] buffer, int bytesToWrite, out int bytesWritten, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(SafeFileHandle file, byte[] buffer, int bytesToRead, out int bytesRead, IntPtr overlapped);
}
