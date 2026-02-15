# 🚚 Guide de Configuration - Bouton de Validation du Camion

## Problème

Le bouton de validation du camion n'est pas détecté par le système d'interaction.

## Solution

Le système d'interaction utilise un **Raycast** qui ne détecte que les objets sur un **Layer spécifique**.

---

## ✅ Configuration Step-by-Step

### 1. Identifier le Layer d'Interaction

1. Sélectionne le prefab **Player** dans la hiérarchie (après avoir lancé le jeu)
2. Trouve le composant **PlayerInteraction**
3. Regarde le champ **Interact Layer** → note le nom du layer (probablement "Interactable" ou "Default")

### 2. Créer le GameObject du Bouton

Dans la hiérarchie du Truck :

```
Truck (NetworkObject)
├── TruckZone (BoxCollider Trigger)
├── Model
└── ValidationButton (NOUVEAU)
    ├── TruckValidationButton (script)
    └── BoxCollider (NON trigger)
```

### 3. Configuration du ValidationButton

**GameObject "ValidationButton"** :
- **Layer** : Mettre le MÊME layer que celui configuré dans PlayerInteraction (ex: "Interactable")
- **Position** : Devant la porte du camion (visible et accessible)
- **Tag** : Default ou custom (peu importe)

**BoxCollider** :
- ✅ **Is Trigger** : **DÉCOCHÉ** (pour que le raycast le détecte)
- **Size** : Assez grand pour être facilement cliquable (ex: 0.5, 0.5, 0.2)
- **Center** : Ajuste pour bien positionner la zone cliquable

**Script TruckValidationButton** :
- `_truckZone` : Assigner manuellement le TruckZone parent (ou laisser vide, il cherchera automatiquement)

### 4. Vérification

Lance le jeu et :
1. Approche-toi du bouton de validation
2. Regarde le bouton (centre de l'écran)
3. Tu devrais voir le prompt **"Fermer le camion [E]"** apparaître en bas de l'écran

Si le prompt n'apparaît pas :
- ❌ Le layer du bouton n'est pas correct
- ❌ Le collider est en "Is Trigger = true" (doit être false)
- ❌ Le collider est trop petit ou mal positionné

---

## 🔍 Debug

### Test 1 : Le Layer

Ajoute ce debug dans `TruckValidationButton.Start()` :

```csharp
Debug.Log($"[TruckValidationButton] Layer = {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
```

### Test 2 : Le Raycast

Dans `PlayerInteraction.ScanForInteractable()`, ajoute un debug :

```csharp
if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactLayer))
{
    Debug.Log($"[PlayerInteraction] Hit = {hit.collider.name} (Layer: {hit.collider.gameObject.layer})");
    // ... reste du code
}
```

---

## 🎯 Exemple de Configuration

**Layer Setup** (dans Unity → Edit → Project Settings → Tags and Layers) :
- Layer 6 : `Interactable`

**PlayerInteraction (Inspector)** :
- Interact Layer : `Interactable` (layer 6)
- Interact Distance : `3.0`

**ValidationButton (Inspector)** :
- Layer : `Interactable` ✅
- Position : `(0, 1, -2)` (devant le camion)

**BoxCollider du ValidationButton** :
- Is Trigger : ❌ (DÉCOCHÉ)
- Size : `(0.5, 0.5, 0.2)`

---

## 📌 Alternative : Utiliser un Trigger pour la Validation

Si tu préfères que la validation se fasse **automatiquement** quand le joueur entre dans la zone (sans appuyer sur E), tu peux utiliser le **TruckZone** avec un `OnTriggerStay` :

```csharp
// Dans TruckZone.cs
private void OnTriggerStay(Collider other)
{
    if (!base.IsServer) return;
    if (MissionManager.Instance == null) return;

    var currentStep = MissionManager.Instance.GetCurrentStep();
    if (currentStep != MissionManager.MissionStep.Validation) return;

    // Si le joueur reste dans la zone pendant X secondes, valider auto
    // (nécessite un timer)
}
```

Mais pour l'instant, garde l'interaction manuelle avec le bouton (plus clair pour le joueur).

---

## ✅ Checklist Finale

- [ ] Le layer du ValidationButton correspond au layer configuré dans PlayerInteraction
- [ ] Le BoxCollider du ValidationButton a "Is Trigger = false"
- [ ] Le TruckValidationButton a une référence au TruckZone (ou peut le trouver via GetComponentInParent)
- [ ] Le bouton est positionné de manière visible et accessible
- [ ] Le prompt "Fermer le camion [E]" s'affiche quand tu regardes le bouton

---

**Note** : Ce système est **modulaire** car il réutilise le système d'interaction existant sans créer de code spaghetti. Le `TruckValidationButton` implémente simplement `IInteractable` et est détecté automatiquement par le `PlayerInteraction`.
