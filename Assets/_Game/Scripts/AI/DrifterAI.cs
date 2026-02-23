using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using FishNet.Object;
using SubSurface.Gameplay;
using VoidWarranty.Player;

namespace SubSurface.AI
{
    /// <summary>
    /// IA du Drifter — créature lovecraftienne 3 états.
    /// Tourne uniquement server-side. La position est synchronisée
    /// automatiquement par FishNet (NetworkTransform ou NetworkObject).
    /// L'audio 3D suit la position côté client.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class DrifterAI : NetworkBehaviour
    {
        // =====================================================================
        // Enums
        // =====================================================================

        public enum DrifterState { Patrol, Investigate, Chase }

        // =====================================================================
        // Configuration — Patrol
        // =====================================================================

        [Header("Patrol")]
        [SerializeField] private Transform[] _patrolWaypoints;
        [SerializeField] private float _patrolSpeed = 1f;
        [SerializeField] private float _waypointReachThreshold = 0.5f;

        // =====================================================================
        // Configuration — Detection
        // =====================================================================

        [Header("Detection — Vision")]
        [SerializeField] private float _visionRange = 12f;
        [SerializeField] private float _visionAngle = 90f;
        [SerializeField] private LayerMask _playerLayer;
        [SerializeField] private LayerMask _obstructionLayer;

        [Header("Detection — Hearing")]
        [Tooltip("Rayon d'audition max (quand le joueur sprint)")]
        [SerializeField] private float _hearingRadiusMax = 25f;
        [Tooltip("Seuil minimum de bruit pour etre entendu (0-1)")]
        [SerializeField] private float _hearingNoiseThreshold = 0.2f;

        [Header("Detection — Proximity (omnidirectional)")]
        [Tooltip("Rayon de detection rapprochee (le Drifter 'sent' le joueur)")]
        [SerializeField] private float _proximityRadius = 3f;

        // =====================================================================
        // Configuration — Investigate
        // =====================================================================

        [Header("Investigate")]
        [SerializeField] private float _investigateSpeed = 2f;
        [SerializeField] private float _investigateTimeout = 30f;

        // =====================================================================
        // Configuration — Chase
        // =====================================================================

        [Header("Chase")]
        [SerializeField] private float _chaseSpeed = 3f;
        [SerializeField] private float _loseSightDuration = 10f;

        // =====================================================================
        // Configuration — Audio
        // =====================================================================

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _patrolAmbience;
        [SerializeField] private AudioClip _investigateSound;
        [SerializeField] private AudioClip _chaseSound;

        // =====================================================================
        // Configuration — Kill
        // =====================================================================

        [Header("Kill")]
        [SerializeField] private float _killRadius = 1.5f;

        // =====================================================================
        // Configuration — Search (fouille cachettes)
        // =====================================================================

        [Header("Search")]
        [Tooltip("Rayon de detection des hiding spots occupes")]
        [SerializeField] private float _searchRadius = 3f;
        [Tooltip("Duree de la fouille (hold breath window)")]
        [SerializeField] private float _searchDuration = 4f;

        // =====================================================================
        // State
        // =====================================================================

        private static readonly int HiddenLayer = 8;

        private NavMeshAgent _agent;
        private DrifterState _currentState = DrifterState.Patrol;
        private int _currentWaypointIndex;

        // Investigate
        private Vector3 _lastKnownPosition;
        private float _investigateTimer;

        // Chase
        private Transform _chaseTarget;
        private float _loseSightTimer;
        private Vector3 _lastSeenPosition;

        // Search (fouille hiding spots)
        private bool _isSearchingHidingSpot;
        private HidingSpot _searchedHidingSpot;
        private float _searchTimer;
        private HidingSpot _lastSearchedSpot;
        private float _searchCooldownTimer;

        // =====================================================================
        // Detection — Threat System (coop multi-joueurs)
        // =====================================================================
        //
        // Architecture simplifiée et fiable :
        //   - Vision  : détection binaire dans le cône, LOS raycast.
        //               → Chase immédiat si vu. Persistance via _loseSightTimer.
        //   - Ouïe    : jauge de suspicion [0..1] qui monte avec le bruit.
        //               → Investigate quand pleine. Pas de Chase direct.
        //   - Proximité : Chase immédiat si dans _proximityRadius.
        //   - Coop    : _threatScores par joueur pour choisir la CIBLE,
        //               mais la DÉCISION d'état (Patrol/Investigate/Chase)
        //               est basée sur des règles claires, pas le score brut.

        [Header("Detection — Suspicion (Patrol→Investigate)")]
        [Tooltip("Vitesse montée suspicion quand bruit détecté (par seconde)")]
        [SerializeField] private float _suspicionBuildRate = 0.35f;
        [Tooltip("Vitesse décroissance suspicion quand silence (par seconde)")]
        [SerializeField] private float _suspicionDecayRate = 0.1f;

        [Header("Detection — Coop Threat (sélection cible)")]
        [Tooltip("Marge pour changer de cible en Chase — évite le flip-flop")]
        [SerializeField] private float _threatSwitchMargin = 20f;
        [Tooltip("Multiplicateur de decay par frame pour les scores non-détectés (ex: 0.95 = -5%/frame)")]
        [SerializeField] private float _threatDecayRate = 0.95f;

        // Suspicion globale [0..1] (Patrol uniquement, déclenche Investigate)
        private float _suspicionLevel;
        private Transform _suspicionSource; // Joueur le plus bruyant actuellement

        /// <summary>Score de menace par joueur. Server-side uniquement.</summary>
        private readonly Dictionary<NetworkObject, float> _threatScores = new();

        /// <summary>Dernière position vue/entendue par joueur.</summary>
        private readonly Dictionary<NetworkObject, Vector3> _lastSeenPositions = new();

        /// <summary>Cache PlayerMovement par NetworkObject.</summary>
        private readonly Dictionary<NetworkObject, PlayerMovement> _playerMovementCache = new();

        /// <summary>NetworkObject de la cible actuelle.</summary>
        private NetworkObject _chaseTargetNob;

        // =====================================================================
        // Events
        // =====================================================================

        /// <summary>
        /// Déclenché quand le Drifter tue un joueur (server-side).
        /// Le GameObject est le joueur tué.
        /// </summary>
        public static event Action<GameObject> OnPlayerKilled;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = true;
            _agent.autoBraking = false;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            SetState(DrifterState.Patrol);
        }

