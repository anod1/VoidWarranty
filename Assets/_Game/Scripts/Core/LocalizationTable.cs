using UnityEngine;
using System.Collections.Generic;

namespace VoidWarranty.Core
{
    [CreateAssetMenu(fileName = "New Language", menuName = "VoidWarranty/Localization Table")]
    public class LocalizationTable : ScriptableObject
    {
        [Header("Configuration")]
        public string LanguageName = "English";
        public string LanguageCode = "en_US";

        [Header("Import (Fichier CSV)")]
        [Tooltip("Glisse ton fichier .csv ici")]
        public TextAsset CsvFile;

        [Header("Données (Générées auto)")]
        [SerializeField] private List<TranslationEntry> _entries;

        // Dictionnaire pour la recherche rapide en jeu
        private Dictionary<string, string> _lookup;

        // --- BOUTON D'IMPORTATION ---
        [ContextMenu("📥 Importer depuis CSV")]
        public void ImportFromCsv()
        {
            if (CsvFile == null)
            {
                Debug.LogError("❌ Aucun fichier CSV assigné !");
                return;
            }

            _entries = new List<TranslationEntry>();

            // On découpe les lignes
            string[] lines = CsvFile.text.Split('\n');

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("KEY;")) continue; // On ignore l'en-tête

                // On découpe au point-virgule
                string[] parts = line.Split(';');

                if (parts.Length >= 2)
                {
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    // Gestion des sauts de ligne (Excel utilise parfois <br> ou \n)
                    value = value.Replace("<br>", "\n").Replace("\\n", "\n");

                    _entries.Add(new TranslationEntry { Key = key, Value = value });
                }
            }

            Debug.Log($"✅ Succès ! {_entries.Count} traductions importées depuis {CsvFile.name}.");
        }

        // --- INITIALISATION RUNTIME ---
        public void Initialize()
        {
            _lookup = new Dictionary<string, string>();
            foreach (var entry in _entries)
            {
                if (!_lookup.ContainsKey(entry.Key))
                {
                    _lookup.Add(entry.Key, entry.Value);
                }
            }
        }

        public string Get(string key)
        {
            if (_lookup == null) Initialize();

            if (_lookup.TryGetValue(key, out string value)) return value;

            return $"[{key}]"; // Retourne [CLÉ_MANQUANTE] pour debug
        }

        [System.Serializable]
        public struct TranslationEntry
        {
            public string Key;
            [TextArea] public string Value;
        }
    }
}