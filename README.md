# Keryx Control Manager v0.7.3

Community-built Windows x64 interface for Keryx mining with NVIDIA GPUs. Keryx Control starts, stops, configures and monitors the official Keryx components; it is not a miner, node or wallet.

v0.7.3 targets:

- **Keryx Miner v0.5.4-PoM**
- **keryxd v1.6.0-PoM**
- Windows 10/11 x64
- NVIDIA RTX 4000/5000 and other CUDA GPUs supported by the official miner

Project: <https://github.com/MaarekXL/KControl>

> Independent community project — not an official Keryx Labs product.

## Main features

- Solo mining through a local managed keryxd node.
- Pool mining through Keryx Stratum v3.
- Automatic NVIDIA detection and multi-GPU selection.
- Per-GPU Auto or forced PoM model tier.
- Hashrate, power, temperature, load, fan, VRAM and efficiency monitoring.
- Solo accepted/rejected block counters and pool share counters.
- Integrated wallet authorization and `escrow.cert` creation.
- Safe IPFS/Kubo preflight and recovery.
- Per-GPU NVIDIA power-limit control.
- Filtered, color-coded, scrollable and copyable activity log.
- French and English interface.

## What is fixed in v0.7.3

- Node synchronization no longer treats an intermediate `IBD ... (100%)` line as final. keryxd can run several IBD phases; after the final completion message, Keryx Control shows a 30-second stability countdown on the **Start** button. Mining is enabled when no new IBD phase begins during that interval. A live relay block can start the same check when the node was already synchronized before Keryx Control was opened.
- Solo counters ignore `Found a block` and relayed node blocks. A block is counted only after the miner reports a successful submission, or from the miner statistics API.
- Pool counters use explicit accepted/stale/low-difficulty/duplicate share messages.
- The stale `miner/.ipfs/blocks/.temp` directory is repaired before a solo launch and after a matching IPFS startup failure. Models and permanent block data are never removed.
- An IPFS daemon that was already running before Keryx Control is not stopped by the application.
- Miner and node receive a graceful Ctrl+C shutdown before the force-stop fallback. This reduces unfinished IPFS writes.
- Rapid process exits and late output from an old process are isolated, preventing stale events and interface crashes.
- Log auto-scroll stops when the user scrolls up. Incoming lines are queued, not lost, and **Resume live** returns to the current output.
- Only the two actionable solo blockers — wallet certificate and IPFS startup — pause the log in red. Ordinary warnings stay orange; repeated relay warnings are summarized.
- Power limits are restored after a normal stop, application exit and unexpected miner/node exit when the GPU still has the value applied by Keryx Control.
- Invalid saved settings fall back safely and are backed up for diagnosis.

See [PATCH_NOTES_0.7.3.md](PATCH_NOTES_0.7.3.md) for the complete release notes.

## Package contents

Keryx Control does **not** redistribute Keryx, CUDA, model or wallet binaries. Add the official files yourself:

```text
KeryxControl-v0.7.3-win-x64/
├── KeryxControl.exe
├── appsettings.json
├── README.txt
├── miner/
│   ├── keryx-miner.exe
│   ├── official miner DLLs
│   ├── ipfs.exe                 (if supplied by the miner release)
│   ├── models/
│   ├── .ipfs/
│   ├── escrow.key
│   └── escrow.cert
└── keryxd/
    ├── keryxd.exe
    └── keryx-cli.exe            (optional)
```

Keep these folders/files when updating:

```text
miner/models/
miner/.ipfs/
miner/escrow.key
miner/escrow.cert
miner/escrow_state.json
KeryxData/
```

`escrow.key` is private and linked to the solo payout authorization. Back it up and never publish it.

## Installation

1. Extract the Keryx Control Windows x64 archive.
2. Copy Keryx Miner v0.5.4-PoM and all its official companion files into `miner`.
3. For solo mode, copy `keryxd.exe` v1.6.0-PoM into `keryxd`. This mandatory H12 node upgrade is installed in place; keep the existing `KeryxData` directory.
4. Launch `KeryxControl.exe`.
5. Run as administrator only when Windows requires elevation to change an NVIDIA power limit.

The supplied Windows package is self-contained; installing .NET is not required to run it.

## Solo mode

1. Select **Solo — keryxd node**.
2. Enter the complete `keryx:` payout address.
3. Keep `127.0.0.1:22110` unless you intentionally changed the local configuration.
4. Start the node and wait for **Synchronization: complete**.
5. Start the miner.

For reliable synchronization validation, v0.7.3 requires the local node to have been started by this Keryx Control session. A service already listening on the selected port is detected and will not be replaced.

### Wallet authorization

At the first solo start, the miner creates `miner/escrow.key` and prints a 64-character public escrow key.

