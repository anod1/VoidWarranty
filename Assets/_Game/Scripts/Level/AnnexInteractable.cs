using UnityEngine;
using VoidWarranty.Core;

namespace SubSurface.Level
{
    /// <summary>
    /// Interactable pour les salles annexes (O2 / Électricité).
    /// Pas de restriction de rôle — c'est le level design qui contraint l'accès.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur chaque GO interactable dans les salles annexes
    /// → Layer 6 (Interactable) + Collider
    /// → Inspector : ref AnnexActivation, systemIndex (0=O2, 1=Electricité), actionLabel
    /// </summary>
    public class AnnexInteractable : MonoBehaviour, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private AnnexActivation _manager;
        [SerializeField] private int _systemIndex; // 0 = O2, 1 = Electricité

        [Header("Prompts")]
        [SerializeField] private string _promptKey = "ACTION_ACTIVATE_SYSTEM";

        private bool _activated;

        public void Interact(GameObject interactor)
        {
            if (_activated) return;
            if (_manager == null) return;

            _manager.CmdActivateSystem(_systemIndex);
            _activated = true;
        }

        public string GetInteractionPrompt()
        {
            if (_activated || _manager == null) return "";

            if (_manager.IsSystemActive(_systemIndex))
            {
                _activated = true;
                return "";
            }

            string input = LocalizationManager.Get("INPUT_PRESS");
            string action = LocalizationManager.Get(_promptKey);
            return $"<size=80%><color=yellow>{input} {action}</color></size>";
        }
    }
}
