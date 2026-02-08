using UnityEngine;

namespace VoidWarranty.Core
{
    // Tout objet qui veut �tre cliqu� DOIT poss�der un script qui impl�mente �a.
    public interface IInteractable
    {
        // On passe l'info de QUI interagit (pour savoir qui a appuy� sur le bouton)
        void Interact(GameObject interactor);

        // Optionnel : Pour afficher un texte "Appuyez sur E pour ouvrir"
        string GetInteractionPrompt();
    }
}