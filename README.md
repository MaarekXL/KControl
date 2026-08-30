# Keryx Control v0.6.1

Interface graphique WPF basée sur **.NET 8 / Windows x64** permettant de piloter un mineur Keryx externe, de gérer un nœud local et de surveiller un ou plusieurs GPU NVIDIA.

> **Keryx Control n'intègre aucun mineur et ne télécharge aucun binaire automatiquement.**
> Les exécutables Keryx doivent être fournis séparément par l'utilisateur.

---

# Français

## Fonctionnalités

Keryx Control permet notamment de :

* démarrer et arrêter un mineur Keryx externe ;
* démarrer et surveiller un nœud Keryx local ;
* sélectionner le GPU utilisé pour le minage ;
* surveiller plusieurs GPU NVIDIA ;
* afficher le hashrate et les shares détectés dans les logs ;
* consulter simultanément les sorties du mineur et du nœud ;
* gérer l'autorisation du mineur et le fichier `escrow.cert` ;
* modifier le power limit lorsque le pilote et les permissions système le permettent ;
* utiliser l'interface en français ou en anglais.

---

## Installation et utilisation

### 1. Installer le mineur

Copiez le binaire officiel ou tiers :

```text
keryx-miner.exe
```

dans le dossier :

```text
miner/
```

### 2. Installer les outils du nœud

Copiez :

```text
keryxd.exe
keryx-cli.exe
```

dans :

```text
keryxd/
```

Keryx Control peut alors démarrer automatiquement un nœud local sur :

```text
127.0.0.1:22110
```

Les données du nœud sont conservées dans le dossier voisin :

```text
KeryxData/
```

Ce dossier est volontairement placé **en dehors du dossier de version de Keryx Control**, afin de conserver les données lors d'une mise à jour de l'application.

### 3. Démarrer Keryx Control

Lancez :

```text
KeryxControl.exe
```

Puis :

1. sélectionnez le GPU ;
2. indiquez l'adresse du wallet ;
3. configurez le nœud souhaité ;
4. démarrez le mineur.

---

## Autorisation du mineur

Lors de sa première utilisation, le mineur Keryx peut nécessiter une autorisation.

### Étape 1 — Générer la paire locale

Démarrez une première fois le mineur.

S'il n'est pas encore autorisé, il génère localement :

```text
escrow.key
```

et affiche dans le journal une **clé publique de 64 caractères**.

### Étape 2 — Autoriser le mineur dans le wallet

Dans Keryx Control, ouvrez :

**Autorisation**

Copiez ensuite la clé publique affichée par le mineur dans la carte :

**Authorise a miner**

du wallet Keryx.

### Étape 3 — Enregistrer le certificat

Le wallet renvoie une ligne de la forme :

```text
--escrow-cert ...
```

Copiez cette ligne complète dans Keryx Control puis sélectionnez :

**Enregistrer escrow.cert**

Keryx Control extrait automatiquement les **128 caractères hexadécimaux** du certificat et crée :

```text
miner/escrow.cert
```

### Étape 4 — Redémarrer le mineur

Redémarrez ensuite le mineur afin qu'il utilise le nouveau certificat.

---

## Sécurité de la clé escrow

Le fichier privé :

```text
miner/escrow.key
```

n'est **jamais lu, importé ou affiché par Keryx Control**.

Il reste entièrement sous le contrôle du mineur et de l'utilisateur.

Conservez ensemble :

* l'adresse de paiement ;
* `escrow.key` ;
* le certificat `escrow.cert` correspondant.

Un certificat généré pour une autre paire peut provoquer une erreur similaire à :

```text
Cert does not match this payout address and escrow key
```

---

## Journal

Le journal regroupe les sorties du :

* nœud Keryx ;
* mineur Keryx.

Les deux sources sont affichées dans la même vue afin de faciliter le diagnostic.

Cependant, **seules les lignes provenant du mineur sont utilisées pour calculer les statistiques de hashrate et de shares**.

Le contenu du journal est sélectionnable.

Vous pouvez utiliser :

* clic droit → **Copier** ;
* `Ctrl+C`.

Pour éviter une consommation excessive de mémoire, Keryx Control :

