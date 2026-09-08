# \# Keryx Control Manager v0.8.2

# 

# Keryx Control Manager is a community-built Windows x64 application for configuring, starting and monitoring Keryx mining on NVIDIA GPUs.

# 

# It brings together the miner, the optional local node, GPU telemetry, power controls and external status displays. Keryx Control Manager is not a miner, node, wallet or official Keryx Labs product.

# 

# Project: <https://github.com/MaarekXL/KControl>

# 

# \## Supported components

# 

# Keryx Control Manager v0.8.2 supports:

# 

# \- Windows 10 and Windows 11 x64.

# \- NVIDIA GPUs supported by the selected CUDA miner.

# \- Keryx Miner v0.5.4-PoM.

# \- Optional `keryx-miner-supr` for NVIDIA RTX 40/50-series GPUs.

# \- keryxd v1.6.x for local solo mining.

# \- TURZX/Turing USB status displays.

# \- Waveshare ESP32-S3-LCD-4.3 non-touch Wi-Fi display.

# 

# Official Keryx, Suprnova, CUDA, model, node and wallet binaries are not redistributed with Keryx Control Manager.

# 

# \## Main features

# 

# \- Solo mining through a locally managed keryxd node.

# \- Pool mining through a compatible Keryx Stratum v3 endpoint.

# \- Optional Suprnova pool profile.

# \- Automatic NVIDIA GPU detection.

# \- Multi-GPU selection and monitoring.

# \- Automatic or forced PoM model profile per GPU.

# \- Total and per-GPU hashrate monitoring.

# \- Temperature, power, load, fan, VRAM and efficiency telemetry.

# \- Accepted and rejected block or share counters.

# \- Integrated solo-wallet authorization.

# \- Safe IPFS/Kubo preflight and recovery.

# \- Per-GPU NVIDIA power-limit control.

# \- Filtered and color-coded activity log.

# \- French and English interface.

# \- Windows notification-area mode.

# \- TURZX/Turing USB dashboards.

# \- Waveshare ESP32-S3 Wi-Fi telemetry.

# \- Single-instance protection.

# \- Zero-hashrate and temperature alerts.

# \- No automatic miner restart.

# \- No automatic thermal shutdown.

# 

# \## Mining configurations

# 

# | Mining software | Mode | Local node | Intended use |

# |---|---|---:|---|

# | Keryx Miner v0.5.4-PoM | Solo | Required | Mining through a local keryxd node |

# | Keryx Miner v0.5.4-PoM | Pool | Not required | Compatible Keryx Stratum v3 pools |

# | keryx-miner-supr | Suprnova pool | Not required | Modern NVIDIA RTX 40/50-series GPUs |

# 

# \## Installation

# 

# 1\. Extract the Keryx Control Manager v0.8.2 Windows x64 archive.

# 2\. Copy Keryx Miner and all its official companion files into `miner`.

# 3\. For solo mining, copy `keryxd.exe` into `keryxd`.

# 4\. Optionally copy the complete Suprnova NVIDIA package into `miner-suprnova`.

# 5\. Launch `KeryxControl.exe`.

# 6\. Run as administrator only if Windows requires elevation to modify an NVIDIA power limit.

# 

# The Windows release is self-contained. Installing the .NET runtime is not required.

# 

# \## Package layout

# 

# ```text

# KeryxControl-v0.8.2-TURZX-ESP32-SUPRNOVA-win-x64/

# ├── KeryxControl.exe

# ├── appsettings.json

# ├── README.txt

# ├── miner/

# │   ├── keryx-miner.exe

# │   ├── official miner DLLs

# │   ├── ipfs.exe

# │   ├── models/

# │   ├── .ipfs/

# │   ├── escrow.key

# │   └── escrow.cert

# ├── miner-suprnova/

# │   ├── keryx-miner-supr.exe

# │   └── all DLLs from the same package

# └── keryxd/

# &#x20;   ├── keryxd.exe

# &#x20;   └── keryx-cli.exe

# ```

# 

# The `miner-suprnova` directory and `keryx-cli.exe` are optional.

# 

# Preserve these files and directories when updating Keryx Control Manager:

# 

# ```text

# miner/models/

# miner/.ipfs/

# miner/escrow.key

# miner/escrow.cert

# miner/escrow\_state.json

# KeryxData/

# ```

# 

# `escrow.key` is private and linked to the solo payout authorization. Back it up securely and never publish it.

# 

# \## Solo mining

# 

# 1\. Select \*\*Keryx Miner v0.5.4-PoM\*\*.

# 2\. Select \*\*Solo — keryxd node\*\*.

# 3\. Enter the complete `keryx:` payout address.

# 4\. Keep `127.0.0.1:22110` unless the node uses another address or port.

# 5\. Start the node.

# 6\. Wait until synchronization is complete and stable.

# 7\. Start the miner.

# 

# Keryx Control Manager keeps mining disabled while the local node is not ready.

# 

# A node already listening on the configured port is detected and is not replaced automatically.

