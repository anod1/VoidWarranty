using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using System.Collections.Generic;

namespace SubSurface.Level
{
    /// <summary>
    /// Mode d'intensité du camera shake pendant la descente.
    /// </summary>
    public enum ShakeMode
    {
        /// <summary>Intensité constante pendant toute la descente.</summary>
        Constant,
        /// <summary>Intensité modulée automatiquement par la vitesse de la courbe de descente.</summary>
        Adaptive,
        /// <summary>Intensité scriptée via une AnimationCurve dédiée (temps normalisé 0-1).</summary>
        Manual
    }

    /// <summary>
    /// Orchestrateur de l'ascenseur — approche statique.
    /// La cabine ne bouge PAS : les portes se ferment, on simule la descente
    /// (audio, camera shake, depth display), puis on swap les niveaux.
    ///
    /// SETUP ÉDITEUR :
    /// → GO "Elevator_Cabin" + NetworkObject + BoxCollider (isTrigger, layer 7 detect)
    /// → Le sol de la cabine DOIT avoir un collider solide (pas trigger)
    /// → Inspector : refs ElevatorDoor gauche/droite, Light statut
    /// → Inspector : _upperLevelRoot / _lowerLevelRoot (construits au même world pos)
    /// → Inspector : DepthDisplay ref (optionnel, world-space TextMeshPro dans la cabine)
    /// → Inspector : _startDepth / _endDepth (mètres, ex: 500 → 1000)
    /// </summary>
    public class ElevatorController : NetworkBehaviour
    {
        // States
        private const int STATE_IDLE = 0;
        private const int STATE_DOORS_OPEN = 1;
        private const int STATE_DESCENDING = 2;
        private const int STATE_ARRIVED = 3;

        public static event System.Action OnLevelComplete;

        [Header("Doors")]
        [SerializeField] private ElevatorDoor _doorLeft;
        [SerializeField] private ElevatorDoor _doorRight;

        [Header("Status Light")]
        [SerializeField] private Light _statusLight;
        [SerializeField] private Color _offColor = Color.black;
        [SerializeField] private Color _electricityColor = Color.red;
        [SerializeField] private Color _readyColor = Color.green;

        [Header("Depth")]
        [SerializeField] private float _startDepth = 500f;
        [SerializeField] private float _endDepth = 1000f;
        [SerializeField] private DepthDisplay _depthDisplay;
        [Tooltip("Courbe de progression (ease-in → croisière → ease-out)")]
        [SerializeField] private AnimationCurve _descentCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Level Swap")]
        [SerializeField] private GameObject _upperLevelRoot;
        [SerializeField] private GameObject _lowerLevelRoot;
        [Tooltip("Pourcentage de descente quand on swap les niveaux (0-1)")]
        [SerializeField] [Range(0f, 1f)] private float _swapAtProgress = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip _descentStartClip;
        [SerializeField] private AudioClip _descentLoopClip;
        [SerializeField] private AudioClip _descentEndClip;

        [Header("Timing")]
        [SerializeField] private float _descentDuration = 30f;
        [SerializeField] private float _doorCloseDelay = 0.5f;
        [SerializeField] private float _descentStartDelay = 1.5f;
        [SerializeField] private float _arrivalDoorDelay = 1.0f;

        [Header("Camera Shake")]
        [SerializeField] private float _shakeIntensity = 0.015f;
        [SerializeField] private float _shakeFrequency = 12f;
        [SerializeField] private ShakeMode _shakeMode = ShakeMode.Adaptive;
        [Tooltip("Courbe d'intensité du shake (mode Manual uniquement, temps 0-1, valeur 0-1)")]
        [SerializeField] private AnimationCurve _shakeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // SyncVars
        private readonly SyncVar<int> _elevatorState = new();
        private readonly SyncVar<bool> _isUnlocked = new();
        private readonly SyncVar<int> _playersInZone = new();

        // Server tracking
        private readonly HashSet<GameObject> _trackedPlayers = new();

        // Client-side shake
        private Coroutine _shakeCoroutine;
        private Transform _cameraTransform;
        private Vector3 _lastShakeOffset;
        private float _shakeSpeedFactor;
        private float _descentProgress;

        // Audio
        private AudioSource _audioSource;
        private AudioSource _loopSource;

        // =====================================================================
        // Public accessors (pour les boutons)
        // =====================================================================

