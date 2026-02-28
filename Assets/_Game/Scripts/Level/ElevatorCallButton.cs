using UnityEngine;
using VoidWarranty.Core;
using VoidWarranty.Interaction;

namespace SubSurface.Level
{
    /// <summary>
    /// Bouton extérieur de l'ascenseur — press E pour ouvrir les portes.
    /// Ne fonctionne que si les systèmes O2 + Elec sont activés.
    ///
    /// SETUP ÉDITEUR :
    /// → GO enfant de Elevator_Cabin, Layer 6 (Interactable)
    /// → Collider sur enfant mesh (Layer 6 aussi)
    /// → Inspector : ref ElevatorController
    /// </summary>
    public class ElevatorCallButton : MonoBehaviour, IInteractable
    {
        [Header("References")]
        [SerializeField] private ElevatorController _elevatorController;

        [Header("Prompts")]
        [SerializeField] private string _promptKeyOpen = "ACTION_CALL_ELEVATOR";
        [SerializeField] private string _promptKeyLocked = "FEEDBACK_ELEVATOR_LOCKED";

        [Header("Audio")]
        [SerializeField] private AudioClip _pressClip;
        [SerializeField] private AudioClip _deniedClip;

        [Header("Animation")]
        [SerializeField] private ButtonAnimator _animator;

        private AudioSource _audioSource;

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

            if (!_elevatorController.IsUnlocked)
            {
                if (_audioSource != null && _deniedClip != null)
                    _audioSource.PlayOneShot(_deniedClip);
                return;
            }

            if (_elevatorController.DoorsOpen || _elevatorController.IsDescending || _elevatorController.HasArrived)
                return;

            _animator?.PressAndReturn();

            if (_audioSource != null && _pressClip != null)
                _audioSource.PlayOneShot(_pressClip);

            _elevatorController.CmdRequestOpenDoors();
        }

        public string GetInteractionPrompt()
        {
            if (_elevatorController == null) return "";
            if (_elevatorController.IsDescending || _elevatorController.HasArrived) return "";
            if (_elevatorController.DoorsOpen) return "";

            if (!_elevatorController.IsUnlocked)
            {
                string lockedText = LocalizationManager.Get(_promptKeyLocked);
                return $"<size=80%><color=#666666>{lockedText}</color></size>";
            }

            string input = LocalizationManager.Get("INPUT_PRESS");
            string action = LocalizationManager.Get(_promptKeyOpen);
            return $"<size=80%><color=yellow>{input} {action}</color></size>";
        }
    }
}