# 

# \## Wallet authorization

# 

# On the first solo start, the miner creates `miner/escrow.key` and prints its public escrow key.

# 

# 1\. Open \*\*Authorization\*\* in Keryx Control Manager.

# 2\. Copy the detected public key.

# 3\. Paste it into \*\*Authorise a miner\*\* in the Keryx wallet.

# 4\. Copy the returned `--escrow-cert` value.

# 5\. Paste it into Keryx Control Manager and save it.

# 6\. Stop and restart the miner to load `miner/escrow.cert`.

# 

# The certificate must contain 128 hexadecimal characters and match the payout address and existing private key.

# 

# Keryx Control Manager never reads the private contents of `escrow.key`.

# 

# \## Standard pool mining

# 

# 1\. Select \*\*Keryx Miner v0.5.4-PoM\*\*.

# 2\. Select \*\*Pool — Stratum v3\*\*.

# 3\. Enter the complete `keryx:` payout address.

# 4\. Enter the complete pool endpoint, including its protocol and port.

# 5\. Start the miner.

# 

# A local node and wallet authorization are not required for pool startup.

# 

# The selected pool must support the protocol expected by the Keryx miner.

# 

# \## Suprnova mining

# 

# Select \*\*Suprnova — NVIDIA RTX 40/50 (pool)\*\* under \*\*Mining software\*\*.

# 

# Keryx Control Manager then configures:

# 

# \- The payout address.

# \- The worker name.

# \- The Suprnova Stratum endpoint.

# \- The local statistics API.

# \- The selected GPUs.

# \- The model profile assigned to each GPU.

# \- Headless miner output for the activity log.

# 

# The default endpoint is:

# 

# ```text

# stratum+tcp://krx.suprnova.cc:4404

# ```

# 

# Endpoints using `stratum+ssl://` are also accepted when an explicit valid port is supplied.

# 

# Keep `keryx-miner-supr.exe` and every DLL from the same official package together.

# 

# Keryx Control Manager searches these locations:

# 

# 1\. The portable `miner-suprnova` directory.

# 2\. `%USERPROFILE%\\Downloads\\keryx-miner-supr-windows-nvidia-pom`.

# 3\. The location selected using \*\*Browse\*\*.

# 

# The first start may download and prepare a large model before reporting a hashrate. Keryx Control Manager provides a 30-minute preparation period before applying the normal zero-hashrate warning.

# 

# It never restarts the miner automatically.

# 

# Official Suprnova resources:

# 

# \- Mining guide: <https://krx.suprnova.cc/StartMining.html>

# \- Miner repository: <https://github.com/ocminer/keryx-miner-supr>

# 

# \## Multi-GPU support

# 

# Only checked GPUs are exposed to the selected miner.

# 

# Keryx Control Manager uses CUDA device ordering and GPU UUIDs to keep the selection consistent.

# 

# In multi-GPU mode:

# 

# \- Total hashrate is the sum of all selected GPUs.

# \- Total power is the sum of all selected GPUs.

# \- Maximum temperature is the highest selected-GPU temperature.

# \- Individual GPU telemetry remains available.

# \- Model profiles can be selected separately for each GPU.

# 

# \## Model profiles

# 

# | Profile | Intended minimum VRAM | Model family |

# |---|---:|---|

# | Very Light | 8 GB | Qwen3.5-9B |

# | Light | 12 GB | GLM-4-9B |

# | Standard | 16 GB | Gemma-4-12B |

# | High | 24 GB | Qwen3.6-27B |

# | Very High | 32 GB | Kimi-Linear-48B |

# 

# \*\*Auto\*\* chooses the highest configured profile compatible with the VRAM reported by NVIDIA.

# 

# The miner remains authoritative and may select another model. Forcing an oversized profile can cause an out-of-memory error.

# 

# \## Windows tray mode

# 

# Minimizing Keryx Control Manager moves it to the Windows notification area.

# 

# The tooltip displays a compact status such as:

# 

# ```text

# 4 GPU • 5.82 MH/s • 61 °C

# ```

# 

# Tray icon states:

# 

# \- Gray: stopped.

# \- Orange: starting, stopping or warning.

# \- Green: mining normally.

# \- Red: error.

# 

# Double-clicking the icon restores the main window.

# 

# The tray menu provides:

# 

# \- \*\*Open\*\*

# \- \*\*Start\*\*

# \- \*\*Stop\*\*

# \- \*\*Exit\*\*

# 

# Exiting while the managed miner or node is active requires confirmation.

# 

# A second application launch restores the existing instance instead of starting another one.

# 

# \## Operational safety

# 

# The \*\*Start\*\* command remains disabled until:

# 

# \- The selected miner executable exists.

# \- The payout address is valid.

# \- At least one GPU is selected.

# \- The selected connection settings are valid.

# \- The local node is ready when solo mode is selected.

# 

# A running miner that remains at `0 H/s` for three minutes triggers a warning. Normal OPoI inference pauses are excluded.

# 

