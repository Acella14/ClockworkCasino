using UnityEngine;

public class TogglePanel : MonoBehaviour
{
    [SerializeField] GameObject panel;

    public void Show()   { if (panel) panel.SetActive(true); }
    public void Hide()   { if (panel) panel.SetActive(false); }
    public void Toggle() { if (panel) panel.SetActive(!panel.activeSelf); }
}
