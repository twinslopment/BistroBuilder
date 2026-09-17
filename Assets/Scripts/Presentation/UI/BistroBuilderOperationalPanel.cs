using System;
using System.Collections.Generic;
using BistroBuilder.UI.Iconography;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared close affordance and scrolling for compact operational screens.</summary>
public sealed class BistroBuilderOperationalPanel : MonoBehaviour
{
    static readonly List<BistroBuilderOperationalPanel> panels = new();
    Action close;
    public static bool AnyOpen { get { foreach (var panel in panels) if (panel != null && panel.isActiveAndEnabled) return true; return false; } }
    public static void CloseAll() { foreach (var panel in panels) if (panel != null && panel.isActiveAndEnabled) panel.close?.Invoke(); }
    void OnDestroy() => panels.Remove(this);
    public static void Install(GameObject panel, TMP_Text body, string title, Action close)
    {
        var controller = panel.AddComponent<BistroBuilderOperationalPanel>(); controller.close = close; panels.Add(controller);
        var image = panel.GetComponent<Image>(); image.color = BistroBuilderUiTokens.Surface1;
        BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Panel);
        var rect = (RectTransform)panel.transform; rect.offsetMax = Vector2.zero;
        var parent = (RectTransform)rect.parent; parent.anchoredPosition = new Vector2(parent.anchoredPosition.x, -82);
        var heading = BistroBuilderMenuEditorUiFactory.CreateText("Heading", panel.transform, title, 21, TextAnchor.MiddleLeft, BistroBuilderUiTokens.TextPrimary);
        heading.font = BistroBuilderTypography.LegacyTitle;
        heading.rectTransform.anchorMin = Vector2.up; heading.rectTransform.anchorMax = Vector2.one;
        heading.rectTransform.offsetMin = new Vector2(16, -52); heading.rectTransform.offsetMax = new Vector2(-108, -8);
        var button = BistroBuilderMenuEditorUiFactory.CreateButton("CloseOperations", panel.transform, "Cerrar", () => close(), BistroBuilderUiTokens.Surface2, 14);
        button.GetComponent<RectTransform>().anchorMin = button.GetComponent<RectTransform>().anchorMax = Vector2.one;
        button.GetComponent<RectTransform>().offsetMin = new Vector2(-104,-48); button.GetComponent<RectTransform>().offsetMax = new Vector2(-12,-12);
        BBIconographyRuntime.Decorate(button, BBIconId.ActionCancel);
        BistroBuilderInteractionSurface.Attach(button);
        var viewport = BistroBuilderMenuEditorUiFactory.CreateRect("BodyViewport", panel.transform, body.rectTransform.anchorMin, body.rectTransform.anchorMax, body.rectTransform.offsetMin, new Vector2(-12, -62));
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.viewport = viewport; scroll.scrollSensitivity = 30; scroll.movementType = ScrollRect.MovementType.Clamped;
        body.transform.SetParent(viewport, false);
        body.rectTransform.anchorMin = Vector2.up; body.rectTransform.anchorMax = Vector2.one; body.rectTransform.pivot = new Vector2(.5f,1);
        body.rectTransform.offsetMin = body.rectTransform.offsetMax = Vector2.zero;
        body.enableAutoSizing = false; body.font = BistroBuilderTypography.Body; body.fontSize = 14; body.fontStyle = FontStyles.Normal;
        body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = body.rectTransform;
    }
}
