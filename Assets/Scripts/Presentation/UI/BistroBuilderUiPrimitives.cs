using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BistroBuilderUiSemanticTone
{
    Neutral = 0,
    Info = 1,
    Success = 2,
    Attention = 3,
    Critical = 4
}

/// <summary>
/// API mínima para que cualquier pantalla construida por código reutilice las primitivas 21A
/// sin duplicar colores, tamaños o reglas de jerarquía.
/// </summary>
public static class BistroBuilderUiPrimitives
{
    public static void Primary(Button button) => TagAndRefresh(button, BistroBuilderUiStyleRole.PrimaryButton);
    public static void Secondary(Button button) => TagAndRefresh(button, BistroBuilderUiStyleRole.SecondaryButton);
    public static void Contextual(Button button) => TagAndRefresh(button, BistroBuilderUiStyleRole.ContextualButton);
    public static void Destructive(Button button) => TagAndRefresh(button, BistroBuilderUiStyleRole.DestructiveButton);
    public static void Navigation(Button button) => TagAndRefresh(button, BistroBuilderUiStyleRole.NavButton);

    public static void Panel(GameObject target) => TagAndRefresh(target, BistroBuilderUiStyleRole.Panel);
    public static void Card(GameObject target) => TagAndRefresh(target, BistroBuilderUiStyleRole.Card);
    public static void Toast(GameObject target) => TagAndRefresh(target, BistroBuilderUiStyleRole.Toast);

    public static void Separator(Image image)
    {
        if (image == null) return;
        image.color = BistroBuilderUiTokens.BorderSubtle;
        image.raycastTarget = false;
    }

    public static BistroBuilderUiSceneSelectionVisual SceneSelection(GameObject host, Transform target)
    {
        if (host == null || target == null) return null;
        BistroBuilderUiSceneSelectionVisual visual = host.GetComponent<BistroBuilderUiSceneSelectionVisual>();
        if (visual == null) visual = host.AddComponent<BistroBuilderUiSceneSelectionVisual>();
        visual.Configure(target);
        return visual;
    }

    public static void Tab(Button button, bool selected)
    {
        if (button == null) return;
        Tag(button.gameObject, BistroBuilderUiStyleRole.Tab);
        BistroBuilderUiTabVisual visual = button.GetComponent<BistroBuilderUiTabVisual>();
        if (visual == null) visual = button.gameObject.AddComponent<BistroBuilderUiTabVisual>();
        visual.Configure(BistroBuilderUiTokens.Primary);
        visual.SetSelected(selected);
        Refresh(button.gameObject);
    }

    public static void Badge(GameObject target, BistroBuilderUiSemanticTone tone)
    {
        if (target == null) return;
        Tag(target, BadgeRole(tone));
        TMP_Text[] texts = target.GetComponentsInChildren<TMP_Text>(true);
        Color color = SemanticColor(tone);
        for (int i = 0; i < texts.Length; i++)
            if (texts[i] != null) texts[i].color = BistroBuilderUiTokens.ContentLight;
        Image image = target.GetComponent<Image>();
        if (image != null) image.color = color;
        Refresh(target);
    }

