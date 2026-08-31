using System.Diagnostics;
using System.IO;
using System.Text;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class MinerService : IAsyncDisposable
{
    private Process? _process;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public bool IsRunning => _process is { HasExited: false };
    public event Action<string>? LineReceived;
    public event Action<int>? Exited;

    public async Task StartAsync(MinerConfig config, string wallet, string nodeAddress, int nodePort, IReadOnlyList<GpuLaunchSelection> gpus, int detectedGpuCount, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (IsRunning) throw new InvalidOperationException("Le mineur est déjà démarré.");
            _process?.Dispose();
            _process = null;
            var executable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.Executable), AppContext.BaseDirectory);
            if (!File.Exists(executable)) throw new FileNotFoundException("Binaire du mineur introuvable.", executable);
            if (gpus.Count == 0) throw new InvalidOperationException("No GPU was selected.");
            var arguments = BuildArguments(config, wallet, nodeAddress, nodePort, gpus, detectedGpuCount);
            var psi = new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true };
            foreach (var arg in arguments) psi.ArgumentList.Add(arg);
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += OnOutput;
            p.ErrorDataReceived += OnOutput;
            p.Exited += OnExited;
            if (!p.Start()) { p.Dispose(); throw new InvalidOperationException("Le mineur n'a pas pu démarrer."); }
            _process = p;
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync(int timeoutSeconds, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var p = _process;
            if (p is null || p.HasExited) return;
            try
            {
                if (p.CloseMainWindow())
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
                    await p.WaitForExitAsync(timeout.Token);
                }
            }
            catch (OperationCanceledException) { }
            if (!p.HasExited) { p.Kill(true); await p.WaitForExitAsync(ct); }
        }
        finally { _gate.Release(); }
    }

    private void OnOutput(object sender, DataReceivedEventArgs e) { if (!string.IsNullOrWhiteSpace(e.Data)) LineReceived?.Invoke(e.Data); }
    private void OnExited(object? sender, EventArgs e) { if (sender is Process p) Exited?.Invoke(p.ExitCode); }
    internal static IReadOnlyList<string> BuildArguments(MinerConfig config, string wallet, string nodeAddress, int nodePort, IReadOnlyList<GpuLaunchSelection> gpus, int detectedGpuCount)
    {
        var ordered = gpus.OrderBy(x => x.Index).ToArray();
        var selected = ordered.Where(x => x.IsSelected).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("No GPU was selected.");
        var args = new List<string>
        {
            "--mining-address", wallet,
            "--keryxd-address", nodeAddress,
            "--port", nodePort.ToString(),
            "--stats-bind", config.StatsAddress,
            "--stats-port", config.StatsPort.ToString()
        };
        if (selected.Length != detectedGpuCount)
        {
            args.Add("--cuda-device");
            args.Add(string.Join(',', selected.Select(x => x.Index)));
        }
        if (selected.Any(x => !x.IsAuto))
        {
            args.Add("--force-model");
            // --force-model is positional in CUDA-driver order. Keep entries for
            // deselected GPUs so a manual tier can never shift to the wrong card.
            args.Add(string.Join(',', ordered.Select(x => x.IsSelected && !x.IsAuto ? x.ForceName : x.AutoForceName)));
        }
        return args;
    }
    public async ValueTask DisposeAsync() { try { await StopAsync(2); } catch { } _process?.Dispose(); _gate.Dispose(); }
}
