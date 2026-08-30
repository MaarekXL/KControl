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

    public async Task StartAsync(MinerConfig config, string wallet, string nodeAddress, int nodePort, int gpuIndex, string tierArgument, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (IsRunning) throw new InvalidOperationException("Le mineur est déjà démarré.");
            var executable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.Executable), AppContext.BaseDirectory);
            if (!File.Exists(executable)) throw new FileNotFoundException("Binaire du mineur introuvable.", executable);
            var arguments = BuildArguments(config.ArgumentsTemplate, wallet, nodeAddress, nodePort, gpuIndex, tierArgument);
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
            try { if (p.CloseMainWindow()) await p.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds))).Token); }
            catch (OperationCanceledException) { }
            if (!p.HasExited) { p.Kill(true); await p.WaitForExitAsync(ct); }
        }
        finally { _gate.Release(); }
    }

    private void OnOutput(object sender, DataReceivedEventArgs e) { if (!string.IsNullOrWhiteSpace(e.Data)) LineReceived?.Invoke(e.Data); }
    private void OnExited(object? sender, EventArgs e) { if (sender is Process p) Exited?.Invoke(p.ExitCode); }
    private static IReadOnlyList<string> BuildArguments(string template, string wallet, string nodeAddress, int nodePort, int gpu, string tier)
    {
        var text = template.Replace("{wallet}", wallet).Replace("{node}", nodeAddress).Replace("{port}", nodePort.ToString()).Replace("{gpu}", gpu.ToString()).Replace("{tier}", tier);
        var args = new List<string>(); var b = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++) { var c = text[i]; if (c == '"') { quoted = !quoted; continue; } if (char.IsWhiteSpace(c) && !quoted) { if (b.Length > 0) { args.Add(b.ToString()); b.Clear(); } } else b.Append(c); }
        if (quoted) throw new FormatException("Guillemet non fermé dans ArgumentsTemplate."); if (b.Length > 0) args.Add(b.ToString()); return args;
    }
    public async ValueTask DisposeAsync() { try { await StopAsync(2); } catch { } _process?.Dispose(); _gate.Dispose(); }
}
