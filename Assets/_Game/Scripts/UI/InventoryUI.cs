using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using VoidWarranty.Core;
using VoidWarranty.Player;
using System.Collections.Generic;

namespace VoidWarranty.UI
{
    /// <summary>
    /// UI d'inventaire — TAB pour toggle.
    /// Blur via Volume (Depth of Field), freeze mouvement, cursor unlock.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private Transform _itemListContainer;
        [SerializeField] private TextMeshProUGUI _emptyText;

        [Header("Item Display")]
        [SerializeField] private GameObject _itemEntryPrefab;

        [Header("Blur")]
        [SerializeField] private Volume _blurVolume;

        private PlayerInventory _inventory;
        private PlayerMovement _playerMovement;
        private bool _isOpen;
        private readonly List<GameObject> _spawnedEntries = new();

        private void Update()
        {
            if (_inventory != null) return;

            _inventory = PlayerInventory.LocalInstance;
            if (_inventory == null) return;

            _inventory.OnToggleInventory += ToggleInventory;
            _inventory.OnInventoryChanged += RefreshDisplay;
            _playerMovement = _inventory.GetComponent<PlayerMovement>();
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnToggleInventory -= ToggleInventory;
                _inventory.OnInventoryChanged -= RefreshDisplay;
            }
        }

        private void ToggleInventory()
        {
            _isOpen = !_isOpen;

            if (_inventoryPanel != null)
                _inventoryPanel.SetActive(_isOpen);

            if (_isOpen)
                RefreshDisplay();

            // Blur
            if (_blurVolume != null)
                _blurVolume.weight = _isOpen ? 1f : 0f;

            // Cursor
            Cursor.lockState = _isOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _isOpen;

            // Freeze mouvement
            if (_playerMovement != null)
            {
                if (_isOpen)
                    _playerMovement.FreezeMovement();
                else
                    _playerMovement.UnfreezeMovement();
            }
        }

        private void RefreshDisplay()
        {
            if (_inventory == null) return;

            foreach (var entry in _spawnedEntries)
            {
                if (entry != null) Destroy(entry);
            }
            _spawnedEntries.Clear();

            var items = _inventory.GetItems();

            if (_emptyText != null)
            {
                _emptyText.gameObject.SetActive(items.Count == 0);
                _emptyText.text = LocalizationManager.Get("UI_INVENTORY_EMPTY");
            }

            foreach (string itemId in items)
            {
                if (_itemEntryPrefab == null || _itemListContainer == null) continue;

                GameObject entry = Instantiate(_itemEntryPrefab, _itemListContainer);
                entry.SetActive(true);

                TextMeshProUGUI label = entry.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = FormatItemName(itemId);

                _spawnedEntries.Add(entry);
            }
        }

        private string FormatItemName(string itemId)
        {
            string key = "ITEM_" + itemId.ToUpper() + "_NAME";
            string localized = LocalizationManager.Get(key);

            if (!localized.StartsWith("["))
                return localized;

            // Fallback title case
            string[] parts = itemId.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }
    }
}
