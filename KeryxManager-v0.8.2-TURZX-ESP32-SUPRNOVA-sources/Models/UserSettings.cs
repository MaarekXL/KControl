namespace KeryxControl.Models;

public sealed class UserSettings
{
    public string Language { get; set; } = "fr";
    public string Wallet { get; set; } = "";
    public string MinerBackend { get; set; } = "reference";
    public string MiningMode { get; set; } = "solo";
    public string NodeAddress { get; set; } = "127.0.0.1";
    public int NodePort { get; set; } = 22110;
    public string PoolAddress { get; set; } = "";
    public string SuprnovaMinerPath { get; set; } = "";
    public string SuprnovaWorker { get; set; } = "rig01";
    public string SuprnovaPoolAddress { get; set; } = "stratum+tcp://krx.suprnova.cc:4404";
    public bool TurzxEnabled { get; set; } = true;
    public string TurzxModel { get; set; } = "AUTO";
    public string TurzxPort { get; set; } = "AUTO";
    public int TurzxBrightness { get; set; } = 70;
    public Dictionary<string, GpuPreference> Gpus { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double WindowWidth { get; set; } = 1160;
    public double WindowHeight { get; set; } = 790;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool WindowMaximized { get; set; }
}

public sealed class GpuPreference
{
    public bool Selected { get; set; } = true;
    public string TierId { get; set; } = "auto";
    public double? PowerLimit { get; set; }
}
