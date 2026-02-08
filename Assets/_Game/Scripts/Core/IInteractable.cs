using UnityEngine;

namespace VoidWarranty.Core
{
    // Tout objet qui veut être cliqué DOIT posséder un script qui implémente ça.
    public interface IInteractable
    {
        // On passe l'info de QUI interagit (pour savoir qui a appuyé sur le bouton)
        void Interact(GameObject interactor);

        // Optionnel : Pour afficher un texte "Appuyez sur E pour ouvrir"
        string GetInteractionPrompt();
    }
}