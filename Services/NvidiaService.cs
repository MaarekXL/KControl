using System.Diagnostics;
using System.Globalization;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class NvidiaService
{
    private const int TimeoutMs = 5000;

    public async Task<IReadOnlyList<GpuInfo>> DetectAsync(CancellationToken ct = default)
    {
        const string fields = "index,uuid,name,memory.total,power.min_limit,power.max_limit,power.limit,driver_version";
        var output = await RunAsync($"--query-gpu={fields} --format=csv,noheader,nounits", ct);
        var result = new List<GpuInfo>();
        foreach (var line in Lines(output))
        {
            var p = SplitCsv(line);
            if (p.Length < 8 || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) continue;
            result.Add(new(index, p[1], p[2], Int(p[3]), Double(p[4]), Double(p[5]), Double(p[6]), p[7]));
        }
        return result;
    }

    public async Task<IReadOnlyDictionary<int, GpuMetrics>> GetAllMetricsAsync(CancellationToken ct = default)
    {
        const string fields = "index,temperature.gpu,power.draw,utilization.gpu,memory.used,memory.total,fan.speed";
        var output = await RunAsync($"--query-gpu={fields} --format=csv,noheader,nounits", ct);
        var result = new Dictionary<int, GpuMetrics>();
        foreach (var line in Lines(output))
        {
            var p = SplitCsv(line);
            if (p.Length < 7 || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) continue;
            result[index] = new(index, Double(p[1]), Double(p[2]), Double(p[3]), Int(p[4]), Int(p[5]), Double(p[6]));
        }
        return result;
    }

    public async Task<GpuMetrics> GetMetricsAsync(int index, CancellationToken ct = default)
    {
        var all = await GetAllMetricsAsync(ct);
        return all.TryGetValue(index, out var metrics) ? metrics : throw new InvalidOperationException("Réponse NVIDIA incomplète.");
    }

    public async Task SetPowerLimitAsync(GpuInfo gpu, double watts, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(watts, gpu.PowerMinW, gpu.PowerMaxW);
        await RunAsync($"--id={gpu.Index} --power-limit={clamped.ToString("0.##", CultureInfo.InvariantCulture)}", ct);
    }

    private static async Task<string> RunAsync(string arguments, CancellationToken ct)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("nvidia-smi", arguments) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        try { if (!process.Start()) throw new InvalidOperationException("Impossible de lancer nvidia-smi."); }
        catch (Exception ex) { throw new InvalidOperationException("nvidia-smi est introuvable. Installez ou mettez à jour le pilote NVIDIA.", ex); }
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeoutMs);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { TryKill(process); throw new TimeoutException("nvidia-smi n'a pas répondu dans les 5 secondes."); }
        var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"nvidia-smi a échoué ({process.ExitCode}) : {error.Trim()}");
        return await stdout;
    }

    private static IEnumerable<string> Lines(string s) => s.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string[] SplitCsv(string s) => s.Split(',', StringSplitOptions.TrimEntries);
    private static int Int(string s) => int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    private static double Double(string s) => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    private static void TryKill(Process p) { try { if (!p.HasExited) p.Kill(true); } catch { } }
}
