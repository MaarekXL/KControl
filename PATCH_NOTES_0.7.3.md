# Keryx Control Manager v0.7.3 — Patch Notes

Release type: stability and compatibility update  
Target: Keryx Miner v0.5.4-PoM / keryxd v1.5.8-PoM / Windows x64

## English

### PoM functionality included since v0.7.2

- Solo keryxd and Keryx Stratum v3 pool modes.
- Multi-GPU selection with stable NVIDIA UUID ordering and one model assignment per selected GPU.
- Updated v0.5.4-PoM statistics: per-GPU hashrate, accepted/rejected blocks, uptime, OPoI and service state, plus optional memory-junction temperature.
- Integrated miner authorization workflow for the 64-hex public escrow key and 128-hex wallet certificate.
- Per-GPU power-limit management and automatic restoration where safe.
- Bounded, colored and copyable live log with French/English UI.

### v0.7.3 fixes

- **Synchronization:** intermediate IBD phases reaching 100% are held at 99%. The UI declares completion only after keryxd completes the last phase, receives live blocks and remains outside IBD for 30 seconds.
- **Counters:** solo candidates and network relay blocks are no longer counted. Solo increments only on a confirmed miner submission (or authoritative miner API data); pool counters use explicit Stratum results.
- **IPFS recovery:** safely removes only `miner/.ipfs/blocks/.temp` when Kubo is stopped. The repair runs before solo startup and after the matching startup failure.
- **IPFS ownership:** detects an already-running Kubo daemon and leaves it running on miner/application shutdown.
- **Clean shutdown:** sends Ctrl+C to the managed miner/node before using the force-stop fallback, reducing interrupted writes.
- **Process stability:** prevents stale output/exit events and handles binaries that exit immediately after launch.
- **Log navigation:** scrolling upward freezes the displayed position while new entries remain in a bounded queue; **Resume live** drains the queue.
- **Log severity:** certificate and IPFS blockers are red and pause the view. Other warnings remain orange, and repeated relay warnings are summarized.
- **Power safety:** restores each modified GPU to its recorded initial limit after stop, close or unexpected service exit when no external tool has changed it since.
- **Settings safety:** unreadable settings are backed up and replaced with defaults instead of crashing startup.

## Français

### Fonctions PoM incluses depuis la v0.7.2

- Modes solo avec keryxd et pool Keryx Stratum v3.
- Sélection multi-GPU avec ordre stable par UUID NVIDIA et attribution d’un modèle par GPU sélectionné.
- Statistiques v0.5.4-PoM : hashrate par GPU, blocs acceptés/rejetés, durée, état OPoI/service et température de jonction mémoire optionnelle.
- Autorisation intégrée : clé escrow publique de 64 caractères et certificat wallet de 128 caractères.
- Réglage de puissance par GPU avec restauration automatique lorsque cela est sûr.
- Journal borné, coloré, copiable et interface français/anglais.

### Correctifs v0.7.3

- **Synchronisation :** un 100 % intermédiaire d’IBD reste affiché à 99 %. La fin est validée après la dernière phase, la réception de blocs en direct et 30 secondes sans nouvelle phase IBD.
- **Compteurs :** les candidats `Found a block` et les blocs relayés par le réseau ne sont plus comptés. En solo, seul un envoi confirmé par le mineur — ou son API — compte ; en pool, seules les réponses Stratum explicites comptent.
- **Récupération IPFS :** suppression limitée au chemin exact `miner/.ipfs/blocks/.temp`, uniquement quand Kubo est arrêté, avant le lancement solo ou après l’erreur correspondante.
- **Propriété IPFS :** un Kubo déjà lancé avant Keryx Control est détecté et n’est pas arrêté par l’application.
- **Arrêt propre :** envoi de Ctrl+C au mineur et au nœud avant l’arrêt forcé de secours.
- **Stabilité des processus :** isolation des anciens événements et gestion des exécutables qui s’arrêtent immédiatement.
- **Navigation du journal :** la molette vers le haut fige l’affichage mais conserve les nouveaux messages ; **Reprendre en direct** vide la file d’attente.
- **Couleurs du journal :** seuls les blocages certificat/IPFS passent en rouge et mettent l’affichage en pause. Les autres avertissements restent orange et les avertissements réseau répétitifs sont regroupés.
- **Sécurité puissance :** restauration de la limite initiale après arrêt, fermeture ou panne, sauf si un autre logiciel a entre-temps modifié la valeur.
- **Paramètres :** sauvegarde du fichier illisible puis retour aux valeurs par défaut au lieu d’un crash.

### Note administrateur

Le réglage du power limit utilise `nvidia-smi`. Selon Windows et le pilote NVIDIA, Keryx Control doit être lancé avec **Exécuter en tant qu’administrateur**. Le minage et le monitoring n’exigent pas cette élévation si le pilote les autorise normalement.

