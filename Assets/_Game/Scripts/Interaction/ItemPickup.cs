using UnityEngine;
using FishNet.Object;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Objet ramassable qui va dans l'inventaire du joueur.
    /// Press E → item ajouté à l'inventaire, objet despawn du réseau.
    /// Au drop, PlayerInventory.SpawnDroppedItem crée une instance fraîche du prefab.
    ///
    /// SETUP ÉDITEUR :
    /// → GO parent : NetworkObject + ItemPickup + Rigidbody (isKinematic=true), Layer 6
    /// → Enfants : meshes/colliders, Layer 6
    /// → Inspector : _itemId (string unique, ex: "orange_badge")
    /// </summary>
    public class ItemPickup : NetworkBehaviour, IInteractable
    {
        [Header("Item")]
        [SerializeField] private string _itemId = "orange_badge";
        [SerializeField] private string _promptKey = "ACTION_PICKUP";

        [Header("Audio")]
        [SerializeField] private AudioClip _pickupClip;

        private bool _used;

        public void Interact(GameObject interactor)
        {
            // Guard client-side contre double-press
            if (_used) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"[ItemPickup] PlayerInventory introuvable sur {interactor.name}");
                return;
            }

            if (inventory.IsFull) return;

            _used = true;

            // Audio côté client (PlayClipAtPoint crée un GO temporaire, survit au Despawn)
            if (_pickupClip != null)
                AudioSource.PlayClipAtPoint(_pickupClip, transform.position);

            inventory.CmdAddItem(_itemId);
            CmdPickup();
        }

        public string GetInteractionPrompt()
        {
            if (_used) return "";
            string input = LocalizationManager.Get("INPUT_PRESS");
            string action = LocalizationManager.Get(_promptKey);
            return $"<size=80%><color=yellow>{input} {action}</color></size>";
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdPickup()
        {
            Debug.Log($"[ItemPickup] {_itemId} ramassé → despawn.");
            ServerManager.Despawn(gameObject);
        }
    }
}
