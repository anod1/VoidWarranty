# Guide de Configuration - Boucle de Gameplay (Tarkov-like)

## Vue d'ensemble

**Setting** : Petite ville americaine, annees 1990 (ambiance Stranger Things).
Les joueurs sont des techniciens envoyes reparer des machines infectees par une force surnaturelle.

La boucle de gameplay utilise un **systeme d'objectifs flexible** inspire de Tarkov :

### Philosophie
- **Extraction libre** : Le joueur peut partir a tout moment
- **Objectifs optionnels** : Seul reparer le patient est obligatoire pour le succes
- **Risk/Reward** : Plus d'objectifs = plus de recompenses, mais plus de risque

### Objectifs
1. **Principal (requis pour succes)** : Reparer le patient (machine infectee)
2. **Optionnels (bonus)** :
   - Ramener la piece defectueuse au camion (+scrap bonus)
   - Ramener les outils au camion (gardes pour prochaine mission, sinon perdus)

### Extraction
- Le joueur peut **extract a tout moment** via le bouton du camion (van de technicien)
- **Success** : Patient repare -> +Scrap + bonus optionnels
- **Failure** : Patient pas repare -> -Penalite expedition

---

## 🔧 Configuration Unity

### 1. Localisation

**Action requise** : Réimporter les fichiers CSV de localisation

1. Ouvre `Assets/_Game/Data/Languages/French.asset` (ou English.asset)
2. Dans l'Inspector, clique sur **"📥 Importer depuis CSV"**
3. Vérifie que les nouvelles clés apparaissent dans la liste `_entries`

**Nouvelles clés ajoutées** :
- `HUD_OBJECTIVES`, `HUD_OPTIONAL_OBJECTIVES`
- `OBJECTIVE_REPAIR_PATIENT`, `OBJECTIVE_RETURN_PART`, `OBJECTIVE_RETURN_TOOLS`
- `INTERACT_EXTRACT`
- `MISSION_EXTRACTED_SUCCESS`, `MISSION_EXTRACTED_FAILURE`
- `CURRENCY_SCRAP`

---

### 2. MissionHUD (Canvas UI)

**Modifications requises** dans la hiérarchie du Canvas :

Le prefab/GameObject `MissionHUD` doit avoir :

```
MissionHUD (GameObject)
├── MissionPanel (CanvasGroup) ← _missionPanel
│   ├── TitleText (TextMeshProUGUI) ← _missionTitle
│   ├── CurrentStepText (TextMeshProUGUI) ← _currentStepText [réutilisé pour objectifs]
│   └── TimerText (TextMeshProUGUI) ← _timerText
├── CompletedBanner (CanvasGroup) ← _completedBanner
└── FailedBanner (CanvasGroup) ← _failedBanner
```

**Note** : `_currentStepText` est réutilisé pour afficher la liste des objectifs au lieu de l'étape en cours.

---

### 3. MissionData (ScriptableObject)

Les `MissionData` existantes sont automatiquement compatibles. Nouveaux champs :

**Configuration** :
- `ScrapReward` : Récompense de base si le patient est réparé
- `DefectivePartBonus` : Bonus supplémentaire si la pièce défectueuse est ramenée
- `TimeLimit` : Temps limite en secondes (0 = pas de limite)

**Champs obsolètes** (ignorés par le nouveau système) :
- `RequiredPatientsRepaired`
- `RequiredDefectivePartsRecovered`

---

### 4. Camion (TruckZone + TruckValidationButton)

#### 4.1. TruckZone (existant)

Le script `TruckZone` a été refactorisé :
- Accepte les pièces défectueuses à tout moment (objectif optionnel)
- Accepte les outils à tout moment (sauvegardés)
- Gère l'extraction via `ValidateMissionServerRpc()`

#### 4.2. Bouton d'Extraction (TruckValidationButton)

Hiérarchie du camion :

```
Truck (GameObject)
├── TruckZone (script existant, BoxCollider trigger)
└── ValidationButton
    ├── TruckValidationButton (script)
    ├── BoxCollider (non-trigger, layer Interactable)
    └── [Optionnel] Model/Icon pour le bouton
```

**Configuration du script `TruckValidationButton`** :
- `_truckZone` : Assigner manuellement ou laisser vide (il cherchera dans le parent)

Le prompt affiche maintenant **"Extraire (quitter) [E]"** à tout moment pendant la mission.

---

### 5. Pièces et Items

**ItemData de la pièce de rechange** :
- `IsDefective` : **false** ✅

**ItemData de la pièce défectueuse** :
- `IsDefective` : **true** ✅

**ItemData des outils** :
- `ItemType` : **Tool** ✅

---

## 🎬 Déroulement du Gameplay

### Flux Complet

