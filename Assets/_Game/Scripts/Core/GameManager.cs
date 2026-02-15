using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using VoidWarranty.Interaction;

namespace VoidWarranty.Core
{
    /// <summary>
    /// Registre central de la session.
    /// - Maintient les listes d'objets trackables (patients, grabbables)
    /// - Suit la progression de la session (pièces récupérées, patients réparés)
    /// - Les objets s'enregistrent/désenregistrent eux-mêmes via Register/Unregister
    ///
    /// Ce script ne gère PAS : le UI, l'audio, les missions (futures responsabilités séparées).
    /// </summary>
    public class GameManager : NetworkBehaviour
    {
        // =====================================================================
        // Singleton
        // =====================================================================

        public static GameManager Instance { get; private set; }

        // =====================================================================
        // Registres (server-side, lecture publique)
        // =====================================================================

        private readonly List<PatientObject> _patients = new();
        private readonly List<GrabbableObject> _grabbables = new();

        /// <summary>Tous les patients enregistrés dans la scène.</summary>
        public IReadOnlyList<PatientObject> Patients => _patients;

        /// <summary>Tous les objets grabbables enregistrés dans la scène.</summary>
        public IReadOnlyList<GrabbableObject> Grabbables => _grabbables;

        // =====================================================================
        // Progression de session (synchronisée sur le réseau)
        // =====================================================================

        public readonly SyncVar<int> PatientsRepaired = new();
        public readonly SyncVar<int> DefectivePartsRecovered = new();
        public readonly SyncVar<int> TotalPatients = new();

        // =====================================================================
        // Événements (pour le futur UI, audio, missions...)
        // =====================================================================

        /// <summary>Un patient vient d'être réparé.</summary>
        public event Action<PatientObject> OnPatientRepaired;

        /// <summary>Une pièce défectueuse a été récupérée dans le truck.</summary>
        public event Action<ItemData> OnDefectivePartRecovered;

        /// <summary>Tous les patients sont réparés.</summary>
        public event Action OnAllPatientsRepaired;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] Doublon détecté, destruction.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // =====================================================================
        // Enregistrement — les objets s'inscrivent eux-mêmes
        // =====================================================================

        public void RegisterPatient(PatientObject patient)
        {
            if (!_patients.Contains(patient))
            {
                _patients.Add(patient);

                if (IsServerInitialized)
                    TotalPatients.Value = _patients.Count;
            }
        }

        public void UnregisterPatient(PatientObject patient)
        {
            _patients.Remove(patient);

            if (IsServerInitialized)
                TotalPatients.Value = _patients.Count;
        }

        public void RegisterGrabbable(GrabbableObject grabbable)
        {
            if (!_grabbables.Contains(grabbable))
                _grabbables.Add(grabbable);
        }

        public void UnregisterGrabbable(GrabbableObject grabbable)
        {
            _grabbables.Remove(grabbable);
        }

        // =====================================================================
        // Notifications — appelées par les scripts concernés
        // =====================================================================

        /// <summary>Appelé par PatientObject quand il passe en état Repaired.</summary>
        [Server]
        public void NotifyPatientRepaired(PatientObject patient)
        {
            PatientsRepaired.Value++;
            OnPatientRepaired?.Invoke(patient);

            Debug.Log($"[GameManager] Patient réparé ({PatientsRepaired.Value}/{TotalPatients.Value})");

            if (PatientsRepaired.Value >= TotalPatients.Value && TotalPatients.Value > 0)
            {
                OnAllPatientsRepaired?.Invoke();
                Debug.Log("[GameManager] Tous les patients sont réparés !");
            }
        }

        /// <summary>Appelé par TruckZone quand une pièce défectueuse est validée.</summary>
        [Server]
        public void NotifyDefectivePartRecovered(ItemData data)
        {
            DefectivePartsRecovered.Value++;
            OnDefectivePartRecovered?.Invoke(data);

            Debug.Log($"[GameManager] Pièce défectueuse récupérée : {data.NameKey} (total: {DefectivePartsRecovered.Value})");
        }
    }
}
