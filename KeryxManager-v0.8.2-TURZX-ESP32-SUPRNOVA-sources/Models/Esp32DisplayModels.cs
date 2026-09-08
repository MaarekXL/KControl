namespace KeryxControl.Models;

public enum Esp32DisplayState
{
    Waiting,
    Mining,
    Stopped,
    Warning,
    Error
}

public sealed record Esp32DisplaySnapshot(
    Esp32DisplayState State,
    int GpuCount,
    double HashrateHs,
    double MaximumTemperatureC,
    double TotalPowerW,
    long Accepted,
    long Rejected,
    long UptimeSeconds,
    string Miner);