# A selected GPU that remains at 85 °C or higher for 30 seconds also triggers a warning. Recovery is detected below 80 °C.

# 

# These warnings are informational:

# 

# \- The miner is not restarted automatically.

# \- Mining is not stopped automatically.

# \- Power limits are not changed automatically.

# 

# \## TURZX/Turing displays

# 

# The recommended display setting is \*\*AUTO\*\*.

# 

# Keryx Control Manager selects a compatible protocol and renders a landscape dashboard adapted to square, standard, 16:9 and ultra-wide screens.

# 

# Supported profiles include:

# 

# \- Serial revision A 3.5-inch.

# \- Serial revision C 2.1/2.8-inch round.

# \- Serial revision C 5-inch.

# \- Older serial revision C 8.8-inch.

# \- Native USB 2.8-inch round.

# \- Native USB 4.6-inch.

# \- Native USB 5.2-inch.

# \- Native USB 8-inch.

# \- Native USB 8.8-inch.

# \- Native USB 9.2-inch.

# \- Native USB 12.3-inch.

# 

# The `USB35INCHIPSV2` 3.5-inch profile has been validated on physical hardware.

# 

# Other profiles are implemented but remain experimental until tested on their corresponding displays.

# 

# Close the vendor `UsbMonitor.exe` before using Keryx Control Manager because only one application can own a serial display.

# 

# Native USB models use the bundled libusb component and may require the seller's WinUSB driver.

# 

# The dashboard can show:

# 

# \- Total hashrate.

# \- Maximum GPU temperature.

# \- Total power.

# \- GPU load.

# \- Accepted and rejected counters.

# \- Mining uptime.

# \- Miner and node state.

# 

# \## Waveshare ESP32-S3 Wi-Fi display

# 

# Keryx Control Manager sends a display-only status packet every two seconds over UDP port `42100`.

# 

# The companion firmware targets the Waveshare `ESP32-S3-LCD-4.3` non-touch display.

# 

# The packet contains:

# 

# \- Operating state.

# \- Selected GPU count.

# \- Total hashrate.

# \- Maximum GPU temperature.

# \- Total power.

# \- Accepted and rejected counters.

# \- Mining uptime.

# 

# It never contains:

# 

# \- The Keryx payout address.

# \- The Suprnova worker name.

# \- Wi-Fi credentials.

# \- Wallet authorization data.

# \- Network peers.

# \- Activity log lines.

# 

# The ESP32 connection is monitoring-only. The display cannot start, stop or control the miner.

# 

# An ordinary Remote Desktop disconnection does not stop transmission while the Windows session and Keryx Control Manager remain active.

# 

# Signing out of Windows closes applications and stops transmission.

# 

# \## IPFS/Kubo behavior

# 

# In solo mode, Keryx Control Manager:

# 

# \- Uses the portable `miner/.ipfs` repository through `IPFS\_PATH`.

# \- Validates the IPFS configuration and API before launch.

# \- Repairs only `miner/.ipfs/blocks/.temp` when Kubo is not running.

# \- Moves a busy gateway port 8080 to an available port between 8081 and 8099.

# \- Backs up the IPFS configuration before changing the gateway port.

# \- Rotates an oversized Kubo log.

# \- Stops Kubo only when it was started by the current miner launch.

# 

# Keryx Control Manager never deletes:

# 

# \- `miner/models`

# \- Permanent IPFS blocks

# \- The complete IPFS repository

# 

# \## Activity log

# 

# Log colors:

# 

# \- Green: normal activity.

# \- Orange: non-blocking warning.

# \- Red: wallet authorization or IPFS startup blocker.

# 

# Scrolling upward pauses only the live view. Log collection continues in a bounded queue.

# 

# \*\*Resume live\*\* returns to the latest activity.

# 

# \*\*Copy log\*\* includes visible and queued entries.

# 

# In solo mode, submitted blocks are counted only after a successful miner submission.

# 

# In pool mode, counters use explicit accepted, stale, low-difficulty and duplicate share messages.

# 

# \## Build from source

# 

# Building Keryx Control Manager requires:

# 

# \- Windows.

# \- .NET 8 SDK.

# 

# Build the application:

# 

# ```powershell

# dotnet build KeryxControl.csproj -c Release

# ```

# 

# Run the smoke tests:

# 

# ```powershell

# dotnet run --project Tests/KeryxControl.SmokeTests.csproj -c Release

# ```

# 

# Publish a self-contained Windows x64 executable:

# 

# ```powershell

# dotnet publish KeryxControl.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# ```

# 

# \## Privacy and operational limits

# 

# Keryx Control Manager does not:

# 

# \- Download or install updates automatically.

# \- Restart a miner automatically.

# \- Stop mining automatically because of temperature.

# \- Transmit sensitive configuration to companion displays.

# \- Allow an ESP32 display to control mining.

# \- Replace mining software, NVIDIA drivers or wallet security.

# 

# Mining software and GPU drivers remain responsible for hardware compatibility and workload execution.

