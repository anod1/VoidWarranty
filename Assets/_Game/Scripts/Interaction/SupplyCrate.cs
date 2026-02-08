// ============================================================================
// SupplyCrate.cs — Caisse de livraison contenant la pièce de rechange
// ============================================================================
// Hérite de GrabbableObject : on peut la transporter comme n'importe quel objet.
// Une fois posée au sol, le joueur interagit pour l'ouvrir.
// L'ouverture désactive le couvercle et rend la pièce de rechange grabbable.
//
// Hiérarchie attendue du Prefab :
//   SupplyCrate (ce script + Rigidbody + NetworkObject + Collider)
//   ├── Model_Body      (mesh de la caisse sans couvercle)
//   ├── Model_Lid        (mesh du couvercle — désactivé à l'ouverture)
//   └── SparePartAnchor  (Transform vide, position de la pièce à l'intérieur)
//       └── [Pièce de rechange prefab] (GrabbableObject, désactivé au Start)
//
// La pièce de rechange est un ENFANT dans le prefab, désactivée.
// À l'ouverture, on la détache, on l'active, et on la spawn sur le réseau.
// ============================================================================

using UnityEngine;
using FishNet.Object;
using VoidWarranty.Core;

namespace VoidWarranty.Interaction
{
    public class SupplyCrate : GrabbableObject
    {
        // =====================================================================
        // Inspector
        // =====================================================================

        [Header("Supply Crate")]
        [SerializeField] private GameObject _lid;               // Le couvercle (désactivé à l'ouverture)
        [SerializeField] private GameObject _sparePart;          // La pièce de rechange (enfant, désactivée)
        [SerializeField] private Transform  _spawnPoint;         // Où spawner la pièce (si pas de sparePart enfant)
        [SerializeField] private GameObject _sparePartPrefab;    // Alternative : prefab à instancier

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip   _openSound;         // Son d'ouverture de caisse

        // =====================================================================
        // State
        // =====================================================================

        private bool _isOpen = false;

        // =====================================================================
        // Initialisation
        // =====================================================================

        protected override void Awake()
        {
            base.Awake();

            // S'assurer que la pièce de rechange est bien désactivée au départ
            if (_sparePart != null)
                _sparePart.SetActive(false);
        }

        // =====================================================================
        // Interaction — Override du GrabbableObject
        // =====================================================================
        //
        // GrabbableObject.Interact() est vide par défaut.
        // PlayerInteraction appelle Interact() quand le joueur appuie sur E.
        // PlayerGrab écoute OnGrabToggle (clic gauche) pour grab/drop.
        //
        // Donc quand le joueur regarde la caisse et appuie sur E :
        //   - Si la caisse est au sol et fermée → on l'ouvre
        //   - Si la caisse est au sol et ouverte → rien (la pièce est déjà accessible)
        //   - Si la caisse est tenue → rien (on ne peut pas ouvrir en portant)
        //
        // Pour GRAB la caisse, le joueur utilise le clic gauche (géré par PlayerGrab).
        // ============================================================================

        public override void Interact(GameObject interactor)
        {
            // Si déjà ouverte ou si on la tient en main, on ne fait rien
            if (_isOpen) return;
            if (IsHeld.Value) return;

            // Demander au serveur d'ouvrir
            CmdRequestOpen();
        }

        // =====================================================================
        // Réseau
        // =====================================================================

        [ServerRpc(RequireOwnership = false)]
        private void CmdRequestOpen()
        {
            if (_isOpen) return;

            _isOpen = true;

            // Spawner la pièce de rechange
            SpawnSparePart();

            // Informer tous les clients de l'ouverture visuelle
            ObserversOnCrateOpened();
        }

        [ObserversRpc]
        private void ObserversOnCrateOpened()
        {
            _isOpen = true;

            // Désactiver le couvercle
            if (_lid != null)
                _lid.SetActive(false);

            // Son d'ouverture
            if (_audioSource != null && _openSound != null)
                _audioSource.PlayOneShot(_openSound);
        }

        // =====================================================================
        // Spawn de la pièce de rechange
        // =====================================================================

        private void SpawnSparePart()
        {
            // Méthode A : La pièce est un enfant désactivé dans le prefab
            if (_sparePart != null)
            {
                // Détacher du parent pour qu'elle devienne indépendante
                _sparePart.transform.SetParent(null);
                _sparePart.SetActive(true);

                // Spawn sur le réseau (elle a déjà un NetworkObject en tant que GrabbableObject)
                if (_sparePart.TryGetComponent(out NetworkObject netObj))
                {
                    Spawn(netObj);
                }

                return;
            }

            // Méthode B : Instancier depuis un prefab
            if (_sparePartPrefab != null && _spawnPoint != null)
            {
                GameObject instance = Instantiate(
                    _sparePartPrefab,
                    _spawnPoint.position,
                    _spawnPoint.rotation
                );

                Spawn(instance);
            }
        }

        // =====================================================================
        // Prompts
        // =====================================================================

        public override string GetInteractionPrompt()
        {
            // Si on peut grab (comportement par défaut de GrabbableObject)
            // ET qu'on peut ouvrir, on doit montrer le bon prompt.

            if (_isOpen)
            {
                // Caisse ouverte : le joueur peut quand même la grab pour la déplacer
                string name = (_data != null) ? LocalizationManager.Get(_data.NameKey) : "Caisse";
                string action = LocalizationManager.Get("ACTION_TAKE");
                return $"{name}\n<size=80%><color=yellow>[{action}]</color></size>";
            }

            if (IsHeld.Value)
            {
                // Tenue en main : pas de prompt spécial (drop est sur clic gauche)
                return "";
            }

            // Fermée et au sol : proposer d'ouvrir
            string openAction = LocalizationManager.Get("ACTION_OPEN_CRATE");
            return $"{LocalizationManager.Get("ITEM_SUPPLY_CRATE")}\n<size=80%><color=yellow>[{openAction}]</color></size>";
        }
    }
}
