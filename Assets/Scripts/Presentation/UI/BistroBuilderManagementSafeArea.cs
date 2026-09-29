using UnityEngine;

/// <summary>Keeps management content clear of the responsive navigation and bottom HUD.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderManagementSafeArea : MonoBehaviour
{
    private RectTransform rect;
    private Canvas canvas;
    private BistroBuilderUiShell shell;
    public static void Install(RectTransform root)
    {
        if (root == null) return;
        var area = root.GetComponent<BistroBuilderManagementSafeArea>() ?? root.gameObject.AddComponent<BistroBuilderManagementSafeArea>();
        area.Apply();
    }
    private void LateUpdate() => Apply();
    private void Apply()
    {
        if (rect == null) rect = transform as RectTransform;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (shell == null) shell = FindFirstObjectByType<BistroBuilderUiShell>();
        if (rect == null || canvas == null) return;
        float top = shell != null ? shell.ContentTopInset(canvas) : 120;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 76); rect.offsetMax = new Vector2(-12, -top);
    }
}
