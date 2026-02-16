# CONTEXTE DU PROJET

Je développe **Sub-Surface 0**, un jeu d'horreur psychologique coopératif (2-4 joueurs) inspiré de SOMA, dans Unity 6 avec Universal Render Pipeline (URP) et FishNet networking.

## AVANT DE COMMENCER - CRITIQUE

**Tu as accès au repo Git complet.** Avant de créer quoi que ce soit :

1. **Explore la structure des scripts existants** (tout sauf `.gitignore`)
2. **Analyse l'architecture FishNet actuelle** :
   - Comment détecter le joueur local ?
   - Quelles sont les conventions de nommage ?
   - Comment fonctionnent les interactions réseau ?
3. **Examine les scripts clés** :
   - `GrabbableObject.cs` : Pattern d'interaction
   - `PlayerInteraction.cs` : Détection joueur
   - `PatientObject.cs` : Système d'états
   - Tout autre script pertinent
4. **Respecte les conventions et patterns** déjà en place

**NE CRÉE RIEN avant d'avoir exploré et compris l'architecture existante.**

---

# PITCH & NARRATIVE

## Mission Initiale (Briefing)
```
ANNÉE : 2043
LIEU : Station de forage sous-marine HELIX-9
INCIDENT : Explosion il y a 3 jours, contact perdu
DERNIÈRE TRANSMISSION : "Containment breach, evacuate immedia—[STATIC]"

MISSION OFFICIELLE :
├── Récupérer la boîte noire (black box) pour enquête accident
├── Durée estimée : 2-3 heures
├── Équipe : 2-4 techniciens de récupération (joueurs)
└── Retour surface prévu : Immédiat après récupération

ESPOIR INITIAL : Mission simple, routine
```

---

## Structure Narrative (3 Actes)

### **ACTE I : RÉCUPÉRATION (Niveaux 1-3, -500m à -1500m)**

**Mindset Joueurs** : "Mission simple, on récupère et on rentre"
```
Niveau 1 (-500m) : Zone de Quarantaine
├── Objectif : Localiser la boîte noire
├── Ambiance : Station abandonnée, calme inquiétant
├── Électricité : Partiellement fonctionnelle (lumières rouges urgence)
├── Découverte : Logs audio fragmentés, effets personnels
└── Fin : Boîte noire trouvée, mais données cryptées

Niveau 2 (-1000m) : Laboratoires Biologiques
├── Objectif : Décrypter boîte noire via terminal
├── Ambiance : Premiers signes contamination (biofilm, algues sur murs)
├── Découverte : Logs parlent de "spécimens", "phase de test"
├── Puzzle : Rétablir alimentation terminal (coordination 2 joueurs)
└── Fin : Décryptage échoue, besoin serveur principal (plus bas)

Niveau 3 (-1500m) : Modules d'Habitation
├── Objectif : Débloquer accès niveaux inférieurs
├── Ambiance : Photos famille, jouets enfants (humaniser victimes)
├── EVENT SCRIPTÉ 1 : Ombre massive passe fenêtre extérieure (2 sec, jamais revue)
├── Découverte : Dernier log : "N'allez pas plus bas, ils ont tout contaminé"
└── Fin : Ascenseur vers zone industrielle déverrouillé

ESPOIR : "Ok, on descend chercher le serveur, puis on remonte"
```

---

### **ACTE II : DESCENTE (Niveaux 4-6, -2000m à -2847m)**

**Mindset Joueurs** : "Ça devient flippant mais on peut encore remonter"
```
Niveau 4 (-2000m) : Zone Industrielle
├── Objectif : Traverser pour atteindre salle serveur
├── Ambiance : Fog dense, obscurité quasi-totale, pression audible (creaking)
├── EVENT SCRIPTÉ 2 : APPARITION "THE DRIFTER"
│   └── Créature lovecraftienne (3m, bioluminescente) apparaît dans couloir
│   └── IA Patrouille : Zone spécifique (5 min gameplay)
│   └── Si détecté : Poursuite → Cachettes nécessaires (lockers, conduits)
│   └── Si attrapé : Écran noir, respawn checkpoint
│   └── Après zone : Disparaît (ne revient pas avant Niveau 6)
├── Découverte : "Si vous lisez ceci, fuyez tant que vous le pouvez"
└── Fin : Accès serveur atteint

Niveau 5 (-2500m) : Conduits Géothermiques
├── Objectif : Atteindre salle serveur principal
├── Ambiance : Chaleur extrême, vapeur, bioluminescence organique (shader animé)
├── Puzzle : Refroidir conduits surchauffés (vannes coordonnées, 2 joueurs)
├── EVENT SCRIPTÉ 3 : Fenêtre se fissure
│   └── CRACK sonore (jump scare)
│   └── Fissure apparaît (shader), eau suinte (VFX)
│   └── Porte sécurité se ferme auto (timer 10 sec)
├── Découverte : Plans de la station montrent "Faille HELIX-9" au niveau -2847m
└── Fin : Serveur principal accédé

Niveau 6 (-2847m) : Faille Thermale (RÉVÉLATION)
├── Objectif : Décrypter données serveur
├── RÉVÉLATION (Terminal) :
│   ├── La faille contient organisme pré-biotique
│   ├── Il se propage via l'eau océanique (remonte vers surface)
│   ├── Contamination mondiale estimée : 6 mois
│   ├── Protocole HELIX-OMEGA existe (charge nucléaire au niveau -3000m)
│   └── Seule solution : Détruire la faille manuellement
├── TWIST : Ascenseur de retour est détruit (éboulement visible)
├── EVENT SCRIPTÉ 4 : RETOUR "THE DRIFTER"
│   └── Pendant lecture données, porte explose
│   └── The Drifter entre, plus agressif (vitesse +50%)
│   └── Joueurs doivent terminer download ET fuir
├── Découverte : Accès Niveau 7 déverrouillé (point de non-retour)
└── Fin : Réalisation collective : "On ne remonte pas"

ESPOIR BRISÉ : "On va mourir... mais on peut sauver l'humanité"
```