```
START
  ↓
[Mission Active]
  │
  ├── Joueur répare le patient (objectif principal)
  │   ↓ (PatientObject déclenche GameManager.OnPatientRepaired)
  │   → MissionManager.PatientRepaired = true
  │
  ├── Joueur ramène pièce défectueuse au camion (optionnel)
  │   ↓ (TruckZone.OnTriggerEnter détecte la pièce)
  │   → MissionManager.DefectivePartReturned = true
  │
  ├── Joueur ramène outils au camion (optionnel)
  │   ↓ (TruckZone.OnTriggerEnter détecte les outils)
  │   → MissionManager.ToolsReturnedCount++
  │
  └── Joueur clique sur le bouton d'extraction À TOUT MOMENT
      ↓ (TruckValidationButton → TruckZone.ValidateMissionServerRpc)
      → MissionManager.Extract()
      ↓
      [Calcul récompenses]
      - Success si patient réparé : +ScrapReward (+bonus si pièce ramenée)
      - Failure si patient pas réparé : -Pénalité expédition
      - Outils ramenés : gardés pour prochaine mission
      - Outils pas ramenés : perdus (doivent être rachetés)
      ↓
[Debrief] Banner de fin s'affiche en PERMANENT
  ↓
END (Retour au menu - à implémenter)
```

---

## 🐛 Debug

### Vérifications Console

Quand le jeu démarre, tu devrais voir :
```
[MissionManager] Mission démarrée : MISSION_PROTO_NAME
```

Quand tu appuies sur **Tab** :
```
[MissionHUD] Panel hidden, showing panel
```

Quand tu ramènes une pièce défectueuse :
```
[TruckZone] Pièce défectueuse ITEM_MOTOR_NAME ramenée → Bonus scrap
[MissionManager] Objectif optionnel complété : Pièce défectueuse ramenée
```

Quand tu ramènes un outil :
```
[TruckZone] Outil ITEM_TOOLBOX_NAME ramené → Conservé
[MissionManager] Outil ramené (1 total)
```

Quand tu extrais :
```
[TruckZone] Extraction demandée → Fin de mission
[MissionManager] Extraction ! Outcome: Success/Failure, Récompense totale: X scrap
```

### Problèmes Connus

**1. Tab ne fonctionne pas**
- Vérifie que `GameControls.inputactions` contient bien l'action "MissionToggle" bindée à Tab
- Vérifie que le `PlayerInputReader` du joueur local est bien `enabled`
- Regarde les logs dans la console

**2. Les objectifs ne se mettent pas à jour**
- Vérifie que `MissionManager.Instance` n'est pas null
- Vérifie que les `ItemData` ont bien `IsDefective` correctement configuré
- Vérifie que les outils ont `ItemType = Tool`

**3. L'extraction ne fonctionne pas**
- Vérifie que le `TruckValidationButton` est sur le bon layer (Interactable)
- Vérifie que le BoxCollider n'est PAS en trigger
- Vérifie que le prompt "Extraire (quitter) [E]" s'affiche quand tu regardes le bouton

---

## 🔄 Migration depuis l'Ancien Système

Les missions configurées avec l'ancien système (state machine) sont **automatiquement compatibles**.

### Changements :
- Les champs `RequiredPatientsRepaired` et `RequiredDefectivePartsRecovered` ne sont plus utilisés
- Ajout du champ `DefectivePartBonus` (défaut = 0)
- La logique linéaire a été remplacée par un système d'objectifs libre

### Compatibilité :
- Les `ItemData` existantes fonctionnent sans modification
- Les prefabs `Patient`, `TruckZone`, `GrabbableObject` sont compatibles
- La localisation a été mise à jour mais les anciennes clés restent présentes

---

## 📝 Notes Importantes

1. **NetworkBehaviour** : Tous les scripts utilisent FishNet
2. **ServerRpc** : Les validations importantes passent par le serveur
3. **SyncVar** : L'état de la mission est synchronisé sur tous les clients
4. **Extraction libre** : Le joueur peut partir quand il veut (Tarkov-like)
5. **Risk/Reward** : Plus tu ramènes d'objectifs, plus tu gagnes, mais tu risques de tout perdre si tu meurs (futur système)

---

## Prochaines Ameliorations

- [ ] Menu de selection de mission avant le gameplay
- [ ] Retour automatique au menu apres le Debrief
- [ ] Systeme de mort : perdre tout ce qu'on n'a pas ramene au camion
- [ ] Systeme d'economie : achat/vente d'outils
- [ ] Feedback visuel pour chaque objectif (checkmarks, highlights)
- [ ] Support multi-patients (plusieurs patients a reparer)
- [ ] Creatures paranormales (menace Stranger Things)
- [ ] Environnements annees 90 (garage, sous-sol, station-service, foret)

---

**Date** : 2026-02-16
**Version** : v2.1 (Setting annees 90 + Tarkov-like)
