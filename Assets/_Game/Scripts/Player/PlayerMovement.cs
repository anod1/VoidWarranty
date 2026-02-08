using UnityEngine;
using FishNet.Object;

namespace VoidWarranty.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerGrab))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Settings - Vitesse")]
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _runSpeed = 7f;
        [SerializeField] private float _crouchSpeed = 2f;

        [Header("Settings - Saut & Gravit�")]
        [SerializeField] private float _jumpHeight = 1.2f;
        [SerializeField] private float _gravity = -9.81f;

        [Header("Settings - Accroupissement")]
        [Tooltip("Hauteur debout (ex: 2m pour un humain)")]
        [SerializeField] private float _standHeight = 2f;
        [Tooltip("Hauteur accroupi (ex: 1m)")]
        [SerializeField] private float _crouchHeight = 1f;
        [SerializeField] private float _crouchTransitionSpeed = 10f;
        [SerializeField] private Transform _cameraHolder;
        [Tooltip("De combien descend la cam�ra quand on s'accroupit (en m�tres)")]
        [SerializeField] private float _crouchCameraOffset = 0.5f; // Nouveau param�tre !
        [SerializeField] private LayerMask _ceilingLayer;

        [Header("Settings - Poids")]
        [SerializeField] private float _weightPenaltyFactor = 0.15f;
        [SerializeField] private float _minSpeed = 1.5f;

        [Header("Settings - Souris")]
        [SerializeField] private float _lookSensitivityX = 0.5f;
        [SerializeField] private float _lookSensitivityY = 0.5f;

        [Header("References")]
        [SerializeField] private Transform _cameraTarget;

        private CharacterController _characterController;
        private PlayerInputReader _inputReader;
        private PlayerGrab _playerGrab;

        private float _verticalRotation;
        private Vector3 _velocity;
        private bool _isCrouchingPhysically = false;

        // On sauvegarde la position initiale de la cam�ra (d�finie dans le prefab)
        private float _standingCameraHeight;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _inputReader = GetComponent<PlayerInputReader>();
            _playerGrab = GetComponent<PlayerGrab>();

            // Initialisation du Character Controller
            _characterController.height = _standHeight;
            _characterController.center = Vector3.zero;

            // On sauvegarde la position Y initiale de la cam�ra (celle du prefab)
            if (_cameraHolder != null)
            {
                _standingCameraHeight = _cameraHolder.localPosition.y;
                Debug.Log($"[PlayerMovement] Cam�ra debout sauvegard�e � Y={_standingCameraHeight}");
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!base.IsOwner)
            {
                _inputReader.enabled = false;
                return;
            }

            _inputReader.OnJumpEvent += HandleJump;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (base.IsOwner) _inputReader.OnJumpEvent -= HandleJump;
        }

        private void Update()
        {
            if (!base.IsOwner) return;

            HandleRotation();
            HandleCrouch();
            HandleMovement();
        }

        private void HandleRotation()
        {
            Vector2 look = _inputReader.LookInput;
            float mouseX = look.x * _lookSensitivityX;
            float mouseY = look.y * _lookSensitivityY;

            transform.Rotate(Vector3.up * mouseX);

            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -85f, 85f);

            if (_cameraTarget != null)
                _cameraTarget.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
        }

        private void HandleJump()
        {
            if (_characterController.isGrounded)
            {
                _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }
        }

        private void HandleCrouch()
        {
            // 1. Le joueur veut-il s'accroupir ?
            bool wantsToCrouch = _inputReader.IsCrouching;

            // 2. V�rification du plafond (Ceiling Check)
            if (!wantsToCrouch && _isCrouchingPhysically)
            {
                Vector3 origin = transform.position + Vector3.up * _crouchHeight;
                float distanceToCheck = _standHeight - _crouchHeight + 0.1f;

                if (Physics.Raycast(origin, Vector3.up, distanceToCheck, _ceilingLayer))
                {
                    wantsToCrouch = true; // Forc� � rester accroupi
                }
            }

            // 3. Application de la hauteur du collider
            if (_isCrouchingPhysically != wantsToCrouch)
            {
                _isCrouchingPhysically = wantsToCrouch;
                float targetHeight = _isCrouchingPhysically ? _crouchHeight : _standHeight;

                _characterController.enabled = false;
                _characterController.height = targetHeight;
                _characterController.center = Vector3.zero;
                _characterController.enabled = true;

                Physics.SyncTransforms();
            }

            // 4. Application fluide de la cam�ra
            // NOUVELLE LOGIQUE : On part de la position du prefab et on descend seulement en crouch
            if (_cameraHolder != null)
            {
                float targetCamHeight;

                if (_isCrouchingPhysically)
                {
                    // En crouch : on descend la cam�ra selon l'offset configur�
                    targetCamHeight = _standingCameraHeight - _crouchCameraOffset;
                }
                else
                {
                    // Debout : on revient � la position du prefab
                    targetCamHeight = _standingCameraHeight;
                }

                Vector3 camPos = _cameraHolder.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, targetCamHeight, Time.deltaTime * _crouchTransitionSpeed);
                _cameraHolder.localPosition = camPos;
            }
        }

        private void HandleMovement()
        {
            if (_characterController.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            Vector2 moveInput = _inputReader.MoveInput;
            Vector3 moveDir = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (moveDir.magnitude > 1f) moveDir.Normalize();

            // Calcul de la vitesse selon l'�tat
            float baseSpeed;
            if (_isCrouchingPhysically)
                baseSpeed = _crouchSpeed;
            else
                baseSpeed = _inputReader.IsSprinting ? _runSpeed : _walkSpeed;

            // P�nalit� de poids
            float currentMass = _playerGrab.CurrentHeldMass;
            float dynamicSpeed = baseSpeed - (currentMass * _weightPenaltyFactor);
            dynamicSpeed = Mathf.Max(dynamicSpeed, _minSpeed);

            // Application gravit� et mouvement
            _velocity.y += _gravity * Time.deltaTime;
            _characterController.Move((moveDir * dynamicSpeed + _velocity) * Time.deltaTime);
        }

        [ContextMenu("Debug Character Controller")]
        private void DebugCharacterController()
        {
            Debug.Log($"=== CHARACTER CONTROLLER DEBUG ===");
            Debug.Log($"Height: {_characterController.height}");
            Debug.Log($"Center: {_characterController.center}");
            Debug.Log($"Player Position: {transform.position}");
            Debug.Log($"Is Crouching: {_isCrouchingPhysically}");
            Debug.Log($"Standing Camera Height: {_standingCameraHeight}");
            Debug.Log($"Current Camera Height: {(_cameraHolder != null ? _cameraHolder.localPosition.y : 0)}");
            Debug.Log($"==================================");
        }
    }
}