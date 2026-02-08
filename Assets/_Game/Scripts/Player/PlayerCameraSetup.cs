using UnityEngine;
using FishNet.Object;

namespace VoidWarranty.Player
{
    /// <summary>
    /// Responsabilité : Gérer l'assignation de la caméra au joueur local.
    /// Ce script ne gère PAS la rotation (c'est le Movement qui le fait).
    /// Il gère juste le "parenting".
    /// </summary>
    public class PlayerCameraSetup : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _cameraRoot; // L'objet vide au niveau des yeux

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Si ce n'est pas MOI, je ne touche pas à la caméra de la scène
            if (!base.IsOwner)
            {
                return;
            }

            // C'est MOI. Je récupère la caméra de la scène.
            // Note : Camera.main est un raccourci Unity pour trouver la caméra taggée "MainCamera"
            Camera sceneCamera = Camera.main;

            if (sceneCamera != null)
            {
                // On détache la caméra de son parent actuel (au cas où)
                sceneCamera.transform.SetParent(null);

                // On l'attache à nos yeux (_cameraRoot)
                sceneCamera.transform.SetParent(_cameraRoot);

                // On reset sa position locale à (0,0,0) pour qu'elle soit pile sur le root
                sceneCamera.transform.localPosition = Vector3.zero;
                sceneCamera.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogError("Aucune MainCamera trouvée dans la scène !");
            }
        }
    }
}