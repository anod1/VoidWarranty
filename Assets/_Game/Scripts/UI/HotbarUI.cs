using UnityEngine;
using TMPro;
using System.Collections.Generic;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.UI
{
    /// <summary>
    /// Hotbar Lethal Company-style — toujours visible en bas de l'écran.
    /// Pas de fullscreen, pas de blur, pas de freeze.
    ///
    /// SETUP ÉDITEUR :
    /// → Canvas Screen Space Overlay (même Canvas que le HUD ou séparé)
    /// → HotbarUI.cs sur un GO parent
    /// → _slotContainer : HorizontalLayoutGroup (bottom center, spacing 6)
    /// → _slotPrefab : HotbarSlot prefab
    /// → _selectedItemName : TMP texte sous le hotbar (centré, petite police)
    ///
    /// Hiérarchie recommandée :
    /// HotbarPanel (anchor bottom-center, padding 20px du bas)
    ///   ├── SlotContainer (HorizontalLayoutGroup)
    ///   │   └── (slots instanciés au Start)
    ///   └── SelectedItemName (TMP, sous SlotContainer)
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private TextMeshProUGUI _selectedItemName;

        private PlayerInventory _inventory;
        private readonly List<HotbarSlot> _slots = new();
        private bool _initialized;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Update()
        {
            if (_inventory != null) return;

            _inventory = PlayerInventory.LocalInstance;
            if (_inventory == null) return;

            _inventory.OnInventoryChanged += RefreshDisplay;
            _inventory.OnSelectedSlotChanged += RefreshDisplay;

            InitializeSlots();
            RefreshDisplay();
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= RefreshDisplay;
                _inventory.OnSelectedSlotChanged -= RefreshDisplay;
            }
        }

        // =====================================================================
        // Initialization
        // =====================================================================

        private void InitializeSlots()
        {
            if (_initialized) return;
            _initialized = true;

            if (_slotPrefab == null || _slotContainer == null) return;

            int slotCount = _inventory.HotbarSize;

            for (int i = 0; i < slotCount; i++)
            {
                GameObject slotGO = Instantiate(_slotPrefab, _slotContainer);
                slotGO.SetActive(true);

                HotbarSlot slot = slotGO.GetComponent<HotbarSlot>();
                if (slot != null)
                {
                    slot.Initialize(i);
                    _slots.Add(slot);
                }
            }
        }

        // =====================================================================
        // Display
        // =====================================================================

        private void RefreshDisplay()
        {
            if (_inventory == null) return;

            var registry = ItemRegistry.Instance;
            int selectedSlot = _inventory.SelectedSlot;
            string selectedName = "";

            for (int i = 0; i < _slots.Count; i++)
            {
                string itemId = _inventory.GetItemAtSlot(i);
                bool isSelected = (i == selectedSlot);

                if (!string.IsNullOrEmpty(itemId) && registry != null)
                {
                    var itemData = registry.GetItemData(itemId);
                    Sprite icon = itemData != null ? itemData.Icon : null;
                    _slots[i].SetItem(icon);

                    if (isSelected && itemData != null)
                        selectedName = GetLocalizedName(itemData);
                }
                else
                {
                    _slots[i].Clear();
                }

                _slots[i].SetSelected(isSelected);
            }

            if (_selectedItemName != null)
                _selectedItemName.text = selectedName;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private string GetLocalizedName(ItemData itemData)
        {
            if (!string.IsNullOrEmpty(itemData.NameKey))
            {
                string localized = LocalizationManager.Get(itemData.NameKey);
                if (!localized.StartsWith("["))
                    return localized;
            }

            // Fallback : title case du ItemId
            string[] parts = itemData.ItemId.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }
    }
}