---

### **ACTE III : SACRIFICE (Niveau 7, -3000m)**

**Mindset Joueurs** : "Mission suicide, mais héroïque"
```
Niveau 7 (-3000m) : L'Abîme
├── Descente Finale : Ascenseur vitré, voir la faille pulser en contrebas
├── Arrivée : Masse organique gigantesque (indescriptible, lovecraftienne)
├── Objectif : Activer Protocole HELIX-OMEGA (charge nucléaire)
├── Puzzle Final : 2 clés à tourner simultanément (2 joueurs minimum)
│   └── Clé 1 : Salle A (30m à gauche)
│   └── Clé 2 : Salle B (30m à droite)
│   └── Timer visible : 90 secondes avant détonation
│   └── Communication vocale nécessaire (coordination timing)
├── FIN UNIQUE : Explosion nucléaire
│   └── Cinématique : Vue extérieure, station implose
│   └── Écran blanc
│   └── Générique
└── Post-Credits (Optionnel) :
    └── Audio : "Équipe HELIX-9, répondez... [SILENCE]"
    └── Texte : "6 mois plus tard : Aucune contamination en surface"
    └── Message final : "Que leur sacrifice ne soit pas vain"

MESSAGE : Sacrifice héroïque, sauver l'humanité
```

---

# RÈGLE ABSOLUE : ZERO COMBAT

## Ce que le jeu N'EST PAS
- ❌ **PAS de combat** (aucune arme, aucun QTE pour tuer)
- ❌ **PAS de boss fights** (aucun ennemi à vaincre)
- ❌ **PAS de mécaniques de dégâts** (pas de barre de vie à gérer)

## Ce que le jeu EST
- ✅ **Horreur d'évitement pur** (Amnesia, SOMA, Outlast)
- ✅ **Esquive via cachettes** (lockers, sous bureaux, conduits)
- ✅ **Furtivité sonore** (marcher lentement, courir = bruit = détection)
- ✅ **Environnement = danger principal** (pression, inondations, éboulements)
- ✅ **Phobie des abysses** (thalassophobie)

---

# LA MENACE : "THE DRIFTER"

## Design Créature
```
APPARENCE :
├── Silhouette humanoïde déformée (3m de haut)
├── Peau translucide bleu-vert (veines bioluminescentes)
├── Pas de visage reconnaissable (masse organique lisse)
├── Tentacules courts dorsaux (ondulent lentement)
└── Déplacement : Glisse sur sol (animation float, pas de marche)

AUDIO SIGNATURE :
├── Respiration aquatique (glouglous graves, 0.5 Hz)
├── Grattement métallique (ongles sur coque)
└── Chant basse fréquence (40-60 Hz, fait vibrer l'eau)

POLYCOUNT : Max 3000 tris (low-poly terrifiant > high-poly)
TEMPS CRÉATION : ~45 min (humanoïde simple + shader bioluminescent)
```

---

## Comportement IA (Simple pour Solo Dev)

**3 États Seulement** :
```csharp
enum DrifterState { Patrol, Investigate, Chase }

// PATROL
├── Suit waypoints prédéfinis (loop)
├── Vitesse : 1 m/s (lent)
├── Si entend bruit > 50 dB dans rayon 20m → Investigate
└── Audio : Chant basse fréquence constant

// INVESTIGATE
├── Se déplace vers position du bruit (dernière connue)
├── Vitesse : 2 m/s (moyen)
├── Si voit joueur (Raycast, angle 120°) → Chase
├── Si rien trouvé après 30 sec → Patrol
└── Audio : Respiration accélérée

// CHASE
├── Suit joueur directement (NavMeshAgent)
├── Vitesse : 3 m/s (rapide mais pas imbloquable)
├── Si perd de vue pendant 10 sec → Investigate
├── Si attrape joueur (OnTriggerEnter) → Kill
│   └── Écran noir + son étranglement
│   └── Respawn au dernier checkpoint
└── Audio : Grattement + respiration rapide
```

**Zones d'Apparition** :
- **Niveau 4** : 1 zone unique (couloir inondé, 5 min gameplay)
- **Niveau 6** : 1 zone finale (salle serveur, 8 min gameplay)
- **JAMAIS ailleurs** (rareté = terreur maximale)

