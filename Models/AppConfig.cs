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
    public string ArgumentsTemplate { get; set; } = "--mining-address {wallet} --keryxd-address {node} --port {port} --cuda-device {gpu} {tier}";
    public int StopTimeoutSeconds { get; set; } = 8;
}
public sealed class TierConfig
{
    public string Name { get; set; } = "Standard";
    public int MinVramGb { get; set; }
    public string Argument { get; set; } = "";
    public override string ToString() => Name;
}
