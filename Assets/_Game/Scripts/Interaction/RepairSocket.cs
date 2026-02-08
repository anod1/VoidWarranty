using UnityEngine;
using FishNet.Object;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.Interaction
{
    public class RepairSocket : NetworkBehaviour, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private ItemType _requiredType;
        [SerializeField] private Transform _socketPoint;

        [Header("Initial State")]
        [SerializeField] private GrabbableObject _startingItem;

        private GrabbableObject _installedItem;

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_startingItem != null)
            {
                GameObject instance = Instantiate(_startingItem.gameObject, _socketPoint.position, _socketPoint.rotation);
                base.Spawn(instance);
                GrabbableObject newItem = instance.GetComponent<GrabbableObject>();
                AttachItem(newItem);
            }
        }

        public void Interact(GameObject interactor)
        {
            PlayerGrab playerHand = interactor.GetComponent<PlayerGrab>();
            if (playerHand == null) return;
            CmdHandleInteraction(playerHand);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdHandleInteraction(PlayerGrab playerHand)
        {
            GrabbableObject handItem = playerHand.GetHeldObject();
            ItemData handData = handItem != null ? handItem.GetData() : null;

            // CAS 1 : INSTALLATION (Machine VIDE + Main PLEINE)
            if (_installedItem == null && handItem != null)
            {
                if (handData != null && handData.Type == _requiredType)
                {
                    playerHand.ForceDrop();
                    AttachItem(handItem);
                    Debug.Log("[SOCKET] Installation reussie.");
                }
                else
                {
                    Debug.LogWarning($"[SOCKET] Mauvais type. Attendu: {_requiredType}, Recu: {handData?.Type}");
                }
            }
            // CAS 2 : RETRAIT (Machine PLEINE + Main VIDE)
            else if (_installedItem != null && handItem == null)
            {
                Debug.Log("[SOCKET] Ejection de la piece.");
                DetachItem();
            }
            // CAS 3 : REFUS STRICT (Machine PLEINE + Main PLEINE)
            else if (_installedItem != null && handItem != null)
            {
                Debug.Log("[SOCKET] ACTION INTERDITE : Retirez d'abord la piece actuelle !");
            }
        }

        private void AttachItem(GrabbableObject item)
        {
            _installedItem = item;

            Rigidbody rb = item.GetRigidbody();
            if (rb != null) rb.isKinematic = true;

            item.transform.position = _socketPoint.position;
            item.transform.rotation = _socketPoint.rotation;
            item.transform.SetParent(_socketPoint);

            SetLayerRecursively(item.gameObject, LayerMask.NameToLayer("Ignore Raycast"));
        }

        private void DetachItem()
        {
            if (_installedItem == null) return;

            GrabbableObject item = _installedItem;

            Rigidbody rb = item.GetRigidbody();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForce(transform.forward * 2f + Vector3.up, ForceMode.Impulse);
            }

            item.transform.SetParent(null);

            SetLayerRecursively(item.gameObject, LayerMask.NameToLayer("Interactable"));

            _installedItem = null;
        }

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

        public string GetInteractionPrompt()
        {
            string key = (_installedItem == null) ? "ACTION_INSERT" : "ACTION_REMOVE";
            return LocalizationManager.Get(key);
        }
    }
}