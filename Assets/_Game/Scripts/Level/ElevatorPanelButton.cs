using UnityEngine;
using VoidWarranty.Core;

namespace SubSurface.Level
{
    /// <summary>
    /// Bouton intérieur de l'ascenseur — press E pour lancer la descente.
    /// Nécessite 2 joueurs dans la zone. Usage unique.
    ///
    /// SETUP ÉDITEUR :
    /// → GO enfant de Elevator_Cabin, Layer 6 (Interactable)
    /// → Collider sur enfant mesh (Layer 6 aussi)
    /// → Inspector : ref ElevatorController
    /// </summary>
    public class ElevatorPanelButton : MonoBehaviour, IInteractable
    {
        [Header("References")]
        [SerializeField] private ElevatorController _elevatorController;

        [Header("Prompts")]
        [SerializeField] private string _promptKeyDescend = "ACTION_START_DESCENT";
        [SerializeField] private string _promptKeyWaiting = "FEEDBACK_WAITING_PARTNER";

        [Header("Audio")]
        [SerializeField] private AudioClip _pressClip;
        [SerializeField] private AudioClip _deniedClip;

        private AudioSource _audioSource;
        private bool _activated;

        private void Awake()
        {
            _audioSource = GetComponentInChildren<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f;
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
                _audioSource.maxDistance = 10f;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (_elevatorController == null) return;
            if (_activated) return;
            if (!_elevatorController.IsUnlocked) return;
            if (_elevatorController.IsDescending || _elevatorController.HasArrived) return;

            if (_elevatorController.PlayersInZone < 2)
            {
                if (_audioSource != null && _deniedClip != null)
                    _audioSource.PlayOneShot(_deniedClip);
                return;
            }

            _activated = true;

            if (_audioSource != null && _pressClip != null)
                _audioSource.PlayOneShot(_pressClip);

            _elevatorController.CmdRequestDescent();
        }

        public string GetInteractionPrompt()
        {
            if (_elevatorController == null) return "";
            if (_activated) return "";
            if (!_elevatorController.IsUnlocked) return "";
            if (_elevatorController.IsDescending || _elevatorController.HasArrived) return "";

            if (_elevatorController.PlayersInZone < 2)
            {
                string waitText = LocalizationManager.Get(_promptKeyWaiting);
                return $"<size=80%><color=#666666>{waitText}</color></size>";
            }

            string input = LocalizationManager.Get("INPUT_PRESS");
            string action = LocalizationManager.Get(_promptKeyDescend);
            return $"<size=80%><color=yellow>{input} {action}</color></size>";
        }
    }
}