* conserve les **500 lignes les plus récentes** ;
* limite la taille de la file d'attente du journal ;
* traite certaines sorties rapides par lots ;
* réduit les répétitions identiques, notamment pendant la synchronisation du nœud.

---

## Français / Anglais

Le sélecteur **FR / EN** traduit :

* l'interface ;
* les principaux messages de Keryx Control ;
* les messages d'état générés par l'application.

Les sorties brutes provenant de :

```text
keryx-miner.exe
keryxd.exe
keryx-cli.exe
```

restent volontairement dans leur langue et leur format d'origine.

Cela permet notamment de conserver des logs identiques à ceux utilisés par les développeurs Keryx pour le diagnostic.

---

## Power Limit

Keryx Control peut tenter de modifier la limite de puissance d'un GPU NVIDIA.

Cette fonctionnalité dépend :

* du modèle du GPU ;
* du pilote NVIDIA installé ;
* du firmware de la carte ;
* des permissions Windows.

Certaines modifications peuvent nécessiter l'exécution de Keryx Control avec des **droits administrateur**.

La présence du réglage dans l'interface ne garantit donc pas que le GPU autorise sa modification.

---

## Nouveautés de la version 0.6.1

Keryx Control **v0.6.1** apporte notamment :

* correction d'un crash lors de l'ouverture de la fenêtre **Autorisation** ;
* protection du parseur contre les nombres dépassant les limites attendues ;
* traitement par lots des sorties très rapides du nœud ;
* limitation du journal aux 500 lignes les plus récentes ;
* limitation de la file d'attente du journal ;
* réduction des répétitions de messages identiques pendant la synchronisation ;
* amélioration générale de la stabilité du traitement des sorties du mineur et du nœud.

---

## Compilation

Prérequis :

* .NET 8 SDK ;
* Windows x64.

Commande de publication :

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

La version produite nécessite donc la présence du runtime **.NET 8 Desktop Runtime** compatible sur la machine cible.

---

## Limites connues

* Le format définitif des logs Keryx doit encore être confirmé sur davantage d'échantillons de console réels. Le parseur accepte actuellement plusieurs variantes génériques.

* La CLI Keryx peut évoluer. Le paramètre `argumentsTemplate` reste volontairement configurable afin de pouvoir adapter les arguments sans modifier le cœur de l'application.

* L'arrêt gracieux dépend du comportement du mineur. Si le processus ne s'arrête pas dans le délai prévu, Keryx Control termine son arbre de processus.

* Le profil automatique GPU repose sur la quantité de VRAM disponible rapportée par NVIDIA. Il ne constitue pas une garantie de compatibilité CUDA avec une version donnée du mineur.

* Les possibilités de modification du power limit dépendent entièrement du matériel, du pilote et du firmware NVIDIA.

---

## Avertissement

Keryx Control est uniquement une **interface de contrôle et de supervision**.

L'application :

* ne contient aucun mineur ;
* ne distribue aucun mineur ;
* ne télécharge aucun exécutable Keryx ;
* ne lit jamais la clé privée `escrow.key`.

Les utilisateurs restent responsables des exécutables Keryx qu'ils choisissent d'installer et d'utiliser.

---

# English

## Overview

Keryx Control is a **WPF frontend built with .NET 8 for Windows x64** designed to control an external Keryx miner, manage a local Keryx node, and monitor one or more NVIDIA GPUs.

> **Keryx Control does not include a miner and does not automatically download any binaries.**
> Keryx executables must be provided separately by the user.

---

## Features

Keryx Control can:

* start and stop an external Keryx miner;
* start and monitor a local Keryx node;
* select the GPU used for mining;
* monitor multiple NVIDIA GPUs;
* display detected hashrate and share statistics;
* display miner and node output in the same log;
* manage miner authorization and `escrow.cert`;
* adjust the GPU power limit when supported by the driver and hardware;
* switch the application interface between French and English.

---

## Installation and Usage

### 1. Install the miner

Copy the official or third-party miner binary:

```text
keryx-miner.exe
```

into:

```text
miner/
```

### 2. Install the node tools

Copy:

```text
keryxd.exe
keryx-cli.exe
```

into:

```text
keryxd/
```

Keryx Control can then start a local node on:

```text
127.0.0.1:22110
```

Node data is stored in the adjacent:

```text
KeryxData/
```

directory.

