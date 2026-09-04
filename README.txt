KERYX CONTROL MANAGER v0.8.0 TURZX
==================================

Community-built Windows x64 interface for Keryx Miner v0.5.4-PoM,
keryxd v1.6.0-PoM and NVIDIA GPUs. Independent community project;
not an official Keryx Labs product.

Project: https://github.com/MaarekXL/KControl

REQUIREMENTS
------------
- Windows 10/11 x64.
- NVIDIA driver and a GPU supported by the official Keryx CUDA miner.
- Official Keryx Miner v0.5.4-PoM files in the "miner" folder.
- For solo mode: keryxd.exe v1.6.0-PoM in the "keryxd" folder.
  This mandatory H12 upgrade is installed in place; keep KeryxData.
- Administrator rights only when required to change an NVIDIA power limit.
- The supplied build is self-contained; no separate .NET installation is needed.
- Optional TURZX/Turing display from 2.1 to 12.3 inches (see compatibility below).

Keryx Control does not include or download the miner, node, CUDA libraries,
models, Kubo/IPFS or wallet software.

INSTALLATION
------------
1. Extract the Keryx Control archive.
2. Put keryx-miner.exe and all official companion files in "miner".
3. For solo mining, put keryxd.exe in "keryxd".
4. Start KeryxControl.exe.

Keep these items when updating:
  miner/models
  miner/.ipfs
  miner/escrow.key
  miner/escrow.cert
  miner/escrow_state.json
  KeryxData

escrow.key is private. Back it up and never publish it.

MAIN FEATURES
-------------
- Responsive TURZX dashboard for square, standard, 16:9 and ultra-wide screens.
- Hashrate, maximum GPU temperature, power, load, blocks and uptime on the display.
- Automatic TURZX reconnection and adjustable brightness. UsbMonitor.exe must be closed.
- Solo mining through a managed local keryxd node.
- Keryx Stratum v3 pool mode.
- Automatic NVIDIA detection and multi-GPU selection.
- Auto or forced PoM model tier per GPU.
- Hashrate, power, temperature, load, fan, VRAM and efficiency.
- Correct solo block and pool share counters.
- Integrated wallet authorization and escrow.cert creation.
- Safe IPFS/Kubo preflight and recovery.
- NVIDIA power-limit control per GPU.
- Filtered, colored, scrollable and copyable log.
- French and English interface.

TURZX COMPATIBILITY
-------------------
- Serial revision A: 3.5-inch, 480 x 320 (hardware validated on USB35INCHIPSV2).
- Serial revision C: 2.1/2.8-inch round 480 x 480, 5-inch 800 x 480,
  and older 8.8-inch 1920 x 480.
- Native USB generation: 2.8-inch round 480 x 480, 4.6-inch 960 x 320,
  5.2-inch 1280 x 720, 8-inch 1280 x 800, 8.8-inch 1920 x 480,
  9.2-inch 1920 x 462 and 12.3-inch 1920 x 720.

AUTO is recommended for the validated 3.5-inch display and native-USB models.
Some older revision-C screens reuse the same USB identity for different sizes;
select their exact size manually if AUTO cannot identify them safely.

Only the 3.5-inch USB35INCHIPSV2 has been tested on physical hardware for this
release. Other profiles implement the documented protocol and have passed frame,
packet and build tests, but remain experimental until tested on each real model.
The bundled libusb component is used only for native-USB models. Depending on the
seller package, its WinUSB device driver may need to be installed once.

V0.7.3 FIXES
------------
- Intermediate IBD 100% lines no longer falsely mark the node synchronized.
  After the final completion message, START displays a 30-second stability
  countdown. A new IBD phase resets it. A live block starts the same check for
  a node that was already synchronized before Keryx Control was opened.
- Solo counters count successful miner submissions, not "Found a block" or
  blocks relayed by the node. Pool counters use explicit Stratum share results.
- Repairs only the exact stale miner/.ipfs/blocks/.temp path before solo mining
  and after the matching failure. Models and permanent blocks are untouched.
- Never stops an IPFS daemon that was already running before Keryx Control.
- Graceful Ctrl+C shutdown for the miner/node before a force-stop fallback.
- Fixes rapid-exit/stale-process events that could crash or confuse the UI.
- Scrolling up pauses the live view while new lines remain queued.
- Only certificate and IPFS blockers pause the log in red. Other warnings are
  orange and repeated relay warnings are summarized.
- Restores changed power limits on stop, exit and unexpected process failure.
- Invalid saved settings fall back safely and are backed up.

SOLO MODE
---------
1. Select Solo - keryxd node.
2. Enter the complete address beginning with keryx:.
3. Use 127.0.0.1:22110 unless intentionally configured otherwise.
4. Start the node and wait for "Synchronization: complete".
5. Start the miner.

For precise synchronization validation, v0.7.3 requires the local node to be
started by this Keryx Control session. It will not replace a service already
listening on the chosen port.

WALLET AUTHORIZATION
--------------------
1. Start solo mining once so the miner creates escrow.key and prints its
   64-character public escrow key.
2. Open Authorization and copy that public key.
3. Paste it into "Authorise a miner" in the Keryx wallet.
4. Copy the returned --escrow-cert line.
5. Paste and save it in Keryx Control, then restart the miner.

The certificate must contain exactly 128 hexadecimal characters and match both
the payout address and the existing escrow.key. Keryx Control never reads the
private key contents.

POOL MODE
---------
Use a full Keryx Stratum v3 endpoint such as:
  stratum+tcp://pool.example.org:PORT

A local node and escrow certificate are not required at pool startup. The pool
must support the Keryx v3/PoM protocol.

MULTI-GPU
---------
The official miner receives only checked GPUs through CUDA_VISIBLE_DEVICES.
UUIDs and PCI_BUS_ID ordering keep GPU/model assignments stable. Auto chooses
the highest configured tier compatible with reported VRAM; the miner may still
downgrade it. An oversized forced model can run out of GPU memory.

IPFS / KUBO
-----------
Solo mode uses miner/.ipfs through IPFS_PATH. Keryx Control validates the API,
repairs the exact stale blocks/.temp path only while Kubo is stopped, backs up
the config before moving a busy gateway port from 8080 to 8081-8099, rotates an
oversized log and stops only a Kubo instance associated with this launch.

LOG AND COUNTERS
----------------
- Green: normal; orange: warning; red + pause: IPFS/certificate blocker.
- Scrolling upward pauses display updates, not collection. Resume Live returns
  to the current output. Copy Log includes visible and queued entries.
- Visible history: 500 entries. Paused queue: up to 1,000 entries.
- Solo dashboard: successfully submitted blocks. Pool dashboard: accepted and
  explicitly rejected/stale/duplicate/low-difficulty shares.

POWER CONTROL
-------------
Power limits are applied with nvidia-smi. If Windows/NVIDIA refuses the change,
restart Keryx Control with "Run as administrator". The initial value is restored
when possible. If another application changes the value afterward, Keryx Control
does not overwrite that newer setting.

No clocks, voltages, fans, FP32 units, CUDA cores or miner kernels are changed.

LIMITS AND PRIVACY
------------------
- Windows x64 only; no native Linux or Wine support in v0.7.3.
- Memory-junction temperature appears only when exposed by the miner API or the
  available NVIDIA interface. LibreHardwareMonitor may use another sensor path.
- Synchronization is conservatively derived from keryxd v1.6.0-PoM logs.
- The application contains no remote-control web server and sends no telemetry
  to the project author. Settings stay under %LOCALAPPDATA%\KeryxControl and
  node data stays in KeryxData.

See PATCH_NOTES_0.7.3.txt for the detailed release notes.
