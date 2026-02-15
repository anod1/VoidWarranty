using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace VoidWarranty.UI
{
    /// <summary>
    /// Affiche des notifications temporaires (toasts) à l'écran.
    /// Singleton local (pas réseau — chaque client a le sien).
    ///
    /// Usage depuis n'importe quel script :
    ///   NotificationHUD.Instance?.Show("Message ici");
    ///   NotificationHUD.Instance?.Show("Message ici", 5f);
    ///
    /// Setup : ajouter ce composant sur un GameObject avec un TextMeshProUGUI.
    /// Le GameObject doit rester actif (le texte est caché via alpha, pas via SetActive).
    /// </summary>
    public class NotificationHUD : MonoBehaviour
    {
        public static NotificationHUD Instance { get; private set; }

        [Header("Références")]
        [SerializeField] private TextMeshProUGUI _notificationText;

        [Header("Réglages")]
        [SerializeField] private float _defaultDuration = 3f;
        [SerializeField] private float _fadeSpeed = 3f;

        private readonly Queue<PendingNotification> _queue = new();
        private float _currentTimer;
        private bool _isShowing;

        private struct PendingNotification
        {
            public string Message;
            public float Duration;
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            // Singleton sans détruire le GameObject (peut être enfant d'un Canvas)
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;

            if (_notificationText != null)
            {
                _notificationText.text = "";
                SetAlpha(0f);
            }
        }

        private void OnEnable()
        {
            // Re-register si l'ancien Instance a été détruit
            if (Instance == null)
                Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!_isShowing)
            {
                if (_queue.Count > 0)
                    DisplayNext();

                return;
            }

            _currentTimer -= Time.deltaTime;

            if (_currentTimer <= 0f)
            {
                float alpha = GetAlpha();
                alpha = Mathf.MoveTowards(alpha, 0f, Time.deltaTime * _fadeSpeed);
                SetAlpha(alpha);

                if (alpha <= 0f)
                {
                    _isShowing = false;
                    _notificationText.text = "";
                }
            }
        }

        // =====================================================================
        // API publique
        // =====================================================================

        public void Show(string message)
        {
            Show(message, _defaultDuration);
        }

        public void Show(string message, float duration)
        {
            if (string.IsNullOrEmpty(message)) return;

            _queue.Enqueue(new PendingNotification
            {
                Message = message,
                Duration = duration
            });

            if (!_isShowing)
                DisplayNext();
        }

        // =====================================================================
        // Interne
        // =====================================================================

        private void DisplayNext()
        {
            if (_queue.Count == 0 || _notificationText == null) return;

            var notif = _queue.Dequeue();
            _notificationText.text = notif.Message;
            SetAlpha(1f);
            _currentTimer = notif.Duration;
            _isShowing = true;
        }

        private void SetAlpha(float alpha)
        {
            if (_notificationText == null) return;
            Color c = _notificationText.color;
            c.a = alpha;
            _notificationText.color = c;
        }

        private float GetAlpha()
        {
            if (_notificationText == null) return 0f;
            return _notificationText.color.a;
        }
    }
}
