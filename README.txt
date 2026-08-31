# Keryx Control v0.7.2

Community Windows x64 frontend for **Keryx Miner v0.5.4-PoM** and NVIDIA RTX GPUs.

Keryx Control provides a bilingual FR/EN desktop interface for solo mining through a keryxd node or pool mining through the new Keryx Stratum v3 protocol. It is a community project and is not an official Keryx Labs product.

- Official miner release: <https://github.com/Keryx-Labs/keryx-miner/releases/tag/v0.5.4-PoM>
- Keryx Control source: <https://github.com/MaarekXL/KControl>

---

## Français

### Nouveautés de la v0.7.2

- Compatibilité avec **Keryx Miner v0.5.4-PoM**.
- Choix entre **Solo / nœud keryxd** et **Pool / Stratum v3**.
- En mode pool, l’adresse complète `stratum+tcp://hôte:port` est transmise au mineur.
- En mode pool v3, aucun nœud keryxd ni certificat escrow local n’est demandé au démarrage, et le mineur officiel ne lance pas automatiquement Kubo.
- Sélection fiable d’un ou plusieurs GPU avec `CUDA_VISIBLE_DEVICES`, sans utiliser l’option inexistante `--cuda-device`.
- Ordre stable des profils par GPU avec `CUDA_DEVICE_ORDER=PCI_BUS_ID` et des UUID NVIDIA.
- Dépôt IPFS Windows portable fixé à `miner/.ipfs` grâce à `IPFS_PATH`, en cohérence avec le contrôle des ports effectué par l’interface.
- Initialisation IPFS vérifiée avant le démarrage en solo.
- Prise en charge des nouveaux champs de télémétrie v0.5.4 : température de jonction mémoire lorsqu’elle est disponible, état OPoI et état de service.
- Compteurs pool corrigés à partir des messages Stratum : shares acceptées, stale, low difficulty et duplicate.
- Compteurs 64 bits et champs de télémétrie optionnels pour éviter la perte complète des statistiques lorsqu’un capteur NVML est indisponible.

### Compatibilité

| Élément | Support |
|---|---|
| Windows | Windows 10/11 x64 |
| Runtime | Build autonome fourni, .NET 8 requis uniquement pour compiler |
| GPU | NVIDIA RTX 4000 / RTX 5000, et autres GPU acceptés par le mineur officiel |
| Mineur | Keryx Miner v0.5.4-PoM |
| Solo | keryxd local ou nœud keryxd accessible |
| Pool | Pool compatible **keryx-stratum-v3** |
| Langues | Français et anglais |

La prise en charge réelle d’un GPU dépend du pilote NVIDIA et du build CUDA fourni par Keryx Labs.

### Contenu de l’archive

Les binaires officiels de Keryx, les DLL CUDA, Kubo/IPFS et les modèles ne sont pas redistribués dans Keryx Control.

```text
KeryxControl-v0.7.2-win-x64/
├── KeryxControl.exe
├── appsettings.json
├── README.txt
├── miner/
│   ├── keryx-miner.exe
│   ├── DLL officielles du mineur
│   ├── ipfs.exe
│   ├── models/
│   ├── .ipfs/
│   ├── escrow.key
│   ├── escrow.cert
│   └── escrow_state.json
└── keryxd/
    ├── keryxd.exe
    └── keryx-cli.exe
```

### Installation ou mise à jour

1. Téléchargez l’archive Windows officielle de Keryx Miner v0.5.4-PoM.
2. Copiez `keryx-miner.exe`, `ipfs.exe` et toutes les DLL/fichiers officiels dans `miner`.
3. Pour le mode solo piloté par l’interface, copiez `keryxd.exe` dans `keryxd`.
4. Lancez `KeryxControl.exe`.

Lors d’une mise à jour, conservez impérativement :

```text
miner/models/
miner/.ipfs/
miner/escrow.key
miner/escrow.cert
miner/escrow_state.json
KeryxData/
```

`escrow.key` est une clé privée liée aux récompenses solo déjà générées. Ne la publiez jamais et ne la remplacez pas sans sauvegarde.

### Minage solo

