# Sub-Surface 0

**Jeu d'horreur coopératif sous-marin construit avec Unity et FishNet**

## Concept

Deux joueurs sont piégés dans un complexe sous-marin abandonné. Pas d'objectifs affichés, pas de marqueurs : le level design guide les joueurs à travers des couloirs séparés, des portes verrouillées et des puzzles coopératifs. Une créature (le Drifter) rôde dans les niveaux avancés.

**Setting** : Fond de l'océan, station de recherche abandonnée, horreur lovecraftienne.

> Anciennement *VoidWarranty* (jeu de réparation coop années 90). Le projet a pivoté vers l'horreur sous-marine.

---

## Tech Stack

| Composant | Version |
|-----------|---------|
| Unity | 6000.3.6f1 |
| URP | 17.3.0 |
| FishNet | v4 (source dans `Assets/FishNet/`) |
| Input System | 1.18.0 |
| ProBuilder | 6.0.9 |
| AI Navigation | 2.0.9 |

---

## Scripts

### Core

| Script | Description |
|--------|-------------|
| `IInteractable.cs` | Interface press E |
| `IHoldInteractable.cs` | Interface hold E (OnHoldStart/Release, GetHoldDuration) |
| `ItemData.cs` | ScriptableObject items |
| `GameManager.cs` | Registre de session |
| `MissionManager.cs` | Boucle mission (legacy, conservé) |
| `MissionData.cs` | ScriptableObject missions (legacy) |
| `LocalizationManager.cs` | Singleton localisation CSV (`LocalizationManager.Get("KEY")`) |
| `LocalizationTable.cs` | Table de traduction FR/EN |
| `RealisticHeadBob.cs` | Head bob caméra (delta only) |

### Player

| Script | Description |
|--------|-------------|
| `PlayerMovement.cs` | Déplacement, sprint, crouch, gravité, Freeze, ValveLook, NoiseOverride APIs |
| `PlayerInteraction.cs` | Raycast interaction E + support IHoldInteractable (hold E) |
| `PlayerInputReader.cs` | Input polling, IsInteractHeld, ValveScroll, singleton `LocalInstance` |
| `PlayerInventory.cs` | Inventaire SyncList\<string\>, TAB toggle, HasItem/CmdAddItem/CmdRemoveItem |
| `PlayerCameraSetup.cs` | Attachement caméra joueur local |
| `PlayerGrab.cs` | Grab/Drop physique réseau (legacy) |
| `PlayerFootsteps.cs` | Audio 3D footsteps |

### Interaction

| Script | Description |
|--------|-------------|
| `LevelDoor.cs` | Porte générique Locked/Closed/Open, lerp animation, SyncVar état |
| `DoorButton.cs` | Bouton porte : Open/Close/Toggle, mode press ou hold E |
| `DoorLockTrigger.cs` | Trigger server : ferme + verrouille une porte (usage unique) |
| `ItemPickup.cs` | Ramassage générique E, ajoute item à l'inventaire, SyncVar \_pickedUp |
| `BadgeReader.cs` | IHoldInteractable, vérifie HasItem, notifie SimultaneousBadge |
| `SimultaneousBadge.cs` | 2 lecteurs simultanés requis pour ouvrir une porte |
| `PupitreInteraction.cs` | Lean-in caméra pour moniteur de surveillance |
| `GrabbableObject.cs` | Objets ramassables physiques (legacy) |
| `PatientObject.cs` | Machine infectée à réparer (legacy) |
| `RepairSocket.cs` | Socket installation pièces (legacy) |
| `Scanner.cs` | Radar + ciblage (legacy) |
| `SupplyCrate.cs` | Caisse de fournitures (legacy) |
| `TruckZone.cs` | Zone d'extraction camion (legacy) |
| `TruckValidationButton.cs` | Bouton extraction (legacy) |

### Puzzle

