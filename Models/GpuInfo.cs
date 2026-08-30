namespace KeryxControl.Models;
public sealed record GpuInfo(int Index, string Uuid, string Name, int MemoryTotalMiB, double PowerMinW, double PowerMaxW, double PowerLimitW, string DriverVersion)
{
    public string DisplayName => $"GPU {Index} — {Name} ({MemoryTotalMiB / 1024d:0.#} Go)";
}

public sealed record GpuMetrics(double TemperatureC, double PowerW, double UtilizationPercent, int MemoryUsedMiB, int MemoryTotalMiB, double FanPercent);
