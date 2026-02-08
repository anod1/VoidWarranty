using UnityEngine;
using UnityEngine.UI;
using VoidWarranty.Core;

namespace VoidWarranty.Interaction
{
    public class Scanner : GrabbableObject
    {
        [Header("Scanner UI References")]
        [SerializeField] private Canvas _uiCanvas;
        [SerializeField] private RectTransform _radarScreen;
        [SerializeField] private RectTransform _blip;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _beepClip;

        [Header("Radar Settings")]
        [SerializeField] private float _maxRadarRange = 50f;
        [SerializeField] private float _radarRadius = 100f;
        [SerializeField] private Color _blipColor = Color.green;
        [SerializeField] private float _blipFadeSpeed = 3f;

        [Header("Audio Frequency")]
        [SerializeField] private float _maxBeepInterval = 2f;
        [SerializeField] private float _minBeepInterval = 0.1f;
        [SerializeField] private float _continuousDistance = 5f;

        [Header("Optimization")]
        [SerializeField] private float _updateRate = 0.05f; // Vitesse rafraichissement UI
        [SerializeField] private float _scanRate = 0.5f;    // Vitesse recherche de cible (Nouveau)

        // On ne stocke plus un Patient, mais un Transform g�n�rique
        private Transform _targetTransform;

        private Transform _playerTransform;
        private Image _blipImage;
        private bool _isActive = false;

        private float _updateTimer = 0f;
        private float _scanTimer = 0f;
        private float _beepTimer = 0f;
        private float _currentBlipAlpha = 0f;

        protected override void Awake()
        {
            base.Awake();

            // Scan initial
            ScanForTarget();

            if (_blip != null && _blip.TryGetComponent(out _blipImage))
            {
                _blipImage.color = _blipColor;
                Color c = _blipImage.color;
                c.a = 0f;
                _blipImage.color = c;
            }

            if (_uiCanvas != null) _uiCanvas.enabled = false;
        }

        private void Update()
        {
            if (!_isActive || _playerTransform == null) return;

            // 1. RECHERCHE DE CIBLE (Scan lent : 2 fois par seconde)
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= _scanRate)
            {
                _scanTimer = 0f;
                ScanForTarget();
            }

            // Si toujours pas de cible apr�s le scan, on arr�te l'update visuel
            if (_targetTransform == null) return;

            // 2. MISE � JOUR RADAR (Rapide)
            _updateTimer += Time.deltaTime;
            if (_updateTimer >= _updateRate)
            {
                _updateTimer = 0f;
                UpdateRadarPosition();
            }

            // 3. AUDIO & VISUEL
            UpdateBeepAndFlash();
            UpdateBlipFade();
        }

        // --- NOUVELLE LOGIQUE DE CIBLAGE ---
        private void ScanForTarget()
        {
            _targetTransform = null;
            float closestDist = float.MaxValue;
            Vector3 myPos = transform.position;

            // PRIORIT� 1 : Chercher une pi�ce infect�e (IsDefective)
            // C'est lourd de faire FindObjectsByType, mais ok pour un prototype avec peu d'objets.
            // (Plus tard on utilisera une liste statique g�r�e par le GameManager)
            GrabbableObject[] allProps = FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None);

            foreach (var prop in allProps)
            {
                ItemData data = prop.GetData();
                // Si l'objet est d�fectueux (c'est le moteur infect� !)
                if (data != null && data.IsDefective)
                {
                    float d = Vector3.Distance(myPos, prop.transform.position);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        _targetTransform = prop.transform;
                    }
                }
            }

            // Si on a trouv� une pi�ce infect�e, on s'arr�te l� (c'est la cible prioritaire)
            if (_targetTransform != null) return;

            // PRIORIT� 2 : Si aucune pi�ce infect�e, chercher un Patient
            PatientObject[] patients = FindObjectsByType<PatientObject>(FindObjectsSortMode.None);
            foreach (var patient in patients)
            {
                float d = Vector3.Distance(myPos, patient.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    _targetTransform = patient.transform;
                }
            }
        }
        // -----------------------------------

        private void UpdateRadarPosition()
        {
            if (_blip == null || _targetTransform == null) return;

            Vector3 directionToTarget = _targetTransform.position - _playerTransform.position;
            Vector3 localDirection = _playerTransform.InverseTransformDirection(directionToTarget);

            float distance = directionToTarget.magnitude;
            float normalizedDist = Mathf.Clamp01(distance / _maxRadarRange);
            float angle = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;

            Vector2 blipPos = new Vector2(
                Mathf.Sin(angle * Mathf.Deg2Rad) * normalizedDist * _radarRadius,
                Mathf.Cos(angle * Mathf.Deg2Rad) * normalizedDist * _radarRadius
            );

            _blip.anchoredPosition = blipPos;
        }

        private void UpdateBeepAndFlash()
        {
            if (_audioSource == null || _beepClip == null || _targetTransform == null) return;

            float distance = Vector3.Distance(_playerTransform.position, _targetTransform.position);

            float interval;
            if (distance < _continuousDistance) interval = 0.05f;
            else
            {
                float t = distance / _maxRadarRange;
                interval = Mathf.Lerp(_minBeepInterval, _maxBeepInterval, Mathf.Clamp01(t));
            }

            _beepTimer += Time.deltaTime;

            if (_beepTimer >= interval)
            {
                _beepTimer = 0f;
                _audioSource.PlayOneShot(_beepClip);
                _currentBlipAlpha = 1f;
            }
        }

        private void UpdateBlipFade()
        {
            if (_blipImage == null) return;
            _currentBlipAlpha = Mathf.MoveTowards(_currentBlipAlpha, 0f, Time.deltaTime * _blipFadeSpeed);
            Color c = _blipColor;
            c.a = _currentBlipAlpha;
            _blipImage.color = c;
        }

        public override void OnGrabbed(Transform playerTransform)
        {
            base.OnGrabbed(playerTransform);
            _playerTransform = playerTransform;
            _isActive = true;
            if (_uiCanvas != null) _uiCanvas.enabled = true;
            _beepTimer = 0f;
            _currentBlipAlpha = 0f;

            // Force un scan imm�diat quand on le prend
            ScanForTarget();
        }

        public override void OnDropped()
        {
            base.OnDropped();
            _isActive = false;
            _playerTransform = null;
            if (_uiCanvas != null) _uiCanvas.enabled = false;
            if (_audioSource != null) _audioSource.Stop();
        }

        public override string GetInteractionPrompt()
        {
            string name = (_data != null) ? LocalizationManager.Get(_data.NameKey) : "Scanner";
            string action = LocalizationManager.Get("ACTION_TAKE");
            return $"{name}\n<size=80%><color=yellow>[{action}]</color></size>";
        }
    }
}