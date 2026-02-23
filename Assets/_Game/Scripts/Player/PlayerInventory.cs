using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;

namespace VoidWarranty.Player
{
    /// <summary>
    /// Inventaire simple synchronisé. Les items sont identifiés par un string ID.
    /// TAB ouvre/ferme l'UI d'inventaire (via OnMissionToggleEvent rebranché).
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur le prefab Player (à côté de PlayerMovement)
    /// → Aucun champ Inspector
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        public static PlayerInventory LocalInstance { get; private set; }

        private readonly SyncList<string> _items = new();

        /// <summary>Fired client-side quand l'inventaire change (ajout ou retrait).</summary>
        public event Action OnInventoryChanged;

        /// <summary>Fired client-side quand TAB est pressé (toggle UI).</summary>
        public event Action OnToggleInventory;

        private PlayerInputReader _inputReader;

        public override void OnStartClient()
        {
            base.OnStartClient();

            _items.OnChange += OnItemsChanged;

            if (!base.IsOwner) return;

            LocalInstance = this;
            _inputReader = GetComponent<PlayerInputReader>();

            if (_inputReader != null)
                _inputReader.OnMissionToggleEvent += HandleToggleInventory;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            _items.OnChange -= OnItemsChanged;

            if (LocalInstance == this)
                LocalInstance = null;

            if (_inputReader != null)
                _inputReader.OnMissionToggleEvent -= HandleToggleInventory;
        }

        private void OnItemsChanged(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
        {
            if (asServer) return;
            OnInventoryChanged?.Invoke();
        }

        private void HandleToggleInventory()
        {
            OnToggleInventory?.Invoke();
        }

        // =====================================================================
        // API publique
        // =====================================================================

        public bool HasItem(string itemId)
        {
            return _items.Contains(itemId);
        }

        public int ItemCount => _items.Count;

        public IReadOnlyList<string> GetItems()
        {
            return _items.Collection;
        }

        /// <summary>
        /// Ajoute un item à l'inventaire. Appelé côté client, exécuté côté serveur.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdAddItem(string itemId)
        {
            if (_items.Contains(itemId)) return;
            _items.Add(itemId);
            Debug.Log($"[PlayerInventory] Item ajouté : {itemId} (joueur {OwnerId})");
        }

        /// <summary>
        /// Retire un item de l'inventaire. Appelé côté client, exécuté côté serveur.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdRemoveItem(string itemId)
        {
            _items.Remove(itemId);
            Debug.Log($"[PlayerInventory] Item retiré : {itemId} (joueur {OwnerId})");
        }
    }
}
