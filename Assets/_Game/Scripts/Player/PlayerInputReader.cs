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
        public event System.Action OnInteractReleasedEvent;
        public event System.Action OnGrabToggleEvent;

        public bool IsInteractHeld { get; private set; }

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

        public bool IsHoldingBreath { get; private set; }

        public void OnHoldBreath(InputAction.CallbackContext context)
        {
            if (context.performed) IsHoldingBreath = true;
            else if (context.canceled) IsHoldingBreath = false;
        }

        // Valve scroll : molette souris (Y) ou gâchettes manette (RT/LT).
        // IMPORTANT : La molette souris est discrète (±120 pendant 1 frame puis canceled = 0).
        // On accumule les deltas dans _valveScrollAccumulator, et le consommateur (PurgeValve)
        // appelle ConsumeValveScroll() pour lire + reset. Pour la manette (continu), on stocke
        // la dernière valeur dans _valveScrollContinuous.
        //
        // ConsumeValveScroll() retourne :
        //   - Souris : la somme accumulée des scroll ticks depuis le dernier consume
        //   - Manette : la valeur continue actuelle (re-lue chaque frame par le consommateur)
        private float _valveScrollAccumulator;
        private float _valveScrollContinuous;

        /// <summary>
        /// Consomme l'input scroll de vanne. Appelé chaque frame par PurgeValve (ou futur puzzle).
        /// Retourne la valeur discrète accumulée (souris) OU la valeur continue (manette).
        /// Reset l'accumulateur souris après lecture.
        /// </summary>
        public float ConsumeValveScroll()
        {
            // S'il y a des scroll discrets accumulés (souris), les prioriser
            if (Mathf.Abs(_valveScrollAccumulator) > 0.01f)
            {
                float val = _valveScrollAccumulator;
                _valveScrollAccumulator = 0f;
                return val;
            }
            // Sinon retourner la valeur continue (manette gâchettes)
            return _valveScrollContinuous;
        }

        public void OnValveScroll(InputAction.CallbackContext context)
        {
            float raw = context.ReadValue<float>();

            // Molette souris : valeurs discrètes ±120 → on accumule
            // Manette : valeurs continues -1..+1 → on stocke
            if (Mathf.Abs(raw) > 2f)
            {
                // Souris (discret ±120 par tick)
                _valveScrollAccumulator += raw;
            }
            else
            {
                // Manette (continu -1..+1)
                _valveScrollContinuous = raw;
            }
        }

        // Non utilisés pour l'instant
        public void OnAttack(InputAction.CallbackContext context) { }
        public void OnPrevious(InputAction.CallbackContext context) { }
        public void OnNext(InputAction.CallbackContext context) { }
    }
}