| Script | Description |
|--------|-------------|
| `PuzzleManager.cs` | Puzzle ballast : 3 chambres (R/V/B, total=8), BFS distance 3-4, cycle fixe R>V>B |
| `ValveInteractable.cs` | IInteractable, E = 1 transfert atomique, cooldown 0.5s, rotation + audio |

### Level

| Script | Description |
|--------|-------------|
| `AnnexInteractable.cs` | Activation O2 ou Electricité en fin de niveau |
| `AnnexActivation.cs` | SyncVar O2 + Elec, quand les 2 actifs : déverrouille l'ascenseur |
| `ElevatorZone.cs` | Trigger : 2 joueurs + unlocked = OnLevelComplete |
| `SurveillanceMonitor.cs` | Active Camera RenderTexture en Awake (visible par tous) |

### AI

| Script | Description |
|--------|-------------|
| `DrifterAI.cs` | IA créature : Patrol/Investigate/Chase/Search, threat scores coop par joueur, jauge suspicion auditive, switch de cible avec marge, InvestigatePosition API |

### Gameplay

| Script | Description |
|--------|-------------|
| `HidingSpot.cs` | Cachettes Alien Isolation style (layer Hidden) |

### Environment

| Script | Description |
|--------|-------------|
| `DepthZoneManager.cs` | Gestion post-processing par zone de profondeur |
| `DepthZoneTrigger.cs` | Trigger transitions visuelles |

### UI

| Script | Description |
|--------|-------------|
| `InteractionHUD.cs` | Prompt texte + crosshair scale |
| `InventoryUI.cs` | TAB toggle, blur Volume DoF, freeze mouvement, cursor unlock |
| `PuzzleConsoleUI.cs` | Canvas World Space, 3 jauges fill + marqueurs cible |
| `MissionHUD.cs` | Panel objectifs (legacy, retiré du HUD prefab) |
| `NotificationHUD.cs` | Notifications résultats |

---

## Design Philosophy

- **Pas de système de rôles** : le level design contraint les joueurs (portes fermées, couloirs séparés)
- **Pas d'objectifs affichés** : le joueur comprend par l'exploration
- **Scripts génériques** réutilisables entre niveaux
- **Inventaire simple** : SyncList\<string\> d'IDs
- **Localisation** : toutes les strings user-facing via clés CSV (`Data/Languages/`)

---

## Conventions

- **Namespaces** : `VoidWarranty.Core`, `.Player`, `.Interaction`, `.UI` + `SubSurface.Environment`, `.Gameplay`, `.AI`, `.Puzzle`, `.Level`, `.UI`
- **Champs** : privés `_camelCase`, publics `PascalCase`
- **Inspector** : `[Header()]` pour organiser
- **SRP strict** : 1 script = 1 responsabilité
- **Layers** : 6 = Interactable, 7 = Player, 2 = Ignore Raycast, 8 = Hidden
- **GO structure** : parent vide (scripts + NetworkObject, Layer 6) > enfants (meshes + colliders, Layer 6)

---

## FishNet Patterns

- Joueur local : `base.IsOwner`
- SyncVar : `SyncVar<T>` struct + `.Value` (FishNet v4)
- RPCs : `[ServerRpc]`, `[ObserversRpc]`, `[TargetRpc]`
- Late-joiners : `[ObserversRpc(BufferLast = true)]`
- Input global : `PlayerInputReader.LocalInstance`

---

## Level 1 Flow

1. Porte commence `Closed` > joueur maintient DoorButton (hold E) > porte `Open`
2. Joueur passe > DoorLockTrigger fire > porte `Close` puis `Lock`
3. Joueurs séparés > puzzle ballast (3 vannes, cycle R>V>B)
4. Badge readers simultanés > porte fin de zone
5. Annexes O2 + Electricité > ascenseur déverrouillé > 2 joueurs dedans = level complete

---

## Setup

1. Clone the repository
2. Open with Unity 6000.3.6f1+
3. FishNet auto-imports dependencies
4. Open `_Game/Scenes/` and run

---

**Last Updated**: 2026-02-23
