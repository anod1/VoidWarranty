using UnityEngine;
using System.Collections;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Animation visuelle d'enfoncement pour boutons physiques.
    /// Composant purement cosmétique — pas de NetworkBehaviour.
    /// Peut être attaché à n'importe quel bouton interactable.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur le même GO que le script d'interaction (DoorButton, ElevatorCallButton, etc.)
    /// → Inspector : _buttonMesh = Transform du mesh enfant à animer
    /// → Inspector : _pressAxis = direction locale d'enfoncement (défaut -Z)
    /// → Optionnel : _pressClip = AudioClip de click mécanique
    /// </summary>
    public class ButtonAnimator : MonoBehaviour
    {
        [Header("Animation")]
        [Tooltip("Transform du mesh à animer (enfant du bouton)")]
        [SerializeField] private Transform _buttonMesh;
        [Tooltip("Direction locale d'enfoncement")]
        [SerializeField] private Vector3 _pressAxis = new Vector3(0f, 0f, -1f);
        [Tooltip("Distance d'enfoncement en mètres")]
        [SerializeField] private float _pressDistance = 0.02f;
        [Tooltip("Durée de l'enfoncement")]
        [SerializeField] private float _pressDuration = 0.1f;
        [Tooltip("Durée du retour")]
        [SerializeField] private float _returnDuration = 0.15f;

        [Header("Audio")]
        [Tooltip("Son de press (optionnel)")]
        [SerializeField] private AudioClip _pressClip;
        [SerializeField] private AudioSource _audioSource;

        private Vector3 _originalLocalPos;
        private Coroutine _currentAnim;
        private bool _isPressed;

        private void Awake()
        {
            if (_buttonMesh != null)
                _originalLocalPos = _buttonMesh.localPosition;

            if (_audioSource == null)
                _audioSource = GetComponentInChildren<AudioSource>();
        }

        /// <summary>Enfonce le bouton. En mode press, revient automatiquement.</summary>
        public void Press()
        {
            if (_buttonMesh == null) return;

            if (_currentAnim != null)
                StopCoroutine(_currentAnim);

            _isPressed = true;
            _currentAnim = StartCoroutine(AnimatePress());

            if (_pressClip != null && _audioSource != null)
                _audioSource.PlayOneShot(_pressClip);
        }

        /// <summary>Relâche le bouton (pour hold mode). Appeler quand le joueur relâche E.</summary>
        public void Release()
        {
            if (_buttonMesh == null || !_isPressed) return;

            if (_currentAnim != null)
                StopCoroutine(_currentAnim);

            _isPressed = false;
            _currentAnim = StartCoroutine(AnimateReturn());
        }

        /// <summary>Press + retour automatique (pour press simple, pas hold).</summary>
        public void PressAndReturn()
        {
            if (_buttonMesh == null) return;

            if (_currentAnim != null)
                StopCoroutine(_currentAnim);

            _currentAnim = StartCoroutine(AnimatePressAndReturn());

            if (_pressClip != null && _audioSource != null)
                _audioSource.PlayOneShot(_pressClip);
        }

        private IEnumerator AnimatePress()
        {
            Vector3 target = _originalLocalPos + _pressAxis.normalized * _pressDistance;
            yield return LerpPosition(_buttonMesh.localPosition, target, _pressDuration);
            _currentAnim = null;
        }

        private IEnumerator AnimateReturn()
        {
            yield return LerpPosition(_buttonMesh.localPosition, _originalLocalPos, _returnDuration);
            _currentAnim = null;
        }

        private IEnumerator AnimatePressAndReturn()
        {
            Vector3 target = _originalLocalPos + _pressAxis.normalized * _pressDistance;
            yield return LerpPosition(_buttonMesh.localPosition, target, _pressDuration);
            yield return LerpPosition(target, _originalLocalPos, _returnDuration);
            _isPressed = false;
            _currentAnim = null;
        }

        private IEnumerator LerpPosition(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                _buttonMesh.localPosition = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _buttonMesh.localPosition = Vector3.Lerp(from, to, t);
                yield return null;
            }

            _buttonMesh.localPosition = to;
        }
    }
}
