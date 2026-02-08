using UnityEngine;
using FishNet.Object;
using FishNet.Component.Transforming; // Nécessaire pour trouver le NetworkTransform
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
                        Debug.Log($"✅ CONTRAT VALIDÉ : Pièce défectueuse {data.NameKey} récupérée !");

                        // --- FIX DE L'ERREUR ROUGE ---
                        // On coupe le sifflet au NetworkTransform avant de tuer l'objet
                        if (item.TryGetComponent(out NetworkTransform nt))
                        {
                            nt.enabled = false;
                        }
                        // -----------------------------

                        item.NetworkObject.Despawn();
                    }
                    else
                    {
                        Debug.Log($"⚠️ REFUSÉ : Vous avez ramené une pièce neuve ({data.NameKey}).");
                    }
                }
            }
        }
    }
}