---

## Système Cachettes (Simple)
```csharp
// Tag "HidingSpot" sur colliders (lockers, bureaux, conduits)

void OnTriggerStay(Collider col) {
    if (col.CompareTag("HidingSpot") && Input.GetKey(KeyCode.C)) {
        EnterHiding();
    }
}

void EnterHiding() {
    _isHiding = true;
    gameObject.layer = LayerMask.NameToLayer("Hidden"); // Invisible pour IA
    _playerMovement.enabled = false; // Immobile
    _firstPersonCamera.enabled = false; // Vue fixe
}

void ExitHiding() {
    _isHiding = false;
    gameObject.layer = LayerMask.NameToLayer("Player");
    _playerMovement.enabled = true;
    _firstPersonCamera.enabled = true;
}
```

**Prefabs Cachettes** :
- **Locker** : Armoire métallique (BoxCollider trigger)
- **Desk** : Bureau avec espace dessous (BoxCollider trigger)
- **Vent** : Grille ventilation avec conduit (BoxCollider trigger + crawl animation)

---

# EVENTS SCRIPTÉS (4 Maximum)

## Event 1 : Ombre Furtive (Niveau 3)
```
TRIGGER : Joueur entre dans zone "HallwayWindow"
SÉQUENCE :
├── 0.0s : Ombre massive (30m) passe devant fenêtre extérieure
├── 0.5s : Son sourd de déplacement d'eau (whoosh grave)
├── 2.0s : Ombre disparaît complètement
└── 3.0s : Silence

IMPLÉMENTATION :
├── Mesh plan noir (scale 30x30) avec shader transparent
├── Animation simple : Position X -50 → +50 (linear, 2 sec)
├── Audio Source 3D (son whale déformé)
└── Destroy après 5 secondes

OBJECTIF : Planter graine "Il y a quelque chose dehors"
```

---

## Event 2 : Apparition The Drifter (Niveau 4)
```
TRIGGER : Joueur arrive à intersection "CorridorT"
SÉQUENCE :
├── 0.0s : Lumières flickent (3 fois, 0.2s interval)
├── 1.0s : The Drifter spawn au bout couloir (30m devant)
├── 2.0s : Tourne tête vers joueurs (Lerp rotation, 1 sec)
├── 3.0s : Commence glissement vers joueurs (IA activée)
└── Joueurs doivent fuir ou se cacher

IMPLÉMENTATION :
├── Light.intensity lerp 1.0 → 0.0 → 1.0 (flicker)
├── Instantiate(DrifterPrefab, spawnPoint)
├── Drifter.LookAt(player) avec Lerp
└── DrifterAI.SetState(Chase)

OBJECTIF : Introduction menace principale
```

---

## Event 3 : Fenêtre Fissurée (Niveau 5)
```
TRIGGER : Joueur passe devant "LargeWindow"
SÉQUENCE :
├── 0.0s : CRACK sonore violent (jump scare)
├── 0.5s : Fissure apparaît sur vitre (shader mask animé)
├── 1.0s : Eau commence à suinter (VFX Graph particules)
├── 5.0s : Porte sécurité se ferme automatiquement (animation)
├── 6.0s : Fissure arrête de grandir (stabilisé)
└── Eau continue de goutter (ambiance)

IMPLÉMENTATION :
├── AudioSource.PlayOneShot(glassCrack)
├── Material window : Shader Graph avec crack mask (alpha cutout animé)
├── VFX Graph : Spawn rate 50/sec, lifetime 2s, direction downward
├── Door animation (Animator trigger "Close")
└── Collider porte devient solid après fermeture

OBJECTIF : Rappel pression extérieure, urgence
```

---

## Event 4 : Retour The Drifter (Niveau 6)
```
TRIGGER : Joueur active terminal serveur
SÉQUENCE :
├── 0.0s : Joueur lit données (UI texte révélation)
├── 30.0s : Son grattement lointain (build tension)
├── 45.0s : Porte explose (particules métal, son violent)
├── 46.0s : The Drifter entre, vitesse +50% (plus agressif)
├── Download progress : 0% → 100% (60 secondes)
└── Joueurs doivent finir download ET fuir vers sortie

IMPLÉMENTATION :
├── UI Panel avec texte révélation (fade in)
├── AudioSource.Play(distantScraping) à 30s
├── Door : Explosion VFX + Rigidbody.AddExplosionForce
├── Instantiate(DrifterPrefab), DrifterAI.SetState(Chase), speed *= 1.5f
├── Progress bar UI (Lerp 0→1 over 60s)
└── OnDownloadComplete() : Unlock exit door

OBJECTIF : Climax tension, course contre montre
```

---

# SYSTÈME DE CONTAMINATION (Visuel Uniquement)

**IMPORTANT** : Pas d'impact gameplay, uniquement ambiance visuelle.

