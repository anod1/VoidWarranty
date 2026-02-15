using UnityEngine;

namespace VoidWarranty.Core
{
    /// <summary>
    /// Définition d'un contrat / mission.
    /// Créé via l'Inspector : Create > VoidWarranty > Mission Data.
    /// Chaque mission a des objectifs chiffrés que le MissionManager vérifie.
    /// </summary>
    [CreateAssetMenu(fileName = "New Mission", menuName = "VoidWarranty/Mission Data")]
    public class MissionData : ScriptableObject
    {
        [Header("Identité")]
        public string NameKey = "MISSION_NAME";
        public string DescriptionKey = "MISSION_DESC";

        [Header("Objectifs")]
        [Tooltip("Nombre de patients à réparer pour compléter la mission (0 = pas d'objectif)")]
        public int RequiredPatientsRepaired;

        [Tooltip("Nombre de pièces défectueuses à ramener au truck (0 = pas d'objectif)")]
        public int RequiredDefectivePartsRecovered;

        [Header("Récompense")]
        public int ScrapReward = 100;

        [Header("Difficulté")]
        [Tooltip("Temps limite en secondes (0 = pas de limite)")]
        public float TimeLimit;
    }
}
