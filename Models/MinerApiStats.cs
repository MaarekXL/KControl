using System.Text.Json.Serialization;

namespace KeryxControl.Models;

public sealed class MinerApiStats
{
    [JsonPropertyName("uptime_s")] public long UptimeSeconds { get; set; }
    [JsonPropertyName("synced")] public bool Synced { get; set; }
    [JsonPropertyName("total_hashrate_hs")] public double TotalHashrateHs { get; set; }
    [JsonPropertyName("accepted_blocks")] public int AcceptedBlocks { get; set; }
    [JsonPropertyName("rejected_blocks")] public int RejectedBlocks { get; set; }
    [JsonPropertyName("devices")] public List<MinerApiDevice> Devices { get; set; } = [];
}

public sealed class MinerApiDevice
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("hashrate_hs")] public double HashrateHs { get; set; }
    [JsonPropertyName("blocks_accepted")] public int BlocksAccepted { get; set; }
    [JsonPropertyName("blocks_rejected")] public int BlocksRejected { get; set; }
    [JsonPropertyName("temp_c")] public double TemperatureC { get; set; }
    [JsonPropertyName("fan_percent")] public double? FanPercent { get; set; }
    [JsonPropertyName("power_draw_w")] public double PowerDrawW { get; set; }
}
