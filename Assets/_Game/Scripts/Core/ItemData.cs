using UnityEngine;

namespace VoidWarranty.Core
{
    // Ajout de Toolbox et Scanner
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

    [CreateAssetMenu(fileName = "New Item", menuName = "VoidWarranty/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Cl�s de Traduction")]
        public string NameKey = "ITEM_NAME";
        public string DescriptionKey = "ITEM_DESC";

        [Header("Gameplay")]
        public ItemType Type = ItemType.Generic;
        public bool IsDefective = false;
        public int ScrapValue = 10;

        [Header("Physique")]
        public float Mass = 5f;
        public float LinearDamping = 0f;
        public float AngularDamping = 10f;

        // --- NOUVEAU : Positionnement en main ---
        [Header("Position en Main (Offset)")]
        // Par d�faut (0,0,0) = Sur le HoldPoint exact
        public Vector3 HeldPositionOffset = Vector3.zero;

        // Rotation ajout�e (ex: incliner le scanner vers le visage)
        public Vector3 HeldRotationOffset = Vector3.zero;
    }
}