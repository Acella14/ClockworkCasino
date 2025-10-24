using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

[DisallowMultipleComponent]
public class HoverSwapLabel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Target")]
    public TextMeshProUGUI target;

    [Header("Texts")]
    public string idleText = "No.";
    public string hoverText = "Absolutely.";

    [Header("State")]
    public bool enabledForThisStep = false;

    void Reset()
    {
        if (target == null) target = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void ApplyIdle()
    {
        if (enabledForThisStep && target) target.text = idleText;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!enabledForThisStep || target == null) return;
        target.text = hoverText;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!enabledForThisStep || target == null) return;
        target.text = idleText;
    }
}
