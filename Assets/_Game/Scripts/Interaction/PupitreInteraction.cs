using UnityEngine;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Interaction lean-in pour le pupitre hydraulique.
    /// E = lean-in vers le moniteur, E = sortir.
    /// Pas de restriction de rôle — c'est le level design qui contraint l'accès.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur le GO "PupitreInteractable" devant le moniteur
    /// → Layer 6 (Interactable) + Collider
    /// → Inspector : Transform _leanInPoint (position/rotation close-up devant le moniteur)
    /// </summary>
    public class PupitreInteraction : MonoBehaviour, IInteractable
    {
        [Header("Lean-in")]
        [SerializeField] private Transform _leanInPoint;

        private bool _isLeanedIn;
        private PlayerMovement _cachedMovement;
        private PlayerInputReader _cachedInput;
        private Transform _playerTransform;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;

        public void Interact(GameObject interactor)
        {
            if (_isLeanedIn)
            {
                ExitLeanIn();
            }
            else
            {
                EnterLeanIn(interactor);
            }
        }

        public string GetInteractionPrompt()
        {
            string input = LocalizationManager.Get("INPUT_PRESS");

            if (_isLeanedIn)
                return $"<size=80%><color=yellow>{input} {LocalizationManager.Get("ACTION_STEP_BACK")}</color></size>";

            return $"<size=80%><color=yellow>{input} {LocalizationManager.Get("ACTION_EXAMINE_MONITOR")}</color></size>";
        }

        private void EnterLeanIn(GameObject player)
        {
            _cachedMovement = player.GetComponent<PlayerMovement>();
            _cachedInput = player.GetComponent<PlayerInputReader>();
            _playerTransform = player.transform;

            if (_cachedMovement == null || _leanInPoint == null) return;

            _savedPosition = _playerTransform.position;
            _savedRotation = _playerTransform.rotation;

            _cachedMovement.FreezeMovement();
            _isLeanedIn = true;

            _cachedMovement.Teleport(_leanInPoint.position);
            _playerTransform.rotation = _leanInPoint.rotation;

            if (_cachedInput != null)
                _cachedInput.OnInteractEvent += HandleExitInput;
        }

        private void ExitLeanIn()
        {
            if (_cachedMovement == null) return;

            if (_cachedInput != null)
                _cachedInput.OnInteractEvent -= HandleExitInput;

            _cachedMovement.Teleport(_savedPosition);
            _playerTransform.rotation = _savedRotation;
            _cachedMovement.UnfreezeMovement();

            _isLeanedIn = false;
            _cachedMovement = null;
            _cachedInput = null;
            _playerTransform = null;
        }

        private void HandleExitInput()
        {
            if (_isLeanedIn)
                ExitLeanIn();
        }

        private void OnDestroy()
        {
            if (_cachedInput != null)
                _cachedInput.OnInteractEvent -= HandleExitInput;
        }
    }
}