1. Sélectionnez **Solo — nœud keryxd**.
2. Saisissez l’adresse de paiement commençant par `keryx:`.
3. Laissez `127.0.0.1` et `22110` pour le nœud local, ou indiquez un nœud accessible.
4. Démarrez le nœud et attendez `Synchronisation : terminée` lorsqu’il est géré par Keryx Control.
5. Démarrez le mineur.

En solo, le mineur utilise `miner/.ipfs`. Keryx Control vérifie l’API 5001 et déplace automatiquement la passerelle 8080 vers un port libre entre 8081 et 8099 si nécessaire.

### Autorisation escrow en solo

Au premier démarrage solo, le mineur crée `miner/escrow.key` et affiche une clé publique de 64 caractères hexadécimaux.

1. Ouvrez **AUTORISATION**.
2. Copiez la clé publique détectée.
3. Dans le wallet Keryx, ouvrez **Authorise a miner** et collez cette clé.
4. Copiez la ligne `--escrow-cert` renvoyée par le wallet.
5. Collez-la dans Keryx Control et enregistrez `escrow.cert`.
6. Redémarrez le mineur avec la même adresse de paiement.

Le certificat doit contenir exactement 128 caractères hexadécimaux et correspondre à la fois à l’adresse et à `escrow.key`. Keryx Control ne lit jamais le contenu de la clé privée.

### Minage en pool Stratum v3

1. Sélectionnez **Pool — Stratum v3**.
2. Saisissez votre adresse de paiement Keryx.
3. Saisissez l’adresse complète fournie par le pool :

   ```text
   stratum+tcp://pool.example.org:PORT
   ```

4. Démarrez le mineur.

Le pool doit fournir des jobs **keryx-stratum-v3** avec score DAA et keepalive. Une ancienne implémentation Stratum peut accepter la connexion mais refuser les jobs ou les shares PoM.

En pool v3 :

- le nœud keryxd local n’est pas requis ;
- `escrow.key` et `escrow.cert` ne sont pas requis ;
- le mineur officiel ne lance pas automatiquement le démon IPFS local ;
- les shares acceptées/rejetées sont lues dans les messages Stratum du mineur ;
- la connexion, les jobs PoM et les keepalives sont gérés par le protocole Stratum v3.

Le code v0.5.4 conserve une voie d’upload vers l’API IPFS configurée pour certaines tâches OPoI envoyées par un bridge. Si votre pool active ces tâches, suivez ses instructions concernant l’API IPFS ; Keryx Control ne démarre pas Kubo de sa propre initiative en mode pool.

Si le test réseau initial échoue, Keryx Control affiche un avertissement mais laisse le mineur démarrer, afin que sa logique de reconnexion puisse fonctionner.

### Profils de modèles v0.5.4-PoM

| Profil | Modèle | VRAM minimale annoncée |
|---|---|---:|
| Very Light | Qwen3.5-9B-abliterated Q5_K_M | 8 Go |
| Light | GLM-4-9B-0414 Q6_K | 12 Go |
| Standard | Gemma-4-12B-abliterated Q6_K | 16 Go |
| High | Qwen3.6-27B Q4_K_M | 24 Go |
| Very High | Kimi-Linear-48B Q4_K_M | 32 Go |

**Auto** choisit le meilleur profil compatible avec la VRAM déclarée. Forcer un profil trop grand peut provoquer une erreur de mémoire GPU.

### Plusieurs GPU

- Un seul processus Keryx Miner pilote les GPU cochés.
- Les cartes décochées sont réellement masquées au mineur avec `CUDA_VISIBLE_DEVICES`.
- Les UUID NVIDIA évitent qu’une différence d’ordre entre `nvidia-smi` et CUDA sélectionne la mauvaise carte.
- `--force-model` est reconstruit dans l’ordre logique des seules cartes visibles.
- Les statistiques renvoyées par le mineur sont remappées vers les indices GPU affichés par Keryx Control.

### Statistiques et journal

