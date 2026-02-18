using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SubSurface.Environment
{
    /// <summary>
    /// Pilote un seul Global Volume par code pour gérer les transitions
    /// de post-processing entre les zones de profondeur.
    /// Client-side uniquement (pas de networking).
    /// </summary>
    public class DepthZoneManager : MonoBehaviour
    {
        // =====================================================================
        // Singleton
        // =====================================================================

        public static DepthZoneManager Instance { get; private set; }

        // =====================================================================
        // Data
        // =====================================================================

        [Serializable]
        public struct DepthZonePreset
        {
            [Header("Zone Info")]
            public string ZoneName;
            public string DepthLabel;

            [Header("Fog")]
            public Color FogColor;
            [Range(0f, 0.25f)] public float FogDensity;
            public float FogMaxDistance;

            [Header("Color Adjustments")]
            [Range(-100f, 100f)] public float Saturation;
            [Range(-180f, 180f)] public float HueShift;
            [Range(-100f, 100f)] public float Temperature;
            [Range(-100f, 100f)] public float Contrast;
            [Range(-5f, 5f)] public float PostExposure;

            [Header("Vignette")]
            [Range(0f, 1f)] public float VignetteIntensity;
            public Color VignetteColor;

            [Header("Chromatic Aberration")]
            [Range(0f, 1f)] public float ChromaticAberration;

            [Header("Film Grain")]
            [Range(0f, 1f)] public float FilmGrainIntensity;

            [Header("Bloom")]
            [Range(0f, 1f)] public float BloomIntensity;
            public float BloomThreshold;
        }

        // =====================================================================
        // Configuration
        // =====================================================================

        [Header("References")]
        [SerializeField] private Volume _globalVolume;

        [Header("Zone Presets")]
        [SerializeField] private DepthZonePreset[] _zonePresets;

        [Header("Transition")]
        [SerializeField] private float _transitionDuration = 1.5f;

        // =====================================================================
        // State
        // =====================================================================

        private int _currentZoneIndex = -1;
        private Coroutine _transitionCoroutine;

        // Volume overrides (cached)
        private ColorAdjustments _colorAdjustments;
        private Vignette _vignette;
        private ChromaticAberration _chromaticAberration;
        private FilmGrain _filmGrain;
        private Bloom _bloom;

        /// <summary>Index de la zone active (-1 si aucune).</summary>
        public int CurrentZoneIndex => _currentZoneIndex;

        /// <summary>Label de profondeur de la zone active (ex: "-2000m").</summary>
        public string CurrentDepthLabel =>
            _currentZoneIndex >= 0 && _currentZoneIndex < _zonePresets.Length
                ? _zonePresets[_currentZoneIndex].DepthLabel
                : "";

        // =====================================================================
        // Events
        // =====================================================================

        public event Action<int> OnZoneChanged;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DepthZoneManager] Doublon détecté, destruction.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CacheVolumeOverrides();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Transition vers une zone de profondeur.
        /// Appelé par DepthZoneTrigger quand le joueur local entre dans une zone.
        /// </summary>
        public void TransitionToZone(int zoneIndex)
        {
            if (zoneIndex < 0 || zoneIndex >= _zonePresets.Length)
            {
                Debug.LogWarning($"[DepthZoneManager] Index de zone invalide : {zoneIndex}");
                return;
            }

            if (zoneIndex == _currentZoneIndex) return;

            int previousZone = _currentZoneIndex;
            _currentZoneIndex = zoneIndex;

            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);

            _transitionCoroutine = StartCoroutine(
                TransitionCoroutine(_zonePresets[zoneIndex], _transitionDuration));

            Debug.Log($"[DepthZoneManager] Transition zone {previousZone} → {zoneIndex} ({_zonePresets[zoneIndex].ZoneName})");

            OnZoneChanged?.Invoke(zoneIndex);
        }

        // =====================================================================
        // Volume Overrides
        // =====================================================================

        private void CacheVolumeOverrides()
        {
            if (_globalVolume == null || _globalVolume.profile == null)
            {
                Debug.LogError("[DepthZoneManager] Global Volume ou profile manquant !");
                return;
            }

            var profile = _globalVolume.profile;

            if (!profile.TryGet(out _colorAdjustments))
            {
                _colorAdjustments = profile.Add<ColorAdjustments>();
                _colorAdjustments.active = true;
            }

            if (!profile.TryGet(out _vignette))
            {
                _vignette = profile.Add<Vignette>();
                _vignette.active = true;
            }

            if (!profile.TryGet(out _chromaticAberration))
            {
                _chromaticAberration = profile.Add<ChromaticAberration>();
                _chromaticAberration.active = true;
            }

            if (!profile.TryGet(out _filmGrain))
            {
                _filmGrain = profile.Add<FilmGrain>();
                _filmGrain.active = true;
            }

            if (!profile.TryGet(out _bloom))
            {
                _bloom = profile.Add<Bloom>();
                _bloom.active = true;
            }
        }

        private void ApplyPreset(DepthZonePreset preset)
        {
            // Fog (RenderSettings, pas Volume)
            RenderSettings.fogColor = preset.FogColor;
            RenderSettings.fogDensity = preset.FogDensity;
            RenderSettings.fogEndDistance = preset.FogMaxDistance;

            // Color Adjustments
            if (_colorAdjustments != null)
            {
                _colorAdjustments.saturation.Override(preset.Saturation);
                _colorAdjustments.hueShift.Override(preset.HueShift);
                _colorAdjustments.colorFilter.Override(
                    Color.Lerp(Color.white, preset.Temperature < 0 ? Color.cyan : Color.yellow,
                        Mathf.Abs(preset.Temperature) / 100f));
                _colorAdjustments.contrast.Override(preset.Contrast);
                _colorAdjustments.postExposure.Override(preset.PostExposure);
            }

            // Vignette
            if (_vignette != null)
            {
                _vignette.intensity.Override(preset.VignetteIntensity);
                _vignette.color.Override(preset.VignetteColor);
            }

            // Chromatic Aberration
            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.Override(preset.ChromaticAberration);
            }

            // Film Grain
            if (_filmGrain != null)
            {
                _filmGrain.intensity.Override(preset.FilmGrainIntensity);
            }

            // Bloom
            if (_bloom != null)
            {
                _bloom.intensity.Override(preset.BloomIntensity);
                _bloom.threshold.Override(preset.BloomThreshold);
            }
        }

        // =====================================================================
        // Transition Coroutine
        // =====================================================================

        private IEnumerator TransitionCoroutine(DepthZonePreset target, float duration)
        {
            // Snapshot current values
            float startFogDensity = RenderSettings.fogDensity;
            Color startFogColor = RenderSettings.fogColor;
            float startFogMaxDist = RenderSettings.fogEndDistance;

            float startSaturation = _colorAdjustments != null ? _colorAdjustments.saturation.value : 0f;
            float startHueShift = _colorAdjustments != null ? _colorAdjustments.hueShift.value : 0f;
            float startContrast = _colorAdjustments != null ? _colorAdjustments.contrast.value : 0f;
            float startExposure = _colorAdjustments != null ? _colorAdjustments.postExposure.value : 0f;
            Color startColorFilter = _colorAdjustments != null ? _colorAdjustments.colorFilter.value : Color.white;

            float startVignette = _vignette != null ? _vignette.intensity.value : 0f;
            Color startVignetteColor = _vignette != null ? _vignette.color.value : Color.black;

            float startChromatic = _chromaticAberration != null ? _chromaticAberration.intensity.value : 0f;
            float startGrain = _filmGrain != null ? _filmGrain.intensity.value : 0f;
            float startBloom = _bloom != null ? _bloom.intensity.value : 0f;
            float startBloomThreshold = _bloom != null ? _bloom.threshold.value : 1f;

            // Target temperature → color filter
            Color targetColorFilter = Color.Lerp(Color.white,
                target.Temperature < 0 ? Color.cyan : Color.yellow,
                Mathf.Abs(target.Temperature) / 100f);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                // Fog
                RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, target.FogDensity, t);
                RenderSettings.fogColor = Color.Lerp(startFogColor, target.FogColor, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(startFogMaxDist, target.FogMaxDistance, t);

                // Color Adjustments
                if (_colorAdjustments != null)
                {
                    _colorAdjustments.saturation.Override(Mathf.Lerp(startSaturation, target.Saturation, t));
                    _colorAdjustments.hueShift.Override(Mathf.Lerp(startHueShift, target.HueShift, t));
                    _colorAdjustments.contrast.Override(Mathf.Lerp(startContrast, target.Contrast, t));
                    _colorAdjustments.postExposure.Override(Mathf.Lerp(startExposure, target.PostExposure, t));
                    _colorAdjustments.colorFilter.Override(Color.Lerp(startColorFilter, targetColorFilter, t));
                }

                // Vignette
                if (_vignette != null)
                {
                    _vignette.intensity.Override(Mathf.Lerp(startVignette, target.VignetteIntensity, t));
                    _vignette.color.Override(Color.Lerp(startVignetteColor, target.VignetteColor, t));
                }

                // Chromatic Aberration
                if (_chromaticAberration != null)
                    _chromaticAberration.intensity.Override(Mathf.Lerp(startChromatic, target.ChromaticAberration, t));

                // Film Grain
                if (_filmGrain != null)
                    _filmGrain.intensity.Override(Mathf.Lerp(startGrain, target.FilmGrainIntensity, t));

                // Bloom
                if (_bloom != null)
                {
                    _bloom.intensity.Override(Mathf.Lerp(startBloom, target.BloomIntensity, t));
                    _bloom.threshold.Override(Mathf.Lerp(startBloomThreshold, target.BloomThreshold, t));
                }

                yield return null;
            }

            // Final snap
            ApplyPreset(target);
            _transitionCoroutine = null;
        }
    }
}
