using UnityEngine;
using TMPro;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.UI
{
    /// <summary>
    /// Affiche l'état de la mission en cours : titre, description, objectifs, timer.
    /// Toggle via Input System (MissionToggle action) → apparaît X secondes puis fade out.
    /// S'affiche aussi automatiquement au démarrage et lors d'un changement d'objectif.
    ///
    /// Les banners completed/failed sont des CanvasGroups SÉPARÉS du mission panel.
    /// Ils doivent être des siblings (pas enfants) du mission panel dans le HUD Canvas.
    /// </summary>
    public class MissionHUD : MonoBehaviour
    {
        [Header("Mission Panel (CanvasGroup)")]
        [SerializeField] private CanvasGroup _missionPanel;
        [SerializeField] private TextMeshProUGUI _missionTitle;
        [SerializeField] private TextMeshProUGUI _missionDescription;
        [SerializeField] private TextMeshProUGUI _objectivesText;
        [SerializeField] private TextMeshProUGUI _timerText;

        [Header("Banners — CanvasGroups SÉPARÉS du panel")]
        [SerializeField] private CanvasGroup _completedBanner;
        [SerializeField] private CanvasGroup _failedBanner;
        [SerializeField] private float _bannerDuration = 5f;

        [Header("Affichage")]
        [SerializeField] private float _displayDuration = 5f;
        [SerializeField] private float _fadeSpeed = 2f;

        [Header("Timer Warning")]
        [SerializeField] private float _timerWarningThreshold = 30f;
        [SerializeField] private Color _timerNormalColor = Color.white;
        [SerializeField] private Color _timerWarningColor = Color.red;

        // State
        private float _panelShowTimer;
        private float _bannerShowTimer;
        private CanvasGroup _activeBanner;
        private bool _subscribedToMission;
        private bool _subscribedToInput;
        private MissionManager.MissionState _lastKnownState;

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

            if (!_subscribedToInput)
                TrySubscribeToInput();

            // Polling de l'état SyncVar (filet de sécurité si on rate l'event)
            PollMissionState();

            UpdatePanelFade();
            UpdateBannerFade();

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

            MissionManager.Instance.OnMissionStarted += HandleMissionStarted;
            MissionManager.Instance.OnMissionCompleted += HandleMissionCompleted;
            MissionManager.Instance.OnMissionFailed += HandleMissionFailed;
            MissionManager.Instance.OnObjectiveProgressChanged += HandleProgressChanged;
            _subscribedToMission = true;

            // Rattraper l'état si la mission a déjà démarré
            var state = (MissionManager.MissionState)MissionManager.Instance.State.Value;
            if (state == MissionManager.MissionState.Active)
            {
                RefreshStatic();
                RefreshDynamic();
                ShowPanel();
            }

            _lastKnownState = state;
        }

        private void UnsubscribeFromMission()
        {
            if (!_subscribedToMission || MissionManager.Instance == null) return;

            MissionManager.Instance.OnMissionStarted -= HandleMissionStarted;
            MissionManager.Instance.OnMissionCompleted -= HandleMissionCompleted;
            MissionManager.Instance.OnMissionFailed -= HandleMissionFailed;
            MissionManager.Instance.OnObjectiveProgressChanged -= HandleProgressChanged;
            _subscribedToMission = false;
        }

        // =====================================================================
        // Subscribe — Input (PlayerInputReader local)
        // =====================================================================

        private void TrySubscribeToInput()
        {
            if (_subscribedToInput) return;

            // Chercher le PlayerInputReader local (celui qui est enabled = owner)
            var readers = FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None);
            foreach (var reader in readers)
            {
                if (reader.enabled)
                {
                    reader.OnMissionToggleEvent += HandleMissionToggle;
                    _subscribedToInput = true;
                    return;
                }
            }
        }

        private void UnsubscribeFromInput()
        {
            if (!_subscribedToInput) return;

            var readers = FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None);
            foreach (var reader in readers)
            {
                reader.OnMissionToggleEvent -= HandleMissionToggle;
            }

            _subscribedToInput = false;
        }

        // =====================================================================
        // Polling SyncVar (filet de sécurité)
        // =====================================================================

        private void PollMissionState()
        {
            if (MissionManager.Instance == null) return;

            var currentState = (MissionManager.MissionState)MissionManager.Instance.State.Value;
            if (currentState == _lastKnownState) return;

            // L'état a changé sans qu'on ait reçu l'event (race condition réseau)
            var mm = MissionManager.Instance;
            switch (currentState)
            {
                case MissionManager.MissionState.Active:
                    RefreshStatic();
                    RefreshDynamic();
                    ShowPanel();
                    break;

                case MissionManager.MissionState.Completed:
                    RefreshDynamic();
                    ShowPanel();
                    ShowBanner(_completedBanner);
                    break;

                case MissionManager.MissionState.Failed:
                    ShowBanner(_failedBanner);
                    break;
            }

            _lastKnownState = currentState;
        }

        // =====================================================================
        // Handlers
        // =====================================================================

        private void HandleMissionStarted(MissionData mission)
        {
            SetAlpha(_completedBanner, 0f);
            SetAlpha(_failedBanner, 0f);
            _activeBanner = null;

            RefreshStatic();
            RefreshDynamic();
            ShowPanel();

            _lastKnownState = MissionManager.MissionState.Active;
        }

        private void HandleMissionCompleted(MissionData mission)
        {
            // Dernier refresh avec tout coché
            RefreshDynamic();
            ShowPanel();
            ShowBanner(_completedBanner);

            _lastKnownState = MissionManager.MissionState.Completed;
        }

        private void HandleMissionFailed(MissionData mission)
        {
            ShowBanner(_failedBanner);
            _lastKnownState = MissionManager.MissionState.Failed;
        }

        private void HandleProgressChanged()
        {
            RefreshDynamic();
            ShowPanel();
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

        /// <summary>Titre + description (ne change pas pendant la mission).</summary>
        private void RefreshStatic()
        {
            var mm = MissionManager.Instance;
            if (mm == null || mm.CurrentMission == null) return;

            if (_missionTitle != null)
                _missionTitle.text = LocalizationManager.Get(mm.CurrentMission.NameKey);

            if (_missionDescription != null)
                _missionDescription.text = LocalizationManager.Get(mm.CurrentMission.DescriptionKey);
        }

        /// <summary>Objectifs + timer (change chaque frame).</summary>
        private void RefreshDynamic()
        {
            var mm = MissionManager.Instance;
            if (mm == null || mm.CurrentMission == null) return;

            var mission = mm.CurrentMission;

            // Objectifs
            if (_objectivesText != null)
            {
                string objectives = "";

                if (mission.RequiredPatientsRepaired > 0)
                {
                    bool done = mm.PatientsRepaired.Value >= mission.RequiredPatientsRepaired;
                    string check = done ? "<color=#00FF00>\u2713</color>" : "\u25CB";
                    objectives += $"{check} {LocalizationManager.Get("HUD_PATIENTS_REPAIRED")} : {mm.PatientsRepaired.Value}/{mission.RequiredPatientsRepaired}\n";
                }

                if (mission.RequiredDefectivePartsRecovered > 0)
                {
                    bool done = mm.PartsRecovered.Value >= mission.RequiredDefectivePartsRecovered;
                    string check = done ? "<color=#00FF00>\u2713</color>" : "\u25CB";
                    objectives += $"{check} {LocalizationManager.Get("HUD_PARTS_RECOVERED")} : {mm.PartsRecovered.Value}/{mission.RequiredDefectivePartsRecovered}\n";
                }

                _objectivesText.text = objectives.TrimEnd('\n');
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

        // =====================================================================
        // Banners
        // =====================================================================

        private void ShowBanner(CanvasGroup banner)
        {
            if (banner == null) return;

            _activeBanner = banner;
            banner.alpha = 1f;
            _bannerShowTimer = _bannerDuration;
        }

        private void UpdateBannerFade()
        {
            if (_activeBanner == null || _activeBanner.alpha <= 0f) return;

            if (_bannerShowTimer > 0f)
            {
                _bannerShowTimer -= Time.deltaTime;
            }
            else
            {
                _activeBanner.alpha = Mathf.MoveTowards(_activeBanner.alpha, 0f, Time.deltaTime * _fadeSpeed);
                if (_activeBanner.alpha <= 0f)
                    _activeBanner = null;
            }
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
