using UnityEngine;
using UnityEngine.Events;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Interaction générique de focus — téléporte le joueur devant un point d'intérêt.
    /// E = entrer en mode focus, E = sortir.
    /// Utilisable pour : moniteurs, tableaux blancs, terminaux, etc.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur le GO interactable (Layer 6 + Collider)
    /// → Inspector : _focusPoint = Transform position/rotation close-up
    /// → Inspector : _enterPromptKey / _exitPromptKey = clés CSV pour les prompts
    /// → Optionnel : _unlockCursor = true pour déverrouiller la souris (ex: whiteboard)
    /// → Optionnel : UnityEvents OnEnterFocus / OnExitFocus pour hook externe
    /// </summary>
    public class FocusPointInteraction : MonoBehaviour, IInteractable
    {
        [Header("Focus Point")]
        [Tooltip("Position/rotation où le joueur est téléporté")]
        [SerializeField] private Transform _focusPoint;

        [Header("Prompts")]
        [SerializeField] private string _enterPromptKey = "ACTION_EXAMINE";
        [SerializeField] private string _exitPromptKey = "ACTION_STEP_BACK";

        [Header("Options")]
        [Tooltip("Déverrouille le curseur en mode focus (pour dessin, UI, etc.)")]
        [SerializeField] private bool _unlockCursor;

        [Header("Item requis (optionnel)")]
        [Tooltip("Si renseigné, le joueur doit tenir cet item en main pour interagir")]
        [SerializeField] private string _requiredItemId;
        [SerializeField] private string _missingItemPromptKey = "FEEDBACK_MARKER_NEEDED";

        [Header("Events")]
        public UnityEvent OnEnterFocus;
        public UnityEvent OnExitFocus;

        private bool _isFocused;
        private PlayerMovement _cachedMovement;
        private PlayerInputReader _cachedInput;
        private Transform _playerTransform;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;

        /// <summary>True si un joueur est actuellement en mode focus.</summary>
        public bool IsFocused => _isFocused;

        public void Interact(GameObject interactor)
        {
            if (_isFocused)
            {
                ExitFocus();
            }
            else
            {
                if (!HasRequiredItem()) return;
                EnterFocus(interactor);
            }
        }

        public string GetInteractionPrompt()
        {
            string input = LocalizationManager.Get("INPUT_PRESS");

            if (_isFocused)
                return $"<size=80%><color=yellow>{input} {LocalizationManager.Get(_exitPromptKey)}</color></size>";

            // Item requis manquant → prompt gris
            if (!HasRequiredItem())
                return $"<size=80%><color=#666666>{LocalizationManager.Get(_missingItemPromptKey)}</color></size>";

            return $"<size=80%><color=yellow>{input} {LocalizationManager.Get(_enterPromptKey)}</color></size>";
        }

        private bool HasRequiredItem()
        {
            if (string.IsNullOrEmpty(_requiredItemId)) return true;
            var inventory = PlayerInventory.LocalInstance;
            return inventory != null && inventory.EquippedItemId == _requiredItemId;
        }

        private void EnterFocus(GameObject player)
        {
            _cachedMovement = player.GetComponent<PlayerMovement>();
            _cachedInput = player.GetComponent<PlayerInputReader>();
            _playerTransform = player.transform;

            if (_cachedMovement == null || _focusPoint == null) return;

            _savedPosition = _playerTransform.position;
            _savedRotation = _playerTransform.rotation;

            _cachedMovement.FreezeMovement();
            _isFocused = true;

            _cachedMovement.Teleport(_focusPoint.position);
            _playerTransform.rotation = _focusPoint.rotation;

            if (_unlockCursor)
            {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
            }

            if (_cachedInput != null)
                _cachedInput.OnInteractEvent += HandleExitInput;

            OnEnterFocus?.Invoke();
        }

        private void ExitFocus()
        {
            if (_cachedMovement == null) return;

            if (_cachedInput != null)
                _cachedInput.OnInteractEvent -= HandleExitInput;

            OnExitFocus?.Invoke();

            if (_unlockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            _cachedMovement.Teleport(_savedPosition);
            _playerTransform.rotation = _savedRotation;
            _cachedMovement.UnfreezeMovement();

            _isFocused = false;
            _cachedMovement = null;
            _cachedInput = null;
            _playerTransform = null;
        }

        private void HandleExitInput()
        {
            if (_isFocused)
                ExitFocus();
        }

        private void OnDestroy()
        {
            if (_cachedInput != null)
                _cachedInput.OnInteractEvent -= HandleExitInput;
        }
    }
}
