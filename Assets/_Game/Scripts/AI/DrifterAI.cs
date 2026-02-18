using System;
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
            // Proximité → Chase immédiat (le Drifter "sent" le joueur)
            Transform close = SenseProximity();
            if (close != null)
            {
                _chaseTarget = close;
                SetState(DrifterState.Chase);
                return;
            }

            // Vision → Chase
            Transform detected = DetectPlayer();
            if (detected != null)
            {
                _chaseTarget = detected;
                SetState(DrifterState.Chase);
                return;
            }

            // Audition → Investigate
            Transform heard = HearPlayer();
            if (heard != null)
            {
                _lastKnownPosition = heard.position;
                SetState(DrifterState.Investigate);
                return;
            }

            // Move to waypoints
            if (_patrolWaypoints == null || _patrolWaypoints.Length == 0) return;

            if (!_agent.pathPending && _agent.remainingDistance <= _waypointReachThreshold)
            {
                GoToNextWaypoint();
            }
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

            // Proximité → Chase immédiat
            Transform close = SenseProximity();
            if (close != null)
            {
                _chaseTarget = close;
                SetState(DrifterState.Chase);
                return;
            }

            // Vision → Chase
            Transform detected = DetectPlayer();
            if (detected != null)
            {
                _chaseTarget = detected;
                SetState(DrifterState.Chase);
                return;
            }

            // Audition → redirige l'investigation
            Transform heard = HearPlayer();
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

            // Reached investigation point — search for occupied hiding spots nearby
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
                    _investigateTimer -= Time.deltaTime * 3f; // Drain faster when at position
                }
            }
        }

        // =====================================================================
        // Chase
        // =====================================================================

        private void UpdateChase()
        {
            if (_chaseTarget == null)
            {
                SetState(DrifterState.Investigate);
                return;
            }

            // Si le joueur est cache, on perd immediatement la cible
            // mais on utilise la derniere position VUE, pas la position actuelle
            if (_chaseTarget.gameObject.layer == HiddenLayer)
            {
                _lastKnownPosition = _lastSeenPosition;
                _chaseTarget = null;
                SetState(DrifterState.Investigate);
                return;
            }

            // Can we still see the target?
            if (CanSeeTarget(_chaseTarget))
            {
                _loseSightTimer = _loseSightDuration;
                _lastSeenPosition = _chaseTarget.position;
                _agent.SetDestination(_chaseTarget.position);
            }
            else
            {
                // Proximité maintient la chase même sans vision
                Transform close = SenseProximity();
                if (close != null && close == _chaseTarget)
                {
                    _loseSightTimer = _loseSightDuration;
                    _lastSeenPosition = _chaseTarget.position;
                    _agent.SetDestination(_chaseTarget.position);
                    return;
                }

                _loseSightTimer -= Time.deltaTime;

                // Aller vers la derniere position VUE (pas la live position)
                _agent.SetDestination(_lastSeenPosition);

                // Audition pendant la perte de vue : permet de rediriger
                Transform heard = HearPlayer();
                if (heard != null)
                {
                    _lastSeenPosition = heard.position;
                    _loseSightTimer = _loseSightDuration * 0.5f;
                }

                if (_loseSightTimer <= 0f)
                {
                    _lastKnownPosition = _lastSeenPosition;
                    _chaseTarget = null;
                    SetState(DrifterState.Investigate);
                }
            }
        }

        // =====================================================================
        // Detection
        // =====================================================================

        /// <summary>
        /// Detection visuelle : cone de vision + raycast line-of-sight.
        /// Ignore les joueurs sur le layer Hidden (caches).
        /// </summary>
        private Transform DetectPlayer()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _visionRange, _playerLayer);

            foreach (var hit in hits)
            {
                if (hit.gameObject.layer == HiddenLayer) continue;

                Transform target = hit.transform;
                Vector3 dirToTarget = (target.position - transform.position).normalized;

                float angle = Vector3.Angle(transform.forward, dirToTarget);
                if (angle > _visionAngle * 0.5f) continue;

                if (CanSeeTarget(target))
                    return target;
            }

            return null;
        }

        /// <summary>
        /// Detection sonore basee sur le NoiseLevel du joueur.
        /// Le rayon effectif = _hearingRadiusMax * noiseLevel.
        /// Sprint = entendu de loin, crouch = presque inaudible, immobile = silence.
        /// Les joueurs caches ne sont PAS detectes (sauf futur systeme talkie-walkie).
        /// </summary>
        private Transform HearPlayer()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _hearingRadiusMax, _playerLayer);

            Transform loudest = null;
            float loudestScore = 0f;

            foreach (var hit in hits)
            {
                if (hit.gameObject.layer == HiddenLayer) continue;

                PlayerMovement movement = hit.GetComponent<PlayerMovement>();
                if (movement == null) continue;

                float noise = movement.NoiseLevel;
                if (noise < _hearingNoiseThreshold) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                float effectiveRadius = _hearingRadiusMax * noise;

                // Hors du rayon effectif pour ce niveau de bruit
                if (dist > effectiveRadius) continue;

                // Score : plus bruyant et plus proche = prioritaire
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
        /// Detection de proximite : omnidirectionnel, courte portee.
        /// Le Drifter "sent" le joueur meme sans le voir ni l'entendre.
        /// Ignore les joueurs caches dans un hiding spot (layer Hidden).
        /// </summary>
        private Transform SenseProximity()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _proximityRadius, _playerLayer);

            foreach (var hit in hits)
            {
                if (hit.gameObject.layer == HiddenLayer) continue;
                return hit.transform;
            }

            return null;
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

            // Reset to patrol
            _chaseTarget = null;
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
