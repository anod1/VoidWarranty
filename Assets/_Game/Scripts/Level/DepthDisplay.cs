using UnityEngine;
using TMPro;

namespace SubSurface.Level
{
    /// <summary>
    /// Affichage de la profondeur dans la cabine de l'ascenseur.
    /// Effet "moniteur industriel" : texte mono, flicker subtil, couleur ambiante.
    ///
    /// SETUP ÉDITEUR :
    /// → Canvas World Space dans la cabine (petit écran mural)
    /// → TextMeshProUGUI enfant (font mono recommandée)
    /// → Inspector : _text ref, couleurs, format
    /// → Référencé par ElevatorController._depthDisplay
    /// </summary>
    public class DepthDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _text;

        [Header("Display")]
        [SerializeField] private string _format = "- {0:F0} m";
        [SerializeField] private Color _idleColor = new(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _descentColor = new(1f, 0.4f, 0.1f, 1f);

        [Header("Flicker")]
        [SerializeField] private float _flickerChance = 0.03f;
        [SerializeField] private float _flickerDuration = 0.05f;

        private float _currentDepth;
        private bool _descending;
        private float _flickerTimer;

        private void Awake()
        {
            if (_text == null)
                _text = GetComponentInChildren<TMP_Text>();
        }

        public void SetDepth(float depth)
        {
            _currentDepth = depth;
            _descending = true;

            if (_text != null)
            {
                _text.text = string.Format(_format, depth);
                _text.color = _descentColor;
            }
        }

        public void SetIdle(float depth)
        {
            _currentDepth = depth;
            _descending = false;

            if (_text != null)
            {
                _text.text = string.Format(_format, depth);
                _text.color = _idleColor;
            }
        }

        private void Update()
        {
            if (!_descending || _text == null) return;

            // Flicker aléatoire (écran industriel)
            if (_flickerTimer > 0f)
            {
                _flickerTimer -= Time.deltaTime;
                if (_flickerTimer <= 0f)
                    _text.enabled = true;
            }
            else if (Random.value < _flickerChance)
            {
                _text.enabled = false;
                _flickerTimer = _flickerDuration;
            }
        }
    }
}
