using UnityEngine;

namespace VoidWarranty.Core
{
    /// <summary>
    /// Extension de IInteractable pour les interactions en maintien (hold E).
    /// PlayerInteraction détecte le type et route vers OnHoldStart/OnHoldRelease.
    /// </summary>
    public interface IHoldInteractable : IInteractable
    {
        void OnHoldStart(GameObject interactor);
        void OnHoldRelease(GameObject interactor);

        /// <summary>
        /// Durée requise en secondes. 0 = maintien indéfini (release manuel).
        /// </summary>
        float GetHoldDuration();

        bool IsHolding { get; }
    }
}
