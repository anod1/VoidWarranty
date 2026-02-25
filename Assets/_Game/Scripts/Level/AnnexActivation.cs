using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace SubSurface.Level
{
    /// <summary>
    /// Gère les deux systèmes annexes (O2 + Électricité).
    /// Quand les deux sont actifs, déverrouille l'ascenseur.
    ///
    /// SETUP ÉDITEUR :
    /// → GO "AnnexActivation" + NetworkObject + AnnexActivation.cs
    /// → 2 GO enfants AnnexInteractable (O2 + Électricité) : Collider Layer 6
    /// → Inspector AnnexActivation : ref ElevatorController
    /// → Inspector : AudioClip pour chaque activation (optionnel)
    /// → Inspector : Light refs pour feedback visuel (optionnel)
    /// </summary>
    public class AnnexActivation : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private ElevatorController _elevatorController;

        [Header("Feedback visuel (optionnel)")]
        [SerializeField] private Light _oxygenLight;
        [SerializeField] private Light _electricityLight;
        [SerializeField] private Color _inactiveColor = Color.red;
        [SerializeField] private Color _activeColor = Color.green;

        [Header("Audio (optionnel)")]
        [SerializeField] private AudioClip _activationClip;

        private readonly SyncVar<bool> _oxygenActive = new();
        private readonly SyncVar<bool> _electricityActive = new();

        private AudioSource _audioSource;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _oxygenActive.OnChange += OnSystemChanged;
            _electricityActive.OnChange += OnSystemChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _audioSource = GetComponentInChildren<AudioSource>();
            UpdateLights();
        }

        private void OnSystemChanged(bool prev, bool next, bool asServer)
        {
            if (asServer) return;
            UpdateLights();
        }

        private void UpdateLights()
        {
            if (_oxygenLight != null)
                _oxygenLight.color = _oxygenActive.Value ? _activeColor : _inactiveColor;

            if (_electricityLight != null)
                _electricityLight.color = _electricityActive.Value ? _activeColor : _inactiveColor;
        }

        /// <summary>
        /// Vérifie si un système est actif (pour le prompt des AnnexInteractable).
        /// </summary>
        public bool IsSystemActive(int index)
        {
            return index switch
            {
                0 => _oxygenActive.Value,
                1 => _electricityActive.Value,
                _ => false
            };
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdActivateSystem(int systemIndex)
        {
            switch (systemIndex)
            {
                case 0:
                    if (_oxygenActive.Value) return;
                    _oxygenActive.Value = true;
                    Debug.Log("[AnnexActivation] Système O2 activé.");
                    break;
                case 1:
                    if (_electricityActive.Value) return;
                    _electricityActive.Value = true;
                    Debug.Log("[AnnexActivation] Système Électricité activé.");
                    _elevatorController?.NotifyElectricityActive();
                    break;
                default:
                    return;
            }

            ObserversSystemActivated(systemIndex);

            // Vérifier si les deux sont actifs
            if (_oxygenActive.Value && _electricityActive.Value)
            {
                if (_elevatorController != null)
                {
                    _elevatorController.NotifyAllSystemsActive();
                    _elevatorController.Unlock();
                }

                Debug.Log("[AnnexActivation] Les deux systèmes actifs → Ascenseur déverrouillé !");
            }
        }

        [ObserversRpc]
        private void ObserversSystemActivated(int systemIndex)
        {
            if (_audioSource != null && _activationClip != null)
                _audioSource.PlayOneShot(_activationClip);

            UpdateLights();
        }
    }
}