        public bool IsUnlocked => _isUnlocked.Value;
        public int PlayersInZone => _playersInZone.Value;
        public bool IsDescending => _elevatorState.Value == STATE_DESCENDING;
        public bool DoorsOpen => _elevatorState.Value == STATE_DOORS_OPEN;
        public bool HasArrived => _elevatorState.Value == STATE_ARRIVED;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            _audioSource = GetComponentInChildren<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f;
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
                _audioSource.maxDistance = 25f;
            }

            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.spatialBlend = 1f;
            _loopSource.rolloffMode = AudioRolloffMode.Linear;
            _loopSource.maxDistance = 25f;
            _loopSource.loop = true;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _elevatorState.OnChange += OnElevatorStateChanged;
            _isUnlocked.OnChange += OnUnlockedChanged;
            _playersInZone.OnChange += OnPlayersChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyStatusLight(0);

            if (_lowerLevelRoot != null)
                _lowerLevelRoot.SetActive(false);

            if (_depthDisplay != null)
                _depthDisplay.SetDepth(_startDepth);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _elevatorState.Value = STATE_IDLE;
            _isUnlocked.Value = false;
            _playersInZone.Value = 0;

            if (_lowerLevelRoot != null)
                _lowerLevelRoot.SetActive(false);
        }

        // =====================================================================
        // SyncVar change callbacks
        // =====================================================================

        private void OnElevatorStateChanged(int prev, int next, bool asServer)
        {
            if (asServer) return;
        }

        private void OnUnlockedChanged(bool prev, bool next, bool asServer)
        {
            if (asServer) return;
        }

        private void OnPlayersChanged(int prev, int next, bool asServer)
        {
            if (asServer) return;
        }

        // =====================================================================
        // API Server — appelée par AnnexActivation
        // =====================================================================

        [Server]
        public void Unlock()
        {
            _isUnlocked.Value = true;
            Debug.Log("[ElevatorController] Ascenseur déverrouillé.");
        }

        [Server]
        public void NotifyElectricityActive()
        {
            ObserversUpdateStatusLight(1);
        }

        [Server]
        public void NotifyAllSystemsActive()
        {
            ObserversUpdateStatusLight(2);
        }

        // =====================================================================
        // RPCs — Boutons
        // =====================================================================

        [ServerRpc(RequireOwnership = false)]
        public void CmdRequestOpenDoors()
        {
            if (!_isUnlocked.Value) return;
            if (_elevatorState.Value != STATE_IDLE) return;

            _doorLeft?.Open();
            _doorRight?.Open();
            _elevatorState.Value = STATE_DOORS_OPEN;
            Debug.Log("[ElevatorController] Portes ouvertes.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdRequestDescent()
        {
            if (!_isUnlocked.Value) return;
            if (_elevatorState.Value != STATE_DOORS_OPEN) return;
            if (_playersInZone.Value < 2) return;

            StartCoroutine(DescentSequence());
        }

        // =====================================================================
        // Trigger tracking
        // =====================================================================

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerInitialized) return;
            if (_elevatorState.Value == STATE_DESCENDING || _elevatorState.Value == STATE_ARRIVED) return;

            if (other.gameObject.layer == 7)
            {
                if (_trackedPlayers.Add(other.gameObject))
                    _playersInZone.Value = _trackedPlayers.Count;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!base.IsServerInitialized) return;
            if (_elevatorState.Value == STATE_DESCENDING) return;

            if (_trackedPlayers.Remove(other.gameObject))
                _playersInZone.Value = _trackedPlayers.Count;
        }

        // =====================================================================
        // Descent sequence (server)
        // =====================================================================

        [Server]
        private IEnumerator DescentSequence()
        {
            Debug.Log("[ElevatorController] Séquence de descente lancée.");

            // 1. Delay avant fermeture
            yield return new WaitForSeconds(_doorCloseDelay);

            // 2. Fermer les portes
            _doorLeft?.Close();
            _doorRight?.Close();

            // 3. État = descending
            _elevatorState.Value = STATE_DESCENDING;

            // 4. Notifier clients : audio + shake
            ObserversDescentStarted();

            // 5. Attendre que les portes se ferment
            yield return new WaitForSeconds(_descentStartDelay);

            // 6. Simuler la descente (timer serveur)
            float elapsed = 0f;
            bool swapped = false;
            float prevCurveT = 0f;

            while (elapsed < _descentDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _descentDuration);

                // Courbe : ease-in (accélération) → croisière → ease-out (freinage)
                float curveT = _descentCurve.Evaluate(t);
                float depth = Mathf.Lerp(_startDepth, _endDepth, curveT);

                // Vitesse normalisée (dérivée de la courbe) pour moduler le shake
                // Une courbe linéaire donne speed=1, ease-in/out donne 0→~1.5→0
                float dt = Time.deltaTime / _descentDuration;
                float speed = dt > 0f ? (curveT - prevCurveT) / dt : 0f;
                prevCurveT = curveT;

                ObserversUpdateDescentState(depth, speed, t);

                // Level swap au bon moment (portes fermées, personne ne voit)
                if (!swapped && t >= _swapAtProgress)
                {
                    swapped = true;
                    ObserversLevelSwap();
                }

                yield return null;
            }

            // 7. Profondeur finale (speed 0 = arrêt)
            ObserversUpdateDescentState(_endDepth, 0f, 1f);

            // 8. Pause avant ouverture
            yield return new WaitForSeconds(_arrivalDoorDelay);

            // 9. Ouvrir les portes
            _doorLeft?.Open();
            _doorRight?.Open();

            // 10. État = arrivé
            _elevatorState.Value = STATE_ARRIVED;
            ObserversDescentComplete();

            Debug.Log("[ElevatorController] Descente terminée → OnLevelComplete.");
        }

        // =====================================================================
        // ObserversRpc — Status light
        // =====================================================================

        [ObserversRpc(BufferLast = true)]
        private void ObserversUpdateStatusLight(int level)
        {
            ApplyStatusLight(level);
        }

        private void ApplyStatusLight(int level)
        {
            if (_statusLight == null) return;

            switch (level)
            {
                case 0:
                    _statusLight.color = _offColor;
                    _statusLight.intensity = 0f;
                    break;
                case 1:
                    _statusLight.color = _electricityColor;
                    _statusLight.intensity = 1f;
                    break;
                case 2:
                    _statusLight.color = _readyColor;
                    _statusLight.intensity = 1f;
                    break;
            }
        }

        // =====================================================================
        // ObserversRpc — Descent simulation
        // =====================================================================

        [ObserversRpc]
        private void ObserversDescentStarted()
        {
            // Audio
            if (_audioSource != null && _descentStartClip != null)
                _audioSource.PlayOneShot(_descentStartClip);

            if (_loopSource != null && _descentLoopClip != null)
            {
                _loopSource.clip = _descentLoopClip;
                _loopSource.Play();
            }

            // Camera shake
            StartCameraShake();
        }

        [ObserversRpc]
        private void ObserversUpdateDescentState(float depth, float speedFactor, float progress)
        {
            if (_depthDisplay != null)
                _depthDisplay.SetDepth(depth);

            _shakeSpeedFactor = speedFactor;
            _descentProgress = progress;
        }

        [ObserversRpc]
        private void ObserversLevelSwap()
        {
            if (_upperLevelRoot != null)
                _upperLevelRoot.SetActive(false);

            if (_lowerLevelRoot != null)
                _lowerLevelRoot.SetActive(true);
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserversDescentComplete()
        {
            // Stop shake
            StopCameraShake();

            // Stop loop audio
            if (_loopSource != null)
                _loopSource.Stop();

            // Audio arrivée
            if (_audioSource != null && _descentEndClip != null)
                _audioSource.PlayOneShot(_descentEndClip);

            OnLevelComplete?.Invoke();
        }

        // =====================================================================
        // Camera shake (client-side)
        // =====================================================================

        private void StartCameraShake()
        {
            if (_shakeIntensity <= 0f) return;

            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;

            if (_cameraTransform == null) return;

            if (_shakeCoroutine != null)
                StopCoroutine(_shakeCoroutine);

            _shakeCoroutine = StartCoroutine(ShakeRoutine());
        }

        private void StopCameraShake()
        {
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = null;
            }

            // Retirer le dernier offset appliqué
            if (_cameraTransform != null)
            {
                _cameraTransform.localPosition -= _lastShakeOffset;
                _lastShakeOffset = Vector3.zero;
            }
        }

        private float EvaluateShakeMultiplier()
        {
            return _shakeMode switch
            {
                ShakeMode.Constant => 1f,
                ShakeMode.Adaptive => Mathf.Clamp01(_shakeSpeedFactor),
                ShakeMode.Manual   => _shakeCurve.Evaluate(_descentProgress),
                _ => 1f
            };
        }

        private IEnumerator ShakeRoutine()
        {
            float time = 0f;
            _lastShakeOffset = Vector3.zero;

            while (true)
            {
                time += Time.deltaTime * _shakeFrequency;

                // Retirer l'offset précédent
                _cameraTransform.localPosition -= _lastShakeOffset;

                // Intensité selon le mode choisi
                float intensity = _shakeIntensity * EvaluateShakeMultiplier();

                // Calculer le nouvel offset
                float offsetX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f * intensity;
                float offsetY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f * intensity;
                _lastShakeOffset = new Vector3(offsetX, offsetY, 0f);

                // Appliquer le nouvel offset
                _cameraTransform.localPosition += _lastShakeOffset;

                yield return null;
            }
        }
    }
}
