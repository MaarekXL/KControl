using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class MiningHealthMonitor
{
    public static readonly TimeSpan ZeroHashrateDelay = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan TemperatureDelay = TimeSpan.FromSeconds(30);
    public const double TemperatureWarningC = 85;
    public const double TemperatureRecoveryC = 80;

    private DateTime? _zeroHashrateSince;
    private DateTime? _highTemperatureSince;
    private bool _zeroHashrateWarning;
    private bool _temperatureWarning;

    public MiningHealthSnapshot Evaluate(DateTime utcNow, bool minerRunning, bool miningExpected,
        double hashrateMh, double? maximumTemperatureC, bool inferenceActive)
    {
        var alerts = new List<MiningHealthAlert>(2);
        var hashrateRecovered = false;
        var temperatureRecovered = false;

        var shouldHash = minerRunning && miningExpected && !inferenceActive;
        if (shouldHash && hashrateMh <= 0.0001)
        {
            _zeroHashrateSince ??= utcNow;
            if (!_zeroHashrateWarning && utcNow - _zeroHashrateSince >= ZeroHashrateDelay)
            {
                _zeroHashrateWarning = true;
                alerts.Add(new(MiningHealthAlertKind.ZeroHashrate));
            }
        }
        else
        {
            hashrateRecovered = _zeroHashrateWarning;
            _zeroHashrateWarning = false;
            _zeroHashrateSince = null;
        }

        if (minerRunning && maximumTemperatureC is >= TemperatureWarningC)
        {
            _highTemperatureSince ??= utcNow;
            if (!_temperatureWarning && utcNow - _highTemperatureSince >= TemperatureDelay)
            {
                _temperatureWarning = true;
                alerts.Add(new(MiningHealthAlertKind.HighTemperature, maximumTemperatureC));
            }
        }
        else if (!_temperatureWarning)
        {
            _highTemperatureSince = null;
        }
        else if (!minerRunning || maximumTemperatureC is null or <= TemperatureRecoveryC)
        {
            temperatureRecovered = _temperatureWarning;
            _temperatureWarning = false;
            _highTemperatureSince = null;
        }

        return new(_zeroHashrateWarning, _temperatureWarning, alerts, hashrateRecovered, temperatureRecovered);
    }

    public void Reset()
    {
        _zeroHashrateSince = null;
        _highTemperatureSince = null;
        _zeroHashrateWarning = false;
        _temperatureWarning = false;
    }
}

public static class TrayTextFormatter
{
    public const string ProductName = "Keryx Control Manager";

    public static string Format(int selectedGpuCount, string? hashrate, string? temperature)
    {
        var gpu = selectedGpuCount == 1 ? "1 GPU" : $"{Math.Max(0, selectedGpuCount)} GPU";
        var rate = string.IsNullOrWhiteSpace(hashrate) ? "0,00 MH/s" : hashrate.Trim();
        var temp = string.IsNullOrWhiteSpace(temperature) ? "— °C" : temperature.Trim();
        var value = $"{ProductName}\n{gpu} • {rate} • {temp}";
        return value.Length <= 63 ? value : value[..63];
    }
}
