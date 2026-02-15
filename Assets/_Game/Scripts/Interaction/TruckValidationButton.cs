using UnityEngine;
using FishNet.Object;
using VoidWarranty.Core;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Bouton d'interaction pour extraire (quitter la mission).
    /// Système Tarkov-like : le joueur peut extract à tout moment.
    /// Peut être attaché à un objet 3D interactif (porte du camion, bouton, etc.).
    /// </summary>
    public class TruckValidationButton : NetworkBehaviour, IInteractable
    {
        [SerializeField] private TruckZone _truckZone;

        protected virtual void Start()
        {
            // Trouver la TruckZone si non assignée
            if (_truckZone == null)
                _truckZone = GetComponentInParent<TruckZone>();
        }

        public void Interact(GameObject interactor)
        {
            if (MissionManager.Instance == null) return;

            var state = MissionManager.Instance.GetState();

            if (state == MissionManager.MissionState.Active)
            {
                Debug.Log("[TruckValidationButton] Extraction demandée...");
                CmdExtract();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdExtract()
        {
            if (_truckZone == null)
            {
                Debug.LogWarning("[TruckValidationButton] TruckZone non assignée !");
                return;
            }

            _truckZone.ValidateMissionServerRpc();
        }

        public string GetInteractionPrompt()
        {
            // Afficher le prompt uniquement si la mission est active
            if (MissionManager.Instance != null)
            {
                var state = MissionManager.Instance.GetState();
                if (state == MissionManager.MissionState.Active)
                {
                    return LocalizationManager.Get("INTERACT_EXTRACT");
                }
            }

            return ""; // Masquer le prompt si pas active
        }
    }
}
