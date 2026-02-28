using UnityEngine;
using System.Collections.Generic;

namespace VoidWarranty.Core
{
    /// <summary>
    /// Registre centralisé des items — lookup itemId → ItemData SO.
    /// Chargé automatiquement depuis Resources/ItemRegistry.
    ///
    /// SETUP ÉDITEUR :
    /// 1. Assets > Create > VoidWarranty > Item Registry
    /// 2. Glisser les ItemData SO dans la liste Items
    /// 3. Placer l'asset dans Assets/_Game/Resources/ItemRegistry.asset
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "VoidWarranty/Item Registry")]
    public class ItemRegistry : ScriptableObject
    {
        [SerializeField] private List<ItemData> _items = new();

        private Dictionary<string, ItemData> _lookup;

        private static ItemRegistry _instance;
        private static bool _warnedMissing;

        public static ItemRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ItemRegistry>("ItemRegistry");
                    if (_instance == null && !_warnedMissing)
                    {
                        _warnedMissing = true;
                        Debug.LogWarning("[ItemRegistry] Aucun ItemRegistry dans Resources/. " +
                            "Créer : Assets > Create > VoidWarranty > Item Registry, " +
                            "puis placer dans Assets/_Game/Resources/ItemRegistry.asset");
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Retourne l'ItemData pour un item ID, ou null si inconnu.
        /// </summary>
        public ItemData GetItemData(string itemId)
        {
            if (_lookup == null)
                BuildLookup();

            if (string.IsNullOrEmpty(itemId)) return null;
            _lookup.TryGetValue(itemId, out ItemData data);
            return data;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, ItemData>();
            foreach (var data in _items)
            {
                if (data != null && !string.IsNullOrEmpty(data.ItemId))
                    _lookup[data.ItemId] = data;
            }
        }

        private void OnEnable()
        {
            _lookup = null;
        }
    }
}
