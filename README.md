# VoidWarranty - Technical Documentation

**Jeu coopératif multijoueur de réparation construit avec Unity et FishNet**

## Project Overview

Jeu coopératif multijoueur se déroulant dans une petite ville américaine des années 90, ambiance Stranger Things.
Des machines et systèmes sont infectés par une force surnaturelle — les joueurs incarnent des techniciens envoyés pour réparer ces "patients" et accomplir des contrats de réparation.

**Setting** : Terre, années 1990 — petite ville américaine, atmosphère rétro, phénomènes paranormaux.

---

## Scripts Structure

### Core

- **[IInteractable.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/IInteractable.cs)** — Interface pour objets interactifs
- **[ItemData.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/ItemData.cs)** — ScriptableObject définition des items
- **[GameManager.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/GameManager.cs)** — Registre de session et gestion globale
- **[MissionManager.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/MissionManager.cs)** — Gestion des missions (extraction Tarkov-like)
- **[MissionData.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/MissionData.cs)** — ScriptableObject définition des missions
- **[LocalizationManager.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/LocalizationManager.cs)** — Singleton localisation
- **[LocalizationTable.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/LocalizationTable.cs)** — Table de traduction CSV
- **[RealisticHeadBob.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/RealisticHeadBob.cs)** — Effet caméra head bob réaliste

### Interaction

- **[GrabbableObject.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/GrabbableObject.cs)** — Base pour objets ramassables
- **[PatientObject.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/PatientObject.cs)** — Machine infectée à réparer (state machine 4 états)
- **[RepairSocket.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/RepairSocket.cs)** — Socket installation/retrait pièces
- **[Scanner.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/Scanner.cs)** — Radar + ciblage prioritaire
- **[SupplyCrate.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/SupplyCrate.cs)** — Caisse de fournitures
- **[TruckZone.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/TruckZone.cs)** — Zone d'extraction (camion)
- **[TruckValidationButton.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/TruckValidationButton.cs)** — Bouton d'extraction du camion
- **[ColorCube.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/ColorCube.cs)** — Debug/test

### Player

- **[PlayerCameraSetup.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerCameraSetup.cs)** — Attachement caméra joueur local
- **[PlayerGrab.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerGrab.cs)** — Grab/Drop physique réseau
- **[PlayerInputReader.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerInputReader.cs)** — Input System polling
- **[PlayerInteraction.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerInteraction.cs)** — Raycast interaction (touche E)
- **[PlayerMovement.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerMovement.cs)** — Déplacement, sprint, crouch, gravité

### UI

- **[InteractionHUD.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/UI/InteractionHUD.cs)** — Prompt d'interaction + crosshair
- **[MissionHUD.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/UI/MissionHUD.cs)** — Panel d'objectifs de mission (Tab)
- **[NotificationHUD.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/UI/NotificationHUD.cs)** — Notifications résultats de mission
- **[ButtonHoverEffect.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/UI/Menu/ButtonHoverEffect.cs)** — Effet hover boutons menu

---

## Known Issues / Work In Progress

### Fixed
- [x] Character Controller center offset bug
- [x] Crouch system collision detection
- [x] PlayerInputReader singleton overwritten by non-owner spawns
- [x] TruckZone despawn items prematurely (now validates on extract only)
- [x] MissionHUD multiplayer sync

### Current Work
- [ ] Scanner target priority optimization (remplacer FindObjectsByType par liste statique)
- [ ] Feedback UI persistant (remplacer Debug.Log par toast notifications)

### Implemented (v0.6)
- [x] GameManager (registre session)
- [x] MissionManager (boucle Tarkov-like)
- [x] MissionHUD (objectifs avec Tab toggle)
- [x] NotificationHUD (résultats mission)
- [x] TruckValidationButton (extraction manuelle)
- [x] RealisticHeadBob (effet caméra)
- [x] UI esthétique cassette années 90

### Planned Features
- [ ] Système économique (shop, achat/vente outils)
- [ ] Multiples niveaux de zones d'intervention
- [ ] Advanced toolbox mechanics
- [ ] Système de propagation d'infection paranormale

---

## Tech Stack

- **Engine**: Unity 6000.3.6f1
- **Networking**: FishNet
- **Input**: New Input System
- **Rendering**: URP

---

## Quick Links to Main Systems

- [PlayerMovement.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerMovement.cs)
- [PlayerGrab.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerGrab.cs)
- [GrabbableObject.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/GrabbableObject.cs)
- [PatientObject.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/PatientObject.cs)
- [Scanner.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/Scanner.cs)
- [MissionManager.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/MissionManager.cs)
- [GameManager.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/GameManager.cs)

---

## Setup Instructions

1. Clone the repository
2. Open with Unity 6000.3.6f1+
3. FishNet will auto-import dependencies
4. Open _Game/Scenes/MainScene

---

**Last Updated**: 2026-02-16
