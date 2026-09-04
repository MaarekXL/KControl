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
var stabilizing = tracker.Tick(start.AddSeconds(62));
Check(!stabilizing.Synchronized && stabilizing.StabilitySecondsRemaining == 29, "completion must expose the stabilization countdown");
Check(!tracker.Tick(start.AddSeconds(90)).Synchronized, "completion must remain in the 30-second stabilization window");
Check(tracker.Tick(start.AddSeconds(91)).Synchronized, "stable completed IBD must synchronize without requiring a later relay block");

var alreadySynced = new NodeSyncTracker();
alreadySynced.Reset(start);
alreadySynced.Observe("[NODE] Accepted block 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef via relay", start.AddSeconds(1));
Check(alreadySynced.Tick(start.AddSeconds(31)).Synchronized, "a node already synchronized before launch must be recognized after the stability window");

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

var fullDisplay = TurzxProtocol.BuildDisplayCommand(new TurzxRect(0, 0, 480, 320));
Check(fullDisplay.SequenceEqual(new byte[] { 0x00, 0x00, 0x07, 0x7d, 0x3f, 0xc5 }), "TURZX full-frame command encoding");
var orientation = TurzxProtocol.BuildLandscapeOrientationCommand();
Check(orientation.Length == 16 && orientation[5] == 121 && orientation[6] == 102
    && orientation[7] == 1 && orientation[8] == 224 && orientation[9] == 1 && orientation[10] == 64,
    "TURZX landscape orientation command");
Check(TurzxProtocol.BuildBrightnessCommand(70).SequenceEqual(new byte[] { 19, 0, 0, 0, 0, 110 }), "TURZX brightness encoding");
var bgra = new byte[TurzxProtocol.DisplayWidth * TurzxProtocol.DisplayHeight * 4];
bgra[0] = 0x00; bgra[1] = 0x00; bgra[2] = 0xff; bgra[3] = 0xff;
var rgb565 = TurzxProtocol.ConvertRegionToRgb565(
    new TurzxRenderedFrame(TurzxProtocol.DisplayWidth, TurzxProtocol.DisplayHeight, TurzxProtocol.DisplayWidth * 4, bgra),
    new TurzxRect(0, 0, 1, 1));
Check(rgb565.SequenceEqual(new byte[] { 0x00, 0xf8 }), "TURZX BGRA to little-endian RGB565 conversion");

Check(TurzxDisplayCatalog.Profiles.Count == 12, "complete TURZX model catalog");
Check(TurzxDisplayCatalog.Profiles.Count(x => x.Protocol == TurzxProtocolFamily.SerialRevisionA) == 1, "TURZX revision A catalog");
Check(TurzxDisplayCatalog.Profiles.Count(x => x.Protocol == TurzxProtocolFamily.SerialRevisionC) == 4, "TURZX revision C catalog");
Check(TurzxDisplayCatalog.Profiles.Count(x => x.Protocol == TurzxProtocolFamily.NativeUsb) == 7, "TURZX native USB catalog");
var usbPacket = TurzxNativeUsbProtocol.BuildSyncCommand(new DateTime(2026, 9, 4, 0, 0, 12, 345, DateTimeKind.Local));
Check(usbPacket.Length == 512 && usbPacket[510] == 0xa1 && usbPacket[511] == 0x1a,
    "TURZX native USB encrypted packet framing");
var usbBrightnessOff = TurzxNativeUsbProtocol.BuildBrightnessCommand(0, new DateTime(2026, 9, 4));
var usbBrightnessFull = TurzxNativeUsbProtocol.BuildBrightnessCommand(100, new DateTime(2026, 9, 4));
Check(!usbBrightnessOff.SequenceEqual(usbBrightnessFull), "TURZX native USB brightness payload");

if (failures.Count == 0)
{
    Console.WriteLine("All Keryx Control v0.8.0 TURZX smoke tests passed.");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine("FAILED: " + failure);
return 1;
