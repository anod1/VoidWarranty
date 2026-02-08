# VoidWarranty - Technical Documentation

**Technician multiplayer game built with Unity and FishNet**

## Project Overview

Multiplayer co-op game where players repair infected machines/patients on a spaceship.

---

## Scripts Structure

### Core

- **[IInteractable.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/IInteractable.cs)**
- **[ItemData.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/ItemData.cs)**
- **[LocalizationManager.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/LocalizationManager.cs)**
- **[LocalizationTable.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Core/LocalizationTable.cs)**

### Interaction

- **[ColorCube.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/ColorCube.cs)**
- **[GrabbableObject.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/GrabbableObject.cs)**
- **[PatientObject.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/PatientObject.cs)**
- **[RepairSocket.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/RepairSocket.cs)**
- **[Scanner.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/Scanner.cs)**
- **[SupplyCrate.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/SupplyCrate.cs)**
- **[TruckZone.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Interaction/TruckZone.cs)**

### Player

- **[PlayerCameraSetup.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerCameraSetup.cs)**
- **[PlayerGrab.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerGrab.cs)**
- **[PlayerInputReader.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerInputReader.cs)**
- **[PlayerInteraction.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerInteraction.cs)**
- **[PlayerMovement.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/Player/PlayerMovement.cs)**

### UI

- **[InteractionHUD.cs](https://raw.githubusercontent.com/anod1/VoidWarranty/main/Assets/_Game/Scripts/UI/InteractionHUD.cs)**

---

## Known Issues / Work In Progress

### Fixed
- [x] Character Controller center offset bug
- [x] Crouch system collision detection

### Current Work
- [ ] Scanner target priority system
- [ ] Network synchronization improvements
- [ ] Patient repair progress UI

### Planned Features
- [ ] Multiple ship levels
- [ ] Advanced toolbox mechanics
- [ ] Infection spread system

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

---

## Setup Instructions

1. Clone the repository
2. Open with Unity 6000.3.6f1+
3. FishNet will auto-import dependencies
4. Open _Game/Scenes/MainScene

---

**Last Updated**: 2026-02-08 18:43

