using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VoidWarranty.UI
{
    /// <summary>
    /// Slot individuel du hotbar. Affiche icône + numéro.
    /// Pas de Button — la sélection se fait au clavier/scroll.
    ///
    /// SETUP PREFAB (HotbarSlot) :
    /// HotbarSlot (RectTransform 80x80, Image fond sombre semi-transparent)
    ///   ├── SlotNumber (TMP, coin haut-gauche, petite police, ex: "1")
    ///   ├── Icon (Image, centré, ~48x48, désactivé si vide)
    ///   └── SelectBorder (Image, stretch, outline/glow, désactivé par défaut)
    /// </summary>
    public class HotbarSlot : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _slotNumber;
        [SerializeField] private Image _selectBorder;
        [SerializeField] private CanvasGroup _canvasGroup;

        public void Initialize(int slotIndex)
        {
            if (_slotNumber != null)
                _slotNumber.text = (slotIndex + 1).ToString();

            Clear();
            SetSelected(false);
        }

        public void SetItem(Sprite icon)
        {
            if (_icon == null) return;

            if (icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = true;
                _icon.preserveAspect = true;
            }
            else
            {
                _icon.enabled = false;
            }
        }

        public void Clear()
        {
            if (_icon != null)
                _icon.enabled = false;
        }

        public void SetSelected(bool selected)
        {
            if (_selectBorder != null)
                _selectBorder.enabled = selected;

            if (_canvasGroup != null)
                _canvasGroup.alpha = selected ? 1f : 0.6f;
        }
    }
}
