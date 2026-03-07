using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidWarranty.Interaction
{
    /// <summary>
    /// Action grab : lever et retourner l'objet devant soi (clic gauche maintenu).
    /// Utilisé sur le whiteboard pour montrer le contenu aux autres joueurs.
    ///
    /// SETUP :
    /// → Ajouter sur le même GO que GrabbableObject
    /// → Les paramètres par défaut conviennent pour le whiteboard
    /// → Ajuster _brandishOffset / _brandishAngle pour d'autres objets
    /// </summary>
    public class BrandishAction : MonoBehaviour, IGrabAction
    {
        [Header("Brandish Settings")]
        [Tooltip("Offset position ajouté quand on brandit l'objet (local au hold point).")]
        [SerializeField] private Vector3 _brandishOffset = new Vector3(0f, 0.3f, 0f);

        [Tooltip("Vitesse de transition repos ↔ brandi.")]
        [SerializeField] private float _brandishSpeed = 8f;

        [Tooltip("Angle de rotation quand on brandit (180 = retournement).")]
        [SerializeField] private float _brandishAngle = 180f;

        private float _brandishT; // 0 = repos, 1 = brandi

        /// <summary>Accès réseau au paramètre d'animation (owner écrit, non-owner lit).</summary>
        public float ReplicatedParam
        {
            get => _brandishT;
            set => _brandishT = value;
        }

        // =====================================================================
        // IGrabAction
        // =====================================================================

        public void OnGrabStart()
        {
            _brandishT = 0f;
        }

        public void OnGrabEnd()
        {
            _brandishT = 0f;
        }

        public void OnGrabUpdate()
        {
            var mouse = Mouse.current;
            bool wantBrandish = mouse != null && mouse.leftButton.isPressed;
            float target = wantBrandish ? 1f : 0f;
            _brandishT = Mathf.MoveTowards(_brandishT, target, _brandishSpeed * Time.deltaTime);
        }

        public void ModifyHoldTarget(ref Vector3 targetPos, ref Quaternion targetRot, Transform holdPoint)
        {
            if (_brandishT < 0.001f) return;

            targetPos += (holdPoint.right * _brandishOffset.x +
                          holdPoint.up * _brandishOffset.y +
                          holdPoint.forward * _brandishOffset.z) * _brandishT;

            targetRot *= Quaternion.Euler(0f, _brandishT * _brandishAngle, 0f);
        }
    }
}
