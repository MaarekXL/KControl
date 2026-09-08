namespace KeryxControl.Models;

public enum TrayIconState
{
    Stopped,
    Warning,
    Mining,
    Error
}

public enum MiningHealthAlertKind
{
    ZeroHashrate,
    HighTemperature
}

public sealed record MiningHealthAlert(MiningHealthAlertKind Kind, double? Value = null);

public sealed record MiningHealthSnapshot(
    bool ZeroHashrateWarning,
    bool TemperatureWarning,
    IReadOnlyList<MiningHealthAlert> NewAlerts,
    bool HashrateRecovered,
    bool TemperatureRecovered);
