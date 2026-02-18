using UnityEngine;
using FishNet.Object;

namespace SubSurface.Environment
{
    /// <summary>
    /// BoxCollider trigger qui détecte l'entrée du joueur local
    /// et demande au DepthZoneManager de transitionner vers la zone associée.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DepthZoneTrigger : MonoBehaviour
    {
        [Header("Zone")]
        [Tooltip("Index dans le tableau DepthZoneManager.ZonePresets")]
        [SerializeField] private int _zoneIndex;

        [Header("VFX (optionnel)")]
        [SerializeField] private GameObject _vfxPrefab;
        [SerializeField] private Transform _vfxSpawnPoint;

        private GameObject _activeVfx;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsLocalPlayer(other)) return;

            if (DepthZoneManager.Instance != null)
                DepthZoneManager.Instance.TransitionToZone(_zoneIndex);

            SpawnVfx();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsLocalPlayer(other)) return;

            DestroyVfx();
        }

        private bool IsLocalPlayer(Collider col)
        {
            var nob = col.GetComponentInParent<NetworkObject>();
            return nob != null && nob.IsOwner;
        }

        private void SpawnVfx()
        {
            if (_vfxPrefab == null || _activeVfx != null) return;

            Vector3 pos = _vfxSpawnPoint != null ? _vfxSpawnPoint.position : transform.position;
            _activeVfx = Instantiate(_vfxPrefab, pos, Quaternion.identity);
        }

        private void DestroyVfx()
        {
            if (_activeVfx != null)
            {
                Destroy(_activeVfx);
                _activeVfx = null;
            }
        }
    }
}
