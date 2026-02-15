using UnityEngine;
using FishNet.Object;
using FishNet.Component.Transforming;
using VoidWarranty.Core;

namespace VoidWarranty.Interaction
{
    public class TruckZone : NetworkBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServer) return;

            if (other.TryGetComponent(out GrabbableObject item))
            {
                ItemData data = item.GetData();
                if (data != null)
                {
                    if (data.IsDefective)
                    {
                        Debug.Log($"[TruckZone] Pièce défectueuse {data.NameKey} récupérée.");

                        // Notifier le GameManager
                        if (GameManager.Instance != null)
                            GameManager.Instance.NotifyDefectivePartRecovered(data);

                        // Désactiver le NetworkTransform avant le Despawn pour éviter l'erreur
                        if (item.TryGetComponent(out NetworkTransform nt))
                            nt.enabled = false;

                        item.NetworkObject.Despawn();
                    }
                    else
                    {
                        Debug.Log($"[TruckZone] Refusé : pièce neuve ({data.NameKey}).");
                    }
                }
            }
        }
    }
}