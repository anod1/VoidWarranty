using UnityEngine;
using FishNet.Object;
using VoidWarranty.Core;
using System.Collections;

namespace SubSurface.Puzzle
{
    /// <summary>
    /// Vanne de ballast — style Signalis.
    /// Chaque pression E = 1 transfert atomique dans une direction fixe.
    /// 3 vannes en cycle : R→V, V→B, B→R.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur chaque GO vanne (×3) en salle des machines
    /// → Layer 6 (Interactable) + Collider + NetworkObject
    /// → Inspector : from/to (ints), PuzzleManager ref
    ///   Vanne 1 : from=0(Rouge), to=1(Vert)
    ///   Vanne 2 : from=1(Vert), to=2(Bleu)
    ///   Vanne 3 : from=2(Bleu), to=0(Rouge)
    /// → Inspector : Transform roue pour animation rotation (optionnel)
    /// → Inspector : AudioClip succès + échec
    /// </summary>
    public class ValveInteractable : NetworkBehaviour, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private int _fromChamber;
        [SerializeField] private int _toChamber;
        [SerializeField] private PuzzleManager _puzzleManager;

        [Header("Visuals")]
        [SerializeField] private Transform _wheelTransform;
        [SerializeField] private float _rotationAmount = 90f;

        [Header("Audio")]
        [SerializeField] private AudioClip _successClip;
        [SerializeField] private AudioClip _failClip;

        private AudioSource _audioSource;
        private bool _onCooldown;
        private const float Cooldown = 0.5f;

        private void Awake()
        {
            _audioSource = GetComponentInChildren<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f;
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
                _audioSource.maxDistance = 20f;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            PuzzleManager.OnTransferResult += HandleTransferResult;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            PuzzleManager.OnTransferResult -= HandleTransferResult;
        }

        // =====================================================================
        // IInteractable
        // =====================================================================

        public void Interact(GameObject interactor)
        {
            if (_onCooldown) return;
            if (_puzzleManager == null || _puzzleManager.IsSolved) return;

            _puzzleManager.CmdTransferPressure(_fromChamber, _toChamber);
            StartCoroutine(CooldownRoutine());
        }

        public string GetInteractionPrompt()
        {
            if (_puzzleManager == null || _puzzleManager.IsSolved) return "";

            string input = LocalizationManager.Get("INPUT_PRESS");
            string fromName = PuzzleManager.GetChamberName(_fromChamber);
            string toName = PuzzleManager.GetChamberName(_toChamber);

            if (_onCooldown)
                return $"<size=80%><color=#666666>{input} {fromName} → {toName}</color></size>";

            return $"<size=80%><color=yellow>{input} {fromName} → {toName}</color></size>";
        }

        // =====================================================================
        // Feedback (réponse du PuzzleManager)
        // =====================================================================

        private void HandleTransferResult(int from, int to, bool success)
        {
            if (from != _fromChamber || to != _toChamber) return;

            if (success)
            {
                if (_audioSource != null && _successClip != null)
                    _audioSource.PlayOneShot(_successClip);

                if (_wheelTransform != null)
                    StartCoroutine(RotateWheel());
            }
            else
            {
                if (_audioSource != null && _failClip != null)
                    _audioSource.PlayOneShot(_failClip);
            }
        }

        private IEnumerator RotateWheel()
        {
            if (_wheelTransform == null) yield break;

            Quaternion startRot = _wheelTransform.localRotation;
            Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, _rotationAmount);
            float elapsed = 0f;
            const float duration = 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _wheelTransform.localRotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            _wheelTransform.localRotation = endRot;
        }

        private IEnumerator CooldownRoutine()
        {
            _onCooldown = true;
            yield return new WaitForSeconds(Cooldown);
            _onCooldown = false;
        }
    }
}