1. Open **Authorization** in Keryx Control and copy the detected public key.
2. Paste it into **Authorise a miner** in the Keryx wallet.
3. Copy the returned `--escrow-cert` line.
4. Paste it into Keryx Control and save it.
5. Stop and restart the miner to load the new `miner/escrow.cert`.

The certificate contains 128 hexadecimal characters and must match both the payout address and the existing `escrow.key`. Keryx Control never reads the private key contents.

## Pool mode

1. Select **Pool — Stratum v3**.
2. Enter the payout address.
3. Enter the full pool endpoint, for example `stratum+tcp://pool.example.org:PORT`.
4. Start the miner.

A local node and escrow authorization are not required at pool startup. The pool must support Keryx Stratum v3/PoM.

## Multi-GPU and model profiles

One official miner process receives only the checked GPUs through `CUDA_VISIBLE_DEVICES`. GPU UUIDs and `CUDA_DEVICE_ORDER=PCI_BUS_ID` keep device ordering stable. Forced model tiers are generated in the same logical order as the selected GPUs.

| Profile | Intended minimum VRAM | Model family in v0.5.4-PoM |
|---|---:|---|
| Very Light | 8 GB | Qwen3.5-9B |
| Light | 12 GB | GLM-4-9B |
| Standard | 16 GB | Gemma-4-12B |
| High | 24 GB | Qwen3.6-27B |
| Very High | 32 GB | Kimi-Linear-48B |

**Auto** chooses the highest configured tier compatible with the VRAM reported by NVIDIA. The miner remains the authority and may downgrade a model. Forcing an oversized tier can cause an out-of-memory error.

## IPFS/Kubo behavior

In solo mode Keryx Control:

- uses the portable repository `miner/.ipfs` through `IPFS_PATH`;
- validates the IPFS configuration and API before launch;
- removes only the exact stale path `miner/.ipfs/blocks/.temp` when Kubo is not running;
- moves a busy gateway port 8080 to a free port from 8081 through 8099 after backing up the configuration;
- rotates an oversized Kubo log;
- stops Kubo only when it was not already running before this miner launch.

The application never deletes `miner/models`, permanent IPFS blocks or the whole IPFS repository.

## Log and counter behavior

- Green: normal activity.
- Orange: non-blocking warning.
- Red + automatic pause: IPFS startup blocker or wallet certificate blocker.
- Scrolling upward pauses only the live view; collection continues in a bounded queue.
- The visible list keeps the latest 500 entries and the paused queue keeps up to 1,000 new entries.
- **Copy log** includes both visible and queued entries.

In solo mode the dashboard counts submitted blocks, not every candidate found by the GPU and not blocks received from peers. In pool mode it counts accepted and explicitly rejected/stale/duplicate/low-difficulty shares.

## Power control

Keryx Control uses `nvidia-smi` to set the selected GPU power limit. Windows/NVIDIA may require administrator rights. If the command is refused, close the application and relaunch it with **Run as administrator**.

The original power limit is remembered per GPU and restored when possible. Restoration is deliberately skipped if another program changed the limit after Keryx Control applied it, so the application does not overwrite a newer user setting.

Keryx Control does not change clocks, voltage, fans, FP32, CUDA cores or miner kernels.

## Sensor limitations

Memory-junction temperature is shown only when the official miner statistics API or the available NVIDIA interface supplies it. `nvidia-smi` does not expose this sensor on every consumer GPU/driver combination, even when LibreHardwareMonitor can obtain it through a different low-level path.

## Privacy and scope

Keryx Control:

- does not mine by itself;
- does not modify the official miner or node;
- does not download Keryx executables;
- does not contain a remote-control web server;
- does not send telemetry to the project author;
- stores settings locally under `%LOCALAPPDATA%\KeryxControl`;
- keeps node data locally in `KeryxData`.

Normal network traffic comes from the official Keryx components and from any pool endpoint configured by the user.

## Build from source

Requirements: Windows, .NET 8 SDK and the Windows Desktop workload.

```powershell
dotnet build KeryxControl.csproj -c Release -r win-x64
dotnet publish KeryxControl.csproj -c Release -r win-x64 --self-contained true
dotnet run --project Tests/KeryxControl.SmokeTests.csproj -c Release
```

No third-party NuGet package is required by Keryx Control.

## Known limits

- Windows x64 only. Native Linux support and Wine are not part of v0.7.3.
- Node synchronization is derived from keryxd v1.6.0-PoM logs because no stable node status API is bundled with this frontend.
- Miner statistics depend on the local `/stats` endpoint and its current schema; the log parser supplies a conservative fallback.
- Keryx Control cannot guarantee support for GPU architectures unsupported by the official CUDA miner build.
