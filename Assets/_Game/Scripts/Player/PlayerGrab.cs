using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Connection;
using VoidWarranty.Interaction;
using VoidWarranty.Core;

namespace VoidWarranty.Player
{
    public class PlayerGrab : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _grabDistance = 3f;
        [SerializeField] private float _throwForce = 5f;
        [SerializeField] private LayerMask _grabLayer;

        [Header("Physics Settings")]
        [SerializeField] private float _followSpeed = 20f;
        [SerializeField] private float _rotateSpeed = 20f;
        [SerializeField] private float _breakDistance = 1.5f;
        [SerializeField] private float _maxGrabVelocity = 15f;

        [Header("Hold Point (séparé de l'inventaire)")]
        [Tooltip("Transform enfant de la caméra, positionné pour le repos (objet tenu bas).")]
        [SerializeField] private Transform _grabHoldPoint;

        [Header("Brandish (lever l'objet — clic gauche)")]
        [Tooltip("Offset position ajouté quand on brandit l'objet (local au hold point).")]
        [SerializeField] private Vector3 _brandishOffset = new Vector3(0f, 0.3f, 0f);
        [Tooltip("Vitesse de transition repos ↔ brandi.")]
        [SerializeField] private float _brandishSpeed = 8f;
        [Tooltip("Angle de rotation quand on brandit (180 = retournement).")]
        [SerializeField] private float _brandishAngle = 180f;

        [Header("References")]
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private PlayerInputReader _inputReader;
        [SerializeField] private CharacterController _characterController;

        private GrabbableObject _currentObject;
        private Rigidbody _heldRb;
        private float _brandishT; // 0 = repos, 1 = brandi
        private PlayerInventory _inventory;

