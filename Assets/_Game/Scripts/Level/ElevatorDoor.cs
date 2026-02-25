using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;

namespace SubSurface.Level
{
    /// <summary>
    /// Porte circulaire animée par rotation autour d'un pivot central.
    /// Deux instances (gauche -90°, droite +90°) forment la double porte de l'ascenseur.
    ///
    /// SETUP ÉDITEUR :
    /// → GO "DoorLeft_Pivot" positionné au centre de l'ascenseur + NetworkObject
    /// → Enfant : mesh courbe + collider bloquant
    /// → Inspector : _doorTransform = ce GO, _openAngle = -90 (gauche) ou +90 (droite)
    /// → Le pivot du _doorTransform DOIT être au centre de l'ascenseur
    /// </summary>
    public class ElevatorDoor : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _doorTransform;
        [SerializeField] private Collider _doorCollider;

        [Header("Settings")]
        [SerializeField] private float _openAngle = 90f;
        [SerializeField] private float _animDuration = 1.2f;
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;

        [Header("Audio")]
        [SerializeField] private AudioClip _openClip;
        [SerializeField] private AudioClip _closeClip;

        private readonly SyncVar<int> _state = new(); // 0=Closed, 1=Open

        private AudioSource _audioSource;
        private Quaternion _closedRotation;
        private Quaternion _openRotation;
        private Coroutine _animCoroutine;

        public bool IsOpen => _state.Value == 1;

        private void Awake()
        {
            _audioSource = GetComponentInChildren<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f;
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
                _audioSource.maxDistance = 20f;
            }

            if (_doorTransform != null)
            {
                _closedRotation = _doorTransform.localRotation;
                _openRotation = _closedRotation * Quaternion.AngleAxis(_openAngle, _rotationAxis);
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
            _state.Value = 0; // Closed
            ApplyVisualState(false, false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyVisualState(_state.Value == 1, false);
        }

        private void OnStateChanged(int prev, int next, bool asServer)
        {
            if (asServer && base.IsClientInitialized) return;
            ApplyVisualState(next == 1, true);
        }

        private void ApplyVisualState(bool open, bool animate)
        {
            if (_doorTransform == null) return;

            Quaternion targetRot = open ? _openRotation : _closedRotation;

            if (_animCoroutine != null)
                StopCoroutine(_animCoroutine);

            if (animate && _animDuration > 0f)
                _animCoroutine = StartCoroutine(AnimateDoor(targetRot));
            else
                _doorTransform.localRotation = targetRot;

            if (_doorCollider != null)
                _doorCollider.enabled = !open;
        }

        private IEnumerator AnimateDoor(Quaternion targetRot)
        {
            Quaternion startRot = _doorTransform.localRotation;
            float elapsed = 0f;

            while (elapsed < _animDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _animDuration);
                _doorTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            _doorTransform.localRotation = targetRot;
            _animCoroutine = null;
        }

        // =====================================================================
        // API Server — appelée par ElevatorController
        // =====================================================================

        [Server]
        public void Open()
        {
            if (IsOpen) return;
            _state.Value = 1;
            ObserversPlayAudio(true);
        }

        [Server]
        public void Close()
        {
            if (!IsOpen) return;
            _state.Value = 0;
            ObserversPlayAudio(false);
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
