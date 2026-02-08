using UnityEngine;
using VoidWarranty.Core;

namespace VoidWarranty.Interaction
{
    public class ColorCube : MonoBehaviour/*, IInteractable*/
    {
        public void Interact(GameObject interactor)
        {
            Debug.Log("Interaction re�ue ! Changement de couleur.");

            // Juste pour le test visuel local imm�diat
            GetComponent<Renderer>().material.color = Random.ColorHSV();
        }

        /*public string GetInteractionPrompt()
        {
            return "Changer Couleur";
        }*/
    }
}