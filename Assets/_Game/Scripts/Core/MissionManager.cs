using System;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using VoidWarranty.Interaction;
using VoidWarranty.UI;

namespace VoidWarranty.Core
{
    /// <summary>
    /// Gère la mission avec un système d'objectifs libre (Tarkov-like).
    /// Le joueur peut extract à tout moment via le camion.
    ///
    /// Objectifs :
    /// - Obligatoire : Réparer le patient (pour mission success + débloquer niveau suivant)
    /// - Optionnel : Ramener pièce défectueuse (bonus argent)
    /// - Optionnel : Ramener outils (gardés pour prochaine mission, sinon perdus)
    ///
    /// Extraction :
    /// - Success (patient réparé) : +Scrap + débloquer niveau suivant
    /// - Échec (patient pas réparé) : -Coût expédition
    /// </summary>
    public class MissionManager : NetworkBehaviour
    {
        // =====================================================================
        // Singleton
        // =====================================================================

        public static MissionManager Instance { get; private set; }

        // =====================================================================
        // Enums
        // =====================================================================

        public enum MissionState { Idle, Active, Extracted }
        public enum MissionOutcome { None, Success, Failure }

        // =====================================================================
        // Configuration
        // =====================================================================

        [Header("Mission active")]
        [SerializeField] private MissionData _currentMission;

        [Header("Pénalités")]
        [SerializeField] private int _failurePenalty = 50; // Coût de l'expédition ratée

        // =====================================================================
        // État (synchronisé sur le réseau)
        // =====================================================================

        public readonly SyncVar<int> State = new(); // MissionState
        public readonly SyncVar<int> Outcome = new(); // MissionOutcome
        public readonly SyncVar<float> TimeRemaining = new();

        // Objectifs trackés
        public readonly SyncVar<bool> PatientRepaired = new();
        public readonly SyncVar<bool> DefectivePartReturned = new();

        /// <summary>La mission active (lecture seule).</summary>
        public MissionData CurrentMission => _currentMission;

        // =====================================================================
        // Événements
        // =====================================================================

        public event Action OnMissionStarted;
        public event Action<MissionOutcome> OnMissionEnded;
        public event Action OnObjectivesChanged; // Quand un objectif est complété

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
                Debug.LogWarning("[MissionManager] GameManager introuvable.");
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
        // Démarrage de mission
        // =====================================================================

        [Server]
        public void StartMission(MissionData mission)
        {
            if (mission == null)
            {
                Debug.LogWarning("[MissionManager] Tentative de démarrer une mission null.");
                return;
            }

            _currentMission = mission;

            // Reset
            State.Value = (int)MissionState.Active;
            Outcome.Value = (int)MissionOutcome.None;
            TimeRemaining.Value = mission.TimeLimit;
            PatientRepaired.Value = false;
            DefectivePartReturned.Value = false;

            Debug.Log($"[MissionManager] Mission démarrée : {mission.NameKey}");

            ObserversNotifyMissionStarted();
        }

        [ObserversRpc]
        private void ObserversNotifyMissionStarted()
        {
            OnMissionStarted?.Invoke();
        }

        // =====================================================================
        // Handlers — Objectifs
        // =====================================================================

        private void HandlePatientRepaired(PatientObject patient)
        {
            if ((MissionState)State.Value != MissionState.Active) return;

            PatientRepaired.Value = true;
            Debug.Log("[MissionManager] Objectif complété : Patient réparé");

            ObserversNotifyObjectivesChanged();
        }

        private void HandlePartRecovered(ItemData data)
        {
            if ((MissionState)State.Value != MissionState.Active) return;

            DefectivePartReturned.Value = true;
            Debug.Log("[MissionManager] Objectif optionnel complété : Pièce défectueuse ramenée");

            ObserversNotifyObjectivesChanged();
        }

        [ObserversRpc]
        private void ObserversNotifyObjectivesChanged()
        {
            OnObjectivesChanged?.Invoke();
        }

        // =====================================================================
        // Extraction
        // =====================================================================

        /// <summary>
        /// Appelé par TruckZone quand le joueur clique sur le bouton d'extraction.
        /// Le joueur peut extract à tout moment.
        /// </summary>
        [Server]
        public void Extract()
        {
            if ((MissionState)State.Value != MissionState.Active)
            {
                Debug.LogWarning("[MissionManager] Tentative d'extraction mais mission pas active.");
                return;
            }

            State.Value = (int)MissionState.Extracted;

            // Calculer le résultat
            MissionOutcome outcome = PatientRepaired.Value ? MissionOutcome.Success : MissionOutcome.Failure;

            // Calculer les récompenses/pénalités
            int totalReward = CalculateReward(outcome);

            Debug.Log($"[MissionManager] Extraction ! Outcome: {outcome}, Récompense totale: {totalReward} scrap");

            EndMission(outcome, totalReward);
        }

        private int CalculateReward(MissionOutcome outcome)
        {
            int total = 0;

            if (outcome == MissionOutcome.Success)
            {
                // Mission réussie : récompense de base
                total += _currentMission.ScrapReward;
                Debug.Log($"[MissionManager] +{_currentMission.ScrapReward} scrap (mission success)");

                // Bonus pièce défectueuse ramenée
                if (DefectivePartReturned.Value)
                {
                    int bonus = _currentMission.DefectivePartBonus;
                    total += bonus;
                    Debug.Log($"[MissionManager] +{bonus} scrap (pièce défectueuse ramenée)");
                }
            }
            else
            {
                // Mission ratée : pénalité
                total -= _failurePenalty;
                Debug.Log($"[MissionManager] -{_failurePenalty} scrap (pénalité expédition ratée)");
            }

            return total;
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
                ForceExtractTimeout();
            }
        }

        [Server]
        private void ForceExtractTimeout()
        {
            Debug.Log("[MissionManager] Temps écoulé ! Extraction forcée.");
            Extract(); // Extraction automatique
        }

        // =====================================================================
        // Fin de mission
        // =====================================================================

        [Server]
        private void EndMission(MissionOutcome outcome, int totalReward)
        {
            Outcome.Value = (int)outcome;

            // TODO : Donner l'argent au joueur (futur système d'économie)
            // TODO : Débloquer le niveau suivant si success (futur système de progression)

            ObserversNotifyMissionEnded(outcome, totalReward);
        }

        [ObserversRpc]
        private void ObserversNotifyMissionEnded(MissionOutcome outcome, int totalReward)
        {
            OnMissionEnded?.Invoke(outcome);

            if (NotificationHUD.Instance != null && _currentMission != null)
            {
                string name = LocalizationManager.Get(_currentMission.NameKey);
                string status = outcome == MissionOutcome.Success
                    ? LocalizationManager.Get("MISSION_EXTRACTED_SUCCESS")
                    : LocalizationManager.Get("MISSION_EXTRACTED_FAILURE");

                string reward = totalReward >= 0
                    ? $"+{totalReward} {LocalizationManager.Get("CURRENCY_SCRAP")}"
                    : $"{totalReward} {LocalizationManager.Get("CURRENCY_SCRAP")}";

                NotificationHUD.Instance.Show($"{name} — {status}\n{reward}", 7f);
            }
        }

        // =====================================================================
        // Utilitaires publics
        // =====================================================================

        public MissionState GetState()
        {
            return (MissionState)State.Value;
        }

        public MissionOutcome GetOutcome()
        {
            return (MissionOutcome)Outcome.Value;
        }

        public bool IsPatientRepaired()
        {
            return PatientRepaired.Value;
        }

        public bool IsDefectivePartReturned()
        {
            return DefectivePartReturned.Value;
        }

    }
}
