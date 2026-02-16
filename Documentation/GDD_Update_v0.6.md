# VOID WARRANTY - GDD UPDATE v0.6

**Document de Suivi Technique** | Date: 2026-02-16

---

## Table des matieres

1. [Vue d'ensemble du projet](#1-vue-densemble-du-projet)
2. [Analyse par systeme](#2-analyse-par-systeme)
   - 2.1 Architecture Core
   - 2.2 Systeme de Joueur
   - 2.3 Systeme d'Interaction
   - 2.4 Systeme d'Interface Utilisateur
3. [Ecarts par rapport au GDD](#3-ecarts-par-rapport-au-gdd)
4. [Points techniques critiques](#4-points-techniques-critiques)
5. [Recommandations pour v0.7](#5-recommandations-pour-v07)

---

## 1. Vue d'ensemble du projet

### Setting

| | |
|---|---|
| **Epoque** | Annees 1990 |
| **Lieu** | Petite ville americaine (ambiance Stranger Things) |
| **Contexte** | Des machines et systemes de la ville sont infectes par une force surnaturelle. Les joueurs incarnent des techniciens envoyes pour reparer ces "patients" (machines corrompues) et accomplir des contrats. |
| **Atmosphere** | Retro, paranormal, VHS/cassette, neons, tension nocturne |

### Architecture Generale

| | |
|---|---|
| **Framework Reseau** | FishNet Networking |
| **Architecture** | Namespace VoidWarranty avec separation modulaire |
| **Total Scripts** | 25 fichiers C# |
| **Organisation** | Core (8), Player (5), Interaction (8), UI (3 + 1 menu) |

### Etat Global : PROTOTYPE FONCTIONNEL v0.6

- [x] Le jeu est jouable en reseau cooperatif
- [x] Les mecaniques principales sont implementees
- [x] Systeme de localisation integre (EN/FR)
- [x] Physique reseau synchronisee
- [x] Boucle de gameplay Tarkov-like implementee
- [x] UI esthetique cassette annees 90
- [x] GameManager et MissionManager operationnels

---

## 2. Analyse par systeme

### 2.1. Architecture Core (8 scripts)

#### IInteractable.cs - COMPLETE

> `Assets/_Game/Scripts/Core/IInteractable.cs`

- Interface pour tous les objets interactifs
- Methode `Interact(GameObject interactor)`
- Methode `GetInteractionPrompt()` pour feedback UI
- Utilise par: GrabbableObject, PatientObject, RepairSocket, TruckValidationButton

#### ItemData.cs - COMPLETE + EXTENSIONS

> `Assets/_Game/Scripts/Core/ItemData.cs`

- ScriptableObject pour definition des objets
- Enum ItemType : Generic, Motor, Fuse, Coolant, Toolbox, ToolboxAdvanced, Scanner
- Proprietes physiques: Mass, LinearDamping, AngularDamping
- Systeme de localisation (NameKey, DescriptionKey)
- Flag `IsDefective` pour pieces corrompues
- `ScrapValue` pour systeme economique
- **Extension** : `HeldPositionOffset` / `HeldRotationOffset` (Vector3) pour positionnement custom en main

#### GameManager.cs - COMPLETE (NOUVEAU v0.6)

> `Assets/_Game/Scripts/Core/GameManager.cs`

- Singleton pattern pour acces global
- Registre de session (joueurs connectes)
- Point central pour logique de jeu
- Coordination entre MissionManager et systemes

#### MissionManager.cs - COMPLETE (NOUVEAU v0.6)

> `Assets/_Game/Scripts/Core/MissionManager.cs`

- Gestion complete de la boucle mission Tarkov-like
- Extraction libre a tout moment
- Tracking objectifs: `PatientRepaired`, `DefectivePartReturned`, `ToolsReturnedCount`
- Calcul recompenses (`ScrapReward` + `DefectivePartBonus`)
- SyncVar pour synchronisation etat mission sur tous les clients
- Integration avec TruckZone pour validation extraction
- Notifications succes/echec

**Workflow** :
1. Mission demarre -> Objectifs affiches
2. Joueurs reparent le patient (objectif principal)
3. Joueurs ramenent pieces/outils au camion (optionnel)
4. Joueur appuie sur bouton extraction -> Calcul resultat
5. Debrief affiche (succes/echec + recompenses)

#### MissionData.cs - COMPLETE (NOUVEAU v0.6)

> `Assets/_Game/Scripts/Core/MissionData.cs`

- ScriptableObject pour definition des missions
- Nom/Description localises
- `ScrapReward` (recompense de base)
- `DefectivePartBonus` (bonus piece defectueuse)
- `TimeLimit` (0 = pas de limite)

#### LocalizationManager.cs - COMPLETE

> `Assets/_Game/Scripts/Core/LocalizationManager.cs`

- Singleton pattern pour acces global
- Chargement de LocalizationTable au demarrage
- Methode statique `Get(string key)` pour traductions
- Fallback robuste si cle manquante: `[KEY_NAME]`

#### LocalizationTable.cs - COMPLETE + OUTILS EDITEUR

> `Assets/_Game/Scripts/Core/LocalizationTable.cs`

- ScriptableObject pour tables de traduction
- Import CSV avec ContextMenu
- Structure `TranslationEntry` (Key/Value)
- Dictionary runtime pour performance
- Support des sauts de ligne (`<br>` et `\n`)

#### RealisticHeadBob.cs - COMPLETE (NOUVEAU v0.6)

> `Assets/_Game/Scripts/Core/RealisticHeadBob.cs`

- Effet camera head bob procedural
- Adapte a la vitesse de deplacement (marche/course)
- Integration avec PlayerMovement
- Desactivable/configurable

---

### 2.2. Systeme de Joueur (5 scripts)

#### PlayerCameraSetup.cs - COMPLETE

> `Assets/_Game/Scripts/Player/PlayerCameraSetup.cs`

- Attachement camera au joueur local uniquement (`IsOwner` check)
- Parenting de `Camera.main` au `_cameraRoot`
- NE GERE PAS la rotation (fait par PlayerMovement)

#### PlayerInputReader.cs - COMPLETE + FIX v0.6

> `Assets/_Game/Scripts/Player/PlayerInputReader.cs`

- Implementation de `GameControls.IPlayerActions`
- Events: `OnInteractEvent`, `OnGrabToggleEvent`, `OnJumpEvent`
- Properties: `MoveInput`, `LookInput`, `IsSprinting`, `IsCrouching`
- Actions reservees: `OnAttack()`, `OnPrevious()`, `OnNext()`
- **Fix v0.6** : Singleton n'est plus ecrase par les spawns non-owner (commit e18b176)

#### PlayerMovement.cs - COMPLETE + ADVANCED FEATURES

> `Assets/_Game/Scripts/Player/PlayerMovement.cs`

- Deplacement WASD avec CharacterController
- Sprint (Shift), Saut, Accroupissement avec ceiling check
- Penalite de vitesse selon poids porte

| Parametre | Valeur |
|---|---|
| Walk speed | 4 m/s |
| Run speed | 7 m/s |
| Crouch speed | 2 m/s |
| Stand height | 2m |
| Crouch height | 1m |
| Gravite | -9.81 |
| Penalite poids | 0.15/kg |

#### PlayerGrab.cs - COMPLETE + PHYSIQUE AVANCEE

> `Assets/_Game/Scripts/Player/PlayerGrab.cs`

- Grab/Drop avec clic gauche (OnGrabToggle)
- Physique velocity-driven (suit la main avec Rigidbody)
- Gestion ownership reseau (GiveOwnership)
- Drop intelligent (raycast pour eviter murs)
- Integration `ItemData.HeldPositionOffset/RotationOffset`
- `ForceDrop()` public pour autres scripts

| Parametre | Valeur |
|---|---|
| followSpeed | 20 |
| rotateSpeed | 20 |
| breakDistance | 1.5 |
| maxGrabVelocity | 15 |

#### PlayerInteraction.cs - COMPLETE

> `Assets/_Game/Scripts/Player/PlayerInteraction.cs`

- Raycast continu pour scan d'interactifs
- Update du HUD avec prompt en temps reel
- `HandleInteractInput()` sur touche E
- NE GERE PAS le grab (fait par PlayerGrab)

---

### 2.3. Systeme d'Interaction (8 scripts)

#### GrabbableObject.cs - COMPLETE + RESEAU

> `Assets/_Game/Scripts/Interaction/GrabbableObject.cs`

- Herite de `NetworkBehaviour`, implemente `IInteractable`
- `SyncVar<bool> IsHeld` avec callback `OnHeldChanged`
- `OnGrabbed/OnDropped` virtuels pour override
- Configuration physique depuis ItemData
- Layer management recursif

#### PatientObject.cs - COMPLETE + COOPERATIF

> `Assets/_Game/Scripts/Interaction/PatientObject.cs`

- State Machine : **Infected** -> **Dismantling** -> **Empty** -> **Repaired**
- Demontage cooperatif avec toolbox (vitesse cumulee multi-joueurs)
- Spawn piece corrompue, visuels dynamiques, audio
- Integration avec `GameManager.OnPatientRepaired`

**Workflow** :
1. Joueur avec Toolbox -> Interact -> Demontage
2. Autres joueurs rejoignent (x2, x3...)
3. Si tous partent -> Reset (punitif)
4. Fin demontage -> Piece corrompue ejectee -> Empty
5. Joueur avec piece neuve -> Installation (1.5s)
6. Repaired

#### RepairSocket.cs - COMPLETE

> `Assets/_Game/Scripts/Interaction/RepairSocket.cs`

- Socket install/remove pour machines
- Type checking avec ItemType
- Ejection avec force physique

#### Scanner.cs - COMPLETE + OPTIMISATIONS

> `Assets/_Game/Scripts/Interaction/Scanner.cs`

- Herite de GrabbableObject
- UI radar 2D avec blip, audio beep selon distance
- Ciblage prioritaire: 1) Pieces defectueuses 2) PatientObject
- Update optimise (UI: 0.05s, Scan: 0.5s)

> **Attention** : `FindObjectsByType` est lourd. Suggestion v0.7 : liste statique via GameManager.

#### SupplyCrate.cs - COMPLETE

> `Assets/_Game/Scripts/Interaction/SupplyCrate.cs`

- Herite de GrabbableObject (transportable)
- Ouverture avec E (quand au sol et fermee)
- Spawn piece de rechange

#### TruckZone.cs - COMPLETE + REFACTORED v0.6

> `Assets/_Game/Scripts/Interaction/TruckZone.cs`

- Zone trigger pour depot d'items dans le camion
- **Les items restent physiquement** dans la zone (plus de despawn auto)
- Validation uniquement au moment de l'extraction
- Integration avec MissionManager pour tracking objectifs

#### TruckValidationButton.cs - COMPLETE (NOUVEAU v0.6)

> `Assets/_Game/Scripts/Interaction/TruckValidationButton.cs`

- Implemente `IInteractable`
- Bouton physique dans la scene (layer Interactable)
- Declenche `TruckZone.ValidateMissionServerRpc()` sur interaction
- Prompt localise "Extraire (quitter) [E]"

#### ColorCube.cs - DEBUG

> `Assets/_Game/Scripts/Interaction/ColorCube.cs`

- [ ] A supprimer dans version finale

---

### 2.4. Systeme d'Interface Utilisateur (4 scripts)

#### InteractionHUD.cs - COMPLETE

> `Assets/_Game/Scripts/UI/InteractionHUD.cs`

- Affichage prompt interaction via TextMeshProUGUI
- Agrandissement crosshair sur interactable (scale x1.5)

#### MissionHUD.cs - COMPLETE (NOUVEAU v0.6)

> `Assets/_Game/Scripts/UI/MissionHUD.cs`

- Panel d'objectifs toggle avec Tab
- Objectif principal + objectifs optionnels
- Timer countdown, banners succes/echec
- **Fix v0.6** : Corrige pour multiplayer (commit e18b176)

```
MissionHUD (GameObject)
|- MissionPanel (CanvasGroup)
|  |- TitleText (TextMeshProUGUI)
|  |- CurrentStepText (TextMeshProUGUI)
|  |- TimerText (TextMeshProUGUI)
|- CompletedBanner (CanvasGroup)
|- FailedBanner (CanvasGroup)
```

#### NotificationHUD.cs - COMPLETE (NOUVEAU v0.6)

> `Assets/_Game/Scripts/UI/NotificationHUD.cs`

- Notifications temporaires pour resultats de mission
- Affichage recompenses, fade in/out anime

#### ButtonHoverEffect.cs - COMPLETE

> `Assets/_Game/Scripts/UI/Menu/ButtonHoverEffect.cs`

- Effet hover visuel pour boutons de menu
- Esthetique cassette annees 90

---

## 3. Ecarts par rapport au GDD

### Changement de Setting (v0.6)

| | Avant (v0.5) | Apres (v0.6) |
|---|---|---|
| **Lieu** | Vaisseau spatial | Petite ville americaine |
| **Epoque** | Futur / sci-fi | Annees 1990 |
| **Menace** | Infection spatiale | Force surnaturelle / paranormale |
| **Ambiance** | Sci-fi | Stranger Things (retro, VHS, neons) |
| **Extraction** | Sas de vaisseau | Van/camion de technicien |

### Ajouts v0.6

- **GameManager** : Registre de session centralise
- **MissionManager** : Boucle Tarkov-like complete (extraction libre, objectifs optionnels)
- **MissionData** : Definition missions en ScriptableObject
- **MissionHUD** : Panel objectifs avec Tab toggle
- **NotificationHUD** : Notifications resultats mission
- **TruckValidationButton** : Bouton extraction physique
- **RealisticHeadBob** : Effet camera procedural
- **ButtonHoverEffect** : UI menu cassette 90s
- **Fix** PlayerInputReader singleton (non-owner safe)
- **Fix** TruckZone despawn premature

### Ajouts non prevus (v0.5 -> v0.6, positifs)

- Systeme de localisation complet (CSV import)
- Systeme d'accroupissement avec ceiling check
- Scanner avec ciblage prioritaire pieces/patient
- Offset position/rotation custom pour objets tenus
- RepairSocket (systeme machines)
- Drop intelligent avec raycast
- Penalite vitesse selon poids

### Fonctionnalites GDD non implementees

- [ ] Systeme d'ennemis / creatures paranormales (creatures surnaturelles style Stranger Things)
- [ ] Systeme economique complet (ScrapValue present mais pas de shop)
- [ ] Multiples types de patients
- [ ] Systeme d'inventaire slots
- [ ] Systeme de sauvegarde
- [ ] Systeme de mort / danger

---

## 4. Points techniques critiques

### Reseau

- [x] FishNet NetworkBehaviour utilise correctement
- [x] Separation logique Server/Client (`IsServer`, `IsOwner` checks)
- [x] `SyncVar<T>` avec callbacks `OnChange`
- [x] ServerRpc / TargetRpc / ObserversRpc utilises appropriement
- [x] `GiveOwnership` pour transfert objets
- [x] Fix singleton PlayerInputReader (non-owner safe)

> **Attention** : Toujours desactiver NetworkTransform AVANT Despawn (fix dans TruckZone.cs)

> **Attention** : `FindObjectsByType` dans Scanner.cs peut causer lag en production

### Physique

- [x] CharacterController pour joueur (non-Rigidbody)
- [x] Rigidbody pour objets (interpolation activee)
- [x] `Physics.SyncTransforms()` apres teleportation
- [x] `IgnoreCollision` pour eviter collision joueur/objet tenu
- [x] Layer management recursif pour hierarchies complexes

### Architecture

- [x] Separation namespaces : Core, Player, Interaction, UI
- [x] Interfaces pour extensibilite (`IInteractable`)
- [x] ScriptableObjects pour data (ItemData, MissionData, LocalizationTable)
- [x] Singleton pour managers (LocalizationManager, GameManager, MissionManager)
- [x] Virtual methods pour heritage (GrabbableObject)

### Performance

- [x] Dictionary pour localization lookup
- [x] Update rates optimises (Scanner: UI 20fps, Scan 2fps)
- [x] Layer masks pour raycasts cibles

> `FindObjectsByType` appele 2x/s dans Scanner : acceptable < 50 objets, problematique > 200

---

## 5. Recommandations pour v0.7

### Priorite haute (requis pour Beta)

1. **Optimiser Scanner** : Remplacer `FindObjectsByType` par liste statique GameManager
2. **TargetRpc feedback** : Remplacer `ObserversRpc` par `TargetRpc` dans PatientObject.cs
3. **Feedback UI persistant** : Remplacer Debug.Log par NotificationHUD pour erreurs joueur
4. **Cleanup Debug.Log** : Supprimer emojis (LocalizationManager, LocalizationTable, PatientObject, TruckZone)

### Priorite moyenne (ameliorations)

5. **Inventaire multi-slots** : Ceinture 4 slots + main, keybinds 1-4
6. **Types de patients varies** : Refroidissement (Motor), Alimentation (Fuse), Fluides (Coolant)
7. **Systeme economique** : Shop pour acheter outils, utiliser ScrapValue existant
8. **Creatures paranormales** : Menace Stranger Things pendant les missions (risk/reward)
9. **Environnements annees 90** : Garage abandonne, sous-sol d'ecole, station-service, foret

### Priorite basse (polish)

10. Audio manager centralise (pools AudioSource)
11. Particle system manager (fumee, etincelles paranormales)
12. Animation joueur (Walk/Run/Crouch/Grab)
13. Effets post-processing (Bloom, Color Grading, Vignette 90s)
14. Tutorial interactif (premiere mission guidee)
15. Sauvegarde progression (PlayerPrefs -> JSON -> Cloud)

### Tests necessaires

- [ ] 4+ joueurs simultanes (stress test reseau)
- [ ] 50+ objets dans scene (performance Scanner)
- [ ] Toutes combinaisons pieces (Motor/Fuse/Coolant)
- [ ] Deconnexion joueur pendant reparation coop
- [ ] Latence elevee (100ms+)
- [ ] Boucle complete : mission -> reparation -> extraction -> debrief

---

## Conclusion

Le projet VoidWarranty v0.6 est dans un etat **solide** avec un prototype fonctionnel complet.

Le setting a ete redefini : on quitte l'espace pour une **petite ville americaine des annees 90, ambiance Stranger Things**. Les machines sont infectees par une force surnaturelle, et les techniciens interviennent dans des lieux typiques de l'epoque.

**Evolutions majeures v0.6** :
- GameManager + MissionManager : boucle gameplay Tarkov-like complete
- MissionHUD + NotificationHUD : feedback joueur en jeu
- TruckValidationButton : extraction manuelle claire
- RealisticHeadBob : immersion deplacement
- Fix multiplayer (PlayerInputReader, TruckZone, MissionHUD)
- UI esthetique cassette annees 90

**Pour atteindre Beta (v0.7)** :
1. Optimisation Scanner (liste statique)
2. Feedback UI persistant
3. Premiers environnements annees 90
4. Creatures paranormales (gameplay tension)

---

*Derniere mise a jour : 2026-02-16 | Version GDD reference : v0.6*
