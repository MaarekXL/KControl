using System.Diagnostics;
using System.IO;
using System.Text;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class NodeService : IAsyncDisposable
{
    private Process? _process;
    public bool IsOwnedRunning => _process is { HasExited: false };
    public event Action<string>? LineReceived;

    public Task StartAsync(NodeConfig config, string address, int port, CancellationToken ct = default)
    {
        if (IsOwnedRunning) return Task.CompletedTask;
        var executable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.Executable), AppContext.BaseDirectory);
        if (!File.Exists(executable)) throw new FileNotFoundException("keryxd.exe is missing.", executable);
        var data = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.DataDirectory), AppContext.BaseDirectory);
        Directory.CreateDirectory(data);
        var psi = new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true };
        psi.ArgumentList.Add("--appdir"); psi.ArgumentList.Add(data); psi.ArgumentList.Add($"--rpclisten={address}:{port}"); psi.ArgumentList.Add("--yes");
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += OnOutput; process.ErrorDataReceived += OnOutput;
        if (!process.Start()) { process.Dispose(); throw new InvalidOperationException("keryxd did not start."); }
        _process = process; process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        var p = _process; if (p is null || p.HasExited) return;
        try { if (p.CloseMainWindow()) { using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(8)); await p.WaitForExitAsync(timeout.Token); } } catch (OperationCanceledException) { }
        if (!p.HasExited) { p.Kill(true); await p.WaitForExitAsync(ct); }
    }
    private void OnOutput(object sender, DataReceivedEventArgs e) { if (!string.IsNullOrWhiteSpace(e.Data)) LineReceived?.Invoke("[NODE] " + e.Data); }
    public async ValueTask DisposeAsync() { try { await StopAsync(); } catch { } _process?.Dispose(); }
}
