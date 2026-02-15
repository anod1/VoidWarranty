using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using VoidWarranty.Core;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Zone de camion pour la boucle de gameplay Tarkov-like :
    /// - Les items déposés dans la zone restent physiquement présents
    /// - À l'extraction (bouton), on vérifie quels items sont dans la zone
    /// - Pièce défectueuse dans la zone → bonus scrap
    /// - Extraction : le joueur peut partir à tout moment
    /// </summary>
    public class TruckZone : NetworkBehaviour
    {
        // Items actuellement dans la zone
        private readonly HashSet<GrabbableObject> _itemsInZone = new();

        // =====================================================================
        // Trigger (tracker les items présents, sans despawn)
        // =====================================================================

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServer) return;

            if (other.TryGetComponent(out GrabbableObject item))
            {
                _itemsInZone.Add(item);
                Debug.Log($"[TruckZone] Item entré dans la zone : {item.name}");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!base.IsServer) return;

            if (other.TryGetComponent(out GrabbableObject item))
            {
                _itemsInZone.Remove(item);
                Debug.Log($"[TruckZone] Item sorti de la zone : {item.name}");
            }
        }

        // =====================================================================
        // Extraction (à tout moment)
        // =====================================================================

        /// <summary>
        /// Appelé quand le joueur appuie sur le bouton d'extraction du camion.
        /// Vérifie les items présents dans la zone et valide les objectifs.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ValidateMissionServerRpc()
        {
            if (MissionManager.Instance == null) return;

            Debug.Log("[TruckZone] Extraction demandée → Vérification des items dans la zone");

            // Vérifier les items dans la zone au moment de l'extraction
            ValidateItemsInZone();

            MissionManager.Instance.Extract();
        }

        /// <summary>
        /// Parcourt les items présents dans la zone et notifie le MissionManager.
        /// </summary>
        [Server]
        private void ValidateItemsInZone()
        {
            // Nettoyer les refs nulles (items détruits entre-temps)
            _itemsInZone.RemoveWhere(item => item == null);

            foreach (var item in _itemsInZone)
            {
                ItemData data = item.GetData();
                if (data == null) continue;

                if (data.IsDefective)
                {
                    Debug.Log($"[TruckZone] Pièce défectueuse {data.NameKey} trouvée dans la zone → Bonus scrap");

                    if (GameManager.Instance != null)
                        GameManager.Instance.NotifyDefectivePartRecovered(data);
                }
            }
        }
    }
}
