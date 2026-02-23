using UnityEngine;
using FishNet;
using System.Collections;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Trigger simple : quand un joueur passe, ferme et verrouille la porte.
    /// Usage unique. Remplace RoleAssignmentTrigger (plus de rôles).
    ///
    /// SETUP ÉDITEUR :
    /// → GO vide sur le seuil de la porte (juste après, côté destination)
    /// → BoxCollider (isTrigger) couvrant le passage
    /// → Inspector : glisser la LevelDoor à verrouiller
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DoorLockTrigger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LevelDoor _doorToLock;

        [Header("Settings")]
        [SerializeField] private float _lockDelay = 0.1f;

        private bool _triggered;

        private void OnTriggerEnter(Collider other)
        {
            if (!InstanceFinder.IsServerStarted) return;
            if (_triggered) return;

            // Vérifier que c'est un joueur (layer 7)
            if (other.gameObject.layer != 7) return;

            _triggered = true;

            if (_doorToLock != null)
            {
                _doorToLock.Close();
                StartCoroutine(LockAfterDelay());
            }

            Debug.Log($"[DoorLockTrigger] Joueur passé → porte fermée et verrouillée.");
        }

        private IEnumerator LockAfterDelay()
        {
            yield return new WaitForSeconds(_lockDelay);
            _doorToLock.Lock();
        }
    }
}