        private void Update()
        {
            if (!IsServerInitialized) return;

            if (_searchCooldownTimer > 0f)
            {
                _searchCooldownTimer -= Time.deltaTime;
                if (_searchCooldownTimer <= 0f)
                    _lastSearchedSpot = null;
            }

            // Threat score : mise à jour avant le state switch
            UpdateThreatScores();

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

            CheckKillProximity();
        }

        // =====================================================================
        // State Machine
        // =====================================================================

        private void SetState(DrifterState newState)
        {
            if (_currentState == newState) return;

            _currentState = newState;

            switch (newState)
            {
                case DrifterState.Patrol:
                    _agent.speed = _patrolSpeed;
                    _chaseTarget = null;
                    _chaseTargetNob = null;
                    _isSearchingHidingSpot = false;
                    _searchedHidingSpot = null;
                    _agent.isStopped = false;
                    GoToNextWaypoint();
                    break;

                case DrifterState.Investigate:
                    _agent.speed = _investigateSpeed;
                    _investigateTimer = _investigateTimeout;
                    _agent.SetDestination(_lastKnownPosition);
                    break;

                case DrifterState.Chase:
                    _agent.speed = _chaseSpeed;
                    _loseSightTimer = _loseSightDuration;
                    if (_chaseTarget != null)
                        _lastSeenPosition = _chaseTarget.position;
                    break;
            }

            // Audio uniquement via RPC → évite le double-play sur host
            ObserversStateChanged(newState);
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserversStateChanged(DrifterState newState)
        {
            // Les clients peuvent réagir au changement d'état
            // (ex: UI warning, ambience audio locale)
            switch (newState)
            {
                case DrifterState.Patrol:
                    PlayAmbience(_patrolAmbience);
                    break;
                case DrifterState.Investigate:
                    PlayAmbience(_investigateSound);
                    break;
                case DrifterState.Chase:
                    PlayAmbience(_chaseSound);
                    break;
            }
        }

        // =====================================================================
        // Patrol
        // =====================================================================

        private void UpdatePatrol()
        {
            // Priorité 1 — Vision directe → Chase immédiat (pas de jauge)
            Transform seen = DetectVisiblePlayer();
            if (seen != null)
            {
                _suspicionLevel = 0f;
                StartChaseOn(seen);
                return;
            }

            // Priorité 2 — Proximité → Chase immédiat
            Transform sensed = DetectProximityPlayer();
            if (sensed != null)
            {
                _suspicionLevel = 0f;
                StartChaseOn(sensed);
                return;
            }

            // Priorité 3 — Ouïe → jauge de suspicion (Alien Isolation style)
            // La jauge monte proportionnellement au bruit, décroît dans le silence.
            // Quand elle est pleine → Investigate vers la dernière position sonore.
            Transform heard = DetectAudioPlayer();
            if (heard != null)
            {
                PlayerMovement mov = heard.GetComponent<PlayerMovement>();
                float noiseFactor = mov != null ? mov.NoiseLevel : 1f;
                _suspicionLevel += _suspicionBuildRate * noiseFactor * Time.deltaTime;
                _suspicionLevel = Mathf.Clamp01(_suspicionLevel);
                _suspicionSource = heard;
                _lastKnownPosition = heard.position;

                if (_suspicionLevel >= 1f)
                {
                    _suspicionLevel = 0f;
                    _suspicionSource = null;
                    SetState(DrifterState.Investigate);
                    return;
                }
            }
            else
            {
                _suspicionLevel -= _suspicionDecayRate * Time.deltaTime;
                _suspicionLevel = Mathf.Clamp01(_suspicionLevel);
            }

            // Patrouille normale
            if (_patrolWaypoints == null || _patrolWaypoints.Length == 0) return;
            if (!_agent.pathPending && _agent.remainingDistance <= _waypointReachThreshold)
                GoToNextWaypoint();
        }

        private void GoToNextWaypoint()
        {
            if (_patrolWaypoints == null || _patrolWaypoints.Length == 0) return;

            _agent.SetDestination(_patrolWaypoints[_currentWaypointIndex].position);
            _currentWaypointIndex = (_currentWaypointIndex + 1) % _patrolWaypoints.Length;
        }

        // =====================================================================
        // Investigate
        // =====================================================================

        private void UpdateInvestigate()
        {
            // Sous-etat : fouille d'un hiding spot
            if (_isSearchingHidingSpot)
            {
                UpdateSearchHidingSpot();
                return;
            }

            // Priorité 1 — Vision directe → Chase immédiat
            Transform seen = DetectVisiblePlayer();
            if (seen != null)
            {
                StartChaseOn(seen);
                return;
            }

            // Priorité 2 — Proximité → Chase immédiat
            Transform sensed = DetectProximityPlayer();
            if (sensed != null)
            {
                StartChaseOn(sensed);
                return;
            }

            // Priorité 3 — Ouïe → mise à jour destination + reset timer
            Transform heard = DetectAudioPlayer();
            if (heard != null)
            {
                _lastKnownPosition = heard.position;
                _investigateTimer = _investigateTimeout;
                _agent.SetDestination(_lastKnownPosition);
            }

            // Timeout
            _investigateTimer -= Time.deltaTime;
            if (_investigateTimer <= 0f)
            {
                SetState(DrifterState.Patrol);
                return;
            }

            // Arrivée au point → fouille des cachettes
            if (!_agent.pathPending && _agent.remainingDistance <= _waypointReachThreshold)
            {
                HidingSpot spot = FindOccupiedHidingSpotNearby();
                if (spot != null)
                {
                    float distToSpot = Vector3.Distance(transform.position, spot.transform.position);
                    if (distToSpot <= _searchRadius)
                    {
                        StartSearchingHidingSpot(spot);
                        return;
                    }
                    else
                    {
                        _agent.SetDestination(spot.transform.position);
                    }
                }
                else
                {
                    _investigateTimer -= Time.deltaTime * 3f;
                }
            }
        }

        // =====================================================================
        // Chase
        // =====================================================================

        private void UpdateChase()
        {
            // Cible nulle → on tente de récupérer la plus menaçante ou on abandonne
            if (_chaseTarget == null || _chaseTargetNob == null)
            {
                Transform fallback = DetectVisiblePlayer() ?? DetectProximityPlayer();
                if (fallback != null) { StartChaseOn(fallback); return; }
                _lastKnownPosition = _lastSeenPosition;
                SetState(DrifterState.Investigate);
                return;
            }

            // Joueur entré dans un hiding spot → perd la cible, go Investigate
            if (_chaseTarget.gameObject.layer == HiddenLayer)
            {
                _lastKnownPosition = _lastSeenPositions.TryGetValue(_chaseTargetNob, out Vector3 lsp)
                    ? lsp : _lastSeenPosition;
                ClearChaseTarget();
                SetState(DrifterState.Investigate);
                return;
            }

            // ---- Coop : switch de cible si un autre joueur est bien plus menaçant ----
            // On utilise _threatScores pour comparer, mais la décision de Chase
            // reste basée sur vision/proximité, pas sur le score brut.
            // Le switch ne se fait QUE si on voit ou sent directement l'autre joueur.
            Transform betterVisible = DetectVisiblePlayer();
            if (betterVisible != null && betterVisible != _chaseTarget)
            {
                NetworkObject betterNob = betterVisible.GetComponent<NetworkObject>();
                if (betterNob != null && betterNob != _chaseTargetNob)
                {
                    float currentScore = _threatScores.TryGetValue(_chaseTargetNob, out float cs) ? cs : 0f;
                    float betterScore  = _threatScores.TryGetValue(betterNob, out float bs) ? bs : 0f;
                    if (betterScore > currentScore + _threatSwitchMargin)
                    {
                        _lastSeenPositions[_chaseTargetNob] = _lastSeenPosition;
                        StartChaseOn(betterVisible);
                        return;
                    }
                }
            }

            // ---- Vision directe sur la cible ----
            if (CanSeeTarget(_chaseTarget))
            {
                _loseSightTimer = _loseSightDuration;
                _lastSeenPosition = _chaseTarget.position;
                _lastSeenPositions[_chaseTargetNob] = _chaseTarget.position;
                _agent.SetDestination(_chaseTarget.position);
                return;
            }

            // ---- Proximité : maintient la chase même sans LOS ----
            float distToCurrent = Vector3.Distance(transform.position, _chaseTarget.position);
            if (distToCurrent <= _proximityRadius)
            {
                _loseSightTimer = _loseSightDuration;
                _lastSeenPosition = _chaseTarget.position;
                _lastSeenPositions[_chaseTargetNob] = _chaseTarget.position;
                _agent.SetDestination(_chaseTarget.position);
                return;
            }

            // ---- Perte de vue : timer + redirect audio ----
            _loseSightTimer -= Time.deltaTime;
            _agent.SetDestination(_lastSeenPosition);

            // L'ouïe prolonge la chase si le joueur fait encore du bruit
            Transform heard = DetectAudioPlayer();
            if (heard != null && heard == _chaseTarget)
            {
                _loseSightTimer = Mathf.Max(_loseSightTimer, _loseSightDuration * 0.5f);
                _lastSeenPosition = heard.position;
            }

            if (_loseSightTimer <= 0f)
            {
                _lastKnownPosition = _lastSeenPosition;
                ClearChaseTarget();
                SetState(DrifterState.Investigate);
            }
        }

        /// <summary>Démarre une chase sur un Transform — résout le NetworkObject associé.</summary>
        private void StartChaseOn(Transform target)
        {
            _chaseTarget = target;
            _chaseTargetNob = target.GetComponent<NetworkObject>();
            _lastSeenPosition = target.position;
            if (_chaseTargetNob != null)
                _lastSeenPositions[_chaseTargetNob] = target.position;
            SetState(DrifterState.Chase);
        }

        private void ClearChaseTarget()
        {
            _chaseTarget = null;
            _chaseTargetNob = null;
        }

        // =====================================================================
        // Threat Score — Core Methods
        // =====================================================================

        /// <summary>
        /// Met à jour le threat score de chaque joueur détecté.
        /// Un seul OverlapSphere (hearingRadiusMax = le plus grand rayon) :
        /// on check vision, audition et proximité dans le même pass.
        /// Appelée chaque frame côté serveur, avant le state switch.
        /// </summary>
        private void UpdateThreatScores()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _hearingRadiusMax, _playerLayer);

