using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace KeryxControl.Services;

internal sealed class LibUsbConnection : IDisposable
{
    private const string LibraryName = "libusb-1.0.dll";
    private const byte EndpointOut = 0x01;
    private const byte EndpointIn = 0x81;
    private IntPtr _context;
    private IntPtr _handle;
    private bool _claimed;
    private bool _disposed;

    private LibUsbConnection(IntPtr context, IntPtr handle)
    {
        _context = context;
        _handle = handle;
    }

    public static LibUsbConnection Open(ushort vendorId, ushort productId)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Le pilote TURZX USB natif requiert Windows.");
        IntPtr context = IntPtr.Zero;
        try
        {
            ThrowIfError(libusb_init(out context), "Initialisation USB impossible");
            var handle = libusb_open_device_with_vid_pid(context, vendorId, productId);
            if (handle == IntPtr.Zero)
            {
                libusb_exit(context);
                throw new IOException($"Écran TURZX USB {vendorId:X4}:{productId:X4} introuvable ou inaccessible.");
            }

            var connection = new LibUsbConnection(context, handle);
            try
            {
                _ = libusb_set_auto_detach_kernel_driver(handle, 1);
                ThrowIfError(libusb_claim_interface(handle, 0), "Interface TURZX USB occupée");
                connection._claimed = true;
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
        catch (DllNotFoundException ex)
        {
            if (context != IntPtr.Zero) libusb_exit(context);
            throw new IOException("Le composant libusb-1.0.dll manque à côté de KeryxControl.exe.", ex);
        }
        catch (BadImageFormatException ex)
        {
            if (context != IntPtr.Zero) libusb_exit(context);
            throw new IOException("La version x64 de libusb-1.0.dll est requise.", ex);
        }
    }

    public void Write(ReadOnlySpan<byte> data, int timeoutMilliseconds = 5000)
    {
        ThrowIfDisposed();
        const int maximumTransfer = 1024 * 1024;
        for (var offset = 0; offset < data.Length;)
        {
            var length = Math.Min(maximumTransfer, data.Length - offset);
            var chunk = data.Slice(offset, length).ToArray();
            var result = libusb_bulk_transfer(_handle, EndpointOut, chunk, chunk.Length, out var transferred, (uint)timeoutMilliseconds);
            ThrowIfError(result, "Envoi vers l’écran TURZX impossible");
            if (transferred <= 0) throw new IOException("Le transfert TURZX USB a été interrompu.");
            offset += transferred;
        }
    }

    public byte[] Read(int length = 512, int timeoutMilliseconds = 2000)
    {
        ThrowIfDisposed();
        var buffer = new byte[length];
        var result = libusb_bulk_transfer(_handle, EndpointIn, buffer, buffer.Length, out var transferred, (uint)timeoutMilliseconds);
        ThrowIfError(result, "Réponse de l’écran TURZX impossible");
        return buffer.AsSpan(0, Math.Max(0, transferred)).ToArray();
    }

    public void Drain()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var buffer = new byte[512];
            var result = libusb_bulk_transfer(_handle, EndpointIn, buffer, buffer.Length, out _, 100);
            if (result < 0) return;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            if (_claimed) _ = libusb_release_interface(_handle, 0);
            libusb_close(_handle);
            _handle = IntPtr.Zero;
        }
        if (_context != IntPtr.Zero)
        {
            libusb_exit(_context);
            _context = IntPtr.Zero;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static void ThrowIfError(int code, string message)
    {
        if (code >= 0) return;
        var name = Marshal.PtrToStringAnsi(libusb_error_name(code)) ?? new Win32Exception(code).Message;
        throw new IOException($"{message} ({name}, code {code}). Vérifie que le pilote WinUSB du fabricant est installé.");
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int libusb_init(out IntPtr context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_exit(IntPtr context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr libusb_open_device_with_vid_pid(IntPtr context, ushort vendorId, ushort productId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int libusb_claim_interface(IntPtr deviceHandle, int interfaceNumber);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int libusb_release_interface(IntPtr deviceHandle, int interfaceNumber);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int libusb_set_auto_detach_kernel_driver(IntPtr deviceHandle, int enable);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_close(IntPtr deviceHandle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int libusb_bulk_transfer(IntPtr deviceHandle, byte endpoint, byte[] data, int length, out int transferred, uint timeout);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr libusb_error_name(int errorCode);
}