## 4 Phases (Progressives)
```
PHASE 1 (Niveaux 1-2, -500m à -1000m) : EXPOSITION
├── Acouphènes légers (Audio Low-Pass aléatoire 1000-2000 Hz, 10% du temps)
├── Vignette subtile (Intensity 0.2, Color #0a1a2a)
└── Film Grain faible (Intensity 0.2)

PHASE 2 (Niveaux 3-4, -1500m à -2000m) : CONTAMINATION
├── Chromatic Aberration (Intensity 0.3)
├── Vignette augmentée (Intensity 0.5)
├── Film Grain moyen (Intensity 0.4)
├── FOV légèrement augmenté (60° → 65°)
└── Rare : Affichage nom autre joueur à la place du sien (5% du temps, 2 sec)

PHASE 3 (Niveaux 5-6, -2500m à -2847m) : TRANSFORMATION
├── Chromatic Aberration forte (Intensity 0.5)
├── Saturation réduite (Color Adjustments: -30)
├── Hue Shift cyan (Color Adjustments: +15)
├── Vignette maximale (Intensity 0.8)
├── Film Grain fort (Intensity 0.6)
└── FOV max (70°)

PHASE 4 (Niveau 7, -3000m) : POINT DE NON-RETOUR
├── Tous effets Phase 3 maintenus
├── Outline shader orange sur masse organique (vision "thermique" narrative)
└── Pas de changement gameplay (juste visuel narratif)
```

**Implémentation** : Volume Profile switching par niveau (pas de mécanique progressive)

---

# PHOBIE DES ABYSSES (Thalassophobie)

## Éléments Visuels Essentiels
```
NIVEAU 1-2 (-500m à -1000m) : Zone Crépusculaire
├── Fog léger (Density 0.05, Color #5a8db8 bleu clair)
├── Lumière directionnelle bleue (Intensity 0.3, simule surface)
├── Fenêtres : Vue vers haut (lumière lointaine visible)
└── Palette : Bleu-Gris industriel (#2a3d4a)

NIVEAU 3-4 (-1500m à -2000m) : Zone Twilight
├── Fog moyen (Density 0.10, Color #2a3d4a bleu foncé)
├── Lumière directionnelle absente (noir extérieur)
├── Fenêtres : Noir total avec reflets intérieurs
├── Spots oranges (urgence, Intensity 3.0)
└── Palette : Vert-Teal (#1a4d3d)

NIVEAU 5-6 (-2500m à -2847m) : Zone Abyssale
├── Fog dense (Density 0.15, Color #1a1a1a noir-vert)
├── Obscurité totale (sauf lumières artificielles)
├── Bioluminescence (Point Lights verts, Intensity 1.5, flicker)
├── Biofilm shader (pulsations organiques, Time-based)
└── Palette : Orange-Rouille (#8a3d2a)

NIVEAU 7 (-3000m) : Abîme Hadal
├── Fog maximal (Density 0.20, Color #000000 noir pur)
├── Seule lumière : Faille (Emission shader rouge #6a1a1a)
├── Pas de fenêtres (enfermement total)
└── Palette : Rouge-Magma (#6a1a1a)
```

---

## Audio Essentiel (Thalassophobie)
```
AMBIANCE GÉNÉRALE (Tous Niveaux) :
├── Creaking métallique constant (pression, 0.2 Hz loop)
├── Gouttes d'eau (random interval 2-5 sec)
├── Hum électrique bas (ventilation, 50 Hz)
└── Heartbeat joueur (amplifié quand stressé, BPM 60-120)

NIVEAU 1-2 :
├── Vagues distantes (muffled, < 200 Hz)
├── Sons de coque sous pression (grincements lents)
└── Occasion : Mouettes très lointaines (nostalgie surface)

NIVEAU 3-4 :
├── Whale calls déformés (Lovecraftiens, 20-80 Hz)
├── Sifflements de pression (steam vents)
└── Gouttes deviennent flaques (splashing)

NIVEAU 5-6 :
├── Plaintes métalliques (torsion coque, aléatoire)
├── Biofilm pulsant (son organique visqueux, wet squelch)
├── Grondements lointains (faille thermale, < 30 Hz)
└── Silence soudain (5 sec) suivi de CRACK (jump scare)

NIVEAU 7 :
├── Silence quasi-total (oppressant)
├── Faille : Pulsation grave (10 Hz, subwoofer)
├── Heartbeat joueur = seul son régulier
└── The Drifter : Chant final (climax)
```

**Implémentation** :
- Audio Sources 3D pour sons directionnels
- Audio Low-Pass Filter global (cutoff varie par niveau)
- Reverb Filter "Underwater" (Unity preset)
- FMOD ou Wwise si budget temps (sinon Unity Audio Mixer suffit)

---

# PALETTE COULEURS PAR PROFONDEUR
```
NIVEAU 1-2 (-500m à -1000m) : INDUSTRIEL
├── Walls : #2a3d4a (Bleu-Gris béton)
├── Lights : #ffffff (Blanc froid néons)
├── Accent : #ff4500 (Orange urgence)
└── Fog : #5a8db8 (Bleu clair)

NIVEAU 3-4 (-1500m à -2000m) : SOUS-MARIN
├── Walls : #1a4d3d (Vert-Teal métal)
├── Lights : #88ffdd (Cyan faible)
├── Accent : #ffaa00 (Jaune avertissement)
└── Fog : #2a3d4a (Bleu foncé)

NIVEAU 5-6 (-2500m à -2847m) : BIOFILM
├── Walls : #8a3d2a (Orange-Rouille + texture organique)
├── Lights : #ff6600 (Orange géothermique)
├── Accent : #00ff88 (Vert bioluminescent)
└── Fog : #1a1a1a (Noir-Vert)

NIVEAU 7 (-3000m) : ABÎME
├── Walls : #000000 (Noir total)
├── Lights : #6a1a1a (Rouge magma faille uniquement)
├── Accent : #ff0000 (Rouge danger pur)
└── Fog : #000000 (Noir absolu)
```

