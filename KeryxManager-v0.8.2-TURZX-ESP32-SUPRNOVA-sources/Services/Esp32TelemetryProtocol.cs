using System.Globalization;
using System.Text;
using KeryxControl.Models;

namespace KeryxControl.Services;

public static class Esp32TelemetryProtocol
{
    public const int Port = 42100;
    public const string Version = "KX1";

    public static byte[] Encode(long sequence, Esp32DisplaySnapshot snapshot)
        => Encoding.ASCII.GetBytes(Format(sequence, snapshot));

    public static string Format(long sequence, Esp32DisplaySnapshot snapshot)
    {
        var state = snapshot.State.ToString().ToLowerInvariant();
        var gpuCount = Math.Clamp(snapshot.GpuCount, 0, 32);
        var hashrate = ClampFinite(snapshot.HashrateHs, 0, 1_000_000_000_000);
        var temperature = ClampFinite(snapshot.MaximumTemperatureC, 0, 150);
        var power = ClampFinite(snapshot.TotalPowerW, 0, 100_000);
        var accepted = Math.Max(0, snapshot.Accepted);
        var rejected = Math.Max(0, snapshot.Rejected);
        var uptime = Math.Max(0, snapshot.UptimeSeconds);
        var miner = SanitizeLabel(snapshot.Miner);

        return string.Join('|',
            Version,
            Math.Max(0, sequence).ToString(CultureInfo.InvariantCulture),
            state,
            gpuCount.ToString(CultureInfo.InvariantCulture),
            hashrate.ToString("0.###", CultureInfo.InvariantCulture),
            temperature.ToString("0.#", CultureInfo.InvariantCulture),
            power.ToString("0.#", CultureInfo.InvariantCulture),
            accepted.ToString(CultureInfo.InvariantCulture),
            rejected.ToString(CultureInfo.InvariantCulture),
            uptime.ToString(CultureInfo.InvariantCulture),
            miner);
    }

    private static double ClampFinite(double value, double minimum, double maximum)
        => double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum;

    private static string SanitizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Keryx Miner";
        var clean = new string(value.Where(c => c is >= ' ' and <= '~' && c != '|').Take(31).ToArray()).Trim();
        return clean.Length == 0 ? "Keryx Miner" : clean;
    }
}
