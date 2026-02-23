using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Objet ramassable qui va dans l'inventaire du joueur.
    /// Générique : badge, clé, document, etc.
    /// Le joueur vise l'objet, voit le prompt, appuie E → item ajouté, objet disparaît.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur le GO de l'item (badge, clé, etc.)
    /// → Layer 6 (Interactable) + Collider + NetworkObject
    /// → Inspector : _itemId (string unique, ex: "orange_badge"), _itemName (affiché UI)
    /// → Le corps du chercheur est un GO séparé sans script — juste du décor
    /// </summary>
    public class ItemPickup : NetworkBehaviour, IInteractable
    {
        [Header("Item")]
        [SerializeField] private string _itemId = "orange_badge";
        [SerializeField] private string _itemNameKey = "ITEM_ORANGE_BADGE_NAME";
        [SerializeField] private string _promptKey = "ACTION_PICKUP";

        [Header("Visuals")]
        [SerializeField] private GameObject _visualRoot;

        [Header("Audio")]
        [SerializeField] private AudioClip _pickupClip;

        private readonly SyncVar<bool> _pickedUp = new();

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _pickedUp.OnChange += OnPickedUpChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Late-joiner : cacher si déjà ramassé
            if (_pickedUp.Value)
                HideVisuals();
        }

        private void OnPickedUpChanged(bool prev, bool next, bool asServer)
        {
            if (asServer) return;
            if (next) HideVisuals();
        }

        public void Interact(GameObject interactor)
        {
            if (_pickedUp.Value) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"[ItemPickup] PlayerInventory introuvable sur {interactor.name} ! Ajouter PlayerInventory au prefab Player.");
                return;
            }

            inventory.CmdAddItem(_itemId);
            CmdPickup();
        }

        public string GetInteractionPrompt()
        {
            if (_pickedUp.Value) return "";
            string input = LocalizationManager.Get("INPUT_PRESS");
            string action = LocalizationManager.Get(_promptKey);
            return $"<size=80%><color=yellow>{input} {action}</color></size>";
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdPickup()
        {
            if (_pickedUp.Value) return;
            _pickedUp.Value = true;
            Debug.Log($"[ItemPickup] {_itemId} ramassé.");
        }

        private void HideVisuals()
        {
            if (_visualRoot != null)
                _visualRoot.SetActive(false);
            else
            {
                // Désactiver renderer + collider
                Renderer rend = GetComponentInChildren<Renderer>();
                if (rend != null) rend.enabled = false;

                Collider col = GetComponentInChildren<Collider>();
                if (col != null) col.enabled = false;
            }

            // Audio pickup
            if (_pickupClip != null)
            {
                AudioSource.PlayClipAtPoint(_pickupClip, transform.position);
            }
        }
    }
}
