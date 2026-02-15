using UnityEngine;

namespace VoidWarranty.Core
{
    /// <summary>
    /// Définition d'un contrat / mission.
    /// Créé via l'Inspector : Create > VoidWarranty > Mission Data.
    /// Système Tarkov-like : extraction libre, objectifs optionnels sauf réparer le patient.
    /// </summary>
    [CreateAssetMenu(fileName = "New Mission", menuName = "VoidWarranty/Mission Data")]
    public class MissionData : ScriptableObject
    {
        [Header("Identité")]
        public string NameKey = "MISSION_NAME";
        public string DescriptionKey = "MISSION_DESC";

        [Header("Récompenses")]
        [Tooltip("Récompense de base si le patient est réparé (objectif principal)")]
        public int ScrapReward = 100;

        [Tooltip("Bonus supplémentaire si la pièce défectueuse est ramenée au camion")]
        public int DefectivePartBonus = 50;

        [Header("Difficulté")]
        [Tooltip("Temps limite en secondes (0 = pas de limite)")]
        public float TimeLimit;
    }
}
