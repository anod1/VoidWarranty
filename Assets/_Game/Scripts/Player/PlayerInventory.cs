using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using VoidWarranty.Core;
using VoidWarranty.Interaction;

namespace VoidWarranty.Player
{
    /// <summary>
    /// Inventaire hotbar synchronisé — style Lethal Company.
    /// 4 slots, scroll/1-2-3-4 pour sélectionner, G pour drop.
    /// L'item au slot sélectionné est tenu en main.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur le prefab Player (à côté de PlayerMovement)
    /// → Inspector : _holdPoint = Transform enfant de la caméra (socket pour item équipé)
    /// → Inspector : _dropForce = force de lancer en avant
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        public static PlayerInventory LocalInstance { get; private set; }

        [Header("Hotbar")]
        [Tooltip("Nombre de slots hotbar")]
        [SerializeField] private int _hotbarSize = 4;
        [Tooltip("Transform enfant de la caméra où l'item équipé apparaît")]
        [SerializeField] private Transform _holdPoint;
        [Tooltip("Force appliquée en avant quand on drop un item")]
        [SerializeField] private float _dropForce = 3f;

        private readonly SyncList<string> _items = new();
        private readonly SyncVar<int> _selectedSlot = new();

        /// <summary>Fired client-side quand l'inventaire change (ajout, retrait).</summary>
        public event Action OnInventoryChanged;

        /// <summary>Fired client-side quand le slot sélectionné change.</summary>
        public event Action OnSelectedSlotChanged;

        private PlayerInputReader _inputReader;
        private readonly Dictionary<string, GameObject> _heldVisualCache = new();
        private string _activeVisualId;

        // =====================================================================
        // Public accessors
        // =====================================================================

        public int HotbarSize => _hotbarSize;
        public int SelectedSlot => _selectedSlot.Value;
        public int ItemCount => _items.Count;
        public bool IsFull => _items.Count >= _hotbarSize;

        /// <summary>L'ID de l'item au slot sélectionné (ou "" si vide).</summary>
        public string EquippedItemId
        {
            get
            {
                int idx = _selectedSlot.Value;
                if (idx >= 0 && idx < _items.Count)
                    return _items[idx];
                return "";
            }
        }

        /// <summary>Retourne l'item ID au slot donné, ou "" si vide.</summary>
        public string GetItemAtSlot(int index)
        {
            if (index < 0 || index >= _items.Count) return "";
            return _items[index];
        }

        /// <summary>Vérifie si un item est dans l'inventaire (utilisé par BadgeReader).</summary>
        public bool HasItem(string itemId)
        {
            return _items.Contains(itemId);
        }

        public IReadOnlyList<string> GetItems()
        {
            return _items.Collection;
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        public override void OnStartClient()
        {
            base.OnStartClient();

            _items.OnChange += OnItemsChanged;
            _selectedSlot.OnChange += OnSelectedSlotChanged_Callback;

            if (!base.IsOwner) return;

            LocalInstance = this;
            _inputReader = GetComponent<PlayerInputReader>();

            if (_inputReader != null)
            {
                _inputReader.OnHotbarSlotEvent += HandleSlotSelect;
                _inputReader.OnHotbarScrollEvent += HandleHotbarScroll;
                _inputReader.OnDropEvent += HandleDrop;
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            _items.OnChange -= OnItemsChanged;
            _selectedSlot.OnChange -= OnSelectedSlotChanged_Callback;

            ClearAllVisuals();

            if (LocalInstance == this)
                LocalInstance = null;

            if (_inputReader != null)
            {
                _inputReader.OnHotbarSlotEvent -= HandleSlotSelect;
                _inputReader.OnHotbarScrollEvent -= HandleHotbarScroll;
                _inputReader.OnDropEvent -= HandleDrop;
            }
        }

        // =====================================================================
        // SyncVar callbacks
        // =====================================================================

        private void OnItemsChanged(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
        {
            if (asServer) return;
            OnInventoryChanged?.Invoke();
            RefreshHeldVisual();
        }

        private void OnSelectedSlotChanged_Callback(int prev, int next, bool asServer)
        {
            if (asServer) return;
            OnSelectedSlotChanged?.Invoke();
            RefreshHeldVisual();
        }

        // =====================================================================
        // Input handlers (owner only)
        // =====================================================================

        private void HandleSlotSelect(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _hotbarSize) return;
            CmdSelectSlot(slotIndex);
        }

        private void HandleHotbarScroll(float scrollDelta)
        {
            int dir = scrollDelta > 0 ? -1 : 1;
            int newSlot = (_selectedSlot.Value + dir + _hotbarSize) % _hotbarSize;
            CmdSelectSlot(newSlot);
        }

        private void HandleDrop()
        {
            CmdDropSelectedItem();
        }

        // =====================================================================
        // RPCs — Add / Remove
        // =====================================================================

        [ServerRpc(RequireOwnership = false)]
        public void CmdAddItem(string itemId)
        {
            if (_items.Contains(itemId)) return;
            if (_items.Count >= _hotbarSize)
            {
                Debug.Log($"[PlayerInventory] Hotbar plein ({_hotbarSize}) — {itemId} refusé (joueur {OwnerId})");
                return;
            }
            _items.Add(itemId);
            Debug.Log($"[PlayerInventory] Item ajouté : {itemId} (joueur {OwnerId})");
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdRemoveItem(string itemId)
        {
            _items.Remove(itemId);
            ClampSelectedSlot();
            Debug.Log($"[PlayerInventory] Item retiré : {itemId} (joueur {OwnerId})");
        }

        // =====================================================================
        // RPCs — Select / Drop
        // =====================================================================

        [ServerRpc]
        private void CmdSelectSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _hotbarSize) return;
            _selectedSlot.Value = slotIndex;
        }

        [ServerRpc]
        private void CmdDropSelectedItem()
        {
            int idx = _selectedSlot.Value;
            if (idx < 0 || idx >= _items.Count) return;

            string itemId = _items[idx];
            if (string.IsNullOrEmpty(itemId)) return;

            _items.RemoveAt(idx);
            ClampSelectedSlot();
            SpawnDroppedItem(itemId);

            Debug.Log($"[PlayerInventory] Item droppé : {itemId} (joueur {OwnerId})");
        }

        [Server]
        private void ClampSelectedSlot()
        {
            if (_items.Count == 0) return;
            if (_selectedSlot.Value >= _items.Count)
                _selectedSlot.Value = _items.Count - 1;
        }

        // =====================================================================
        // Drop spawn (server)
        // =====================================================================

        [Server]
        private void SpawnDroppedItem(string itemId)
        {
            var registry = ItemRegistry.Instance;
            if (registry == null) return;

            var itemData = registry.GetItemData(itemId);
            if (itemData == null || itemData.DropPrefab == null)
            {
                Debug.LogWarning($"[PlayerInventory] Pas de DropPrefab pour '{itemId}' — item perdu.");
                return;
            }

            Transform t = transform;
            Vector3 spawnPos = t.position + t.forward * 1.5f + Vector3.up * 0.5f;

            GameObject dropped = Instantiate(itemData.DropPrefab, spawnPos, Quaternion.identity);
            ServerManager.Spawn(dropped);

            // Activer la physique (le prefab est kinematic par défaut pour rester fixe en scène)
            Rigidbody rb = dropped.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.AddForce(t.forward * _dropForce, ForceMode.VelocityChange);
            }
        }

