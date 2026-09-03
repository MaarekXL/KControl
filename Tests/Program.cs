using KeryxControl.Models;
using KeryxControl.Services;

var failures = new List<string>();

void Check(bool condition, string name)
{
    if (!condition) failures.Add(name);
}

var start = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc);
var tracker = new NodeSyncTracker();
tracker.Reset(start);
var firstHundred = tracker.Observe("[NODE] IBD: Processed 118979 blocks (100%)", start.AddSeconds(10));
Check(!firstHundred.Synchronized && firstHundred.Percent == 99, "IBD phase 100 must not mean synchronized");
tracker.Observe("[NODE] IBD with peer 1.2.3.4:22111 completed successfully", start.AddSeconds(11));
tracker.Observe("[NODE] IBD started with peer 5.6.7.8:22111", start.AddSeconds(12));
Check(!tracker.Tick(start.AddSeconds(60)).Synchronized, "a new IBD cycle must cancel completion");
tracker.Observe("[NODE] IBD with peer 5.6.7.8:22111 completed successfully", start.AddSeconds(61));
tracker.Observe("[NODE] Accepted block 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef via relay", start.AddSeconds(62));
Check(!tracker.Tick(start.AddSeconds(89)).Synchronized, "completion must remain in stabilization window");
Check(tracker.Tick(start.AddSeconds(92)).Synchronized, "stable completed IBD with live blocks must synchronize");

var parser = new MinerLogParser();
var solo = parser.Parse("Found a block on #0: abc", new MinerStats(), false);
Check(solo.Accepted == 0, "found block must not be counted before submission");
solo = parser.Parse("Block submitted successfully!", solo, false);
Check(solo.Accepted == 1, "successful solo submission must count once");
solo = parser.Parse("[NODE] Accepted block 0123456789abcdef via relay", solo, false);
Check(solo.Accepted == 1, "relayed node block must not affect miner counters");

var pool = parser.Parse("Shares: Accepted: 12 Stale: 1 Low difficulty: 2 Duplicate: 3 Pending: 4", new MinerStats(), true);
Check(pool.Accepted == 12 && pool.Rejected == 6, "pool summary counters");
var rate = parser.Parse("Current hashrate is 1.45 Mhash/s", new MinerStats(), false);
Check(Math.Abs(rate.HashrateMh - 1.45) < 0.0001, "Mhash/s parser");

var minerConfig = new MinerConfig { StatsAddress = "127.0.0.1", StatsPort = 3338 };
var selections = new[]
{
    new GpuLaunchSelection(1, "GPU-one", true, false, "light", "light"),
    new GpuLaunchSelection(3, "GPU-three", true, true, "", "very-high")
};
var argsList = MinerService.BuildArguments(minerConfig, "keryx:testaddress1234567890", MinerConnection.Solo("127.0.0.1", 22110), selections);
Check(argsList.Contains("--force-model") && argsList.Contains("light,very-high"), "non-contiguous multi-GPU model order");
Check(IpfsPreflightService.ParseTcpPort("/ip4/127.0.0.1/tcp/8081") == 8081, "IPFS multiaddress port parser");

if (failures.Count == 0)
{
    Console.WriteLine("All Keryx Control v0.7.3 smoke tests passed.");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine("FAILED: " + failure);
return 1;
