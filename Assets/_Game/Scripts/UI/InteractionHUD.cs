using UnityEngine;
using TMPro; // N�cessaire pour le texte moderne

namespace VoidWarranty.UI
{
    public class InteractionHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _promptText;
        [SerializeField] private GameObject _crosshair; // Le petit point au centre

        private void Awake()
        {
            // Au d�marrage, on cache le texte
            if (_promptText != null) _promptText.text = "";
        }

        public void UpdatePrompt(string message)
        {
            if (_promptText == null) return;

            _promptText.text = message;

            // Optionnel : Agrandir le crosshair si on vise un truc interactif
            if (_crosshair != null)
            {
                bool isInteractable = !string.IsNullOrEmpty(message);
                _crosshair.transform.localScale = isInteractable ? Vector3.one * 1.5f : Vector3.one;
            }
        }
    }
}