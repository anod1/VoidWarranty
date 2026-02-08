using UnityEngine;
using FishNet.Object;
using VoidWarranty.Core;
using VoidWarranty.UI;

namespace VoidWarranty.Player
{
    public class PlayerInteraction : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _interactDistance = 3f;
        [SerializeField] private LayerMask _interactLayer;

        [Header("References")]
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private PlayerInputReader _inputReader;

        private InteractionHUD _hud;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!base.IsOwner) return;

            _inputReader.OnInteractEvent += HandleInteractInput;
            _hud = FindFirstObjectByType<InteractionHUD>();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!base.IsOwner) return;

            _inputReader.OnInteractEvent -= HandleInteractInput;
        }

        private void Update()
        {
            if (!base.IsOwner) return;
            ScanForInteractable();
        }

        // =====================================================================
        // Scan passif — Met à jour le prompt UI en continu
        // =====================================================================

        private void ScanForInteractable()
        {
            if (_hud == null) return;

            Ray ray = new Ray(_cameraRoot.position, _cameraRoot.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactLayer))
            {
                // GetComponentInParent cherche sur le collider touché PUIS remonte la hiérarchie
                // Nécessaire car les colliders peuvent être sur des enfants (compound colliders)
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    string prompt = interactable.GetInteractionPrompt();
                    _hud.UpdatePrompt(prompt);
                    return;
                }
            }

            _hud.UpdatePrompt("");
        }

        // =====================================================================
        // Interaction active — Quand le joueur appuie sur E
        // =====================================================================

        private void HandleInteractInput()
        {
            Ray ray = new Ray(_cameraRoot.position, _cameraRoot.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactLayer))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    interactable.Interact(this.gameObject);
                }
            }
        }
    }
}
