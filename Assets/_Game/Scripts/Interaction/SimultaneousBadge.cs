using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Orchestrateur des deux lecteurs de badge simultanés.
    /// Les deux joueurs doivent maintenir E en même temps pour ouvrir la porte.
    ///
    /// SETUP ÉDITEUR :
    /// → GO parent "BadgeSystem" + NetworkObject + SimultaneousBadge.cs
    /// → 2 GO enfants "ReaderA" / "ReaderB" : chacun BadgeReader.cs + Collider Layer 6
    /// → Inspector parent : glisser les BadgeReader dans _readerA / _readerB
    /// → Inspector parent : glisser la LevelDoor cible
    /// → Inspector parent : Light refs pour feedback (optionnel)
    /// </summary>
    public class SimultaneousBadge : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private BadgeReader _readerA;
        [SerializeField] private BadgeReader _readerB;
        [SerializeField] private LevelDoor _targetDoor;

        [Header("Feedback visuel (optionnel)")]
        [SerializeField] private Light _lightA;
        [SerializeField] private Light _lightB;
        [SerializeField] private Color _inactiveColor = Color.red;
        [SerializeField] private Color _activeColor = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private Color _completeColor = Color.green;

        private readonly SyncVar<bool> _readerAActive = new();
        private readonly SyncVar<bool> _readerBActive = new();
        private readonly SyncVar<bool> _doorOpened = new();

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _readerAActive.OnChange += OnReaderStateChanged;
            _readerBActive.OnChange += OnReaderStateChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            UpdateLights();
        }

        private void OnReaderStateChanged(bool prev, bool next, bool asServer)
        {
            if (asServer) return;
            UpdateLights();
        }

        private void UpdateLights()
        {
            bool bothActive = _readerAActive.Value && _readerBActive.Value;

            if (_lightA != null)
            {
                if (_doorOpened.Value) _lightA.color = _completeColor;
                else if (_readerAActive.Value) _lightA.color = bothActive ? _completeColor : _activeColor;
                else _lightA.color = _inactiveColor;
            }

            if (_lightB != null)
            {
                if (_doorOpened.Value) _lightB.color = _completeColor;
                else if (_readerBActive.Value) _lightB.color = bothActive ? _completeColor : _activeColor;
                else _lightB.color = _inactiveColor;
            }
        }

        // =====================================================================
        // API — appelée par BadgeReader enfants
        // =====================================================================

        public void NotifyReaderHoldStart(int readerIndex)
        {
            CmdSetReaderState(readerIndex, true);
        }

        public void NotifyReaderHoldRelease(int readerIndex)
        {
            CmdSetReaderState(readerIndex, false);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdSetReaderState(int readerIndex, bool active)
        {
            if (_doorOpened.Value) return;

            if (readerIndex == 0)
                _readerAActive.Value = active;
            else
                _readerBActive.Value = active;

            // Vérifier si les deux sont actifs
            if (_readerAActive.Value && _readerBActive.Value)
            {
                _doorOpened.Value = true;

                if (_targetDoor != null)
                    _targetDoor.UnlockAndOpen();

                ObserversBadgeSuccess();
                Debug.Log("[SimultaneousBadge] Les deux badges scannés ! Porte ouverte.");
            }
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserversBadgeSuccess()
        {
            UpdateLights();
        }
    }
}
