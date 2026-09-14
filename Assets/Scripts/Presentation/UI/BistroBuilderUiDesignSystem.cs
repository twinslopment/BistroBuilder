using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 21A UI/UX definitiva: estilos centralizados y escalado responsive.
/// Es Presentation pura; no decide ningÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âºn estado de negocio.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Design System")]
public sealed class BistroBuilderUiDesignSystem : MonoBehaviour
{
    public const string RuntimeRevision = "21A-UIUX-V1.3-SCROLL21C-UNIVERSAL";

    [SerializeField] private BistroBuilderUiTheme theme;
    [SerializeField] private bool styleRuntimeContent = true;
    [SerializeField, Range(0.2f, 3f)] private float rescanIntervalSeconds = 0.55f;
    [SerializeField, Range(0.8f, 1.5f)] private float uiScale = 1f;

    private readonly HashSet<int> styledIds = new HashSet<int>();
    private Canvas canvas;
    private CanvasScaler scaler;
    private float nextScanAt;

    public BistroBuilderUiTheme Theme => theme;
    public Canvas Canvas => canvas;
    public float UiScale => uiScale;

    private void Awake()
    {
        Resolve();
        ConfigureCanvas();
        ApplyAllNow(true);
    }

    private void OnEnable()
    {
        Resolve();
        ConfigureCanvas();
        ApplyAllNow(true);
    }

    private void Update()
    {
        if (!Application.isPlaying || !styleRuntimeContent || Time.unscaledTime < nextScanAt)
            return;
        ApplyAllNow(false);
    }

