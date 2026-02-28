using UnityEngine;
using VoidWarranty.Core;
using VoidWarranty.Player;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Lecteur de badge individuel — composant enfant de SimultaneousBadge.
    /// IHoldInteractable : le joueur maintient E pour scanner son badge.
    /// Vérifie que le joueur TIENT le badge en main (slot sélectionné).
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur chaque GO lecteur enfant ("ReaderA", "ReaderB")
    /// → Layer 6 (Interactable) + Collider
    /// → Inspector : glisser le SimultaneousBadge parent, définir _readerIndex (0 ou 1)
    /// → Inspector : _requiredItemId = "orange_badge" (ou autre selon l'item requis)
    /// </summary>
    public class BadgeReader : MonoBehaviour, IHoldInteractable
    {
        [Header("Configuration")]
        [SerializeField] private SimultaneousBadge _parentBadgeSystem;
        [SerializeField] private int _readerIndex;
        [SerializeField] private string _requiredItemId = "orange_badge";

        private bool _isHolding;

        public bool IsHolding => _isHolding;

        public void Interact(GameObject interactor)
        {
            OnHoldStart(interactor);
        }

        public string GetInteractionPrompt()
        {
            if (_parentBadgeSystem == null) return "";

            PlayerInventory inventory = PlayerInventory.LocalInstance;
            if (inventory == null) return "";

            if (inventory.EquippedItemId != _requiredItemId)
                return $"<size=80%><color=#666666>{LocalizationManager.Get("FEEDBACK_BADGE_NEEDED")}</color></size>";

            string hold = LocalizationManager.Get("INPUT_HOLD");

            if (_isHolding)
                return $"<size=80%><color=#00FF00>{hold} {LocalizationManager.Get("ACTION_SCANNING")}</color></size>";

            return $"<size=80%><color=yellow>{hold} {LocalizationManager.Get("ACTION_SCAN_BADGE")}</color></size>";
        }

        public void OnHoldStart(GameObject interactor)
        {
            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null || inventory.EquippedItemId != _requiredItemId) return;

            _isHolding = true;

            if (_parentBadgeSystem != null)
                _parentBadgeSystem.NotifyReaderHoldStart(_readerIndex);
        }

        public void OnHoldRelease(GameObject interactor)
        {
            if (!_isHolding) return;

            _isHolding = false;

            if (_parentBadgeSystem != null)
                _parentBadgeSystem.NotifyReaderHoldRelease(_readerIndex);
        }

        public float GetHoldDuration() => 0f;
    }
}
