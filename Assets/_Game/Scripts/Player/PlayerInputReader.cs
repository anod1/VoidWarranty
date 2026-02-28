using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidWarranty.Player
{
    public class PlayerInputReader : MonoBehaviour, GameControls.IPlayerActions
    {
        public static PlayerInputReader LocalInstance { get; private set; }

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsInteractHeld { get; private set; }
        public bool IsHoldingBreath { get; private set; }

        public event System.Action OnInteractEvent;
        public event System.Action OnInteractReleasedEvent;
        public event System.Action OnGrabToggleEvent;
        public event System.Action OnJumpEvent;

        // Hotbar
        public event System.Action<int> OnHotbarSlotEvent;
        public event System.Action OnDropEvent;
        public event System.Action<float> OnHotbarScrollEvent;

        private GameControls _controls;

        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new GameControls();
                _controls.Player.SetCallbacks(this);
            }
            _controls.Player.Enable();
        }

        private void OnDisable()
        {
            if (LocalInstance == this)
                LocalInstance = null;

            _controls?.Player.Disable();
        }

        /// <summary>
        /// Appelé par PlayerMovement.OnStartClient() uniquement pour le joueur local (IsOwner).
        /// </summary>
        public void SetAsLocalInstance()
        {
            LocalInstance = this;
        }

        // =================================================================
        // Movement
        // =================================================================

        public void OnMove(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.performed) IsSprinting = true;
            else if (context.canceled) IsSprinting = false;
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (context.performed) IsCrouching = true;
            else if (context.canceled) IsCrouching = false;
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started) OnJumpEvent?.Invoke();
        }

        // =================================================================
        // Interaction
        // =================================================================

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                IsInteractHeld = true;
                OnInteractEvent?.Invoke();
            }
            else if (context.canceled)
            {
                IsInteractHeld = false;
                OnInteractReleasedEvent?.Invoke();
            }
        }

        public void OnGrab(InputAction.CallbackContext context)
        {
            if (context.started) OnGrabToggleEvent?.Invoke();
        }

        public void OnHoldBreath(InputAction.CallbackContext context)
        {
            if (context.performed) IsHoldingBreath = true;
            else if (context.canceled) IsHoldingBreath = false;
        }

        // =================================================================
        // Hotbar
        // =================================================================

        public void OnHotbarSlot1(InputAction.CallbackContext context)
        {
            if (context.started) OnHotbarSlotEvent?.Invoke(0);
        }

        public void OnHotbarSlot2(InputAction.CallbackContext context)
        {
            if (context.started) OnHotbarSlotEvent?.Invoke(1);
        }

        public void OnHotbarSlot3(InputAction.CallbackContext context)
        {
            if (context.started) OnHotbarSlotEvent?.Invoke(2);
        }

        public void OnHotbarSlot4(InputAction.CallbackContext context)
        {
            if (context.started) OnHotbarSlotEvent?.Invoke(3);
        }

        public void OnHotbarScroll(InputAction.CallbackContext context)
        {
            float raw = context.ReadValue<float>();
            if (Mathf.Abs(raw) > 0.1f)
                OnHotbarScrollEvent?.Invoke(raw);
        }

        public void OnDrop(InputAction.CallbackContext context)
        {
            if (context.started) OnDropEvent?.Invoke();
        }

        // =================================================================
        // Legacy (conservé pour MissionHUD — jamais fire)
        // =================================================================

        public event System.Action OnMissionToggleEvent;

        public void OnMissionToggle(InputAction.CallbackContext context) { }
        public void OnAttack(InputAction.CallbackContext context) { }
    }
}