            // Track qui a été détecté ce frame (pour le decay des autres)
            HashSet<NetworkObject> sensedThisFrame = new();

            foreach (var hit in hits)
            {
                if (hit.gameObject.layer == HiddenLayer) continue;

                NetworkObject nob = hit.GetComponent<NetworkObject>();
                if (nob == null) continue;

                // Cache PlayerMovement (évite GetComponent chaque frame)
                if (!_playerMovementCache.TryGetValue(nob, out PlayerMovement movement))
                {
                    movement = hit.GetComponent<PlayerMovement>();
                    if (movement == null) continue;
                    _playerMovementCache[nob] = movement;
                }

                if (!_threatScores.ContainsKey(nob))
                    _threatScores[nob] = 0f;

                float addedThreat = 0f;
                Transform target = hit.transform;
                float dist = Vector3.Distance(transform.position, target.position);

                // --- Vision check ---
                if (dist <= _visionRange)
                {
                    Vector3 dirToTarget = (target.position - transform.position).normalized;
                    float angle = Vector3.Angle(transform.forward, dirToTarget);
                    if (angle <= _visionAngle * 0.5f && CanSeeTarget(target))
                    {
                        addedThreat += 10f;
                        _lastSeenPositions[nob] = target.position;
                    }
                }

                // --- Hearing check ---
                float noise = movement.NoiseLevel;
                if (noise >= _hearingNoiseThreshold)
                {
                    float effectiveRadius = _hearingRadiusMax * noise;
                    if (dist <= effectiveRadius)
                    {
                        addedThreat += (noise * 5f) / Mathf.Max(dist, 0.1f);
                        // Audition met à jour la position (toujours, le son donne une position)
                        _lastSeenPositions[nob] = target.position;
                    }
                }

                // --- Proximity check ---
                if (dist <= _proximityRadius)
                {
                    addedThreat += 15f;
                    _lastSeenPositions[nob] = target.position;
                }

                if (addedThreat > 0f)
                {
                    _threatScores[nob] += addedThreat;
                    sensedThisFrame.Add(nob);
                }
            }

