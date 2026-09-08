using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class MinerService : IAsyncDisposable
{
    private Process? _process;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _generation;
    public bool IsRunning => _process is { HasExited: false };
    public event Action<string>? LineReceived;
    public event Action<int>? Exited;

    public async Task StartAsync(
        MinerConfig config,
        MinerBackend backend,
        string wallet,
        MinerConnection connection,
        IReadOnlyList<GpuLaunchSelection> gpus,
        string? executableOverride = null,
        string? worker = null,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (IsRunning) throw new InvalidOperationException("Le mineur est déjà démarré.");
            _process?.Dispose();
            _process = null;
            var executable = ResolveExecutablePath(executableOverride ?? config.Executable,
                backend == MinerBackend.Suprnova ? "keryx-miner-supr.exe" : "keryx-miner.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Binaire du mineur introuvable.", executable);
            if (gpus.Count == 0) throw new InvalidOperationException("No GPU was selected.");
            var arguments = BuildArguments(config, backend, wallet, connection, gpus, worker);
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
            foreach (var arg in arguments) psi.ArgumentList.Add(arg);
            ConfigureGpuEnvironment(psi, gpus);
            if (backend == MinerBackend.Reference && !connection.IsPool)
                psi.Environment["IPFS_PATH"] = Path.Combine(psi.WorkingDirectory, ".ipfs");
            var generation = ++_generation;
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => OnOutput(p, generation, e.Data);
            p.ErrorDataReceived += (_, e) => OnOutput(p, generation, e.Data);
            p.Exited += (_, _) => OnExited(p, generation);
            _process = p;
            try
            {
                if (!p.Start()) throw new InvalidOperationException("Le mineur n'a pas pu démarrer.");
            }
            catch
            {
                if (ReferenceEquals(_process, p)) _process = null;
                p.Dispose();
                throw;
            }
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
            if (!await GracefulProcessStopper.TryStopAsync(p, TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)), ct) && !p.HasExited)
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
        LineReceived?.Invoke(line);
    }

    private void OnExited(Process process, long generation)
    {
        if (!ReferenceEquals(_process, process) || generation != _generation) return;
        Exited?.Invoke(process.ExitCode);
    }
    internal static IReadOnlyList<string> BuildArguments(
        MinerConfig config,
        MinerBackend backend,
        string wallet,
        MinerConnection connection,
        IReadOnlyList<GpuLaunchSelection> gpus,
        string? worker = null)
    {
        var ordered = gpus.OrderBy(x => x.Index).ToArray();
        var selected = ordered.Where(x => x.IsSelected).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("No GPU was selected.");
        if (backend == MinerBackend.Suprnova)
        {
            if (!connection.IsPool)
                throw new InvalidOperationException("The Suprnova miner requires a pool endpoint.");
            if (string.IsNullOrWhiteSpace(worker))
                throw new ArgumentException("A Suprnova worker name is required.", nameof(worker));

            var args = new List<string>
            {
                "-a", $"{wallet}.{worker.Trim()}",
                "-s", connection.Address,
                "--api-bind", $"{config.StatsAddress}:{config.StatsPort}",
                "--no-tui"
            };
            AddModelArguments(args, selected);
            return args;
        }

        var referenceArgs = new List<string>
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
            referenceArgs.Insert(4, "--port");
            referenceArgs.Insert(5, port.ToString());
        }
        AddModelArguments(referenceArgs, selected);
        return referenceArgs;
    }

    private static void AddModelArguments(List<string> args, IReadOnlyList<GpuLaunchSelection> selected)
    {
        if (selected.Any(x => !x.IsAuto))
        {
            args.Add("--force-model");
            // CUDA_VISIBLE_DEVICES below remaps the selected cards to contiguous
            // logical ordinals in this exact order. --force-model is positional
            // in that remapped order, so deselected cards must not be included.
            args.Add(string.Join(',', selected.Select(x => x.IsAuto ? x.AutoForceName : x.ForceName)));
        }
    }

    internal static string ResolveExecutablePath(string value, string expectedFileName = "keryx-miner-supr.exe")
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        var candidate = Path.GetFullPath(expanded, AppContext.BaseDirectory);
        return Directory.Exists(candidate) ? Path.Combine(candidate, expectedFileName) : candidate;
    }

    internal static bool IsValidSuprnovaWorker(string? value) => Regex.IsMatch(value?.Trim() ?? "",
        @"^[a-z0-9][a-z0-9_-]{0,31}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParsePoolEndpoint(string? value, out string host, out int port)
    {
        host = "";
        port = 0;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || !(uri.Scheme.Equals("stratum+tcp", StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals("stratum+ssl", StringComparison.OrdinalIgnoreCase))
            || string.IsNullOrWhiteSpace(uri.Host)
            || uri.Port is <= 0 or > 65535)
            return false;
        host = uri.Host;
        port = uri.Port;
        return true;
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
