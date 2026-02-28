using UnityEngine;

namespace VoidWarranty.Core
{
    /// <summary>
    /// Donnees d'un item — un ScriptableObject par type d'item.
    /// Contient toutes les infos pour l'affichage hotbar, l'equip et le drop.
    ///
    /// SETUP EDITEUR :
    /// 1. Assets > Create > VoidWarranty > Item Data
    /// 2. Remplir les champs (ItemId doit matcher ItemPickup._itemId)
    /// 3. Placer dans Assets/_Game/Data/Items/
    /// 4. Ajouter la reference dans ItemRegistry (Resources/ItemRegistry.asset)
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "VoidWarranty/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("ID unique (ex: orange_badge) — doit matcher ItemPickup._itemId")]
        public string ItemId;

        [Header("Localization")]
        [Tooltip("Cle CSV pour le nom (ex: ITEM_ORANGE_BADGE_NAME)")]
        public string NameKey = "ITEM_NAME";
        [Tooltip("Cle CSV pour la description (ex: ITEM_ORANGE_BADGE_DESC)")]
        public string DescriptionKey = "ITEM_DESC";

        [Header("Hotbar")]
        [Tooltip("Icone affichee dans le hotbar")]
        public Sprite Icon;
        [Tooltip("Prefab a spawner quand l'item est droppe (doit avoir Rigidbody + ItemPickup)")]
        public GameObject DropPrefab;

        [Header("Position en Main (Offset)")]
        public Vector3 HeldPositionOffset = Vector3.zero;
        public Vector3 HeldRotationOffset = Vector3.zero;

        // =============================================================
        // Legacy (ancien jeu VoidWarranty — utilise par GrabbableObject,
        // PatientObject, Scanner, etc. Ne pas supprimer tant que ces
        // scripts existent.)
        // =============================================================
        [Header("Legacy")]
        public ItemType Type = ItemType.Generic;
        public bool IsDefective = false;
        public int ScrapValue = 10;
        public float Mass = 5f;
        public float LinearDamping = 0f;
        public float AngularDamping = 10f;
    }

    public enum ItemType
    {
        Generic,
        Motor,
        Fuse,
        Coolant,
        Toolbox,
        ToolboxAdvanced,
        Scanner
    }
}