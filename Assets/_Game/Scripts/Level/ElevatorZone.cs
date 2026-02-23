using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;

namespace SubSurface.Level
{
    /// <summary>
    /// Zone ascenseur — fin de level.
    /// Les deux joueurs doivent être présents simultanément pour compléter.
    /// Pattern identique à TruckZone.
    ///
    /// SETUP ÉDITEUR :
    /// → GO cabine ascenseur + BoxCollider (isTrigger) + NetworkObject
    /// → ElevatorZone.cs
    /// → Inspector : ref Light indicateur, couleurs configurables
    /// → Inspector : AudioClip levelComplete (optionnel)
    /// </summary>
    public class ElevatorZone : NetworkBehaviour
    {
        public static event System.Action OnLevelComplete;

        [Header("Feedback visuel")]
        [SerializeField] private Light _indicatorLight;
        [SerializeField] private Color _lockedColor = Color.red;
        [SerializeField] private Color _onePlayerColor = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private Color _readyColor = Color.green;

        [Header("Audio")]
        [SerializeField] private AudioClip _levelCompleteClip;

        private readonly SyncVar<bool> _isUnlocked = new();
        private readonly SyncVar<int> _playersInZone = new();
        private readonly SyncVar<bool> _levelCompleted = new();

        private readonly HashSet<GameObject> _playersTracked = new();
        private AudioSource _audioSource;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _isUnlocked.OnChange += OnStateChanged;
            _playersInZone.OnChange += OnStateChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _audioSource = GetComponentInChildren<AudioSource>();
            UpdateIndicator();
        }

        private void OnStateChanged(int prev, int next, bool asServer)
        {
            if (asServer) return;
            UpdateIndicator();
        }

        private void OnStateChanged(bool prev, bool next, bool asServer)
        {
            if (asServer) return;
            UpdateIndicator();
        }

        private void UpdateIndicator()
        {
            if (_indicatorLight == null) return;

            if (!_isUnlocked.Value)
                _indicatorLight.color = _lockedColor;
            else if (_playersInZone.Value >= 2)
                _indicatorLight.color = _readyColor;
            else if (_playersInZone.Value == 1)
                _indicatorLight.color = _onePlayerColor;
            else
                _indicatorLight.color = _lockedColor;
        }

        // =====================================================================
        // API — appelée par AnnexActivation
        // =====================================================================

        [Server]
        public void Unlock()
        {
            _isUnlocked.Value = true;
            Debug.Log("[ElevatorZone] Ascenseur déverrouillé.");
        }

        // =====================================================================
        // Trigger — tracking des joueurs
        // =====================================================================

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerInitialized) return;
            if (_levelCompleted.Value) return;

            if (other.gameObject.layer == 7) // Layer Player
            {
                if (_playersTracked.Add(other.gameObject))
                {
                    _playersInZone.Value = _playersTracked.Count;
                    CheckCompletion();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!base.IsServerInitialized) return;
            if (_levelCompleted.Value) return;

            if (_playersTracked.Remove(other.gameObject))
            {
                _playersInZone.Value = _playersTracked.Count;
            }
        }

        [Server]
        private void CheckCompletion()
        {
            if (!_isUnlocked.Value) return;
            if (_playersInZone.Value < 2) return;

            _levelCompleted.Value = true;
            Debug.Log("[ElevatorZone] Les deux joueurs dans l'ascenseur → OnLevelComplete !");
            ObserversLevelComplete();
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserversLevelComplete()
        {
            if (_audioSource != null && _levelCompleteClip != null)
                _audioSource.PlayOneShot(_levelCompleteClip);

            OnLevelComplete?.Invoke();
        }
    }
}