This directory is intentionally kept **outside the version-specific Keryx Control folder**, allowing node data to remain available when the application is upgraded.

### 3. Start Keryx Control

Run:

```text
KeryxControl.exe
```

Then:

1. select the GPU;
2. enter the wallet/payout address;
3. configure the node;
4. start the miner.

---

## Miner Authorization

The miner may require authorization before it can begin mining.

### Step 1 — Generate the local escrow pair

Start the miner once.

If the miner has not yet been authorized, it creates the local private file:

```text
escrow.key
```

and prints a **64-character public key** in the log.

### Step 2 — Authorize the miner

Open:

**Authorization**

in Keryx Control.

Copy the public key displayed by the miner into the wallet's:

**Authorise a miner**

card.

### Step 3 — Save the certificate

The wallet returns a command similar to:

```text
--escrow-cert ...
```

Copy the complete line into Keryx Control and select:

**Save escrow.cert**

Keryx Control automatically extracts the **128 hexadecimal characters** from the certificate and writes:

```text
miner/escrow.cert
```

### Step 4 — Restart the miner

Restart the miner so it can use the new certificate.

---

## Escrow Key Security

The private file:

```text
miner/escrow.key
```

is **never read, imported, or displayed by Keryx Control**.

It remains entirely under the control of the miner and the user.

Keep the following items together:

* the payout address;
* `escrow.key`;
* the matching `escrow.cert`.

Using a certificate generated for another escrow pair may result in an error such as:

```text
Cert does not match this payout address and escrow key
```

---

## Log Viewer

The log viewer combines output from both:

* the Keryx node;
* the Keryx miner.

Both streams are displayed in the same view to make troubleshooting easier.

However, **only miner output is used to update hashrate and share counters**.

Log text can be selected and copied using:

* right-click → **Copy**;
* `Ctrl+C`.

To prevent excessive memory usage, Keryx Control:

* keeps the **500 most recent log lines**;
* limits the internal log queue;
* processes very fast node output in batches;
* reduces repeated identical messages, particularly while the node is synchronizing.

---

## French / English Interface

The **FR / EN** selector translates:

* the user interface;
* the main Keryx Control messages;
* application-generated status messages.

Raw output produced by:

```text
keryx-miner.exe
keryxd.exe
keryx-cli.exe
```

is intentionally left unchanged.

This preserves the original log format and makes troubleshooting with Keryx developers easier.

---

## Power Limit

Keryx Control can attempt to change the NVIDIA GPU power limit.

Support depends on:

* the GPU model;
* the installed NVIDIA driver;
* the GPU firmware;
* Windows permissions.

Some changes may require Keryx Control to be started with **administrator privileges**.

The presence of the option in the interface therefore does not guarantee that a specific GPU allows its power limit to be modified.

---

## What's New in v0.6.1

Keryx Control **v0.6.1** includes:

* a fix for a crash when opening the **Authorization** window;
* parser protection against out-of-range numeric values;
* batched processing of very fast node output;
* a 500-line limit for the log viewer;
* a bounded internal log queue;
* reduced duplication of identical messages during node synchronization;
* general improvements to miner and node output processing stability.

---

## Build

Requirements:

* .NET 8 SDK;
* Windows x64.

Publish command:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

The resulting application therefore requires a compatible **.NET 8 Desktop Runtime** to be installed on the target system.

---

## Known Limitations

* The final Keryx log format still needs to be validated against additional real-world console samples. The current parser accepts several generic variants.

* The Keryx CLI may evolve over time. `argumentsTemplate` is intentionally configurable so command-line arguments can be adapted without modifying the application's core logic.

* Graceful shutdown depends on the miner's behavior. If the process does not stop within the expected timeout, Keryx Control terminates the process tree.

* Automatic GPU profile selection is based on the amount of available VRAM reported by NVIDIA. It does not guarantee CUDA compatibility with a specific miner build.

* Power-limit control depends entirely on NVIDIA hardware, drivers, firmware, and system permissions.

---

## Disclaimer

Keryx Control is only a **control and monitoring frontend**.

The application:

* does not include a miner;
* does not distribute a miner;
* does not download Keryx executables;
* never reads the private `escrow.key` file.

Users remain responsible for the Keryx binaries they choose to install and execute.
