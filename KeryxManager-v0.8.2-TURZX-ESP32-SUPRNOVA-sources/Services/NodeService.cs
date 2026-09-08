using System.Diagnostics;
using System.IO;
using System.Text;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class NodeService : IAsyncDisposable
{
    private Process? _process;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _generation;
    public bool IsOwnedRunning => _process is { HasExited: false };
    public event Action<string>? LineReceived;
    public event Action<int>? Exited;

    public async Task StartAsync(NodeConfig config, string address, int port, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (IsOwnedRunning) return;
            _process?.Dispose();
            _process = null;
            var executable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.Executable), AppContext.BaseDirectory);
            if (!File.Exists(executable)) throw new FileNotFoundException("keryxd.exe is missing.", executable);
            var data = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.DataDirectory), AppContext.BaseDirectory);
            Directory.CreateDirectory(data);
            var psi = new ProcessStartInfo(executable)
            {
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            psi.ArgumentList.Add("--appdir");
            psi.ArgumentList.Add(data);
            psi.ArgumentList.Add($"--rpclisten={address}:{port}");
            psi.ArgumentList.Add("--yes");
            var generation = ++_generation;
            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => OnOutput(process, generation, e.Data);
            process.ErrorDataReceived += (_, e) => OnOutput(process, generation, e.Data);
            process.Exited += (_, _) => OnExited(process, generation);
            _process = process;
            try
            {
                if (!process.Start()) throw new InvalidOperationException("keryxd did not start.");
            }
            catch
            {
                if (ReferenceEquals(_process, process)) _process = null;
                process.Dispose();
                throw;
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var p = _process;
            if (p is null || p.HasExited) return;
            if (!await GracefulProcessStopper.TryStopAsync(p, TimeSpan.FromSeconds(15), ct) && !p.HasExited)
            {
                p.Kill(true);
                await p.WaitForExitAsync(ct);
            }
        }
        finally { _gate.Release(); }
    }

    private void OnOutput(Process process, long generation, string? line)
    {
        if (!ReferenceEquals(_process, process) || generation != _generation || string.IsNullOrWhiteSpace(line)) return;
        LineReceived?.Invoke("[NODE] " + line);
    }

    private void OnExited(Process process, long generation)
    {
        if (!ReferenceEquals(_process, process) || generation != _generation) return;
        Exited?.Invoke(process.ExitCode);
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync(); } catch { }
        _process?.Dispose();
        _gate.Dispose();
    }
}
