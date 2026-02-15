using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidWarranty.Player
{
    public class PlayerInputReader : MonoBehaviour, GameControls.IPlayerActions
    {
        // Singleton pour le PlayerInputReader LOCAL (celui du joueur possédé)
        public static PlayerInputReader LocalInstance { get; private set; }

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprinting { get; private set; }

        // NOUVEAU : On ajoute l'�tat Accroupi
        public bool IsCrouching { get; private set; }

        public event System.Action OnInteractEvent;
        public event System.Action OnGrabToggleEvent;

        public event System.Action OnJumpEvent;
        public event System.Action OnMissionToggleEvent;

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
        /// Évite qu'un joueur non-owner écrase le singleton au spawn.
        /// </summary>
        public void SetAsLocalInstance()
        {
            LocalInstance = this;
            Debug.Log("[PlayerInputReader] LocalInstance défini pour le joueur local.");
        }

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

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.started) OnInteractEvent?.Invoke();
        }

        public void OnGrab(InputAction.CallbackContext context)
        {
            if (context.started) OnGrabToggleEvent?.Invoke();
        }

        // --- NOUVELLES IMPLEMENTATIONS ---

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started) OnJumpEvent?.Invoke();
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (context.performed) IsCrouching = true;
            else if (context.canceled) IsCrouching = false;
        }

        public void OnMissionToggle(InputAction.CallbackContext context)
        {
            if (context.started)
                OnMissionToggleEvent?.Invoke();
        }

        // Non utilisés pour l'instant
        public void OnAttack(InputAction.CallbackContext context) { }
        public void OnPrevious(InputAction.CallbackContext context) { }
        public void OnNext(InputAction.CallbackContext context) { }
    }
}