- L’API locale du mineur est interrogée sur `127.0.0.1:3338/stats` par défaut.
- Si le port est occupé, un port libre proche est choisi automatiquement.
- La température de jonction mémoire est affichée uniquement lorsque NVML et le GPU la fournissent.
- Pendant une inférence OPoI, l’interface indique que le minage est temporairement en pause.
- En pool, le total accepté/rejeté représente les shares. En solo, il représente les blocs signalés par l’API du mineur.
- Les avertissements restent orange. Les blocages solo liés au certificat ou à IPFS apparaissent en rouge et mettent le journal en pause.
- Faire défiler vers le haut suspend le suivi automatique sans arrêter la collecte.
- Le journal conserve 500 lignes et filtre les messages périodiques les plus bruyants.

### Contrôle de puissance

La limite de puissance est réglée séparément pour chaque GPU avec `nvidia-smi`. Les droits administrateur peuvent être nécessaires. Keryx Control mémorise la valeur initiale et tente de la restaurer à l’arrêt, à la désélection d’une carte et à la fermeture.

Keryx Control ne modifie pas les fréquences, la tension, les ventilateurs, le FP32, les cœurs CUDA ni le noyau du mineur.

### Dépannage rapide

- **Code 2 / argument inconnu** : vérifiez que le binaire est bien Keryx Miner v0.5.4-PoM.
- **Pool invalide** : utilisez exactement `stratum+tcp://hôte:port` et un pool v3.
- **Certificat incorrect** : vérifiez l’adresse de paiement et restaurez la paire originale `escrow.key` / `escrow.cert`.
- **IPFS 5001 occupé** : arrêtez l’autre démon IPFS ou choisissez consciemment lequel doit être utilisé.
- **Modèle téléchargé à nouveau** : vérifiez le profil sélectionné et le nom exact du dossier sous `miner/models`.
- **Power limit refusé** : relancez Keryx Control en tant qu’administrateur.
- **Crash de l’interface** : consultez `%LOCALAPPDATA%\KeryxControl\crash.log`.

### Compilation

SDK .NET 8 requis :

```powershell
dotnet restore
dotnet build -c Release -r win-x64
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## English

### What is new in v0.7.2

- Compatibility with **Keryx Miner v0.5.4-PoM**.
- Separate **Solo / keryxd node** and **Pool / Stratum v3** modes.
- Full `stratum+tcp://host:port` endpoint forwarding in pool mode.
- No local keryxd node or escrow certificate is requested at pool startup, and the official miner does not automatically start Kubo in pool mode.
- Reliable one-or-many GPU filtering through `CUDA_VISIBLE_DEVICES`, without the unsupported `--cuda-device` argument.
- Stable per-GPU tier ordering through `CUDA_DEVICE_ORDER=PCI_BUS_ID` and NVIDIA UUIDs.
- Portable Windows IPFS repository fixed to `miner/.ipfs` through `IPFS_PATH`, matching the port checks performed by the UI.
- New v0.5.4 telemetry fields: memory junction temperature when available, OPoI activity and service status.
- Correct pool counters from Stratum accepted, stale, low-difficulty and duplicate share messages.
- 64-bit counters and nullable telemetry fields so one unavailable NVML sensor cannot discard the complete statistics response.

### Compatibility

| Component | Support |
|---|---|
| Windows | Windows 10/11 x64 |
| Runtime | Self-contained build; .NET 8 is only required to compile |
| GPU | NVIDIA RTX 4000 / RTX 5000 and other GPUs accepted by the official miner |
| Miner | Keryx Miner v0.5.4-PoM |
| Solo | Local or reachable keryxd node |
| Pool | **keryx-stratum-v3** capable pool |
| Languages | French and English |

Actual GPU support also depends on the NVIDIA driver and the CUDA build shipped by Keryx Labs.

### Installing or upgrading

Official Keryx binaries, CUDA libraries, Kubo/IPFS and model weights are intentionally not bundled.

1. Download the official Keryx Miner v0.5.4-PoM Windows archive.
2. Put `keryx-miner.exe`, `ipfs.exe` and every official library/file in `miner`.
3. For solo mode controlled by the UI, put `keryxd.exe` in `keryxd`.
4. Run `KeryxControl.exe`.

Always preserve these files and folders while upgrading:

```text
miner/models/
miner/.ipfs/
miner/escrow.key
miner/escrow.cert
miner/escrow_state.json
KeryxData/
```

`escrow.key` is a private key connected to existing solo rewards. Never publish or replace it without a backup.

