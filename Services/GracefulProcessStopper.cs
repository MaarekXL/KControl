using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeryxControl.Services;

internal static class GracefulProcessStopper
{
    private const uint CtrlCEvent = 0;
    private const uint AttachParentProcess = 0xFFFFFFFF;

    public static async Task<bool> TryStopAsync(Process process, TimeSpan timeout, CancellationToken ct)
    {
        if (process.HasExited) return true;

        if (OperatingSystem.IsWindows())
            TrySendCtrlC(process.Id);
        else
            TryCloseMainWindow(process);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return process.HasExited;
        }
    }

    private static void TryCloseMainWindow(Process process)
    {
        try { process.CloseMainWindow(); }
        catch { }
    }

    private static void TrySendCtrlC(int processId)
    {
        // The child is launched with a hidden console. Attach briefly so the
        // Rust Ctrl+C handler can flush miner state and let Kubo stop cleanly.
        try
        {
            FreeConsole();
            if (!AttachConsole((uint)processId)) return;
            SetConsoleCtrlHandler(0, true);
            GenerateConsoleCtrlEvent(CtrlCEvent, 0);
            Thread.Sleep(100);
        }
        catch { }
        finally
        {
            try { FreeConsole(); } catch { }
            try { SetConsoleCtrlHandler(0, false); } catch { }
            // A GUI process normally has no console, but restore a parent
            // console if the host provided one (for example during debugging).
            try { AttachConsole(AttachParentProcess); } catch { }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleCtrlHandler(nint handlerRoutine, [MarshalAs(UnmanagedType.Bool)] bool add);
}
