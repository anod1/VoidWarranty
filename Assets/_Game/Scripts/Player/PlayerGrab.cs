using UnityEngine;
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

        [Header("References")]
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private Transform _holdPoint;
        [SerializeField] private PlayerInputReader _inputReader;
        [SerializeField] private CharacterController _characterController;

        private GrabbableObject _currentObject;
        private Rigidbody _heldRb;

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
                _inputReader.OnGrabToggleEvent += HandleGrabToggle;
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
        // Physics Update — Déplace l'objet tenu vers la main
        // =====================================================================

        private void FixedUpdate()
        {
            if (!base.IsOwner || _currentObject == null || _heldRb == null) return;
            MoveObjectToHand();
        }

        private void MoveObjectToHand()
        {
            // 1. Calcul de la position cible avec l'offset de l'ItemData
            Vector3 targetPos = _holdPoint.position;
            Quaternion targetRot = _holdPoint.rotation;

            ItemData data = _currentObject.GetData();
            if (data != null)
            {
                targetPos += (_holdPoint.right * data.HeldPositionOffset.x) +
                             (_holdPoint.up * data.HeldPositionOffset.y) +
                             (_holdPoint.forward * data.HeldPositionOffset.z);

                targetRot *= Quaternion.Euler(data.HeldRotationOffset);
            }

            // 2. Velocity Drive — pilotage par vélocité
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
                // GetComponentInParent : cherche sur le collider touché PUIS remonte la hiérarchie
                // Nécessaire pour les compound colliders (colliders sur les enfants)
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
            _heldRb.position = _holdPoint.position;
            _heldRb.rotation = _holdPoint.rotation;

            Physics.SyncTransforms();
            ToggleCollisions(true);
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
                // Mur ou sol devant → poser juste avant l'impact
                finalDropPos = hit.point - (dropDirection * 0.3f);
            }
            else
            {
                // Rien devant → lâcher à bout de bras
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
