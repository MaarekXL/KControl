KERYX CONTROL v0.7.1 — WINDOWS x64
=================================

FRANÇAIS
--------

Présentation
Keryx Control est une interface communautaire Windows pour Keryx Miner 0.5.3 et les GPU NVIDIA RTX 4000 / RTX 5000. Elle pilote le mineur et, si vous le souhaitez, un nœud keryxd local. Ce logiciel n’est pas un produit officiel de Keryx Labs.

Prérequis
- Windows 10 ou 11 x64.
- Pilote NVIDIA récent et nvidia-smi fonctionnel.
- Keryx Miner 0.5.3 compatible avec votre GPU et ses DLL officielles.
- keryxd si vous souhaitez faire tourner le nœud local depuis l’interface.
- Une adresse de paiement Keryx complète commençant par keryx:.

Installation des composants officiels
Les binaires Keryx, les DLL CUDA, IPFS et les modèles ne sont volontairement pas inclus dans cette archive communautaire.

1. Placez dans le dossier "miner" :
   keryx-miner.exe, ses DLL officielles, ipfs.exe s’il est fourni, et les autres fichiers de la distribution du mineur.
2. Placez dans le dossier "keryxd" :
   keryxd.exe et, si vous l’utilisez séparément, keryx-cli.exe.
3. Ne supprimez pas les fichiers créés dans "miner", notamment escrow.key, escrow.cert, escrow_state.json, le dossier .ipfs et le dossier models.
4. Lancez KeryxControl.exe.

Premier démarrage
1. Choisissez FR ou EN.
2. Vérifiez les GPU détectés et laissez le profil sur Auto si vous ne savez pas lequel choisir.
3. Collez votre adresse Keryx.
4. Démarrez le nœud local ou renseignez un nœud déjà disponible.
5. Pour un nœud lancé par Keryx Control, attendez "Synchronisation : terminée" avant de miner.
6. Démarrez le mineur.

Autorisation du mineur (escrow)
Au premier démarrage, le mineur crée sa clé privée "miner/escrow.key" et affiche une clé publique de 64 caractères hexadécimaux.

1. Ouvrez AUTORISATION dans Keryx Control.
2. Copiez la clé publique détectée.
3. Dans le wallet Keryx, ouvrez "Authorise a miner" et collez cette clé.
4. Copiez la ligne --escrow-cert retournée par le wallet.
5. Collez la ligne complète dans Keryx Control et cliquez sur ENREGISTRER ESCROW.CERT.
6. Redémarrez le mineur avec exactement la même adresse Keryx.

Le certificat contient 128 caractères hexadécimaux et est lié à l’adresse et à escrow.key. Keryx Control ne lit jamais la clé privée escrow.key. Ne partagez ni votre phrase de récupération, ni vos clés privées, ni escrow.key.

Plusieurs GPU
- Un seul processus Keryx Miner gère toutes les cartes sélectionnées.
- Sans sélection particulière, tous les GPU NVIDIA sont utilisés.
- Auto choisit un modèle adapté à la VRAM de chaque carte.
- Vous pouvez sélectionner un modèle différent par GPU. Keryx Control conserve l’ordre CUDA attendu par --force-model, y compris lorsqu’une carte est désactivée.
- Les statistiques globales et par GPU proviennent en priorité de l’API locale du mineur (/stats).

Profils de Keryx Miner 0.5.3
- Very Light : Qwen3.5-9B-abliterated Q5_K_M, 8 Go+.
- Light : GLM-4-9B Q6_K, 12 Go+.
- Standard : Gemma-4-12B-abliterated Q6_K, 16 Go+.
- High : Qwen3.6-27B Q4_K_M, 24 Go+.
- Very High : Kimi-Linear-48B Q4_K_M, 32 Go+.

Choisir un profil trop grand peut provoquer une erreur de mémoire GPU. La compatibilité RTX 5000 dépend aussi du pilote NVIDIA et du build CUDA du mineur officiel.

Journal et erreurs
- Les informations normales apparaissent en vert clair.
- Les avertissements apparaissent en orange, sans interrompre l’affichage.
- Les deux blocages assistés — certificat/wallet et IPFS — apparaissent en rouge et mettent le journal en pause.
- Faire rouler la molette vers le haut met le suivi automatique en pause. Le mineur continue et les lignes restent collectées. Cliquez sur REPRENDRE EN DIRECT pour revenir à la fin.
- COPIER LE JOURNAL copie les lignes actuellement conservées. L’interface garde au maximum 500 lignes et masque les messages périodiques les plus bruyants du nœud.

IPFS et ports
- API IPFS : port 5001 par défaut. Si ce port est déjà occupé, le démarrage est bloqué pour éviter d’utiliser le mauvais service.
- Passerelle IPFS : port 8080 par défaut. S’il est occupé, Keryx Control choisit automatiquement un port libre entre 8081 et 8099 et modifie uniquement la configuration IPFS du dossier du mineur.
- Statistiques du mineur : port 3338 par défaut. S’il est occupé, l’application choisit automatiquement un port libre proche.
- RPC keryxd : port 22110 par défaut. Le P2P principal utilise généralement 22111.

Contrôle de puissance
La limite est réglée séparément pour chaque GPU avec nvidia-smi. Keryx Control mémorise la valeur trouvée avant sa propre modification et tente de la restaurer à l’arrêt du mineur, lors de la désélection du GPU et à la fermeture de l’application. Par sécurité, la restauration n’écrase pas une valeur qui aurait été changée entre-temps par un autre outil. Les droits administrateur peuvent être nécessaires.

Keryx Control ne modifie pas les fréquences, la tension, les ventilateurs, le FP32, les cœurs CUDA ni le code du noyau du mineur.