        // =====================================================================
        // Held visual cache (client-side — purement cosmétique)
        // Les visuels sont créés une seule fois par item et activés/désactivés
        // selon le slot sélectionné. Pas de Destroy/Instantiate à chaque switch.
        // =====================================================================

        private void RefreshHeldVisual()
        {
            string itemId = EquippedItemId;

            // Même item déjà actif → rien à faire
            if (_activeVisualId == itemId) return;

            // Désactiver l'ancien visuel
            if (!string.IsNullOrEmpty(_activeVisualId) && _heldVisualCache.TryGetValue(_activeVisualId, out var oldVisual))
                oldVisual.SetActive(false);

            _activeVisualId = itemId;

            // Rien à afficher
            if (string.IsNullOrEmpty(itemId))
                return;

            // Activer depuis le cache ou créer
            if (_heldVisualCache.TryGetValue(itemId, out var cached))
            {
                cached.SetActive(true);
            }
            else
            {
                CreateHeldVisual(itemId);
            }

            // Nettoyer les visuels d'items qui ne sont plus dans l'inventaire
            PurgeStaleVisuals();
        }

        private void CreateHeldVisual(string itemId)
        {
            if (_holdPoint == null) return;

            var registry = ItemRegistry.Instance;
            if (registry == null) return;

            var itemData = registry.GetItemData(itemId);
            if (itemData == null || itemData.DropPrefab == null) return;

            var visual = new GameObject($"Held_{itemId}");
            visual.transform.SetParent(_holdPoint, false);

            foreach (Transform child in itemData.DropPrefab.transform)
            {
                GameObject clone = Instantiate(child.gameObject, visual.transform);
                clone.SetActive(true);
            }

            foreach (Collider col in visual.GetComponentsInChildren<Collider>())
                col.enabled = false;
            SetLayerRecursive(visual, 2);

            var offset = visual.AddComponent<HeldItemOffset>();
            offset.Position = itemData.HeldPositionOffset;
            offset.Rotation = itemData.HeldRotationOffset;

            _heldVisualCache[itemId] = visual;
        }

        /// <summary>
        /// Supprime les visuels cachés dont l'item n'est plus dans l'inventaire.
        /// Appelé après chaque refresh pour éviter les fuites mémoire.
        /// </summary>
        private void PurgeStaleVisuals()
        {
            // Collecter les clés à supprimer (pas de modif pendant itération)
            List<string> stale = null;
            foreach (var kvp in _heldVisualCache)
            {
                if (!_items.Contains(kvp.Key))
                {
                    stale ??= new List<string>();
                    stale.Add(kvp.Key);
                }
            }

            if (stale == null) return;
            foreach (string key in stale)
            {
                if (_heldVisualCache.TryGetValue(key, out var visual))
                    Destroy(visual);
                _heldVisualCache.Remove(key);
            }
        }

        /// <summary>Détruit tous les visuels cachés (cleanup).</summary>
        private void ClearAllVisuals()
        {
            foreach (var kvp in _heldVisualCache)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            _heldVisualCache.Clear();
            _activeVisualId = null;
        }

        private void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
