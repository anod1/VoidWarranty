using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using VoidWarranty.Core;

namespace VoidWarranty.Interaction
{
    public enum DoorButtonAction { Open, Close, Toggle }

    /// <summary>
    /// Bouton générique pour portes. Gère press E et hold E.
    ///
    /// _holdMode = false : press E → exécute l'action (open/close/toggle)
    /// _holdMode = true  : hold E → ouvre, release → ferme (ou l'inverse selon _action)
    ///
    /// Ne peut PAS agir sur une porte Locked (prompt masqué, action ignorée).
    /// </summary>
    public class DoorButton : NetworkBehaviour, IHoldInteractable
    {
        [Header("Settings")]
        [SerializeField] private LevelDoor _targetDoor;
        [SerializeField] private DoorButtonAction _action = DoorButtonAction.Toggle;
        [SerializeField] private bool _holdMode;
        [SerializeField] private bool _disableAfterUse;

        [Header("Prompts")]
        [SerializeField] private string _promptKeyOpen = "ACTION_HOLD_OPEN";
        [SerializeField] private string _promptKeyClose = "ACTION_DOOR_CLOSE";

        [Header("Audio")]
        [SerializeField] private AudioClip _pressClip;

        [Header("Animation")]
        [Tooltip("Optionnel — composant ButtonAnimator pour l'enfoncement visuel")]
        [SerializeField] private ButtonAnimator _animator;

        private readonly SyncVar<bool> _isDisabled = new();
        private bool _holding;

        // =====================================================================
        // IInteractable (hérité de IHoldInteractable)
        // =====================================================================

        public void Interact(GameObject interactor)
        {
            OnHoldStart(interactor);
        }

        public string GetInteractionPrompt()
        {
            if (_isDisabled.Value) return "";
            if (_targetDoor == null) return "";
            if (_targetDoor.CurrentState == DoorState.Locked) return "";

            string input = _holdMode
                ? LocalizationManager.Get("INPUT_HOLD")
                : LocalizationManager.Get("INPUT_PRESS");

            string actionKey = ResolvePromptKey();

            return $"<size=80%><color=yellow>{input} {LocalizationManager.Get(actionKey)}</color></size>";
        }

        // =====================================================================
        // IHoldInteractable
        // =====================================================================

        public void OnHoldStart(GameObject interactor)
        {
            if (_isDisabled.Value) return;
            _holding = true;
            _animator?.Press();
            CmdDoAction();
        }

        public void OnHoldRelease(GameObject interactor)
        {
            if (!_holding) return;
            _holding = false;
            _animator?.Release();

            if (_holdMode)
                CmdUndoAction();
        }

        public float GetHoldDuration() => 0f;
        public bool IsHolding => _holding;

        // =====================================================================
        // RPCs
        // =====================================================================

        [ServerRpc(RequireOwnership = false)]
        private void CmdDoAction()
        {
            if (_isDisabled.Value) return;
            if (_targetDoor == null) return;
            if (_targetDoor.CurrentState == DoorState.Locked) return;

            switch (_action)
            {
                case DoorButtonAction.Open:
                    _targetDoor.Open();
                    break;
                case DoorButtonAction.Close:
                    _targetDoor.Close();
                    break;
                case DoorButtonAction.Toggle:
                    if (_targetDoor.CurrentState == DoorState.Open)
                        _targetDoor.Close();
                    else
                        _targetDoor.Open();
                    break;
            }

            if (_disableAfterUse && !_holdMode)
                _isDisabled.Value = true;

            ObserversPlayAudio();
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdUndoAction()
        {
            if (_targetDoor == null) return;
            if (_targetDoor.CurrentState == DoorState.Locked) return;

            // Inverse de l'action
            switch (_action)
            {
                case DoorButtonAction.Open:
                    _targetDoor.Close();
                    break;
                case DoorButtonAction.Close:
                    _targetDoor.Open();
                    break;
                case DoorButtonAction.Toggle:
                    // Toggle inverse : re-toggle
                    if (_targetDoor.CurrentState == DoorState.Open)
                        _targetDoor.Close();
                    else
                        _targetDoor.Open();
                    break;
            }
        }

        [ObserversRpc]
        private void ObserversPlayAudio()
        {
            if (_pressClip != null)
            {
                AudioSource source = GetComponentInChildren<AudioSource>();
                if (source != null)
                    source.PlayOneShot(_pressClip);
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private string ResolvePromptKey()
        {
            return _action switch
            {
                DoorButtonAction.Open => _promptKeyOpen,
                DoorButtonAction.Close => _promptKeyClose,
                DoorButtonAction.Toggle => _targetDoor.CurrentState == DoorState.Open
                    ? _promptKeyClose
                    : _promptKeyOpen,
                _ => _promptKeyOpen
            };
        }
    }
}