            // Decay des joueurs non-détectés ce frame.
            // On collecte les updates AVANT de modifier le dictionnaire
            // pour éviter l'InvalidOperationException (collection modified during enumeration).
            List<NetworkObject> toRemove = null;
            List<(NetworkObject nob, float value)> toUpdate = null;

            foreach (var kvp in _threatScores)
            {
                if (sensedThisFrame.Contains(kvp.Key)) continue;

                float decayed = kvp.Value * _threatDecayRate;
                if (decayed < 0.1f)
                {
                    toRemove ??= new List<NetworkObject>();
                    toRemove.Add(kvp.Key);
                }
                else
                {
                    toUpdate ??= new List<(NetworkObject, float)>();
                    toUpdate.Add((kvp.Key, decayed));
                }
            }

            // Appliquer les updates APRÈS l'itération — jamais modifier un dict pendant foreach
            if (toUpdate != null)
                foreach (var (nob, val) in toUpdate)
                    _threatScores[nob] = val;

            if (toRemove != null)
                foreach (var nob in toRemove)
                {
                    _threatScores.Remove(nob);
                    _playerMovementCache.Remove(nob);
                    // On garde _lastSeenPositions pour investigate
                }

            CleanupDestroyedPlayers();
        }

        /// <summary>
        /// Purge les NetworkObject détruits/déconnectés de tous les dictionnaires.
        /// </summary>
        private void CleanupDestroyedPlayers()
        {
            // Collecte dans une liste séparée — ne jamais modifier Keys pendant l'itération
            List<NetworkObject> dead = null;
            foreach (var nob in _threatScores.Keys)
            {
                if (nob == null || nob.gameObject == null)
                {
                    dead ??= new List<NetworkObject>();
                    dead.Add(nob);
                }
            }
            if (dead == null) return;

            foreach (var nob in dead)
            {
                _threatScores.Remove(nob);
                _lastSeenPositions.Remove(nob);
                _playerMovementCache.Remove(nob);
                if (_chaseTargetNob == nob)
                {
                    _chaseTargetNob = null;
                    _chaseTarget = null;
                }
            }
        }


        // =====================================================================
        // Detection — méthodes propres utilisées par la state machine
        // =====================================================================

        /// <summary>
        /// Vision : cône + LOS raycast. Retourne le joueur le plus proche dans le cône.
        /// Joueurs accroupis : cône réduit de 50%.
        /// Ignore le layer Hidden.
        /// </summary>
        private Transform DetectVisiblePlayer()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _visionRange, _playerLayer);
            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.gameObject.layer == HiddenLayer) continue;

                Transform t = hit.transform;
                Vector3 dir = (t.position - transform.position).normalized;
                float dist = Vector3.Distance(transform.position, t.position);

                // Cône réduit de 50% si le joueur est en crouch-walk
                float effectiveAngle = _visionAngle;
                PlayerMovement mov = hit.GetComponent<PlayerMovement>();
                if (mov != null && mov.NoiseLevel > 0f && mov.NoiseLevel <= 0.3f)
                    effectiveAngle *= 0.5f;

                if (Vector3.Angle(transform.forward, dir) > effectiveAngle * 0.5f) continue;
                if (!CanSeeTarget(t)) continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = t;
                }
            }

            return best;
        }

        /// <summary>
        /// Ouïe : retourne le joueur le plus bruyant dans le rayon effectif.
        /// Rayon effectif = hearingRadiusMax * NoiseLevel du joueur.
        /// Ignore le layer Hidden.
        /// </summary>
        private Transform DetectAudioPlayer()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _hearingRadiusMax, _playerLayer);
            Transform loudest = null;
            float loudestScore = 0f;

            foreach (var hit in hits)
            {
                if (hit.gameObject.layer == HiddenLayer) continue;

                PlayerMovement mov = hit.GetComponent<PlayerMovement>();
                if (mov == null) continue;

                float noise = mov.NoiseLevel;
                if (noise < _hearingNoiseThreshold) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist > _hearingRadiusMax * noise) continue;

                float score = noise / Mathf.Max(dist, 0.1f);
                if (score > loudestScore)
                {
                    loudestScore = score;
                    loudest = hit.transform;
                }
            }

            return loudest;
        }

        /// <summary>
        /// Proximité omnidirectionnelle : le Drifter "sent" le joueur.
        /// Chase immédiat, pas de jauge. Retourne le joueur le plus proche.
        /// Ignore le layer Hidden.
        /// </summary>
        private Transform DetectProximityPlayer()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _proximityRadius, _playerLayer);
            Transform closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.gameObject.layer == HiddenLayer) continue;
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < closestDist) { closestDist = d; closest = hit.transform; }
            }

            return closest;
        }

        private bool CanSeeTarget(Transform target)
        {
            if (target == null) return false;

            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 targetPos = target.position + Vector3.up * 1f;
            Vector3 dir = targetPos - origin;

            if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, _visionRange, _obstructionLayer))
            {
                return hit.transform == target || hit.transform.IsChildOf(target);
            }

            return dir.magnitude <= _visionRange;
        }

        // =====================================================================
        // Search Hiding Spots
        // =====================================================================

        private HidingSpot FindOccupiedHidingSpotNearby()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _searchRadius);

            foreach (var hit in hits)
            {
                HidingSpot spot = hit.GetComponent<HidingSpot>();
                if (spot == null || !spot.IsOccupied) continue;
                if (spot == _lastSearchedSpot) continue; // Cooldown : pas re-fouiller

                return spot;
            }

            return null;
        }

        private void StartSearchingHidingSpot(HidingSpot spot)
        {
            _isSearchingHidingSpot = true;
            _searchedHidingSpot = spot;
            _searchTimer = _searchDuration;
            _agent.isStopped = true;

            // Face the hiding spot
            Vector3 dir = spot.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);

            spot.NotifySearchStarted();
            ObserversNotifySearching(true);

            Debug.Log($"[DrifterAI] Debut fouille de {spot.gameObject.name}");
        }

        private void UpdateSearchHidingSpot()
        {
            if (_searchedHidingSpot == null || !_searchedHidingSpot.IsOccupied)
            {
                EndSearch();
                return;
            }

            _searchTimer -= Time.deltaTime;

            if (_searchTimer <= 0f)
            {
                if (!_searchedHidingSpot.PlayerHeldBreath())
                {
                    // Player failed hold breath — pull out and kill
                    GameObject occupant = _searchedHidingSpot.ForceExitAndGetOccupant();
                    _searchedHidingSpot.NotifySearchEnded();
                    EndSearch();

                    if (occupant != null)
                    {
                        Debug.Log($"[DrifterAI] Joueur sorti de force et tue !");
                        KillPlayer(occupant);
                    }
                }
                else
                {
                    // Player survived — Drifter gives up on this spot
                    Debug.Log($"[DrifterAI] Joueur a retenu son souffle, Drifter repart");
                    _searchedHidingSpot.NotifySearchEnded();
                    _lastSearchedSpot = _searchedHidingSpot;
                    _searchCooldownTimer = _investigateTimeout; // Ne re-fouille pas avant le timeout
                    EndSearch();
                    SetState(DrifterState.Patrol);
                }
            }
        }

        private void EndSearch()
        {
            _isSearchingHidingSpot = false;
            _searchedHidingSpot = null;
            _agent.isStopped = false;
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserversNotifySearching(bool isSearching)
        {
            // Les clients peuvent reagir (ambiance tendue, UI)
            if (isSearching)
                Debug.Log("[DrifterAI] Le Drifter fouille une cachette...");
        }

        // =====================================================================
        // Kill
        // =====================================================================

        private void CheckKillProximity()
        {
            if (_chaseTarget == null) return;

            // Ne pas tuer un joueur caché (layer Hidden)
            if (_chaseTarget.gameObject.layer == HiddenLayer) return;

            float dist = Vector3.Distance(transform.position, _chaseTarget.position);
            if (dist <= _killRadius)
            {
                KillPlayer(_chaseTarget.gameObject);
            }
        }

        private void KillPlayer(GameObject player)
        {
            Debug.Log($"[DrifterAI] Joueur tué : {player.name}");

            OnPlayerKilled?.Invoke(player);

            // Notifier le client pour l'écran noir
            ObserversNotifyPlayerKilled(player);

            // Nettoyer le joueur tué des dictionnaires de threat
            NetworkObject nob = player.GetComponent<NetworkObject>();
            if (nob != null)
            {
                _threatScores.Remove(nob);
                _lastSeenPositions.Remove(nob);
                _playerMovementCache.Remove(nob);
            }

            // Reset to patrol
            _chaseTarget = null;
            _chaseTargetNob = null;
            SetState(DrifterState.Patrol);
        }

        [ObserversRpc]
        private void ObserversNotifyPlayerKilled(GameObject player)
        {
            if (player == null) return;

            // Chaque client vérifie si c'est son joueur
            var nob = player.GetComponent<NetworkObject>();
            if (nob != null && nob.IsOwner)
            {
                Debug.Log("[DrifterAI] Vous avez été tué par le Drifter !");
                // TODO: Écran noir, disable player, système de mort à définir
            }
        }

        // =====================================================================
        // Audio
        // =====================================================================

        private void PlayAmbience(AudioClip clip)
        {
            if (_audioSource == null || clip == null) return;

            if (_audioSource.clip == clip && _audioSource.isPlaying) return;

            _audioSource.clip = clip;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>État actuel du Drifter.</summary>
        public DrifterState CurrentState => _currentState;

        /// <summary>
        /// Force le Drifter en état Chase sur une cible.
        /// Utilisé par les events scriptés (ex: Event 2, apparition Niv 4).
        /// </summary>
        [Server]
        public void ForceChase(Transform target)
        {
            NetworkObject nob = target.GetComponent<NetworkObject>();
            if (nob != null)
            {
                _threatScores[nob] = 100f; // Force high threat
                _chaseTargetNob = nob;
                _lastSeenPositions[nob] = target.position;
            }
            _chaseTarget = target;
            SetState(DrifterState.Chase);
        }

        /// <summary>
        /// Force le Drifter en état Patrol.
        /// </summary>
        [Server]
        public void ForcePatrol()
        {
            SetState(DrifterState.Patrol);
        }

        /// <summary>
        /// Force le Drifter à investiguer une position dans le monde.
        /// Utilisé pour les distractions (futur : jets d'objets).
        /// Ne perturbe pas un Chase en cours (le chase est prioritaire).
        /// </summary>
        [Server]
        public void InvestigatePosition(Vector3 position)
        {
            _lastKnownPosition = position;
            if (_currentState == DrifterState.Patrol)
            {
                SetState(DrifterState.Investigate);
            }
            else if (_currentState == DrifterState.Investigate)
            {
                _investigateTimer = _investigateTimeout;
                _agent.SetDestination(position);
            }
            // Si en Chase, on n'interrompt pas — le chase est prioritaire
        }

        // =====================================================================
        // Gizmos
        // =====================================================================

        private void OnDrawGizmosSelected()
        {
            // Vision cone
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _visionRange);

            // Hearing radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _hearingRadiusMax);

            // Proximity radius
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _proximityRadius);

            // Kill radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _killRadius);

            // Search radius
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, _searchRadius);

            // Vision angle
            Vector3 leftBound = Quaternion.Euler(0, -_visionAngle * 0.5f, 0) * transform.forward * _visionRange;
            Vector3 rightBound = Quaternion.Euler(0, _visionAngle * 0.5f, 0) * transform.forward * _visionRange;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, leftBound);
            Gizmos.DrawRay(transform.position, rightBound);
        }
    }
}