        /// <summary>True si le joueur porte un objet grabbé.</summary>
        public bool IsHolding => _currentObject != null;
        public float CurrentHeldMass { get; private set; } = 0f;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!base.IsOwner)
                enabled = false;
            else
            {
                _inputReader.OnGrabToggleEvent += HandleGrabToggle;
                _inventory = GetComponent<PlayerInventory>();
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (base.IsOwner)
                _inputReader.OnGrabToggleEvent -= HandleGrabToggle;
        }

        // =====================================================================
        // Toggle Grab / Drop
        // =====================================================================

        private void HandleGrabToggle()
        {
            if (_currentObject == null)
                TryGrab();
            else
                Drop();
        }

        // =====================================================================
        // Brandish — Clic gauche lève et retourne l'objet
        // =====================================================================

        private void Update()
        {
            if (!base.IsOwner || _currentObject == null) return;

            var mouse = Mouse.current;
            bool wantBrandish = mouse != null && mouse.leftButton.isPressed;
            float target = wantBrandish ? 1f : 0f;
            _brandishT = Mathf.MoveTowards(_brandishT, target, _brandishSpeed * Time.deltaTime);
        }

        // =====================================================================
        // Physics Update — Déplace l'objet tenu vers le grab hold point
        // =====================================================================

        private void FixedUpdate()
        {
            if (!base.IsOwner || _currentObject == null || _heldRb == null) return;
            MoveObjectToHand();
        }

        private void MoveObjectToHand()
        {
            Transform hold = _grabHoldPoint;

            // 1. Position et rotation de base (repos)
            Vector3 targetPos = hold.position;
            Quaternion targetRot = hold.rotation;

            // 2. Offsets ItemData (position/rotation en main)
            ItemData data = _currentObject.GetData();
            if (data != null)
            {
                targetPos += (hold.right * data.HeldPositionOffset.x) +
                             (hold.up * data.HeldPositionOffset.y) +
                             (hold.forward * data.HeldPositionOffset.z);

                targetRot *= Quaternion.Euler(data.HeldRotationOffset);
            }

            // 3. Brandish — lerp position + rotation quand clic gauche
            if (_brandishT > 0.001f)
            {
                targetPos += (hold.right * _brandishOffset.x +
                              hold.up * _brandishOffset.y +
                              hold.forward * _brandishOffset.z) * _brandishT;

                targetRot *= Quaternion.Euler(0f, _brandishT * _brandishAngle, 0f);
            }

            // 4. Velocity Drive — pilotage par vélocité
            Vector3 direction = targetPos - _heldRb.position;
            float distance = direction.magnitude;

            if (distance > _breakDistance)
            {
                Drop();
                return;
            }

            Vector3 targetVelocity = direction * _followSpeed;
            _heldRb.linearVelocity = Vector3.ClampMagnitude(targetVelocity, _maxGrabVelocity);

            Quaternion rotationDiff = targetRot * Quaternion.Inverse(_heldRb.rotation);
            rotationDiff.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);

            if (float.IsInfinity(rotationAxis.x)) return;
            if (angleInDegrees > 180f) angleInDegrees -= 360f;

            Vector3 angularDisplacement = rotationAxis * (angleInDegrees * Mathf.Deg2Rad);
            _heldRb.angularVelocity = angularDisplacement * _rotateSpeed;
        }

        // =====================================================================
        // Grab — Raycast + demande au serveur
        // =====================================================================

        private void TryGrab()
        {
            Ray ray = new Ray(_cameraRoot.position, _cameraRoot.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _grabDistance, _grabLayer))
            {
                GrabbableObject grabbable = hit.collider.GetComponentInParent<GrabbableObject>();
                if (grabbable != null)
                {
                    CmdRequestGrab(grabbable);
                }
            }
        }

        [ServerRpc]
        private void CmdRequestGrab(GrabbableObject grabbable)
        {
            if (grabbable != null && grabbable.NetworkObject != null)
            {
                grabbable.NetworkObject.GiveOwnership(base.Owner);
                TargetGrabSuccess(base.Owner, grabbable);
            }
        }

        [TargetRpc]
        private void TargetGrabSuccess(NetworkConnection conn, GrabbableObject grabbable)
        {
            _currentObject = grabbable;
            _heldRb = _currentObject.GetRigidbody();

            ItemData data = _currentObject.GetData();
            CurrentHeldMass = (data != null) ? data.Mass : 5f;

            _currentObject.transform.SetParent(null);
            _currentObject.OnGrabbed(transform);

            _heldRb.linearVelocity = Vector3.zero;
            _heldRb.angularVelocity = Vector3.zero;
            _heldRb.position = _grabHoldPoint.position;
            _heldRb.rotation = _grabHoldPoint.rotation;

            Physics.SyncTransforms();
            ToggleCollisions(true);

            // Range automatiquement l'objet d'inventaire en main
            if (_inventory != null)
                _inventory.SetGrabActive(true);
        }

        // =====================================================================
        // Drop — Lâcher l'objet
        // =====================================================================

        private void Drop()
        {
            if (_currentObject == null) return;

            // 1. Réactiver les collisions immédiatement
            ToggleCollisions(false);

            // 2. Calcul de la position de sortie (devant le joueur, pas dans les pieds)
            float dropReach = 1.5f;
            Vector3 dropOrigin = _cameraRoot.position;
            Vector3 dropDirection = _cameraRoot.forward;
            Vector3 finalDropPos;

            int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

            if (Physics.Raycast(dropOrigin, dropDirection, out RaycastHit hit, dropReach, layerMask))
            {
                finalDropPos = hit.point - (dropDirection * 0.3f);
            }
            else
            {
                finalDropPos = dropOrigin + (dropDirection * dropReach);
            }

            // 3. Positionner et lâcher
            _heldRb.position = finalDropPos;
            _currentObject.OnDropped();

            if (_heldRb != null)
                _heldRb.AddForce(_cameraRoot.forward * _throwForce, ForceMode.Impulse);

            // 4. Reset
            _currentObject = null;
            _heldRb = null;
            CurrentHeldMass = 0f;
            _brandishT = 0f;

            // Restaure l'objet d'inventaire en main
            if (_inventory != null)
                _inventory.SetGrabActive(false);
        }

        // =====================================================================
        // Public API
        // =====================================================================

        public GrabbableObject GetHeldObject() => _currentObject;

        public void ForceDrop()
        {
            if (_currentObject != null) Drop();
        }

        // =====================================================================
        // Collision Management
        // =====================================================================

        private void ToggleCollisions(bool ignore)
        {
            if (_currentObject == null) return;

            Collider[] colliders = _currentObject.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                Physics.IgnoreCollision(_characterController, col, ignore);
            }
        }
    }
}
