using UnityEngine;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace SubSurface.Gameplay
{
    public enum HidingType { Locker, UnderDesk }

    /// <summary>
    /// Cachette interactive style Alien Isolation.
    /// Teleporte le joueur dans la cachette, camera limitee, hold breath mechanic.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class HidingSpot : MonoBehaviour, IInteractable
    {
        [Header("Type")]
        [SerializeField] private HidingType _hidingType = HidingType.Locker;

        [Header("Positions")]
        [Tooltip("Transform enfant : position ou le joueur sera teleporte")]
        [SerializeField] private Transform _hidePosition;
        [Tooltip("Offset de sortie relatif au hiding spot")]
        [SerializeField] private Vector3 _exitOffset = new Vector3(0f, 0f, 1f);

        [Header("Camera")]
        [Tooltip("Amplitude horizontale de rotation (Locker=90, UnderDesk=180)")]
        [SerializeField] private float _yawRange = 90f;
        [Tooltip("Amplitude verticale de rotation")]
        [SerializeField] private float _pitchRange = 60f;

        [Header("Prompts")]
        [SerializeField] private string _hidePromptKey = "ACTION_HIDE";
        [SerializeField] private string _exitPromptKey = "ACTION_EXIT_HIDE";

        // =====================================================================
        // State
        // =====================================================================

        private bool _isOccupied;
        private GameObject _occupant;
        private int _occupantOriginalLayer;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;

        // Cached components
        private PlayerMovement _cachedMovement;
        private PlayerGrab _cachedGrab;
        private PlayerInputReader _cachedInput;

        private static readonly int HiddenLayer = 8;

        // Hold breath (fouille par Drifter)
        private bool _isBeingSearched;
        private bool _holdBreathSuccess;
        private bool _holdBreathInputActive;

        // =====================================================================
        // Properties
        // =====================================================================

        public bool IsOccupied => _isOccupied;
        public GameObject Occupant => _occupant;

        // =====================================================================
        // IInteractable
        // =====================================================================

        public void Interact(GameObject interactor)
        {
            if (_isOccupied && _occupant == interactor)
            {
                if (_isBeingSearched) return; // Pas de sortie pendant la fouille
                ExitHiding();
            }
            else if (!_isOccupied)
            {
                EnterHiding(interactor);
            }
        }

        public string GetInteractionPrompt()
        {
            if (_isOccupied)
            {
                if (_isBeingSearched) return ""; // Pas de prompt pendant la fouille
                string action = LocalizationManager.Get(_exitPromptKey);
                if (string.IsNullOrEmpty(action) || action == _exitPromptKey)
                    action = "Exit";
                return $"<size=80%><color=yellow>[{action}]</color></size>";
            }
            else
            {
                string action = LocalizationManager.Get(_hidePromptKey);
                if (string.IsNullOrEmpty(action) || action == _hidePromptKey)
                    action = "Hide";
                return $"<size=80%><color=yellow>[{action}]</color></size>";
            }
        }

        // =====================================================================
        // Hide / Unhide
        // =====================================================================

        private void EnterHiding(GameObject player)
        {
            _isOccupied = true;
            _occupant = player;

            // Cache components
            _cachedMovement = player.GetComponent<PlayerMovement>();
            _cachedGrab = player.GetComponent<PlayerGrab>();
            _cachedInput = player.GetComponent<PlayerInputReader>();

            // Drop held item
            if (_cachedGrab != null)
                _cachedGrab.ForceDrop();

            // Save position & rotation
            _savedPosition = player.transform.position;
            _savedRotation = player.transform.rotation;

            // Save & change layer
            _occupantOriginalLayer = player.layer;
            SetLayerRecursively(player, HiddenLayer);

            // Teleport player inside the hiding spot
            if (_cachedMovement != null && _hidePosition != null)
            {
                _cachedMovement.Teleport(_hidePosition.position);

                // Orient player to look in the hiding spot's forward direction
                player.transform.rotation = _hidePosition.rotation;

                // Crouch height for both types
                _cachedMovement.ForceSetCrouchHeight(true);

                // Enable limited mouselook
                _cachedMovement.EnableHiddenLook(_yawRange, _pitchRange);
            }

            // Subscribe to interact event for exit (since raycast may not reach from inside)
            if (_cachedInput != null)
                _cachedInput.OnInteractEvent += HandleExitInput;

            Debug.Log($"[HidingSpot] Joueur cache dans {gameObject.name} ({_hidingType})");
        }

        private void ExitHiding()
        {
            if (_occupant == null) return;

            // Unsubscribe exit input
            if (_cachedInput != null)
                _cachedInput.OnInteractEvent -= HandleExitInput;

            if (_cachedMovement != null)
            {
                // Disable hidden look
                _cachedMovement.DisableHiddenLook();

                // Restore standing height
                _cachedMovement.ForceSetCrouchHeight(false);

                // Teleport to exit position
                Vector3 exitPos = _hidePosition != null
                    ? _hidePosition.position + _hidePosition.rotation * _exitOffset
                    : _savedPosition;
                _cachedMovement.Teleport(exitPos);
            }

            // Restore rotation
            _occupant.transform.rotation = _savedRotation;

            // Restore layer
            SetLayerRecursively(_occupant, _occupantOriginalLayer);

            Debug.Log($"[HidingSpot] Joueur sorti de {gameObject.name}");

            // Clear state
            _isOccupied = false;
            _isBeingSearched = false;
            _holdBreathSuccess = false;
            _holdBreathInputActive = false;

            GameObject occupant = _occupant;
            _occupant = null;
            _cachedMovement = null;
            _cachedGrab = null;
            _cachedInput = null;
        }

        private void HandleExitInput()
        {
            if (_isOccupied && _occupant != null && !_isBeingSearched)
                ExitHiding();
        }

        // =====================================================================
        // Hold Breath — Drifter search mechanic
        // =====================================================================

        private void Update()
        {
            if (!_isBeingSearched || !_isOccupied || _cachedInput == null) return;

            bool holding = _cachedInput.IsHoldingBreath;

            if (holding)
            {
                _holdBreathInputActive = true;
                _holdBreathSuccess = true;
            }
            else if (_holdBreathInputActive)
            {
                // Player released the button during search
                _holdBreathSuccess = false;
            }
        }

        /// <summary>
        /// Called by DrifterAI when it starts searching this hiding spot.
        /// </summary>
        public void NotifySearchStarted()
        {
            _isBeingSearched = true;
            _holdBreathSuccess = false;
            _holdBreathInputActive = false;
            Debug.Log($"[HidingSpot] Drifter fouille {gameObject.name} !");
            // TODO: UI prompt "Hold [LMB] to hold breath"
        }

        /// <summary>
        /// Called by DrifterAI when search ends (survived or killed).
        /// </summary>
        public void NotifySearchEnded()
        {
            _isBeingSearched = false;
            _holdBreathInputActive = false;
            // TODO: UI hide prompt
        }

        /// <summary>
        /// Server checks if the player held their breath during the search.
        /// </summary>
        public bool PlayerHeldBreath()
        {
            return _holdBreathSuccess;
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Checks if a specific player is hidden here.
        /// </summary>
        public bool IsPlayerHiddenHere(GameObject player)
        {
            return _isOccupied && _occupant == player;
        }

        /// <summary>
        /// Force exit (hiding spot destroyed, or Drifter found player).
        /// Returns the occupant GameObject before clearing.
        /// </summary>
        public GameObject ForceExitAndGetOccupant()
        {
            if (!_isOccupied) return null;
            GameObject occupant = _occupant;
            ExitHiding();
            return occupant;
        }

        /// <summary>
        /// Force exit without kill (backward compat / destruction).
        /// </summary>
        public void ForceExit()
        {
            if (_isOccupied)
                ExitHiding();
        }

        private void OnDestroy()
        {
            if (_cachedInput != null)
                _cachedInput.OnInteractEvent -= HandleExitInput;
            ForceExit();
        }

        // =====================================================================
        // Utilities
        // =====================================================================

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                if (child == null) continue;
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