Données conservées
- Les préférences d’interface sont enregistrées dans %LOCALAPPDATA%\KeryxControl\settings.json.
- Les données de chaîne du nœud local sont placées par défaut dans un dossier KeryxData voisin de la version de l’application, afin de survivre aux mises à jour.
- Remplacer KeryxControl.exe ne doit pas effacer miner, models, .ipfs, escrow.key, escrow.cert ou KeryxData.

Arrêt propre
Utilisez ARRÊTER avant de remplacer des fichiers. Si le mineur ou le nœud lancé par l’application est encore actif à la fermeture, Keryx Control demande confirmation, arrête les processus qu’il possède, restaure les limites de puissance qu’il a modifiées et sauvegarde les préférences.

Compilation des sources
SDK .NET 8 requis :
  dotnet restore
  dotnet build -c Release
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true


ENGLISH
-------

Overview
Keryx Control is a Windows community frontend for Keryx Miner 0.5.3 and NVIDIA RTX 4000 / RTX 5000 GPUs. It controls the miner and can also launch a local keryxd node. This software is not an official Keryx Labs product.

Requirements
- Windows 10 or 11 x64.
- A recent NVIDIA driver with working nvidia-smi.
- An official Keryx Miner 0.5.3 build compatible with your GPU, including its DLL files.
- keryxd if you want the interface to run a local node.
- A complete Keryx payout address beginning with keryx:.

Installing the official components
The Keryx binaries, CUDA DLLs, IPFS and models are intentionally not included in this community archive.

1. Put keryx-miner.exe, its official DLLs, ipfs.exe when supplied, and the other miner distribution files in the "miner" folder.
2. Put keryxd.exe and, when needed separately, keryx-cli.exe in the "keryxd" folder.
3. Do not delete files created under "miner", especially escrow.key, escrow.cert, escrow_state.json, .ipfs and models.
4. Run KeryxControl.exe.

First run
1. Select FR or EN.
2. Check the detected GPUs and leave each profile on Auto unless you need a manual tier.
3. Paste your Keryx address.
4. Start the local node or enter an already available node.
5. For a node launched by Keryx Control, wait for "Synchronization: complete" before mining.
6. Start the miner.

Miner authorization (escrow)
On its first run, the miner creates the private "miner/escrow.key" file and prints a 64-character hexadecimal public key.

1. Open AUTHORIZATION in Keryx Control.
2. Copy the detected public key.
3. Open "Authorise a miner" in the Keryx wallet and paste that key.
4. Copy the --escrow-cert line returned by the wallet.
5. Paste the complete line into Keryx Control and click SAVE ESCROW.CERT.
6. Restart the miner with exactly the same Keryx address.

The certificate contains 128 hexadecimal characters and is bound to both the payout address and escrow.key. Keryx Control never reads the private escrow.key file. Never share your recovery phrase, private keys or escrow.key.

Multiple GPUs
- One Keryx Miner process manages all selected cards.
- All NVIDIA GPUs are used by default.
- Auto selects a model that fits each GPU’s VRAM.
- A different model can be selected for each GPU. Keryx Control preserves the CUDA order required by --force-model, including when one card is disabled.
- Aggregate and per-GPU mining statistics primarily come from the miner’s local /stats API.

Keryx Miner 0.5.3 tiers
- Very Light: Qwen3.5-9B-abliterated Q5_K_M, 8 GB+.
- Light: GLM-4-9B Q6_K, 12 GB+.
- Standard: Gemma-4-12B-abliterated Q6_K, 16 GB+.
- High: Qwen3.6-27B Q4_K_M, 24 GB+.
- Very High: Kimi-Linear-48B Q4_K_M, 32 GB+.

Forcing a tier that is too large can cause a GPU out-of-memory error. RTX 5000 compatibility also depends on the NVIDIA driver and the official miner’s CUDA build.

Log and errors
- Normal information is light green.
- Warnings are orange and do not pause the display.
- The two assisted blockers — wallet/certificate and IPFS — are red and pause the log.
- Scrolling up pauses automatic following. Mining continues and lines are still collected. Click RESUME LIVE to return to the end.
- COPY LOG copies all currently retained lines. The UI retains at most 500 lines and hides the noisiest periodic node messages.

IPFS and ports
- IPFS API: port 5001 by default. If it is occupied, startup is blocked to avoid using the wrong service.
- IPFS gateway: port 8080 by default. If it is occupied, Keryx Control automatically chooses a free port from 8081 through 8099 and changes only the miner folder’s IPFS configuration.
- Miner statistics: port 3338 by default. If it is occupied, the application automatically selects a nearby free port.
- keryxd RPC: port 22110 by default. Mainnet P2P generally uses port 22111.

Power control
The limit is adjusted separately for each GPU through nvidia-smi. Keryx Control remembers the value observed before its own change and attempts to restore it when the miner stops, when the GPU is deselected, and when the application closes. For safety, it does not overwrite a value that another tool changed in the meantime. Administrator rights may be required.

Keryx Control does not modify clock speeds, voltage, fans, FP32, CUDA cores or the miner’s kernel code.

Stored data
- UI preferences are stored in %LOCALAPPDATA%\KeryxControl\settings.json.
- Local node chain data is stored by default in a KeryxData folder next to the application version, allowing it to survive upgrades.
- Replacing KeryxControl.exe must not delete miner, models, .ipfs, escrow.key, escrow.cert or KeryxData.

Clean shutdown
Use STOP before replacing files. If the miner or an application-owned node is still active when the window closes, Keryx Control asks for confirmation, stops the processes it owns, restores power limits it changed and saves the preferences.

Building from source
.NET 8 SDK required:
  dotnet restore
  dotnet build -c Release
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

Version 0.7.1 — Community frontend — Windows x64
