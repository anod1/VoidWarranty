using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VoidWarranty.Core;
using VoidWarranty.Player;
using SubSurface.Puzzle;

namespace SubSurface.UI
{
    /// <summary>
    /// UI diegétique sur le pupitre en salle de commandes.
    /// 3 jauges verticales (pression actuelle + marqueur cible).
    /// Lean-in caméra quand CommandRoom interagit.
    ///
    /// SETUP ÉDITEUR :
    /// → Canvas World Space sur le mesh moniteur du pupitre
    /// → PuzzleConsoleUI.cs sur le Canvas
    /// → Inspector : 3 Image fill (jauges), 3 RectTransform (marqueurs cible)
    /// → Inspector : 3 TMP labels (valeurs), PuzzleManager ref
    /// → Inspector : Transform _leanInPoint (position caméra close-up)
    /// → GO parent "PupitreInteractable" : Collider Layer 6
    ///   avec PupitreInteraction.cs (IInteractable séparé) pour le lean-in
    /// </summary>
    public class PuzzleConsoleUI : MonoBehaviour
    {
        [Header("Jauges (Image fill, Filled Vertical)")]
        [SerializeField] private Image _gaugeRed;
        [SerializeField] private Image _gaugeGreen;
        [SerializeField] private Image _gaugeBlue;

        [Header("Marqueurs cible (RectTransform, position Y)")]
        [SerializeField] private RectTransform _targetMarkerRed;
        [SerializeField] private RectTransform _targetMarkerGreen;
        [SerializeField] private RectTransform _targetMarkerBlue;

        [Header("Labels valeur")]
        [SerializeField] private TextMeshProUGUI _labelRed;
        [SerializeField] private TextMeshProUGUI _labelGreen;
        [SerializeField] private TextMeshProUGUI _labelBlue;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI _statusText;

        private PuzzleManager _puzzle;
        private bool _initialized;

        private void Update()
        {
            if (_puzzle == null)
            {
                _puzzle = PuzzleManager.Instance;
                if (_puzzle == null) return;
                InitializeTargetMarkers();
                _initialized = true;
            }

            UpdateGauges();
            UpdateStatus();
        }

        private void InitializeTargetMarkers()
        {
            SetTargetMarkerPosition(_targetMarkerRed, _puzzle.GetTarget(0), PuzzleManager.GetCapacity(0));
            SetTargetMarkerPosition(_targetMarkerGreen, _puzzle.GetTarget(1), PuzzleManager.GetCapacity(1));
            SetTargetMarkerPosition(_targetMarkerBlue, _puzzle.GetTarget(2), PuzzleManager.GetCapacity(2));
        }

        private void SetTargetMarkerPosition(RectTransform marker, int target, int capacity)
        {
            if (marker == null || capacity == 0) return;

            // Le marqueur est enfant du conteneur de jauge.
            // Ancré en bas (pivot Y=0.5), on positionne en Y proportionnellement.
            RectTransform parent = marker.parent as RectTransform;
            if (parent == null) return;

            float normalized = (float)target / capacity;
            float parentHeight = parent.rect.height;

            marker.anchorMin = new Vector2(0f, 0f);
            marker.anchorMax = new Vector2(1f, 0f);
            marker.sizeDelta = new Vector2(0f, marker.sizeDelta.y);
            marker.anchoredPosition = new Vector2(0f, normalized * parentHeight);
        }

        private void UpdateGauges()
        {
            UpdateGauge(_gaugeRed, _labelRed, 0);
            UpdateGauge(_gaugeGreen, _labelGreen, 1);
            UpdateGauge(_gaugeBlue, _labelBlue, 2);
        }

        private void UpdateGauge(Image gauge, TextMeshProUGUI label, int chamber)
        {
            int pressure = _puzzle.GetPressure(chamber);
            int capacity = PuzzleManager.GetCapacity(chamber);
            int target = _puzzle.GetTarget(chamber);

            if (gauge != null)
            {
                float targetFill = capacity > 0 ? (float)pressure / capacity : 0f;
                gauge.fillAmount = Mathf.Lerp(gauge.fillAmount, targetFill, Time.deltaTime * 8f);
            }

            if (label != null)
            {
                string name = PuzzleManager.GetChamberName(chamber);
                bool atTarget = pressure == target;
                string color = atTarget ? "#00FF00" : "#FFFFFF";
                label.text = $"<color={color}>{name}\n{pressure}/{capacity}</color>";
            }
        }

        private void UpdateStatus()
        {
            if (_statusText == null) return;

            if (_puzzle.IsSolved)
                _statusText.text = $"<color=#00FF00>{LocalizationManager.Get("PUZZLE_BALANCE_COMPLETE")}</color>";
            else
                _statusText.text = LocalizationManager.Get("PUZZLE_BALANCE_PROGRESS");
        }
    }
}
