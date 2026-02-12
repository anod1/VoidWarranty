using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text textComponent;
    public TMP_FontAsset normalFont;
    public Material normalMaterial;
    public Material hoverMaterial;

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Quand la souris entre : on applique le matériau avec l'effet
        textComponent.fontSharedMaterial = hoverMaterial;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Quand la souris sort : on remet le matériau normal
        textComponent.fontSharedMaterial = normalMaterial;
    }
}