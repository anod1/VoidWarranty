using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;

namespace VoidWarranty.Interaction
{
    public enum DoorState { Locked, Closed, Open }

    /// <summary>
    /// Porte générique avec états Locked/Closed/Open.
    /// Animation par lerp de position (slide). Synchro réseau via SyncVar.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur un GO parent vide (ex: "Door_CommandExit")
    /// → NetworkObject sur ce GO
    /// → Inspector : glisser le Transform du mesh porte dans _doorTransform
    /// → Inspector : définir _openOffset (ex: Vector3(2,0,0) pour slide latéral)
    /// → Inspector : _initialState (Locked ou Closed)
    /// → BoxCollider sur le mesh porte (non-trigger, bloquant)
    /// </summary>
    public class LevelDoor : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _doorTransform;
        [SerializeField] private Collider _doorCollider;

        [Header("Settings")]
        [SerializeField] private Vector3 _openOffset = new Vector3(2f, 0f, 0f);
        [SerializeField] private float _animDuration = 0.8f;
        [SerializeField] private DoorState _initialState = DoorState.Locked;

        [Header("Audio")]
        [SerializeField] private AudioClip _openClip;
        [SerializeField] private AudioClip _closeClip;
        [SerializeField] private AudioClip _lockedClip;

        private readonly SyncVar<int> _state = new();
        private AudioSource _audioSource;
        private Vector3 _closedPosition;
        private Vector3 _openPosition;
        private Coroutine _animCoroutine;

        public DoorState CurrentState => (DoorState)_state.Value;

        private void Awake()
        {
            _audioSource = GetComponentInChildren<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f;
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
                _audioSource.maxDistance = 15f;
            }

            if (_doorTransform != null)
            {
                _closedPosition = _doorTransform.localPosition;
                _openPosition = _closedPosition + _openOffset;
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _state.OnChange += OnStateChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _state.Value = (int)_initialState;
            ApplyVisualState(_initialState, false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Appliquer immédiatement l'état visuel pour les late-joiners
            ApplyVisualState((DoorState)_state.Value, false);
        }

        private void OnStateChanged(int prev, int next, bool asServer)
        {
            // Sur host : le callback fire 2× (asServer + client).
            // On skip le côté server du host pour éviter un double-anim.
            // Sur client pur : asServer est toujours false → toujours appliqué.
            // Sur serveur dédié (pas de client) : on applique côté server.
            if (asServer && base.IsClientInitialized) return;

            ApplyVisualState((DoorState)next, true);
        }

        private void ApplyVisualState(DoorState state, bool animate)
        {
            if (_doorTransform == null) return;

            Vector3 targetPos = state == DoorState.Open ? _openPosition : _closedPosition;

            if (_animCoroutine != null)
                StopCoroutine(_animCoroutine);

            if (animate && _animDuration > 0f)
                _animCoroutine = StartCoroutine(AnimateDoor(targetPos));
            else
                _doorTransform.localPosition = targetPos;

            // Collider : désactivé quand ouverte
            if (_doorCollider != null)
                _doorCollider.enabled = state != DoorState.Open;
        }

        private IEnumerator AnimateDoor(Vector3 targetPos)
        {
            Vector3 startPos = _doorTransform.localPosition;
            float elapsed = 0f;

            while (elapsed < _animDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _animDuration);
                _doorTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            _doorTransform.localPosition = targetPos;
            _animCoroutine = null;
        }

        // =====================================================================
        // API Server — appelée par HoldButton, PuzzleManager, etc.
        // =====================================================================

        [Server]
        public void Open()
        {
            if (CurrentState == DoorState.Locked) return;
            _state.Value = (int)DoorState.Open;
            ObserversPlayAudio(true);
        }

        [Server]
        public void Close()
        {
            if (CurrentState == DoorState.Locked) return;
            _state.Value = (int)DoorState.Closed;
            ObserversPlayAudio(false);
        }

        [Server]
        public void Lock()
        {
            _state.Value = (int)DoorState.Locked;
        }

        [Server]
        public void Unlock()
        {
            if (CurrentState == DoorState.Locked)
                _state.Value = (int)DoorState.Closed;
        }

        /// <summary>
        /// Unlock + Open en une seule opération (raccourci pour PuzzleManager).
        /// </summary>
        [Server]
        public void UnlockAndOpen()
        {
            _state.Value = (int)DoorState.Open;
            ObserversPlayAudio(true);
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserversPlayAudio(bool opening)
        {
            if (_audioSource == null) return;
            AudioClip clip = opening ? _openClip : _closeClip;
            if (clip != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
