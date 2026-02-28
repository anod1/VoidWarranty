using UnityEngine;

namespace VoidWarranty.Player
{
    /// <summary>
    /// Ajouté dynamiquement sur l'item tenu (held visual).
    /// Permet de tweaker position/rotation en temps réel dans l'Inspector.
    ///
    /// WORKFLOW :
    /// 1. Équiper un item en jeu
    /// 2. Dans la hiérarchie, sélectionner le GO "Held_xxx" sous HoldPoint
    /// 3. Ajuster Position / Rotation dans l'Inspector → feedback immédiat
    /// 4. Copier les valeurs finales dans ItemRegistry (Hold Point Offsets)
    /// 5. Stop Play → les valeurs sont sauvées dans le ScriptableObject
    /// </summary>
    public class HeldItemOffset : MonoBehaviour
    {
        [Header("Tweaker en temps réel")]
        [Tooltip("Position locale par rapport au HoldPoint")]
        public Vector3 Position;
        [Tooltip("Rotation locale en Euler angles")]
        public Vector3 Rotation;

        private void LateUpdate()
        {
            transform.localPosition = Position;
            transform.localRotation = Quaternion.Euler(Rotation);
        }
    }
}
