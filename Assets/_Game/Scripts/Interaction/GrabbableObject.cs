using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public class GrabbableObject : NetworkBehaviour, IInteractable
    {
        [SerializeField] protected ItemData _data;

        protected Rigidbody _rb;
        protected int _defaultLayer;

        public readonly SyncVar<bool> IsHeld = new SyncVar<bool>();

        // Suivi visuel non-owner : l'objet suit le hold point du joueur distant
        // au lieu du NetworkTransform (élimine le lag de double interpolation).
        private Transform _holderGrabPoint;
        private IGrabAction _action;

        // Sync du paramètre d'action (brandish T, etc.) — unreliable pour le perf
        private readonly SyncVar<float> _grabActionParam = new(new SyncTypeSettings(
            Channel.Unreliable));
        private float _lastSyncedActionParam;
        private float _localActionParam; // interpolation locale non-owner

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _defaultLayer = gameObject.layer;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (_data != null)
            {
                _rb.mass = _data.Mass;
                _rb.linearDamping = _data.LinearDamping;
                _rb.angularDamping = _data.AngularDamping;
            }

            IsHeld.OnChange += OnHeldChanged;
        }

        protected virtual void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.RegisterGrabbable(this);
        }

        private void OnDestroy()
        {
            IsHeld.OnChange -= OnHeldChanged;

            if (GameManager.Instance != null)
                GameManager.Instance.UnregisterGrabbable(this);
        }

        // La signature de la fonction reste la même : (AncienneValeur, NouvelleValeur, EstServeur)
        private void OnHeldChanged(bool oldValue, bool newValue, bool asServer)
        {
            if (newValue == true) // L'objet vient d'être attrapé
            {
                transform.SetParent(null);
                _action = GetComponent<IGrabAction>();

                if (!base.IsOwner)
                {
                    _rb.isKinematic = true;
                    _rb.useGravity = false;
                    _localActionParam = 0f;
                    CacheHolderGrabPoint();
                }
                else
                {
                    _rb.isKinematic = false;
                    _rb.useGravity = false;
                    _rb.linearDamping = 10f;
                    _rb.angularDamping = 10f;
                    _lastSyncedActionParam = 0f;
                    SetLayerRecursively(gameObject, LayerMask.NameToLayer("Ignore Raycast"));
                }
            }
            else // L'objet vient d'être lâché
            {
                _holderGrabPoint = null;
                _action = null;
                _rb.isKinematic = false;
                _rb.useGravity = true;
                SetLayerRecursively(gameObject, _defaultLayer);

                if (_data != null)
                {
                    _rb.linearDamping = _data.LinearDamping;
                    _rb.angularDamping = _data.AngularDamping;
                }
                else
                {
                    _rb.linearDamping = 0f;
                    _rb.angularDamping = 0.05f;
                }
            }
        }

        public virtual void OnGrabbed(Transform playerTransform)
        {
            // On modifie la .Value de la SyncVar
            if (base.IsServerInitialized)
            {
                IsHeld.Value = true;
            }
            else
            {
                ServerSetHeld(true);
            }
        }

        public virtual void OnDropped()
        {
            if (base.IsServerInitialized) 
            {
                IsHeld.Value = false;
            }
            else 
            {
                ServerSetHeld(false);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerSetHeld(bool state)
        {
            // Sur le serveur, on modifie la .Value
            IsHeld.Value = state;
        }

        // =====================================================================
        // Non-owner visual follow — l'objet colle au hold point du joueur distant.
        // Exécuté en LateUpdate (après le NetworkTransform) pour override la position.
        // =====================================================================

        private void CacheHolderGrabPoint()
        {
            _holderGrabPoint = null;
            var ownerConn = base.NetworkObject?.Owner;
            if (ownerConn != null && ownerConn.FirstObject != null)
            {
                var grab = ownerConn.FirstObject.GetComponent<PlayerGrab>();
                if (grab != null)
                    _holderGrabPoint = grab.GrabHoldPoint;
            }
        }

        private void LateUpdate()
        {
            if (!IsHeld.Value) return;

            // --- Owner : sync le paramètre d'action vers le serveur (throttlé) ---
            if (base.IsOwner)
            {
                if (_action != null)
                {
                    float p = _action.ReplicatedParam;
                    if (Mathf.Abs(p - _lastSyncedActionParam) > 0.02f)
                    {
                        _lastSyncedActionParam = p;
                        CmdSyncGrabActionParam(p);
                    }
                }
                return;
            }

            // --- Non-owner : suivi visuel du hold point distant ---
            if (_holderGrabPoint == null)
            {
                CacheHolderGrabPoint();
                if (_holderGrabPoint == null) return;
            }

            // Position/rotation = hold point + offsets ItemData
            Vector3 targetPos = _holderGrabPoint.position;
            Quaternion targetRot = _holderGrabPoint.rotation;

            if (_data != null)
            {
                targetPos += _holderGrabPoint.right * _data.HeldPositionOffset.x
                           + _holderGrabPoint.up * _data.HeldPositionOffset.y
                           + _holderGrabPoint.forward * _data.HeldPositionOffset.z;
                targetRot *= Quaternion.Euler(_data.HeldRotationOffset);
            }

            // Appliquer l'action (brandish) avec interpolation locale pour le smooth
            if (_action != null)
            {
                _localActionParam = Mathf.MoveTowards(
                    _localActionParam, _grabActionParam.Value, 12f * Time.deltaTime);
                _action.ReplicatedParam = _localActionParam;
                _action.ModifyHoldTarget(ref targetPos, ref targetRot, _holderGrabPoint);
            }

            transform.position = targetPos;
            transform.rotation = targetRot;
        }

        [ServerRpc]
        private void CmdSyncGrabActionParam(float value)
        {
            _grabActionParam.Value = value;
        }

        // =====================================================================

        public Rigidbody GetRigidbody() => _rb;
        public ItemData GetData() => _data;
        public virtual void Interact(GameObject interactor) { }

        private void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                if (child == null) continue;
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }

        public virtual string GetInteractionPrompt()
        {
            if (_data == null) return "???";
            string name = LocalizationManager.Get(_data.NameKey);
            string input = LocalizationManager.Get("INPUT_GRAB");
            string action = LocalizationManager.Get("ACTION_CARRY");
            return $"{name}\n<size=80%><color=yellow>{input} {action}</color></size>";
        }
    }
}