---

# TRANSITIONS ENTRE NIVEAUX (Descente)

## Ascenseur de Descente (Entre Chaque Niveau)
```
DESIGN :
├── Petit espace confiné (2x2m, 4 joueurs max serrés)
├── 4 parois : 3 métal, 1 vitre (vue extérieur)
├── Durée trajet : 30-45 secondes (temps de respirer)
└── UI Profondeur : "-1000m... -1500m... -2000m..." (compte en temps réel)

SÉQUENCE VISUELLE (Exemple -1000m → -1500m) :
├── 0-10s : Passage zone éclairée (bleu clair visible dehors)
├── 10-20s : Transition (bleu → noir progressif)
├── 20-30s : Noir total (sauf lumière ascenseur intérieur)
├── 30s : Arrivée, porte s'ouvre (révèle niveau suivant)
└── Son : Câbles qui grincent, métal qui se tord (intensifie avec profondeur)

IMPLÉMENTATION :
├── Ascenseur = Kinematic Rigidbody (MovePosition smooth)
├── Vitre = Plane transparent avec Skybox extérieur (gradient bleu→noir)
├── Audio Source 3D (cable strain, volume croissant)
├── UI Canvas World Space (texte profondeur)
└── Post-Processing Volume local (transition fog density)
```

---

# RECYCLAGE SCRIPTS EXISTANTS

**AVANT DE CRÉER, EXPLORE CES SCRIPTS** :

1. `GrabbableObject.cs` → Comprendre pattern interaction FishNet
2. `PlayerInteraction.cs` → Comment détecter joueur local
3. `PatientObject.cs` → Système états d'objets
4. Tout autre script pertinent

**ADAPTATIONS PRÉVUES** :
```
PatientObject.cs → BlackBoxTerminal.cs
├── États : Locked → Decrypting → Unlocked → DataRead
├── Ajout : Audio log playback
└── Modification : ~10 lignes

GrabbableObject.cs → StoryObject.cs (Inchangé)
├── Ajout : _loreTextKey (string)
├── OnGrabbed() : Display lore text in UI
└── Modification : ~5 lignes

TruckZone.cs → SafeZone.cs (Checkpoints)
├── OnTriggerEnter : Save player position
├── OnPlayerDeath : Respawn ici
└── Modification : ~15 lignes
```

---

# OBJECTIF IMMÉDIAT : SYSTÈME UNDERWATER COMPLET

## PROCESSUS DE TRAVAIL

