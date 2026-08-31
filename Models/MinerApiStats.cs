using System.Text.Json.Serialization;

namespace KeryxControl.Models;

public sealed class MinerApiStats
{
    [JsonPropertyName("uptime_s")] public long UptimeSeconds { get; set; }
    [JsonPropertyName("synced")] public bool Synced { get; set; }
    [JsonPropertyName("opoi_challenge_active")] public bool OpoiChallengeActive { get; set; }
    [JsonPropertyName("service_status")] public string? ServiceStatus { get; set; }
    [JsonPropertyName("total_hashrate_hs")] public double TotalHashrateHs { get; set; }
    [JsonPropertyName("accepted_blocks")] public long AcceptedBlocks { get; set; }
    [JsonPropertyName("rejected_blocks")] public long RejectedBlocks { get; set; }
    [JsonPropertyName("claimed_outputs")] public long ClaimedOutputs { get; set; }
    [JsonPropertyName("claimed_sompi")] public long ClaimedSompi { get; set; }
    [JsonPropertyName("escrow_pending_outputs")] public long EscrowPendingOutputs { get; set; }
    [JsonPropertyName("escrow_pending_sompi")] public long EscrowPendingSompi { get; set; }
    [JsonPropertyName("devices")] public List<MinerApiDevice> Devices { get; set; } = [];
}

public sealed class MinerApiDevice
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("hashrate_hs")] public double HashrateHs { get; set; }
    [JsonPropertyName("blocks_accepted")] public long BlocksAccepted { get; set; }
    [JsonPropertyName("blocks_rejected")] public long BlocksRejected { get; set; }
    [JsonPropertyName("temp_c")] public double? TemperatureC { get; set; }
    [JsonPropertyName("memory_temp_c")] public double? MemoryTemperatureC { get; set; }
    [JsonPropertyName("fan_percent")] public double? FanPercent { get; set; }
    [JsonPropertyName("power_draw_w")] public double? PowerDrawW { get; set; }
}
