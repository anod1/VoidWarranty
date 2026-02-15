using System;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using VoidWarranty.Interaction;
using VoidWarranty.UI;

namespace VoidWarranty.Core
{
    /// <summary>
    /// Gère la mission/contrat en cours.
    /// - S'abonne aux événements du GameManager (découplé)
    /// - Suit la progression des objectifs
    /// - Gère le timer si la mission a une limite de temps
    /// - Émet des événements pour le UI (futur)
    ///
    /// Ce script ne gère PAS : l'affichage UI, le choix de mission (futur menu).
    /// </summary>
    public class MissionManager : NetworkBehaviour
    {
        // =====================================================================
        // Singleton
        // =====================================================================

        public static MissionManager Instance { get; private set; }

        // =====================================================================
        // Configuration
        // =====================================================================

        [Header("Mission active")]
        [SerializeField] private MissionData _currentMission;

        // =====================================================================
        // État (synchronisé sur le réseau)
        // =====================================================================

        public enum MissionState { Idle, Active, Completed, Failed }

        public readonly SyncVar<int> State = new();
        public readonly SyncVar<float> TimeRemaining = new();
        public readonly SyncVar<int> PatientsRepaired = new();
        public readonly SyncVar<int> PartsRecovered = new();

        /// <summary>La mission active (lecture seule, côté client via les SyncVars).</summary>
        public MissionData CurrentMission => _currentMission;

        // =====================================================================
        // Événements (pour le futur UI)
        // =====================================================================

        public event Action<MissionData> OnMissionStarted;
        public event Action<MissionData> OnMissionCompleted;
        public event Action<MissionData> OnMissionFailed;
        public event Action OnObjectiveProgressChanged;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MissionManager] Doublon détecté, destruction.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            Unsubscribe();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Subscribe();

            // Si une mission est assignée dans l'Inspector, la démarrer automatiquement
            if (_currentMission != null)
                StartMission(_currentMission);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Unsubscribe();
        }

        private void Update()
        {
            if (!IsServerInitialized) return;
            if ((MissionState)State.Value != MissionState.Active) return;

            UpdateTimer();
        }

        // =====================================================================
        // Abonnement aux événements du GameManager
        // =====================================================================

        private void Subscribe()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[MissionManager] GameManager introuvable. Le MissionManager doit être chargé après le GameManager.");
                return;
            }

            GameManager.Instance.OnPatientRepaired += HandlePatientRepaired;
            GameManager.Instance.OnDefectivePartRecovered += HandlePartRecovered;
        }

        private void Unsubscribe()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnPatientRepaired -= HandlePatientRepaired;
            GameManager.Instance.OnDefectivePartRecovered -= HandlePartRecovered;
        }

        // =====================================================================
        // Démarrage / Arrêt de mission
        // =====================================================================

        /// <summary>Démarre une mission. Appelé côté serveur uniquement.</summary>
        [Server]
        public void StartMission(MissionData mission)
        {
            if (mission == null)
            {
                Debug.LogWarning("[MissionManager] Tentative de démarrer une mission null.");
                return;
            }

            _currentMission = mission;

            // Reset des compteurs
            PatientsRepaired.Value = 0;
            PartsRecovered.Value = 0;
            TimeRemaining.Value = mission.TimeLimit;
            State.Value = (int)MissionState.Active;

            Debug.Log($"[MissionManager] Mission démarrée : {mission.NameKey}");

            ObserversNotifyMissionStarted();
        }

        [ObserversRpc]
        private void ObserversNotifyMissionStarted()
        {
            OnMissionStarted?.Invoke(_currentMission);
        }

        // =====================================================================
        // Handlers — réagit aux événements du GameManager
        // =====================================================================

        private void HandlePatientRepaired(PatientObject patient)
        {
            if ((MissionState)State.Value != MissionState.Active) return;

            PatientsRepaired.Value++;
            OnObjectiveProgressChanged?.Invoke();

            Debug.Log($"[MissionManager] Objectif patients : {PatientsRepaired.Value}/{_currentMission.RequiredPatientsRepaired}");

            CheckCompletion();
        }

        private void HandlePartRecovered(ItemData data)
        {
            if ((MissionState)State.Value != MissionState.Active) return;

            PartsRecovered.Value++;
            OnObjectiveProgressChanged?.Invoke();

            Debug.Log($"[MissionManager] Objectif pièces : {PartsRecovered.Value}/{_currentMission.RequiredDefectivePartsRecovered}");

            CheckCompletion();
        }

        // =====================================================================
        // Timer
        // =====================================================================

        private void UpdateTimer()
        {
            if (_currentMission == null || _currentMission.TimeLimit <= 0f) return;

            TimeRemaining.Value -= Time.deltaTime;

            if (TimeRemaining.Value <= 0f)
            {
                TimeRemaining.Value = 0f;
                FailMission();
            }
        }

        // =====================================================================
        // Vérification de complétion
        // =====================================================================

        [Server]
        private void CheckCompletion()
        {
            if (_currentMission == null) return;

            bool patientsOk = _currentMission.RequiredPatientsRepaired <= 0
                              || PatientsRepaired.Value >= _currentMission.RequiredPatientsRepaired;

            bool partsOk = _currentMission.RequiredDefectivePartsRecovered <= 0
                           || PartsRecovered.Value >= _currentMission.RequiredDefectivePartsRecovered;

            if (patientsOk && partsOk)
                CompleteMission();
        }

        [Server]
        private void CompleteMission()
        {
            State.Value = (int)MissionState.Completed;

            Debug.Log($"[MissionManager] Mission complétée : {_currentMission.NameKey} — Récompense : {_currentMission.ScrapReward} scrap");

            ObserversNotifyMissionCompleted();
        }

        [Server]
        private void FailMission()
        {
            State.Value = (int)MissionState.Failed;

            Debug.Log($"[MissionManager] Mission échouée : {_currentMission.NameKey} — Temps écoulé !");

            ObserversNotifyMissionFailed();
        }

        [ObserversRpc]
        private void ObserversNotifyMissionCompleted()
        {
            OnMissionCompleted?.Invoke(_currentMission);

            if (NotificationHUD.Instance != null && _currentMission != null)
            {
                string name = LocalizationManager.Get(_currentMission.NameKey);
                NotificationHUD.Instance.Show($"{name} — {LocalizationManager.Get("MISSION_COMPLETED")}", 5f);
            }
        }

        [ObserversRpc]
        private void ObserversNotifyMissionFailed()
        {
            OnMissionFailed?.Invoke(_currentMission);

            if (NotificationHUD.Instance != null && _currentMission != null)
            {
                string name = LocalizationManager.Get(_currentMission.NameKey);
                NotificationHUD.Instance.Show($"{name} — {LocalizationManager.Get("MISSION_FAILED")}", 5f);
            }
        }

        // =====================================================================
        // Utilitaires publics (pour le futur UI)
        // =====================================================================

        /// <summary>Progression des patients (0 à 1).</summary>
        public float GetPatientsProgress()
        {
            if (_currentMission == null || _currentMission.RequiredPatientsRepaired <= 0) return 1f;
            return (float)PatientsRepaired.Value / _currentMission.RequiredPatientsRepaired;
        }

        /// <summary>Progression des pièces récupérées (0 à 1).</summary>
        public float GetPartsProgress()
        {
            if (_currentMission == null || _currentMission.RequiredDefectivePartsRecovered <= 0) return 1f;
            return (float)PartsRecovered.Value / _currentMission.RequiredDefectivePartsRecovered;
        }
    }
}
