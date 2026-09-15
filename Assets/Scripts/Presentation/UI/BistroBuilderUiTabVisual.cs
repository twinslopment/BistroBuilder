using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Selección visual persistente para tabs 21A. No decide qué página debe abrirse;
/// únicamente representa la selección que la UI ya ha solicitado.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[AddComponentMenu("Bistro Builder/UI/Tab Visual")]
public sealed class BistroBuilderUiTabVisual : MonoBehaviour,
    IPointerClickHandler, ISelectHandler
{
    private const string UnderlineName = "BB_TabUnderline";

    [SerializeField] private bool selected;
    private Image underline;
    private Color accent = BistroBuilderUiTokens.Primary;

    public bool IsSelected => selected;

    private void Awake()
    {
        EnsureUnderline();
        Refresh();
    }

    private void OnEnable()
    {
        EnsureUnderline();
        Refresh();
    }

    public void Configure(Color accentColor)
    {
        accent = accentColor;
        EnsureUnderline();
        Refresh();
    }

    public void SetSelected(bool value)
    {
        selected = value;
        Refresh();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        SelectWithinGroup();
    }

    public void OnSelect(BaseEventData eventData)
    {
        SelectWithinGroup();
    }

    private void SelectWithinGroup()
    {
        Transform parent = transform.parent;
        if (parent != null)
        {
            BistroBuilderUiTabVisual[] tabs = parent.GetComponentsInChildren<BistroBuilderUiTabVisual>(true);
            for (int i = 0; i < tabs.Length; i++)
            {
                BistroBuilderUiTabVisual tab = tabs[i];
                if (tab != null && tab.transform.parent == parent)
                    tab.SetSelected(tab == this);
            }
        }
        else SetSelected(true);
    }

    private void EnsureUnderline()
    {
        if (underline != null) return;
        Transform existing = transform.Find(UnderlineName);
        GameObject lineObject;
        if (existing != null)
        {
            lineObject = existing.gameObject;
            underline = lineObject.GetComponent<Image>();
            if (underline == null) underline = lineObject.AddComponent<Image>();
        }
        else
        {
            lineObject = new GameObject(UnderlineName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lineObject.transform.SetParent(transform, false);
            underline = lineObject.GetComponent<Image>();
        }

        RectTransform rect = lineObject.transform as RectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, BistroBuilderUiTokens.TabUnderline);
        underline.raycastTarget = false;
        underline.color = accent;
        lineObject.transform.SetAsLastSibling();
    }

    private void Refresh()
    {
        if (underline == null) return;
        underline.color = accent;
        underline.enabled = selected;
    }
}
