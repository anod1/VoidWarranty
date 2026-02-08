using UnityEngine;
using FishNet.Object;

namespace VoidWarranty.Player
{
    /// <summary>
    /// Responsabilit� : G�rer l'assignation de la cam�ra au joueur local.
    /// Ce script ne g�re PAS la rotation (c'est le Movement qui le fait).
    /// Il g�re juste le "parenting".
    /// </summary>
    public class PlayerCameraSetup : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _cameraRoot; // L'objet vide au niveau des yeux

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Si ce n'est pas MOI, je ne touche pas � la cam�ra de la sc�ne
            if (!base.IsOwner)
            {
                return;
            }

            // C'est MOI. Je r�cup�re la cam�ra de la sc�ne.
            // Note : Camera.main est un raccourci Unity pour trouver la cam�ra tagg�e "MainCamera"
            Camera sceneCamera = Camera.main;

            if (sceneCamera != null)
            {
                // On d�tache la cam�ra de son parent actuel (au cas o�)
                sceneCamera.transform.SetParent(null);

                // On l'attache � nos yeux (_cameraRoot)
                sceneCamera.transform.SetParent(_cameraRoot);

                // On reset sa position locale � (0,0,0) pour qu'elle soit pile sur le root
                sceneCamera.transform.localPosition = Vector3.zero;
                sceneCamera.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogError("Aucune MainCamera trouv�e dans la sc�ne !");
            }
        }
    }
}