    public static void Indicator(GameObject target, BistroBuilderUiSemanticTone tone)
    {
        if (target == null) return;
        Color color = SemanticColor(tone);
        Image[] images = target.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.gameObject == target) continue;
            image.color = color;
        }
        TMP_Text[] texts = target.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null) continue;
            BistroBuilderUiStyleTag tag = text.GetComponent<BistroBuilderUiStyleTag>();
            if (tag == null) tag = text.gameObject.AddComponent<BistroBuilderUiStyleTag>();
            tag.Configure(StatusRole(tone));
        }
        Refresh(target);
    }

    public static void Progress(Slider slider, BistroBuilderUiSemanticTone tone)
    {
        if (slider == null) return;
        BistroBuilderUiStyleRole role = tone == BistroBuilderUiSemanticTone.Success
            ? BistroBuilderUiStyleRole.ProgressSuccess
            : tone == BistroBuilderUiSemanticTone.Attention || tone == BistroBuilderUiSemanticTone.Critical
                ? BistroBuilderUiStyleRole.ProgressAttention
                : BistroBuilderUiStyleRole.ProgressInfo;
        Tag(slider.gameObject, role);
        Refresh(slider.gameObject);
    }

    public static void SetTextHierarchy(TMP_Text text, BistroBuilderUiStyleRole role)
    {
        if (text == null) return;
        if (role != BistroBuilderUiStyleRole.Title && role != BistroBuilderUiStyleRole.Heading &&
            role != BistroBuilderUiStyleRole.Body && role != BistroBuilderUiStyleRole.Caption &&
            role != BistroBuilderUiStyleRole.Kpi) return;
        Tag(text.gameObject, role);
        Refresh(text.gameObject);
    }

    private static void TagAndRefresh(Component component, BistroBuilderUiStyleRole role)
    {
        if (component == null) return;
        TagAndRefresh(component.gameObject, role);
    }

    private static void TagAndRefresh(GameObject target, BistroBuilderUiStyleRole role)
    {
        if (target == null) return;
        Tag(target, role);
        Refresh(target);
    }

    private static void Tag(GameObject target, BistroBuilderUiStyleRole role)
    {
        BistroBuilderUiStyleTag tag = target.GetComponent<BistroBuilderUiStyleTag>();
        if (tag == null) tag = target.AddComponent<BistroBuilderUiStyleTag>();
        tag.Configure(role);
    }

    private static void Refresh(GameObject target)
    {
        BistroBuilderUiDesignSystem design = target.GetComponentInParent<BistroBuilderUiDesignSystem>();
        if (design == null)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null) design = canvas.GetComponent<BistroBuilderUiDesignSystem>();
        }
        if (design != null) design.ApplyAllNow(true);
    }

    private static BistroBuilderUiStyleRole BadgeRole(BistroBuilderUiSemanticTone tone)
    {
        switch (tone)
        {
            case BistroBuilderUiSemanticTone.Info: return BistroBuilderUiStyleRole.BadgeInfo;
            case BistroBuilderUiSemanticTone.Success: return BistroBuilderUiStyleRole.BadgeSuccess;
            case BistroBuilderUiSemanticTone.Attention: return BistroBuilderUiStyleRole.BadgeAttention;
            case BistroBuilderUiSemanticTone.Critical: return BistroBuilderUiStyleRole.BadgeCritical;
            default: return BistroBuilderUiStyleRole.BadgeNeutral;
        }
    }

    private static BistroBuilderUiStyleRole StatusRole(BistroBuilderUiSemanticTone tone)
    {
        switch (tone)
        {
            case BistroBuilderUiSemanticTone.Info: return BistroBuilderUiStyleRole.StatusInfo;
            case BistroBuilderUiSemanticTone.Success: return BistroBuilderUiStyleRole.StatusSuccess;
            case BistroBuilderUiSemanticTone.Attention: return BistroBuilderUiStyleRole.StatusAttention;
            case BistroBuilderUiSemanticTone.Critical: return BistroBuilderUiStyleRole.StatusCritical;
            default: return BistroBuilderUiStyleRole.Body;
        }
    }

    private static Color SemanticColor(BistroBuilderUiSemanticTone tone)
    {
        switch (tone)
        {
            case BistroBuilderUiSemanticTone.Info: return BistroBuilderUiTokens.Info;
            case BistroBuilderUiSemanticTone.Success: return BistroBuilderUiTokens.Success;
            case BistroBuilderUiSemanticTone.Attention: return BistroBuilderUiTokens.Attention;
            case BistroBuilderUiSemanticTone.Critical: return BistroBuilderUiTokens.Critical;
            default: return BistroBuilderUiTokens.SecondaryAction;
        }
    }
}
