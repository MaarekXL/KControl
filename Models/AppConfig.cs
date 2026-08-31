namespace KeryxControl.Models;

public sealed class AppConfig
{
    public MinerConfig Miner { get; set; } = new();
    public NodeConfig Node { get; set; } = new();
    public List<TierConfig> Tiers { get; set; } = [];
}

public sealed class NodeConfig
{
    public string Executable { get; set; } = "keryxd/keryxd.exe";
    public string DataDirectory { get; set; } = "../KeryxData";
}

public sealed class MinerConfig
{
    public string Executable { get; set; } = "miner/keryx-miner.exe";
    public int StopTimeoutSeconds { get; set; } = 8;
    public string StatsAddress { get; set; } = "127.0.0.1";
    public int StatsPort { get; set; } = 3338;
}

public sealed class TierConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameFr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Model { get; set; } = "";
    public int MinVramGb { get; set; }
    public string Argument { get; set; } = "";
    public string ForceName { get; set; } = "";
    public bool IsAuto => Id.Equals("auto", StringComparison.OrdinalIgnoreCase);
}

public sealed record GpuLaunchSelection(int Index, bool IsSelected, bool IsAuto, string ForceName, string AutoForceName);