1. **Explore le repo** (30 min minimum)
2. **Pose-moi des questions** sur ce qui n'est pas clair
3. **Crée les assets** en respectant architecture existante
4. **Teste** (vérifie compilation, pas d'erreurs)
5. **Documente** (commentaires code)

---

## DELIVERABLES REQUIS

### 1. POST-PROCESSING PROFILES (6 Volumes)

**Locations** : `Assets/_Game/Settings/PostProcessing/`

**VP_Level1.asset** (-500m à -1000m) :
```yaml
Fog:
  Mode: Exponential
  Density: 0.05
  Color: #5a8db8
  Max Distance: 60

Color Adjustments:
  Saturation: 0 (normal)
  Hue Shift: 0
  Temperature: -5

Vignette:
  Intensity: 0.2
  Color: #0a1a2a

Film Grain:
  Intensity: 0.2
```

**VP_Level2.asset** (-1000m à -1500m) :
```yaml
Fog:
  Density: 0.08
  Color: #2a3d4a

Color Adjustments:
  Saturation: -10
  Hue Shift: +5
  Temperature: -10

Chromatic Aberration:
  Intensity: 0.1

Vignette:
  Intensity: 0.3

Film Grain:
  Intensity: 0.3
```

**VP_Level3.asset** (-1500m à -2000m) :
```yaml
Fog:
  Density: 0.10
  Color: #1a4d3d

Color Adjustments:
  Saturation: -20
  Hue Shift: +10
  Temperature: -15

Chromatic Aberration:
  Intensity: 0.3

Vignette:
  Intensity: 0.5

Film Grain:
  Intensity: 0.4
```

**VP_Level4.asset** (-2000m à -2500m) :
```yaml
Fog:
  Density: 0.12
  Color: #1a4d3d

Color Adjustments:
  Saturation: -30
  Hue Shift: +15
  Temperature: -20
  Contrast: +10

Chromatic Aberration:
  Intensity: 0.4

Vignette:
  Intensity: 0.6

Film Grain:
  Intensity: 0.5

Bloom:
  Intensity: 0.3
  Threshold: 1.2
```

**VP_Level5.asset** (-2500m à -2847m) :
```yaml
Fog:
  Density: 0.15
  Color: #1a1a1a

Color Adjustments:
  Saturation: -40
  Hue Shift: +20
  Temperature: -25
  Contrast: +15

Chromatic Aberration:
  Intensity: 0.5

Vignette:
  Intensity: 0.7

Film Grain:
  Intensity: 0.6

Bloom:
  Intensity: 0.4
  Threshold: 1.0
```

**VP_Level6.asset** (-3000m, Abîme) :
```yaml
Fog:
  Density: 0.20
  Color: #000000

Color Adjustments:
  Post Exposure: -1.0
  Saturation: -50
  Hue Shift: +25
  Temperature: -30
  Contrast: +20

Chromatic Aberration:
  Intensity: 0.6

Vignette:
  Intensity: 0.9
  Color: #000000

Film Grain:
  Intensity: 0.7

Bloom:
  Intensity: 0.5
  Threshold: 0.8
```

---

### 2. VFX GRAPH PARTICULES

**VFX_UnderwaterParticles.vfx** :
```yaml
Location: Assets/_Game/Art/VFX/

Spawn:
  Rate: 100 particles/sec
  Capacity: 500

Initialize:
  Lifetime: Random (3.0 to 8.0)
  Position: Random Box (50x50x50)
  Size: Random (0.05 to 0.2)
  Velocity: Random Direction (-0.1 to 0.1)
  Color: Gradient (White α=1.0 → White α=0.0)

Update:
  Turbulence: Curl Noise (Intensity 0.3, Frequency 0.5)
  Gravity: -0.05
  Drag: 0.1

Output:
  Blend Mode: Additive
  Soft Particles: 1.0
```

**VFX_Bioluminescence.vfx** (Niveaux 5-6) :
```yaml
Spawn:
  Rate: 20 particles/sec
  Capacity: 100

Initialize:
  Lifetime: Random (5.0 to 10.0)
  Position: Random Sphere (Radius 20)
  Size: Random (0.1 to 0.3)
  Velocity: Zero
  Color: #00ff88 (Vert bioluminescent)

Update:
  Flicker: Sine wave on Color Intensity (Frequency 0.5 Hz)
  Float: Noise Y Position (Amplitude 0.5)

Output:
  Blend Mode: Additive
  Glow: Bloom compatible
```

---

### 3. SHADER GRAPH EAU

**SG_WaterSurface.shadergraph** :
```
Location: Assets/_Game/Art/Shaders/

Type: URP Lit Shader Graph

PROPERTIES:
├── _BaseColor: #1a4d5c (Bleu océan profond)
├── _NormalMap: Texture2D (water normals)
├── _NormalScale: 1.0
├── _Smoothness: 0.95
├── _DepthFade: 5.0

GRAPH:
Time Node
  └→ Multiply (0.1)
  └→ Add to UV
  └→ Sample Normal Map (animated)

Fresnel
  └→ View Direction + Normal
  └→ Power (3)
  └→ Multiply Base Color (reflets)

Depth Fade
  └→ Scene Depth - Camera Depth
  └→ Divide by _DepthFade
  └→ Saturate
  └→ Alpha

OUTPUT:
├── Base Color: Fresnel blend
├── Normal: Normal Map (Tangent Space)
├── Smoothness: 0.95
├── Metallic: 0
├── Alpha: Depth Fade
```

**Material** : `MAT_WaterSurface.mat`
- Surface Type: Transparent
- Render Face: Both
- Render Queue: Transparent

---

### 4. SHADER GRAPH BIOFILM (Niveaux 5-6)

**SG_Biofilm.shadergraph** :
```
Location: Assets/_Game/Art/Shaders/

Type: URP Lit Shader Graph

PROPERTIES:
├── _BaseColor: #8a3d2a (Orange-Rouille)
├── _BiolumColor: #00ff88 (Vert luminescent)
├── _PulseSpeed: 0.5
├── _NoiseScale: 5.0

GRAPH:
Time Node
  └→ Multiply (_PulseSpeed)
  └→ Sine
  └→ Remap (0-1 → 0.3-1.0)
  └→ Multiply _BiolumColor
  └→ Emission

Voronoi Noise
  └→ Scale (_NoiseScale)
  └→ Time-based UV offset
  └→ Multiply Base Color (texture organique)

OUTPUT:
├── Base Color: Voronoi blend
├── Emission: Pulsating Biolum
├── Normal: Bumpy (from noise)
├── Smoothness: 0.3 (organique mat)
```

---

### 5. SCRIPTS ENVIRONNEMENT

**IMPORTANT** : Explore d'abord les scripts existants pour comprendre :
- Comment détecter le joueur local (FishNet)
- Conventions de nommage
- Architecture du projet

#### **Script A : DepthZoneTrigger.cs**
```csharp
Location: Assets/_Game/Scripts/Environment/DepthZoneTrigger.cs

Namespace: SubSurface.Environment

Fonction:
├── Détecte entrée joueur local dans zone
├── Switch Volume Profile (transition smooth 1 sec)
├── Active/désactive VFX particules
├── Update UI profondeur ("-2000m")
└── Client-side uniquement (pas de [ServerRpc])

Requirements:
├── [SerializeField] Volume _globalVolume
├── [SerializeField] VolumeProfile _zoneProfile
├── [SerializeField] GameObject _vfxPrefab
├── [SerializeField] string _depthText
├── [SerializeField] float _transitionDuration = 1.0f
└── Coroutine pour lerp smooth

Template (À ADAPTER selon architecture existante):
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

namespace SubSurface.Environment
{
    [RequireComponent(typeof(BoxCollider))]
    public class DepthZoneTrigger : MonoBehaviour
    {
        [Header("Volume Profile")]
        [SerializeField] private Volume _globalVolume;
        [SerializeField] private VolumeProfile _zoneProfile;
        
        [Header("VFX")]
        [SerializeField] private GameObject _vfxPrefab;
        private GameObject _activeVfx;
        
        [Header("UI")]
        [SerializeField] private string _depthText = "-2000m";
        
        [Header("Settings")]
        [SerializeField] private float _transitionDuration = 1.0f;
        
        private VolumeProfile _previousProfile;
        private Coroutine _transitionCoroutine;
        
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // TODO: Check if local player (use existing pattern from repo)
            // If yes:
            //   - Start profile transition
            //   - Spawn VFX
            //   - Update depth UI
        }
        
        private void OnTriggerExit(Collider other)
        {
            // TODO: Restore previous profile
            // Destroy VFX
        }
        
        private IEnumerator TransitionProfile(VolumeProfile target)
        {
            // TODO: Smooth lerp transition
            yield return null;
        }
    }
}
```

---

#### **Script B : HidingSpot.cs**
```csharp
Location: Assets/_Game/Scripts/Gameplay/HidingSpot.cs

Namespace: SubSurface.Gameplay

Fonction:
├── Détecte joueur entrant (OnTriggerStay)
├── Input "C" pour entrer/sortir
├── Rend joueur invisible pour IA (Layer "Hidden")
├── Désactive mouvement pendant cachette
└── Affiche prompt UI "Appuyez C pour se cacher"

Template:
using UnityEngine;
using FishNet.Object; // Si nécessaire pour IsOwner

namespace SubSurface.Gameplay
{
    [RequireComponent(typeof(BoxCollider))]
    public class HidingSpot : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private KeyCode _hideKey = KeyCode.C;
        [SerializeField] private string _promptText = "Press C to hide";
        
        private bool _playerInRange = false;
        private GameObject _currentPlayer;
        private bool _isHiding = false;
        
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // TODO: Check if local player
            // Set _playerInRange = true
            // Show UI prompt
        }
        
        private void OnTriggerStay(Collider other)
        {
            if (_playerInRange && Input.GetKeyDown(_hideKey))
            {
                if (!_isHiding)
                    EnterHiding(other.gameObject);
                else
                    ExitHiding();
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            // TODO: Check if local player
            // Set _playerInRange = false
            // Hide UI prompt
            // If was hiding, force exit
        }
        
        private void EnterHiding(GameObject player)
        {
            // TODO:
            // - Set layer to "Hidden"
            // - Disable player movement
            // - Disable camera rotation (optional)
            // - Play "hide" animation (optional)
            _isHiding = true;
        }
        
        private void ExitHiding()
        {
            // TODO:
            // - Restore layer to "Player"
            // - Enable movement
            // - Enable camera
            _isHiding = false;
        }
    }
}
```

---

#### **Script C : DrifterAI.cs** (Simple 3-State)
```csharp
Location: Assets/_Game/Scripts/AI/DrifterAI.cs

Namespace: SubSurface.AI

Fonction:
├── 3 états : Patrol, Investigate, Chase
├── Détection joueur : Raycast (120° FOV)
├── Détection bruit : Sphere overlap (20m radius)
├── Poursuite : NavMeshAgent
└── Kill : OnTriggerEnter → Respawn joueur

IMPORTANT:
├── Pas de synchronisation réseau (AI server-side)
├── Joueurs voient le même Drifter (NetworkObject)
└── Kill = [ServerRpc] pour validation

Template (Simplifié, à étendre):
using UnityEngine;
using UnityEngine.AI;
using FishNet.Object;

namespace SubSurface.AI
{
    public enum DrifterState { Patrol, Investigate, Chase }
    
    [RequireComponent(typeof(NavMeshAgent))]
    public class DrifterAI : NetworkBehaviour
    {
        [Header("States")]
        [SerializeField] private DrifterState _currentState = DrifterState.Patrol;
        
        [Header("Patrol")]
        [SerializeField] private Transform[] _patrolWaypoints;
        [SerializeField] private float _patrolSpeed = 1.0f;
        private int _currentWaypointIndex = 0;
        
        [Header("Detection")]
        [SerializeField] private float _detectionRadius = 20f;
        [SerializeField] private float _visionAngle = 120f;
        [SerializeField] private LayerMask _playerLayer;
        
        [Header("Chase")]
        [SerializeField] private float _chaseSpeed = 3.0f;
        [SerializeField] private float _loseDuration = 10f;
        private Transform _target;
        private float _loseTimer;
        
        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _patrolSound;
        [SerializeField] private AudioClip _chaseSound;
        
        private NavMeshAgent _agent;
        
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            SetState(DrifterState.Patrol);
        }
        
        private void Update()
        {
            if (!IsServer) return; // AI runs only on server
            
            switch (_currentState)
            {
                case DrifterState.Patrol:
                    UpdatePatrol();
                    break;
                case DrifterState.Investigate:
                    UpdateInvestigate();
                    break;
                case DrifterState.Chase:
                    UpdateChase();
                    break;
            }
        }
        
        private void UpdatePatrol()
        {
            // TODO:
            // - Move to waypoints
            // - Check for noise/vision
            // - If detected → SetState(Investigate/Chase)
        }
        
        private void UpdateInvestigate()
        {
            // TODO:
            // - Move to last known position
            // - Check for player
            // - Timeout → Patrol
        }
        
        private void UpdateChase()
        {
            // TODO:
            // - Follow target
            // - If lose sight → Timer → Investigate
        }
        
        private void SetState(DrifterState newState)
        {
            _currentState = newState;
            
            switch (newState)
            {
                case DrifterState.Patrol:
                    _agent.speed = _patrolSpeed;
                    PlaySound(_patrolSound);
                    break;
                case DrifterState.Chase:
                    _agent.speed = _chaseSpeed;
                    PlaySound(_chaseSound);
                    break;
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            
            // TODO: Check if player
            // If yes → KillPlayer(other.gameObject)
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void KillPlayer(GameObject player)
        {
            // TODO:
            // - Fade to black
            // - Respawn at checkpoint
            // - Play death sound
        }
        
        private void PlaySound(AudioClip clip)
        {
            if (_audioSource && clip)
                _audioSource.PlayOneShot(clip);
        }
    }
}
```

---

### 6. PREFABS

**PREFAB_HidingLocker** :
```
Hierarchy:
├── Model (Mesh locker, 800 tris)
├── Trigger (BoxCollider, Size: 1x2x1)
└── HidingSpot (Script)

Location: Assets/_Game/Prefabs/Gameplay/
```

**PREFAB_Drifter** :
```
Hierarchy:
├── Model (Mesh créature, 3000 tris)
│   └── Biolum_Shader (Material avec émission)
├── AudioSource (3D, spatial blend 1.0)
├── NavMeshAgent
├── CapsuleCollider (Trigger pour kill)
└── DrifterAI (Script)

Location: Assets/_Game/Prefabs/AI/
```

**PREFAB_DepthZone** :
```
Hierarchy:
├── Trigger (BoxCollider, Size: 20x10x20)
├── DepthZoneTrigger (Script)
└── VFX_Spawn_Point (Empty GameObject)

Location: Assets/_Game/Prefabs/Environment/
```

---

# CONTRAINTES STRICTES

## Performance
- **Target** : 55-60 FPS (2-4 joueurs coop)
- VFX : GPU Instancing enabled
- Shaders : Pas de calculs coûteux (< 50 instructions fragment)
- AI : Max 1 Drifter actif simultanément

## Compatibilité URP
- **Tous shaders** : URP uniquement
- Post-Processing : Volume Profile overrides (pas de scripts custom)
- Lighting : Baked + Mixed (pas de Realtime sauf spots)

## Networking FishNet
- Effets visuels : Client-side (pas de sync)
- AI : Server-side uniquement
- Kill : [ServerRpc] validation

## Nomenclature (À VÉRIFIER dans repo)
- Scripts : PascalCase
- Prefabs : PREFAB_ prefix
- Materials : MAT_ prefix
- Shaders : SG_ prefix (Shader Graph)
- VFX : VFX_ prefix

---

# QUESTIONS À ME POSER AVANT DE COMMENCER

1. **FishNet** : Comment les scripts existants détectent-ils le joueur local ? (Tag, Layer, IsOwner ?)
2. **Naming** : Y a-t-il des conventions spécifiques non mentionnées ?
3. **Packages** : VFX Graph, Shader Graph, Post-Processing installés ?
4. **Structure** : Les chemins donnés existent-ils ?
5. **Clarifications** : Tout ce qui n'est pas clair

---

# VALIDATION FINALE

Une fois terminé, je veux :

1. ✅ 6 Volume Profiles (1 par niveau)
2. ✅ 2 VFX Graphs (particules + bioluminescence)
3. ✅ 2 Shader Graphs (eau + biofilm)
4. ✅ 3 Scripts (DepthZone, HidingSpot, DrifterAI)
5. ✅ 3 Prefabs (Locker, Drifter, DepthZone)
6. ✅ Code propre, commenté, respectant architecture existante

**Esthétique** : 90% SOMA en ambiance sous-marine  
**Performance** : 55-60 FPS coop  
**Temps solo dev** : < 3 heures setup

---

# GO ! 🚀

**Commence par explorer le repo (30 min) et me poser tes questions.**  
Ne crée rien avant d'avoir compris l'architecture existante.

Une fois prêt, crée tous les assets listés ci-dessus.