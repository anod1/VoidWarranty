using UnityEngine;
using FishNet.Object;

namespace VoidWarranty.Player
{
    // =========================================================================
    // PlayerFootsteps
    // =========================================================================
    // Joue les sons de pas en 3D en fonction du NoiseLevel du joueur et du
    // type de surface sous ses pieds (détecté via PhysicMaterial).
    //
    // FishNet / Réseau :
    //   - Tourne sur l'owner client ET sur les autres clients.
    //   - Le serveur dédié ne joue JAMAIS de son (IsServerOnly guard).
    //   - Les autres clients entendent les pas via l'AudioSource 3D attachée
    //     au NetworkObject dont la position est synchro par FishNet.
    //
    // Responsabilité unique :
    //   - Ce composant NE calcule PAS le NoiseLevel, il le lit.
    //   - Ce composant NE gère PAS le mouvement, il l'observe.
    // =========================================================================

    [System.Serializable]
    public class SurfaceFootstepProfile
    {
        [Tooltip("PhysicMaterial correspondant à cette surface (null = fallback)")]
        public PhysicsMaterial physicMaterial;

        [Tooltip("Clips de pas pour cette surface (1 sera choisi aléatoirement)")]
        public AudioClip[] footstepClips;

        [Tooltip("Volume de base pour cette surface (métal = plus fort, tapis = plus doux)")]
        [Range(0f, 1f)]
        public float baseVolume = 0.5f;

        [Tooltip("Pitch min/max pour la variation aléatoire")]
        public float pitchMin = 0.9f;
        public float pitchMax = 1.1f;
    }

    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerFootsteps : NetworkBehaviour
    {
        // =====================================================================
        // Configuration
        // =====================================================================

        [Header("Surfaces")]
        [Tooltip("Profils par type de surface. L'entrée avec physicMaterial=null sert de fallback.")]
        [SerializeField] private SurfaceFootstepProfile[] _surfaceProfiles;

        [Header("Timing")]
        [Tooltip("Intervalle entre pas en marche normale (secondes)")]
        [SerializeField] private float _walkStepInterval = 0.5f;
        [Tooltip("Intervalle entre pas en sprint")]
        [SerializeField] private float _runStepInterval = 0.3f;
        [Tooltip("Intervalle entre pas en crouch")]
        [SerializeField] private float _crouchStepInterval = 0.7f;

        [Header("Volume")]
        [Tooltip("Multiplicateur volume pour le joueur local (feedback subjectif)")]
        [SerializeField] private float _localVolumeMultiplier = 0.6f;
        [Tooltip("Multiplicateur volume pour les autres clients (son 3D monde)")]
        [SerializeField] private float _remoteVolumeMultiplier = 1f;

        [Header("Surface Detection")]
        [Tooltip("Distance du raycast sol")]
        [SerializeField] private float _groundRayDistance = 1.2f;
        [SerializeField] private LayerMask _groundLayer;

        // =====================================================================
        // Références
        // =====================================================================

        private AudioSource _audioSource;
        private PlayerMovement _playerMovement;

        // =====================================================================
        // State
        // =====================================================================

        private float _stepTimer;
        private SurfaceFootstepProfile _fallbackProfile;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _playerMovement = GetComponent<PlayerMovement>();

            // Configuration de l'AudioSource pour le son 3D
            _audioSource.spatialBlend = 1f;       // Full 3D
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 1f;
            _audioSource.maxDistance = 20f;
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;

            // Pré-cache le profil fallback (physicMaterial == null)
            if (_surfaceProfiles != null)
            {
                foreach (var profile in _surfaceProfiles)
                {
                    if (profile.physicMaterial == null)
                    {
                        _fallbackProfile = profile;
                        break;
                    }
                }
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // L'owner entend ses pas moins fort (feedback local subjectif)
            // Les autres entendent en volume monde pour la localisation sonore
            _audioSource.volume = base.IsOwner ? _localVolumeMultiplier : _remoteVolumeMultiplier;
        }

        private void Update()
        {
            // Le serveur dédié ne joue jamais de son
            if (base.IsServerOnlyInitialized) return;

            float noise = _playerMovement.NoiseLevel;

            // Pas de son si le joueur est immobile, caché, ou inaudible
            if (noise < 0.05f)
            {
                _stepTimer = 0f;
                return;
            }

            // Calcul de l'intervalle selon l'état de mouvement
            float interval = GetStepInterval(noise);

            _stepTimer -= Time.deltaTime;
            if (_stepTimer <= 0f)
            {
                _stepTimer = interval;
                PlayFootstep(noise);
            }
        }

        // =====================================================================
        // Logique
        // =====================================================================

        /// <summary>
        /// Retourne l'intervalle entre pas en fonction du NoiseLevel.
        ///   0.3 = crouch walk, 0.6 = walk normal, 1.0 = sprint.
        /// </summary>
        private float GetStepInterval(float noise)
        {
            if (noise >= 0.9f)  return _runStepInterval;
            if (noise >= 0.5f)  return _walkStepInterval;
            return _crouchStepInterval;
        }

        private void PlayFootstep(float noise)
        {
            SurfaceFootstepProfile profile = DetectSurface();
            if (profile == null || profile.footstepClips == null || profile.footstepClips.Length == 0)
                return;

            // Clip aléatoire parmi ceux de la surface
            AudioClip clip = profile.footstepClips[Random.Range(0, profile.footstepClips.Length)];
            if (clip == null) return;

            // Volume : surface base * noise (crouch = plus doux, sprint = plus fort)
            float volumeScale = profile.baseVolume * Mathf.Lerp(0.4f, 1f, noise);

            // Pitch variation légère pour éviter la répétition robotique
            _audioSource.pitch = Random.Range(profile.pitchMin, profile.pitchMax);

            _audioSource.PlayOneShot(clip, volumeScale);
        }

        /// <summary>
        /// Détecte la surface sous les pieds via raycast + PhysicMaterial.
        /// Retourne le profil correspondant, ou le fallback si aucun match.
        /// </summary>
        private SurfaceFootstepProfile DetectSurface()
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _groundRayDistance, _groundLayer))
                return _fallbackProfile;

            PhysicsMaterial mat = hit.collider.sharedMaterial;

            if (mat == null) return _fallbackProfile;

            foreach (var profile in _surfaceProfiles)
            {
                if (profile.physicMaterial == mat)
                    return profile;
            }

            return _fallbackProfile;
        }

        // =====================================================================
        // Gizmos
        // =====================================================================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * _groundRayDistance);
        }
#endif
    }
}