### Solo mining

1. Select **Solo — keryxd node**.
2. Enter the payout address beginning with `keryx:`.
3. Keep `127.0.0.1:22110` for the local node, or enter a reachable node.
4. Start the node and wait for `Synchronization: complete` when Keryx Control owns it.
5. Start the miner.

Solo mode uses `miner/.ipfs`. Keryx Control checks API port 5001 and automatically moves the 8080 gateway to an available port from 8081 through 8099 when required.

### Solo escrow authorization

On the first solo start, the miner creates `miner/escrow.key` and prints a 64-character hexadecimal public key.

1. Open **AUTHORIZATION**.
2. Copy the detected public key.
3. Open **Authorise a miner** in the Keryx wallet and paste the key.
4. Copy the returned `--escrow-cert` line.
5. Paste it into Keryx Control and save `escrow.cert`.
6. Restart with the same payout address.

The certificate must contain exactly 128 hexadecimal characters and match both the payout address and `escrow.key`. Keryx Control never reads the private key contents.

### Stratum v3 pool mining

1. Select **Pool — Stratum v3**.
2. Enter the Keryx payout address.
3. Enter the complete endpoint supplied by the pool:

   ```text
   stratum+tcp://pool.example.org:PORT
   ```

4. Start the miner.

The pool must serve **keryx-stratum-v3** jobs with DAA scores and keepalive support. A legacy Stratum server may accept the TCP connection but reject PoM jobs or shares.

In pool v3 mode:

- no local keryxd node is required;
- `escrow.key` and `escrow.cert` are not required;
- the official miner does not automatically start a local IPFS daemon;
- accepted/rejected share totals are parsed from the miner's Stratum messages;
- the connection, PoM jobs and keepalives use the Stratum v3 protocol.

The v0.5.4 code still contains an upload path to the configured IPFS API for some OPoI tasks dispatched by a bridge. If your pool enables those tasks, follow its IPFS API instructions; Keryx Control does not start Kubo on its own in pool mode.

If the initial network probe fails, Keryx Control warns but still starts the miner so its reconnect logic remains available.

### v0.5.4-PoM model tiers

| Tier | Model | Announced minimum VRAM |
|---|---|---:|
| Very Light | Qwen3.5-9B-abliterated Q5_K_M | 8 GB |
| Light | GLM-4-9B-0414 Q6_K | 12 GB |
| Standard | Gemma-4-12B-abliterated Q6_K | 16 GB |
| High | Qwen3.6-27B Q4_K_M | 24 GB |
| Very High | Kimi-Linear-48B Q4_K_M | 32 GB |

**Auto** chooses the highest tier fitting the reported VRAM. Forcing an oversized tier can cause a GPU out-of-memory failure.

### Multiple GPUs

- One miner process controls every checked GPU.
- Unchecked cards are hidden with `CUDA_VISIBLE_DEVICES`.
- NVIDIA UUIDs prevent an ordering difference between `nvidia-smi` and CUDA from selecting the wrong card.
- `--force-model` is rebuilt in the logical order of the visible cards only.
- Miner telemetry is mapped back to the physical GPU indexes shown by Keryx Control.

### Statistics and log

- The miner API is read from `127.0.0.1:3338/stats` by default.
- A nearby available port is selected when 3338 is busy.
- Memory junction temperature appears only when NVML and the GPU expose it.
- The interface reports active OPoI inference, during which mining is temporarily paused.
- Pool accepted/rejected totals represent shares; solo totals represent blocks reported by the miner API.
- Warnings remain orange. Solo certificate and IPFS blockers appear in red and pause the log.
- Scrolling up pauses automatic following without stopping collection.
- The UI retains 500 lines and filters the noisiest periodic messages.

### Power control

Power limits are adjusted per GPU through `nvidia-smi`; administrator rights may be required. Keryx Control remembers the original value and attempts to restore it when mining stops, a GPU is deselected or the application exits.

Keryx Control does not modify clocks, voltage, fans, FP32, CUDA cores or miner kernel code.

### Building from source

The .NET 8 SDK is required:

```powershell
dotnet restore
dotnet build -c Release -r win-x64
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

Keryx Control v0.7.2 — Community frontend — Windows x64
