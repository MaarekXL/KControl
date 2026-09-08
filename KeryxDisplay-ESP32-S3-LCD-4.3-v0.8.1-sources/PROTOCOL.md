# Keryx Display Protocol v1

Keryx Manager broadcasts one UTF-8 UDP datagram every two seconds on port `42100`.
The display never contacts the node, the miner, or the blockchain directly.

The v1 wire format is a pipe-delimited line:

```text
KX1|sequence|state|gpu_count|hashrate_hs|max_temp_c|power_w|accepted|rejected|uptime_s|miner
```

Example:

```text
KX1|42|mining|4|5820000|61|179.8|33|0|3672|Keryx Miner
```

Rules:

- `state`: `waiting`, `mining`, `stopped`, `warning`, or `error`.
- `gpu_count`: 0 to 32.
- `hashrate_hs`: total rate in hashes per second.
- `max_temp_c`: hottest active GPU, in degrees Celsius.
- `power_w`: total GPU power in watts.
- `miner` must not contain `|`.
- No wallet address, Wi-Fi credential, authorization token, node peer, or log line is sent.
- A display marks the Manager offline after ten seconds without a valid datagram.

