using UnityEngine;

namespace SubSurface.Level
{
    /// <summary>
    /// Caméra de surveillance dans le couloir.
    /// La RenderTexture est affichée sur un quad physique dans la scène —
    /// tout joueur qui peut voir le quad voit l'image. Pas besoin de restriction.
    ///
    /// Le script garde la Camera active en jeu et la désactive en éditeur
    /// pour éviter de render inutilement.
    ///
    /// SETUP ÉDITEUR :
    /// → Créer une Camera dans le couloir A (position + rotation de surveillance)
    /// → Créer une RenderTexture dans Assets/_Game/Textures/
    /// → Assigner la RenderTexture à la Camera (Target Texture)
    /// → Créer un Material Unlit utilisant cette RenderTexture
    /// → Appliquer ce Material sur le quad/mesh du moniteur en salle de commandes
    /// → Attacher SurveillanceMonitor.cs sur la Camera
    /// → La Camera peut être activée directement dans l'éditeur
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SurveillanceMonitor : MonoBehaviour
    {
        private void Awake()
        {
            // S'assurer que la caméra est active en jeu
            Camera cam = GetComponent<Camera>();
            if (cam != null)
                cam.enabled = true;
        }
    }
}