    public bool ValidateConfiguration(out string error)
    {
        Resolve();
        if (canvas == null)
        {
            error = "BB UI Design System debe vivir bajo el Canvas HUD canÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³nico.";
            return false;
        }
        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            error = "El Canvas HUD no dispone de GraphicRaycaster.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void SetTheme(BistroBuilderUiTheme value)
    {
        theme = value;
        ApplyAllNow(true);
    }

    public void SetUiScale(float value)
    {
        uiScale = Mathf.Clamp(value, 0.8f, 1.5f);
        ConfigureCanvas();
    }

    public void ApplyAllNow(bool force)
    {
        Resolve();
        if (canvas == null) return;
        ConfigureCanvas();
        if (force) styledIds.Clear();

        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++) StyleButton(buttons[i], force);
        Toggle[] toggles = canvas.GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < toggles.Length; i++) StyleToggle(toggles[i], force);
        TMP_InputField[] tmpInputs = canvas.GetComponentsInChildren<TMP_InputField>(true);
        for (int i = 0; i < tmpInputs.Length; i++) StyleTmpInput(tmpInputs[i], force);
        InputField[] inputs = canvas.GetComponentsInChildren<InputField>(true);
        for (int i = 0; i < inputs.Length; i++) StyleLegacyInput(inputs[i], force);
        TMP_Dropdown[] tmpDropdowns = canvas.GetComponentsInChildren<TMP_Dropdown>(true);
        for (int i = 0; i < tmpDropdowns.Length; i++) StyleTmpDropdown(tmpDropdowns[i], force);
        Dropdown[] dropdowns = canvas.GetComponentsInChildren<Dropdown>(true);
        for (int i = 0; i < dropdowns.Length; i++) StyleLegacyDropdown(dropdowns[i], force);
        Scrollbar[] scrollbars = canvas.GetComponentsInChildren<Scrollbar>(true);
        for (int i = 0; i < scrollbars.Length; i++) StyleScrollbar(scrollbars[i], force);
        ScrollRect[] scrollRects = UnityEngine.Object.FindObjectsByType<ScrollRect>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scrollRects.Length; i++)
            if (scrollRects[i] != null && scrollRects[i].gameObject.scene == canvas.gameObject.scene &&
                (!Application.isPlaying || scrollRects[i].gameObject.activeInHierarchy))
                BistroBuilderUiScrollRegion.Configure(scrollRects[i]);
        TMP_Text[] tmpTexts = canvas.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++) StyleTmpText(tmpTexts[i], force);
        Text[] legacyTexts = canvas.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < legacyTexts.Length; i++) StyleLegacyText(legacyTexts[i], force);
        Image[] images = canvas.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++) StyleStructuralImage(images[i], force);
        RawImage[] rawImages = canvas.GetComponentsInChildren<RawImage>(true);
        for (int i = 0; i < rawImages.Length; i++) StyleRawImagePlaceholder(rawImages[i], force);

        nextScanAt = Time.unscaledTime + Mathf.Max(0.2f, rescanIntervalSeconds);
    }
    public Color ResolveColor(BistroBuilderUiStyleRole role)
    {
        switch (role)
        {
            case BistroBuilderUiStyleRole.Background: return Background;
            case BistroBuilderUiStyleRole.Surface: return Surface1;
            case BistroBuilderUiStyleRole.SurfaceElevated: return SurfaceElevated;
            case BistroBuilderUiStyleRole.Overlay: return BistroBuilderUiTokens.Overlay;
            case BistroBuilderUiStyleRole.PrimaryButton:
            case BistroBuilderUiStyleRole.NavButton: return Primary;
            case BistroBuilderUiStyleRole.DestructiveButton:
            case BistroBuilderUiStyleRole.StatusCritical: return Critical;
            case BistroBuilderUiStyleRole.StatusSuccess: return Success;
            case BistroBuilderUiStyleRole.StatusAttention: return Attention;
            case BistroBuilderUiStyleRole.StatusInfo: return Info;
            case BistroBuilderUiStyleRole.SecondaryButton:
            case BistroBuilderUiStyleRole.Tab:
            case BistroBuilderUiStyleRole.Field:
            case BistroBuilderUiStyleRole.Row:
            case BistroBuilderUiStyleRole.BottomDock: return Surface2;
            default: return Surface1;
        }
    }

    private void Resolve()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) scaler = canvas.GetComponent<CanvasScaler>();
    }

    private void ConfigureCanvas()
    {
        if (canvas == null) return;
        if (scaler == null) scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = BistroBuilderUiTokens.ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
    }

    private bool Begin(Component component, bool force)
    {
        if (component == null) return false;
        int id = component.GetInstanceID();
        if (!force && styledIds.Contains(id)) return false;
        styledIds.Add(id);
        if (component is Selectable selectable) BistroBuilderInteractionSurface.Attach(selectable);
        if (component.GetComponentInParent<BistroBuilderTopBarSurface>() != null) return false;
        return true;
    }

    private void StyleButton(Button button, bool force)
    {
        if (!Begin(button, force)) return;
        BistroBuilderUiStyleRole role = ResolveButtonRole(button.gameObject);
        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image != null)
        {
            Color normal = ResolveColor(role);
            if (role == BistroBuilderUiStyleRole.TertiaryButton)
                normal = new Color(Surface1.r, Surface1.g, Surface1.b, 0.35f);
            button.transition = Selectable.Transition.ColorTint;
            button.colors = BistroBuilderUiTokens.ButtonColors(
                normal,
                Color.Lerp(normal, Color.white, role == BistroBuilderUiStyleRole.PrimaryButton ? 0.10f : 0.07f),
                Color.Lerp(normal, Color.black, 0.16f));
            image.color = Color.white;
        }
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = Mathf.Max(12f, label.fontSize);
        }
        if (button.GetComponent<BistroBuilderUiSelectableMotion>() == null)
            button.gameObject.AddComponent<BistroBuilderUiSelectableMotion>();
        var depth = BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Base);
        if (role == BistroBuilderUiStyleRole.DestructiveButton) depth?.SetBorder(BistroBuilderBorderState.Critical);
    }

    private void StyleToggle(Toggle toggle, bool force)
    {
        if (!Begin(toggle, force)) return;
        toggle.colors = BistroBuilderUiTokens.ButtonColors(
            Surface2, Color.Lerp(Surface2, Color.white, 0.08f), Color.Lerp(Surface2, Color.black, 0.12f));
        if (toggle.GetComponent<BistroBuilderUiSelectableMotion>() == null)
            toggle.gameObject.AddComponent<BistroBuilderUiSelectableMotion>();
        BistroBuilderSurface.Apply(toggle.targetGraphic as Image, BistroBuilderSurfaceLevel.Base);
    }

    private void StyleTmpInput(TMP_InputField input, bool force)
    {
        if (!Begin(input, force)) return;
        if (input.targetGraphic is Image image) { image.color = Surface2; BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Base); }
        if (input.textComponent != null)
        {
            input.textComponent.color = TextPrimary;
            ApplyBodyFont(input.textComponent);
        }
        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.color = new Color(TextMuted.r, TextMuted.g, TextMuted.b, 0.86f);
            ApplyBodyFont(placeholder);
        }
    }

    private void StyleLegacyInput(InputField input, bool force)
    {
        if (!Begin(input, force)) return;
        if (input.targetGraphic is Image image) { image.color = Surface2; BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Base); }
        if (input.textComponent != null) input.textComponent.color = TextPrimary;
        if (input.placeholder is Text placeholder) placeholder.color = TextMuted;
    }

    private void StyleTmpDropdown(TMP_Dropdown dropdown, bool force)
    {
        if (!Begin(dropdown, force)) return;
        if (dropdown.targetGraphic is Image image) { image.color = Surface2; BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Base); }
        dropdown.colors = BistroBuilderUiTokens.ButtonColors(
            Surface2, Color.Lerp(Surface2, Color.white, 0.08f), Color.Lerp(Surface2, Color.black, 0.12f));
        if (dropdown.captionText != null)
        {
            dropdown.captionText.color = TextPrimary;
            ApplyBodyFont(dropdown.captionText);
        }
        if (dropdown.GetComponent<BistroBuilderUiDropdownMotion>() == null)
            dropdown.gameObject.AddComponent<BistroBuilderUiDropdownMotion>();
    }

    private void StyleLegacyDropdown(Dropdown dropdown, bool force)
    {
        if (!Begin(dropdown, force)) return;
        if (dropdown.targetGraphic is Image image) { image.color = Surface2; BistroBuilderSurface.Apply(image, BistroBuilderSurfaceLevel.Base); }
        dropdown.colors = BistroBuilderUiTokens.ButtonColors(
            Surface2, Color.Lerp(Surface2, Color.white, 0.08f), Color.Lerp(Surface2, Color.black, 0.12f));
        if (dropdown.captionText != null) dropdown.captionText.color = TextPrimary;
        if (dropdown.GetComponent<BistroBuilderUiDropdownMotion>() == null)
            dropdown.gameObject.AddComponent<BistroBuilderUiDropdownMotion>();
    }

    private void StyleScrollbar(Scrollbar scrollbar, bool force)
    {
        if (!Begin(scrollbar, force)) return;
        scrollbar.colors = BistroBuilderUiTokens.ButtonColors(
            Surface2, Color.Lerp(Surface2, Color.white, 0.08f), Color.Lerp(Surface2, Color.black, 0.10f));
        if (scrollbar.targetGraphic != null)
            scrollbar.targetGraphic.color = new Color(WarmAccent.r, WarmAccent.g, WarmAccent.b, 0.72f);
    }

    private void StyleTmpText(TMP_Text text, bool force)
    {
        if (!Begin(text, force)) return;
        BistroBuilderUiStyleTag tag = text.GetComponent<BistroBuilderUiStyleTag>();
        BistroBuilderUiStyleRole role = tag != null && tag.Role != BistroBuilderUiStyleRole.Auto
            ? tag.Role : ResolveTextRole(text.gameObject, text.text);

        if (tag == null || !tag.PreserveFont)
        {
            BistroBuilderTypography.Apply(text, role, true);
            if ((role == BistroBuilderUiStyleRole.Title || role == BistroBuilderUiStyleRole.Heading) &&
                theme != null && theme.TitleFont != null)
                text.font = theme.TitleFont;
            else if (role != BistroBuilderUiStyleRole.Title && role != BistroBuilderUiStyleRole.Heading && role != BistroBuilderUiStyleRole.Label && role != BistroBuilderUiStyleRole.Subheading && role != BistroBuilderUiStyleRole.Kpi) ApplyBodyFont(text);
        }

        text.color = ResolveTextColor(role);
        if (role != BistroBuilderUiStyleRole.StatusCritical && role != BistroBuilderUiStyleRole.StatusAttention && role != BistroBuilderUiStyleRole.StatusSuccess && role != BistroBuilderUiStyleRole.StatusInfo && IsOnLightSurface(text.transform)) text.color = BistroBuilderUiTokens.TextOnLight;
        if (tag == null || !tag.PreserveFontSize) ApplyTextSizePolicy(text, role);
    }

    private void StyleLegacyText(Text text, bool force)
    {
        if (!Begin(text, force)) return;
        if (BistroBuilderTypography.LegacyBody != null) text.font = BistroBuilderTypography.LegacyBody;
        text.color = ResolveTextColor(ResolveTextRole(text.gameObject, text.text));
        if (IsOnLightSurface(text.transform)) text.color = BistroBuilderUiTokens.TextOnLight;
    }

    private void StyleStructuralImage(Image image, bool force)
    {
        if (!Begin(image, force)) return;
        if (image.GetComponent<Button>() != null || image.GetComponent<Toggle>() != null ||
            image.GetComponent<Scrollbar>() != null) return;

        BistroBuilderUiStyleTag tag = image.GetComponent<BistroBuilderUiStyleTag>();
        if (tag != null && tag.PreserveGraphicColor) return;
        if (image.color.a < 0.01f && tag == null) return;
        BistroBuilderUiStyleRole role = tag != null && tag.Role != BistroBuilderUiStyleRole.Auto
            ? tag.Role : ResolveStructuralRole(image);
        if (role != BistroBuilderUiStyleRole.Auto)
        {
            image.color = ResolveColor(role);
            if (role != BistroBuilderUiStyleRole.Overlay)
            {
                var existing = image.GetComponent<BistroBuilderSurface>();
                bool hud = existing != null && existing.Hud;
                var level = role == BistroBuilderUiStyleRole.SurfaceElevated ? BistroBuilderSurfaceLevel.Floating :
                    role == BistroBuilderUiStyleRole.Row ? BistroBuilderSurfaceLevel.Card :
                    role == BistroBuilderUiStyleRole.Background ? BistroBuilderSurfaceLevel.Base : BistroBuilderSurfaceLevel.Panel;
                BistroBuilderSurface.Apply(image, level, hud);
            }
            return;
        }

        if (image.sprite == null && IsNearlyWhite(image.color))
        {
            image.color = Color.clear;
            image.raycastTarget = false;
        }
    }

    private void StyleRawImagePlaceholder(RawImage image, bool force)
    {
        if (!Begin(image, force)) return;
        BistroBuilderUiStyleTag tag = image.GetComponent<BistroBuilderUiStyleTag>();
        if (tag != null && tag.PreserveGraphicColor) return;
        if (image.texture == null && IsNearlyWhite(image.color))
        {
            image.color = Color.clear;
            image.raycastTarget = false;
        }
    }

    private static bool IsNearlyWhite(Color color)
    {
        return color.a > 0.85f && color.r > 0.92f && color.g > 0.92f && color.b > 0.92f;
    }
    private BistroBuilderUiStyleRole ResolveButtonRole(GameObject target)
    {
        BistroBuilderUiStyleTag tag = target.GetComponent<BistroBuilderUiStyleTag>();
        if (tag != null && tag.Role != BistroBuilderUiStyleRole.Auto) return tag.Role;
        string name = target.name ?? string.Empty;
        string key = (name + " " + ReadLabel(target)).ToLowerInvariant();

        if (ContainsAny(key, "delete", "remove", "discard", "dismiss", "desped", "eliminar",
            "descartar", "cancelar reserva", "borrar")) return BistroBuilderUiStyleRole.DestructiveButton;
        if (name.StartsWith("Open", StringComparison.OrdinalIgnoreCase)) return BistroBuilderUiStyleRole.NavButton;
        if (ContainsAny(key, "tab")) return BistroBuilderUiStyleRole.Tab;
        if (ContainsAny(key, "save", "guardar", "crear", "confirm", "accept", "contratar",
            "pedido", "nueva", "nuevo", "aplicar")) return BistroBuilderUiStyleRole.PrimaryButton;
        if (ContainsAny(key, "close", "cerrar", "back", "volver", "previous", "next", "minus",
            "plus", "refrescar", "cancel")) return BistroBuilderUiStyleRole.SecondaryButton;
        return BistroBuilderUiStyleRole.SecondaryButton;
    }

    private BistroBuilderUiStyleRole ResolveTextRole(GameObject target, string value)
    {
        BistroBuilderUiStyleTag tag = target.GetComponent<BistroBuilderUiStyleTag>();
        if (tag != null && tag.Role != BistroBuilderUiStyleRole.Auto) return tag.Role;
        string name = (target.name ?? string.Empty).ToLowerInvariant();
        string key = (name + " " + (value ?? string.Empty)).ToLowerInvariant();

        if (target.GetComponentInParent<Selectable>() != null) return BistroBuilderUiStyleRole.Label;
        if (ContainsAny(name, "subheading", "subtitle")) return BistroBuilderUiStyleRole.Subheading;
        if (ContainsAny(name, "column", "fieldlabel", "tableheader")) return BistroBuilderUiStyleRole.Label;

        if (ContainsAny(name, "title", "screenname", "maintitle")) return BistroBuilderUiStyleRole.Title;
        if (ContainsAny(name, "header", "heading", "sectiontitle")) return BistroBuilderUiStyleRole.Heading;
        if (ContainsAny(name, "summary", "caption", "hint", "help", "secondary", "empty"))
            return BistroBuilderUiStyleRole.Caption;
        if (ContainsAny(name, "kpi", "metric", "value", "amount", "money")) return BistroBuilderUiStyleRole.Kpi;
        if (ContainsAny(key, "crÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­tico", "critico", "bloquead", "error")) return BistroBuilderUiStyleRole.StatusCritical;
        if (ContainsAny(key, "atenciÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³n", "atencion", "saturad", "espera")) return BistroBuilderUiStyleRole.StatusAttention;
        if (ContainsAny(key, "correcto", "fluida", "activo", "confirmad")) return BistroBuilderUiStyleRole.StatusSuccess;
        return BistroBuilderUiStyleRole.Body;
    }

    private BistroBuilderUiStyleRole ResolveStructuralRole(Image image)
    {
        string name = image.name != null ? image.name.ToLowerInvariant() : string.Empty;
        if (image.sprite != null && !ContainsAny(name, "panel", "background", "modal", "card", "row", "dock", "bar"))
            return BistroBuilderUiStyleRole.Auto;
        if (ContainsAny(name, "overlay", "scrim")) return BistroBuilderUiStyleRole.Overlay;
        if (ContainsAny(name, "modal", "popup", "dropdown", "tooltip")) return BistroBuilderUiStyleRole.SurfaceElevated;
        if (ContainsAny(name, "row", "card", "detail", "content")) return BistroBuilderUiStyleRole.Row;
        if (ContainsAny(name, "panel", "background", "root", "dock", "bar")) return BistroBuilderUiStyleRole.Surface;
        return BistroBuilderUiStyleRole.Auto;
    }

    private Color ResolveTextColor(BistroBuilderUiStyleRole role)
    {
        switch (role)
        {
            case BistroBuilderUiStyleRole.Caption: return TextSecondary;
            case BistroBuilderUiStyleRole.StatusSuccess: return Success;
            case BistroBuilderUiStyleRole.StatusAttention: return Attention;
            case BistroBuilderUiStyleRole.StatusCritical: return Critical;
            case BistroBuilderUiStyleRole.StatusInfo: return Info;
            case BistroBuilderUiStyleRole.StatusDisabled: return BistroBuilderUiTokens.Disabled;
            default: return TextPrimary;
        }
    }

    private void ApplyTextSizePolicy(TMP_Text text, BistroBuilderUiStyleRole role)
    {
        text.enableAutoSizing = true;
        text.fontSize = text.fontSizeMax = BistroBuilderTypography.Size(role);
        text.fontSizeMin = role == BistroBuilderUiStyleRole.Title ? 22 : role == BistroBuilderUiStyleRole.Heading ? 18 : 12;
    }
    private void ApplyBodyFont(TMP_Text text)
    {
        var font = theme != null && theme.BodyFont != null ? theme.BodyFont : BistroBuilderTypography.Body;
        if (font != null) text.font = font;
    }

    private static bool IsOnLightSurface(Transform target)
    {
        for (var p = target.parent; p != null; p = p.parent)
        {
            var selectable = p.GetComponent<Selectable>();
            if (selectable != null && selectable.transition == Selectable.Transition.ColorTint)
            {
                var c = selectable.colors.normalColor;
                if (c.r < 0.8f || c.g < 0.8f || c.b < 0.8f) return c.grayscale > 0.6f;
            }
            var surface = p.GetComponent<BistroBuilderSurface>();
            if (surface != null) return surface.Level == BistroBuilderSurfaceLevel.Floating;
        }
        return false;
    }

    private static string ReadLabel(GameObject target)
    {
        TMP_Text tmp = target.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) return tmp.text ?? string.Empty;
        Text legacy = target.GetComponentInChildren<Text>(true);
        return legacy != null ? legacy.text ?? string.Empty : string.Empty;
    }

    private static bool ContainsAny(string source, params string[] tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
            if (source.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private Color Background => theme != null ? theme.Background : BistroBuilderUiTokens.Background;
    private Color Surface1 => theme != null ? theme.Surface1 : BistroBuilderUiTokens.Surface1;
    private Color Surface2 => theme != null ? theme.Surface2 : BistroBuilderUiTokens.Surface2;
    private Color SurfaceElevated => theme != null ? theme.SurfaceElevated : BistroBuilderUiTokens.SurfaceElevated;
    private Color TextPrimary => theme != null ? theme.TextPrimary : BistroBuilderUiTokens.TextPrimary;
    private Color TextSecondary => theme != null ? theme.TextSecondary : BistroBuilderUiTokens.TextSecondary;
    private Color TextMuted => theme != null ? theme.TextMuted : BistroBuilderUiTokens.TextMuted;
    private Color Primary => theme != null ? theme.Primary : BistroBuilderUiTokens.Primary;
    private Color WarmAccent => theme != null ? theme.WarmAccent : BistroBuilderUiTokens.WarmAccent;
    private Color Success => theme != null ? theme.Success : BistroBuilderUiTokens.Success;
    private Color Attention => theme != null ? theme.Attention : BistroBuilderUiTokens.Attention;
    private Color Critical => theme != null ? theme.Critical : BistroBuilderUiTokens.Critical;
    private Color Info => theme != null ? theme.Info : BistroBuilderUiTokens.Info;
}
