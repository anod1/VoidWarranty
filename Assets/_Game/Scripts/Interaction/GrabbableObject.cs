using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing; // Indispensable
using VoidWarranty.Core;

namespace VoidWarranty.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public class GrabbableObject : NetworkBehaviour, IInteractable
    {
        [SerializeField] protected ItemData _data;

        protected Rigidbody _rb;
        protected int _defaultLayer;

        // --- CORRECTION MAJEURE ICI ---
        // On n'utilise plus [SyncVar] bool _isHeld.
        // On utilise la structure SyncVar<bool> qui est readonly.
        public readonly SyncVar<bool> IsHeld = new SyncVar<bool>();
        // ------------------------------

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

            // --- ABONNEMENT AU CHANGEMENT ---
            // C'est ici qu'on dit "Quand la valeur change, lance la fonction OnHeldChanged"
            IsHeld.OnChange += OnHeldChanged;
        }

        // On doit se désabonner proprement quand l'objet est détruit pour éviter les fuites de mémoire
        private void OnDestroy()
        {
            IsHeld.OnChange -= OnHeldChanged;
        }

        // La signature de la fonction reste la même : (AncienneValeur, NouvelleValeur, EstServeur)
        private void OnHeldChanged(bool oldValue, bool newValue, bool asServer)
        {
            if (newValue == true) // L'objet vient d'être attrapé
            {
                if (!base.IsOwner)
                {
                    _rb.isKinematic = true;
                    _rb.useGravity = false;
                }
                else
                {
                    _rb.isKinematic = false;
                    _rb.useGravity = false;
                    _rb.linearDamping = 10f;
                    _rb.angularDamping = 10f;
                }
                SetLayerRecursively(gameObject, LayerMask.NameToLayer("Ignore Raycast"));
            }
            else // L'objet vient d'être lâché
            {
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

        // --- Reste du script inchangé ---

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
            if (_data == null) return "Unknown Object";
            string name = LocalizationManager.Get(_data.NameKey);
            string action = LocalizationManager.Get("ACTION_TAKE");
            return $"{name}\n<size=80%><color=yellow>[{action}]</color></size>";
        }
    }
}