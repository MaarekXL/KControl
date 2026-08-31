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

    public async Task StartAsync(
        MinerConfig config,
        string wallet,
        MinerConnection connection,
        IReadOnlyList<GpuLaunchSelection> gpus,
        CancellationToken ct = default)
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
            var arguments = BuildArguments(config, wallet, connection, gpus);
            var psi = new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true };
            foreach (var arg in arguments) psi.ArgumentList.Add(arg);
            ConfigureGpuEnvironment(psi, gpus);
            if (!connection.IsPool)
                psi.Environment["IPFS_PATH"] = Path.Combine(psi.WorkingDirectory, ".ipfs");
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
    internal static IReadOnlyList<string> BuildArguments(
        MinerConfig config,
        string wallet,
        MinerConnection connection,
        IReadOnlyList<GpuLaunchSelection> gpus)
    {
        var ordered = gpus.OrderBy(x => x.Index).ToArray();
        var selected = ordered.Where(x => x.IsSelected).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("No GPU was selected.");
        var args = new List<string>
        {
            "--mining-address", wallet,
            "--keryxd-address", connection.Address,
            "--stats-bind", config.StatsAddress,
            "--stats-port", config.StatsPort.ToString()
        };
        if (!connection.IsPool)
        {
            var port = connection.Port.GetValueOrDefault();
            if (port is <= 0 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(connection), "The local node port is invalid.");
            args.Insert(4, "--port");
            args.Insert(5, port.ToString());
        }
        if (selected.Any(x => !x.IsAuto))
        {
            args.Add("--force-model");
            // CUDA_VISIBLE_DEVICES below remaps the selected cards to contiguous
            // logical ordinals in this exact order. --force-model is positional
            // in that remapped order, so deselected cards must not be included.
            args.Add(string.Join(',', selected.Select(x => x.IsAuto ? x.AutoForceName : x.ForceName)));
        }
        return args;
    }

    internal static void ConfigureGpuEnvironment(ProcessStartInfo startInfo, IReadOnlyList<GpuLaunchSelection> gpus)
    {
        var selected = gpus.Where(x => x.IsSelected).OrderBy(x => x.Index).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("No GPU was selected.");

        startInfo.Environment["CUDA_DEVICE_ORDER"] = "PCI_BUS_ID";
        startInfo.Environment["CUDA_VISIBLE_DEVICES"] = string.Join(',', selected.Select(x =>
            !string.IsNullOrWhiteSpace(x.Uuid) ? x.Uuid.Trim() : x.Index.ToString()));
    }
    public async ValueTask DisposeAsync() { try { await StopAsync(2); } catch { } _process?.Dispose(); _gate.Dispose(); }
}
