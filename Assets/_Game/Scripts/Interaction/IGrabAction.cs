using UnityEngine;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Interface pour les actions exécutées sur un objet tenu (grab).
    /// Chaque GrabbableObject peut avoir un composant IGrabAction.
    /// PlayerGrab détecte l'interface et délègue les inputs.
    ///
    /// Exemples :
    ///   - BrandishAction : lever/retourner l'objet (whiteboard)
    ///   - ShockwaveAction : onde de choc qui repousse les objets
    ///   - FlashlightAction : allumer/éteindre une lampe
    ///
    /// SETUP :
    /// → Ajouter le composant IGrabAction sur le même GO que GrabbableObject
    /// → PlayerGrab appelle automatiquement les méthodes
    /// </summary>
    public interface IGrabAction
    {
        /// <summary>
        /// Appelé chaque frame (Update) pendant que l'objet est tenu.
        /// Utilisé pour lire les inputs et mettre à jour l'état interne (ex: _brandishT).
        /// </summary>
        void OnGrabUpdate();

        /// <summary>
        /// Appelé dans FixedUpdate. Modifie targetPos/targetRot AVANT le velocity drive.
        /// Permet d'ajouter des offsets visuels (brandish, oscillation, etc.).
        /// </summary>
        /// <param name="targetPos">Position cible (modifiable)</param>
        /// <param name="targetRot">Rotation cible (modifiable)</param>
        /// <param name="holdPoint">Transform du hold point (pour référence axes)</param>
        void ModifyHoldTarget(ref Vector3 targetPos, ref Quaternion targetRot, Transform holdPoint);

        /// <summary>
        /// Appelé quand l'objet est grabbé. Init/reset état.
        /// </summary>
        void OnGrabStart();

        /// <summary>
        /// Appelé quand l'objet est lâché. Cleanup état.
        /// </summary>
        void OnGrabEnd();

        /// <summary>
        /// Paramètre répliqué pour la synchronisation réseau.
        /// Le owner écrit (ex: _brandishT), le non-owner lit la valeur synchro.
        /// </summary>
        float ReplicatedParam { get; set; }
    }
}
