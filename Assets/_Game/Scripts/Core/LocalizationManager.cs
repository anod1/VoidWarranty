using UnityEngine;

namespace VoidWarranty.Core
{
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance;

        [Header("Langue Active")]
        [SerializeField] private LocalizationTable _currentLanguage;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (_currentLanguage != null)
            {
                _currentLanguage.Initialize();
                Debug.Log($"🌍 Langue chargée : {_currentLanguage.LanguageName}");
            }
        }

        public static string Get(string key)
        {
            if (Instance == null) return $"[NO_MANAGER:{key}]";
            if (Instance._currentLanguage == null) return $"[NO_LANG:{key}]";

            return Instance._currentLanguage.Get(key);
        }
    }
}