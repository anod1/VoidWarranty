using UnityEngine;
using TMPro;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.UI
{
    /// <summary>
    /// Affiche les objectifs de mission (système Tarkov-like).
    /// Toggle via Input System (MissionToggle action = Tab).
    /// Banners Completed/Failed : affichage PERMANENT quand la mission est finie.
    /// </summary>
    public class MissionHUD : MonoBehaviour
    {
        [Header("Mission Panel (CanvasGroup)")]
        [SerializeField] private CanvasGroup _missionPanel;
        [SerializeField] private TextMeshProUGUI _missionTitle;
        [SerializeField] private TextMeshProUGUI _currentStepText; // Renommé → _objectivesText dans l'usage
        [SerializeField] private TextMeshProUGUI _timerText;

        [Header("Banners — CanvasGroups SÉPARÉS du panel")]
        [SerializeField] private CanvasGroup _completedBanner;
        [SerializeField] private CanvasGroup _failedBanner;

        [Header("Affichage")]
        [SerializeField] private float _displayDuration = 5f;
        [SerializeField] private float _fadeSpeed = 2f;

        [Header("Timer Warning")]
        [SerializeField] private float _timerWarningThreshold = 30f;
        [SerializeField] private Color _timerNormalColor = Color.white;
        [SerializeField] private Color _timerWarningColor = Color.red;

        // State
        private float _panelShowTimer;
        private bool _subscribedToMission;
        private PlayerInputReader _subscribedReader; // Garde trace du reader actuel

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Start()
        {
            SetAlpha(_missionPanel, 0f);
            SetAlpha(_completedBanner, 0f);
            SetAlpha(_failedBanner, 0f);

            TrySubscribeToMission();
        }

        private void OnDestroy()
        {
            UnsubscribeFromMission();
            UnsubscribeFromInput();
        }

        private void Update()
        {
            if (!_subscribedToMission)
                TrySubscribeToMission();

            // Vérifier si le LocalInstance a changé (nouveau joueur spawné)
            // Ne pas unsub si LocalInstance est temporairement null (évite de perdre le reader)
            var currentLocal = PlayerInputReader.LocalInstance;
            if (currentLocal != null && _subscribedReader != currentLocal)
            {
                UnsubscribeFromInput();
                TrySubscribeToInput();
            }
            else if (_subscribedReader == null && currentLocal != null)
            {
                TrySubscribeToInput();
            }

            // Polling de l'étape SyncVar (filet de sécurité)
            PollMissionStep();

            UpdatePanelFade();

            // Refresh dynamique si le panel est visible
            if (_missionPanel != null && _missionPanel.alpha > 0f)
                RefreshDynamic();
        }

        // =====================================================================
        // Subscribe — MissionManager (events)
        // =====================================================================

        private void TrySubscribeToMission()
        {
            if (_subscribedToMission) return;
            if (MissionManager.Instance == null) return;

            MissionManager.Instance.OnObjectivesChanged += HandleObjectivesChanged;
            MissionManager.Instance.OnMissionEnded += HandleMissionEnded;
            _subscribedToMission = true;

            // Rattraper l'état si la mission a déjà démarré
            var state = MissionManager.Instance.GetState();
            if (state == MissionManager.MissionState.Active)
            {
                RefreshStatic();
                RefreshDynamic();
                ShowPanel();
            }
        }

        private void UnsubscribeFromMission()
        {
            if (!_subscribedToMission || MissionManager.Instance == null) return;

            MissionManager.Instance.OnObjectivesChanged -= HandleObjectivesChanged;
            MissionManager.Instance.OnMissionEnded -= HandleMissionEnded;
            _subscribedToMission = false;
        }

        // =====================================================================
        // Subscribe — Input (PlayerInputReader local)
        // =====================================================================

        private void TrySubscribeToInput()
        {
            // Utiliser le singleton PlayerInputReader.LocalInstance
            if (PlayerInputReader.LocalInstance == null)
            {
                // Pas encore de joueur local spawné
                return;
            }

            // Se subscribe au nouveau LocalInstance
            _subscribedReader = PlayerInputReader.LocalInstance;
            _subscribedReader.OnMissionToggleEvent += HandleMissionToggle;
        }

        private void UnsubscribeFromInput()
        {
            if (_subscribedReader == null) return;

            _subscribedReader.OnMissionToggleEvent -= HandleMissionToggle;
            _subscribedReader = null;
        }

        // =====================================================================
        // Polling SyncVar (filet de sécurité)
        // =====================================================================

        private void PollMissionStep()
        {
            if (MissionManager.Instance == null) return;

            var currentState = MissionManager.Instance.GetState();

            // Mission terminée : afficher le banner approprié
            if (currentState == MissionManager.MissionState.Extracted)
            {
                var outcome = MissionManager.Instance.GetOutcome();
                if (outcome == MissionManager.MissionOutcome.Success)
                    ShowBannerPermanent(_completedBanner);
                else if (outcome == MissionManager.MissionOutcome.Failure)
                    ShowBannerPermanent(_failedBanner);
            }
        }

        // =====================================================================
        // Handlers
        // =====================================================================

        private void HandleObjectivesChanged()
        {
            RefreshDynamic();
        }

        private void HandleMissionEnded(MissionManager.MissionOutcome outcome)
        {
            // Affichage PERMANENT du banner
            if (outcome == MissionManager.MissionOutcome.Success)
                ShowBannerPermanent(_completedBanner);
            else
                ShowBannerPermanent(_failedBanner);
        }

        private void HandleMissionToggle()
        {
            // Toggle : si visible on force le fade, sinon on show
            if (_missionPanel != null && _missionPanel.alpha > 0.5f)
            {
                _panelShowTimer = 0f; // Force le fade out
            }
            else
            {
                RefreshDynamic();
                ShowPanel();
            }
        }

        // =====================================================================
        // Panel
        // =====================================================================

        private void ShowPanel()
        {
            _panelShowTimer = _displayDuration;
            SetAlpha(_missionPanel, 1f);
        }

        private void UpdatePanelFade()
        {
            if (_missionPanel == null) return;

            if (_panelShowTimer > 0f)
            {
                _panelShowTimer -= Time.deltaTime;
            }
            else if (_missionPanel.alpha > 0f)
            {
                _missionPanel.alpha = Mathf.MoveTowards(_missionPanel.alpha, 0f, Time.deltaTime * _fadeSpeed);
            }
        }

        // =====================================================================
        // Refresh
        // =====================================================================

        /// <summary>Titre (ne change pas pendant la mission).</summary>
        private void RefreshStatic()
        {
            var mm = MissionManager.Instance;
            if (mm == null || mm.CurrentMission == null) return;

            if (_missionTitle != null)
                _missionTitle.text = LocalizationManager.Get(mm.CurrentMission.NameKey);
        }

        /// <summary>Objectifs + timer (change chaque frame).</summary>
        private void RefreshDynamic()
        {
            var mm = MissionManager.Instance;
            if (mm == null || mm.CurrentMission == null) return;

            var mission = mm.CurrentMission;

            // Affichage des objectifs
            if (_currentStepText != null)
            {
                string objectives = BuildObjectivesText(mm);
                _currentStepText.text = objectives;
            }

            // Timer
            if (_timerText != null)
            {
                if (mission.TimeLimit > 0f)
                {
                    float time = mm.TimeRemaining.Value;
                    int minutes = Mathf.FloorToInt(time / 60f);
                    int seconds = Mathf.FloorToInt(time % 60f);

                    _timerText.text = $"{minutes:00}:{seconds:00}";
                    _timerText.color = time <= _timerWarningThreshold ? _timerWarningColor : _timerNormalColor;
                    _timerText.gameObject.SetActive(true);
                }
                else
                {
                    _timerText.gameObject.SetActive(false);
                }
            }
        }

        private string BuildObjectivesText(MissionManager mm)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{LocalizationManager.Get("HUD_OBJECTIVES")}</b>");

            // Objectif principal : Réparer le patient
            string patientIcon = mm.IsPatientRepaired() ? "✓" : "○";
            sb.AppendLine($"{patientIcon} {LocalizationManager.Get("OBJECTIVE_REPAIR_PATIENT")}");

            // Objectifs optionnels
            sb.AppendLine($"\n<b>{LocalizationManager.Get("HUD_OPTIONAL_OBJECTIVES")}</b>");

            string partIcon = mm.IsDefectivePartReturned() ? "✓" : "○";
            sb.AppendLine($"{partIcon} {LocalizationManager.Get("OBJECTIVE_RETURN_PART")}");

            return sb.ToString();
        }

        // =====================================================================
        // Banners
        // =====================================================================

        /// <summary>Affiche un banner de façon PERMANENTE (ne fade jamais).</summary>
        private void ShowBannerPermanent(CanvasGroup banner)
        {
            if (banner == null) return;

            SetAlpha(banner, 1f);
            banner.blocksRaycasts = true;
            banner.interactable = true;
        }

        // =====================================================================
        // Utilitaires
        // =====================================================================

        private void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group == null) return;
            group.alpha = alpha;
        }
    }
}
