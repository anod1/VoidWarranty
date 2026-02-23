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

        // Hold interaction tracking
        private IHoldInteractable _currentHoldTarget;
        private GameObject _currentHoldInteractor;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!base.IsOwner) return;

            _inputReader.OnInteractEvent += HandleInteractInput;
            _inputReader.OnInteractReleasedEvent += HandleInteractReleased;
            _hud = FindFirstObjectByType<InteractionHUD>();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!base.IsOwner) return;

            _inputReader.OnInteractEvent -= HandleInteractInput;
            _inputReader.OnInteractReleasedEvent -= HandleInteractReleased;
        }

        private void Update()
        {
            if (!base.IsOwner) return;
            ScanForInteractable();
            UpdateHold();
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
                // Hold interactable : start hold au lieu de Interact()
                IHoldInteractable holdTarget = hit.collider.GetComponentInParent<IHoldInteractable>();
                if (holdTarget != null)
                {
                    StartHold(holdTarget);
                    return;
                }

                // Standard interactable
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    interactable.Interact(gameObject);
                }
            }
        }

        // =====================================================================
        // Hold interaction
        // =====================================================================

        private void StartHold(IHoldInteractable target)
        {
            // Release previous hold if any
            if (_currentHoldTarget != null)
                ReleaseHold();

            _currentHoldTarget = target;
            _currentHoldInteractor = gameObject;
            target.OnHoldStart(gameObject);
        }

        private void ReleaseHold()
        {
            if (_currentHoldTarget == null) return;

            _currentHoldTarget.OnHoldRelease(_currentHoldInteractor);
            _currentHoldTarget = null;
            _currentHoldInteractor = null;
        }

        private void HandleInteractReleased()
        {
            ReleaseHold();
        }

        private void UpdateHold()
        {
            if (_currentHoldTarget == null) return;

            // Si le joueur ne regarde plus la cible, release auto
            Ray ray = new Ray(_cameraRoot.position, _cameraRoot.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactLayer))
            {
                IHoldInteractable target = hit.collider.GetComponentInParent<IHoldInteractable>();
                if (target == _currentHoldTarget) return; // Toujours en vue
            }

            // Plus en vue → release
            ReleaseHold();
        }
    }
}
