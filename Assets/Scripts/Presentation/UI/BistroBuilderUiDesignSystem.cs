using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Design System canónico de Bistro Builder. Normaliza las primitivas UI reutilizables
/// de todas las pantallas sin asumir autoridad de gameplay.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Design System")]
public sealed class BistroBuilderUiDesignSystem : MonoBehaviour
{
    public const string RuntimeRevision = "21A-UIUX-PRIMITIVES-V2.0";

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
            error = "BB UI Design System debe vivir bajo el Canvas HUD canónico.";
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
        Slider[] sliders = canvas.GetComponentsInChildren<Slider>(true);
        for (int i = 0; i < sliders.Length; i++) StyleSlider(sliders[i], force);
        Scrollbar[] scrollbars = canvas.GetComponentsInChildren<Scrollbar>(true);
        for (int i = 0; i < scrollbars.Length; i++) StyleScrollbar(scrollbars[i], force);
        ScrollRect[] scrollRects = UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scrollRects.Length; i++)
            if (scrollRects[i] != null && scrollRects[i].gameObject.scene == canvas.gameObject.scene && (!Application.isPlaying || scrollRects[i].gameObject.activeInHierarchy))
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
            case BistroBuilderUiStyleRole.Surface:
            case BistroBuilderUiStyleRole.Panel: return Surface1;
            case BistroBuilderUiStyleRole.SurfaceElevated:
            case BistroBuilderUiStyleRole.Card:
            case BistroBuilderUiStyleRole.Toast: return SurfaceElevated;
            case BistroBuilderUiStyleRole.Overlay: return BistroBuilderUiTokens.Overlay;
            case BistroBuilderUiStyleRole.PrimaryButton: return Primary;
            case BistroBuilderUiStyleRole.SecondaryButton: return SecondaryAction;
            case BistroBuilderUiStyleRole.ContextualButton: return new Color(Surface2.r, Surface2.g, Surface2.b, 0.28f);
            case BistroBuilderUiStyleRole.DestructiveButton:
            case BistroBuilderUiStyleRole.BadgeCritical:
            case BistroBuilderUiStyleRole.StatusCritical: return Critical;
            case BistroBuilderUiStyleRole.NavButton:
            case BistroBuilderUiStyleRole.Tab:
            case BistroBuilderUiStyleRole.Field:
            case BistroBuilderUiStyleRole.Row:
            case BistroBuilderUiStyleRole.ProgressTrack:
            case BistroBuilderUiStyleRole.BottomDock: return Surface2;
            case BistroBuilderUiStyleRole.BadgeSuccess:
            case BistroBuilderUiStyleRole.ProgressSuccess:
            case BistroBuilderUiStyleRole.StatusSuccess: return Success;
            case BistroBuilderUiStyleRole.BadgeAttention:
            case BistroBuilderUiStyleRole.ProgressAttention:
            case BistroBuilderUiStyleRole.StatusAttention: return Attention;
            case BistroBuilderUiStyleRole.BadgeInfo:
            case BistroBuilderUiStyleRole.ProgressInfo:
            case BistroBuilderUiStyleRole.StatusInfo: return Info;
            case BistroBuilderUiStyleRole.BadgeNeutral: return SecondaryAction;
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
        scaler.scaleFactor = uiScale;
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
        Color normal = ResolveColor(role);
        Color hover;
        Color pressed;

        if (role == BistroBuilderUiStyleRole.PrimaryButton)
        {
            normal = Primary;
            hover = Color.Lerp(Primary, Color.white, 0.10f);
            pressed = Color.Lerp(Primary, Color.black, 0.18f);
        }
        else if (role == BistroBuilderUiStyleRole.DestructiveButton)
        {
            normal = Critical;
            hover = Color.Lerp(Critical, Color.white, 0.08f);
            pressed = Color.Lerp(Critical, Color.black, 0.18f);
        }
        else if (role == BistroBuilderUiStyleRole.ContextualButton)
        {
            normal = new Color(Surface1.r, Surface1.g, Surface1.b, 0.08f);
            hover = new Color(Surface2.r, Surface2.g, Surface2.b, 0.68f);
            pressed = new Color(Surface2.r, Surface2.g, Surface2.b, 0.92f);
        }
        else if (role == BistroBuilderUiStyleRole.NavButton || role == BistroBuilderUiStyleRole.Tab)
        {
            normal = new Color(Surface1.r, Surface1.g, Surface1.b, 0.82f);
            hover = Surface2;
            pressed = Color.Lerp(Surface2, Color.black, 0.10f);
        }
        else
        {
            normal = SecondaryAction;
            hover = Color.Lerp(SecondaryAction, Color.white, 0.07f);
            pressed = Color.Lerp(SecondaryAction, Color.black, 0.16f);
        }

        button.transition = Selectable.Transition.ColorTint;
        button.colors = BistroBuilderUiTokens.ButtonColors(normal, hover, pressed);
        if (image != null) image.color = Color.white;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = Mathf.Max(12f, label.fontSize);
        }
        EnsureControlHeight(button.gameObject,
            role == BistroBuilderUiStyleRole.PrimaryButton ? BistroBuilderUiTokens.ControlPrimary : BistroBuilderUiTokens.ControlStandard);

        if (button.GetComponent<BistroBuilderUiSelectableMotion>() == null)
            button.gameObject.AddComponent<BistroBuilderUiSelectableMotion>();

        if (role == BistroBuilderUiStyleRole.Tab)
        {
            BistroBuilderUiTabVisual tab = button.GetComponent<BistroBuilderUiTabVisual>();
            if (tab == null) tab = button.gameObject.AddComponent<BistroBuilderUiTabVisual>();
            tab.Configure(Primary);
        }
    }

    private void StyleToggle(Toggle toggle, bool force)
    {
        if (!Begin(toggle, force)) return;
        toggle.colors = BistroBuilderUiTokens.ButtonColors(
            Surface2, Color.Lerp(Surface2, Color.white, 0.08f), Color.Lerp(Surface2, Color.black, 0.12f));
        EnsureControlHeight(toggle.gameObject, BistroBuilderUiTokens.ControlSmall);
        if (toggle.graphic != null) toggle.graphic.color = Success;
        if (toggle.GetComponent<BistroBuilderUiSelectableMotion>() == null)
            toggle.gameObject.AddComponent<BistroBuilderUiSelectableMotion>();
    }

    private void StyleTmpInput(TMP_InputField input, bool force)
    {
        if (!Begin(input, force)) return;
        if (input.targetGraphic is Image image) image.color = Surface1;
        input.colors = BistroBuilderUiTokens.ButtonColors(
            Surface1, Surface2, Color.Lerp(Surface2, Color.black, 0.10f));
        EnsureControlHeight(input.gameObject, BistroBuilderUiTokens.ControlStandard);
        if (input.textComponent != null)
        {
            input.textComponent.color = TextPrimary;
            input.textComponent.fontSize = BistroBuilderUiTokens.FontBody;
            ApplyBodyFont(input.textComponent);
        }
        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.color = new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b, 0.82f);
            placeholder.fontSize = BistroBuilderUiTokens.FontSecondary;
            ApplyBodyFont(placeholder);
        }
    }

    private void StyleLegacyInput(InputField input, bool force)
    {
        if (!Begin(input, force)) return;
        if (input.targetGraphic is Image image) image.color = Surface1;
        input.colors = BistroBuilderUiTokens.ButtonColors(
            Surface1, Surface2, Color.Lerp(Surface2, Color.black, 0.10f));
        EnsureControlHeight(input.gameObject, BistroBuilderUiTokens.ControlStandard);
        if (input.textComponent != null)
        {
            input.textComponent.color = TextPrimary;
            input.textComponent.fontSize = Mathf.RoundToInt(BistroBuilderUiTokens.FontBody);
        }
        if (input.placeholder is Text placeholder) placeholder.color = TextSecondary;
    }

    private void StyleTmpDropdown(TMP_Dropdown dropdown, bool force)
    {
        if (!Begin(dropdown, force)) return;
        if (dropdown.targetGraphic is Image image) image.color = Surface1;
        dropdown.colors = BistroBuilderUiTokens.ButtonColors(
            Surface1, Surface2, Color.Lerp(Surface2, Color.black, 0.10f));
        EnsureControlHeight(dropdown.gameObject, BistroBuilderUiTokens.ControlStandard);
        if (dropdown.captionText != null)
        {
            dropdown.captionText.color = TextPrimary;
            dropdown.captionText.fontSize = BistroBuilderUiTokens.FontBody;
            ApplyBodyFont(dropdown.captionText);
        }
        if (dropdown.GetComponent<BistroBuilderUiDropdownMotion>() == null)
            dropdown.gameObject.AddComponent<BistroBuilderUiDropdownMotion>();
    }

    private void StyleLegacyDropdown(Dropdown dropdown, bool force)
    {
        if (!Begin(dropdown, force)) return;
        if (dropdown.targetGraphic is Image image) image.color = Surface1;
        dropdown.colors = BistroBuilderUiTokens.ButtonColors(
            Surface1, Surface2, Color.Lerp(Surface2, Color.black, 0.10f));
        EnsureControlHeight(dropdown.gameObject, BistroBuilderUiTokens.ControlStandard);
        if (dropdown.captionText != null)
        {
            dropdown.captionText.color = TextPrimary;
            dropdown.captionText.fontSize = Mathf.RoundToInt(BistroBuilderUiTokens.FontBody);
        }
        if (dropdown.GetComponent<BistroBuilderUiDropdownMotion>() == null)
            dropdown.gameObject.AddComponent<BistroBuilderUiDropdownMotion>();
    }

    private void StyleSlider(Slider slider, bool force)
    {
        if (!Begin(slider, force)) return;
        BistroBuilderUiStyleRole fillRole = ResolveProgressRole(slider.gameObject);
        Image track = slider.GetComponent<Image>();
        if (track == null)
        {
            Transform background = slider.transform.Find("Background");
            if (background != null) track = background.GetComponent<Image>();
        }
        if (track != null) track.color = Surface2;
        if (slider.fillRect != null)
        {
            Image fill = slider.fillRect.GetComponent<Image>();
            if (fill != null) fill.color = ResolveColor(fillRole);
        }
        if (slider.handleRect != null)
        {
            Image handle = slider.handleRect.GetComponent<Image>();
            if (handle != null) handle.color = ResolveColor(fillRole);
        }
        RectTransform rect = slider.transform as RectTransform;
        if (rect != null && rect.sizeDelta.y > 0f && rect.sizeDelta.y < BistroBuilderUiTokens.ProgressHeight)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, BistroBuilderUiTokens.ProgressHeight);
    }

    private void StyleScrollbar(Scrollbar scrollbar, bool force)
    {
        if (!Begin(scrollbar, force)) return;
        scrollbar.colors = BistroBuilderUiTokens.ButtonColors(
            Surface2, Color.Lerp(Surface2, Color.white, 0.08f), Color.Lerp(Surface2, Color.black, 0.10f));
        if (scrollbar.targetGraphic != null)
            scrollbar.targetGraphic.color = new Color(Primary.r, Primary.g, Primary.b, 0.82f);
    }

    private void StyleTmpText(TMP_Text text, bool force)
    {
        if (!Begin(text, force)) return;
        BistroBuilderUiStyleTag tag = text.GetComponent<BistroBuilderUiStyleTag>();
        BistroBuilderUiStyleRole role = tag != null && tag.Role != BistroBuilderUiStyleRole.Auto
            ? tag.Role : ResolveTextRole(text.gameObject, text.text);

        if (tag == null || !tag.PreserveFont)
        {
            if ((role == BistroBuilderUiStyleRole.Title || role == BistroBuilderUiStyleRole.Heading) &&
                theme != null && theme.TitleFont != null)
                text.font = theme.TitleFont;
            else ApplyBodyFont(text);
        }

        text.color = ResolveTextColor(role);
        if (tag == null || !tag.PreserveFontSize) ApplyTextSizePolicy(text, role);
    }

    private void StyleLegacyText(Text text, bool force)
    {
        if (!Begin(text, force)) return;
        BistroBuilderUiStyleRole role = ResolveTextRole(text.gameObject, text.text);
        text.color = ResolveTextColor(role);
        text.fontSize = Mathf.RoundToInt(ResolveTextSize(role));
    }

    private void StyleStructuralImage(Image image, bool force)
    {
        if (!Begin(image, force)) return;
        if (image.GetComponent<Button>() != null || image.GetComponent<Toggle>() != null ||
            image.GetComponent<Scrollbar>() != null || image.GetComponent<Slider>() != null) return;
        if (image.transform.parent != null && image.transform.parent.GetComponent<Slider>() != null) return;

        BistroBuilderUiStyleTag tag = image.GetComponent<BistroBuilderUiStyleTag>();
        if (tag != null && tag.PreserveGraphicColor) return;
        BistroBuilderUiStyleRole role = tag != null && tag.Role != BistroBuilderUiStyleRole.Auto
            ? tag.Role : ResolveStructuralRole(image);
        if (role == BistroBuilderUiStyleRole.Auto)
        {
            if (image.sprite == null && IsNearlyWhite(image.color))
            {
                image.color = Color.clear;
                image.raycastTarget = false;
            }
            return;
        }

        image.color = ResolveColor(role);
        if (role == BistroBuilderUiStyleRole.Card || role == BistroBuilderUiStyleRole.SurfaceElevated ||
            role == BistroBuilderUiStyleRole.Toast)
        {
            EnsureOutline(image.gameObject);
            if (role == BistroBuilderUiStyleRole.Toast || role == BistroBuilderUiStyleRole.SurfaceElevated)
                EnsureShadow(image.gameObject);
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
            "descartar", "cancelar reserva", "borrar", "vender")) return BistroBuilderUiStyleRole.DestructiveButton;
        if (name.StartsWith("Open", StringComparison.OrdinalIgnoreCase) ||
            ContainsAny(name, "nav_", "bbnav_")) return BistroBuilderUiStyleRole.NavButton;
        if (ContainsAny(key, "tab")) return BistroBuilderUiStyleRole.Tab;
        if (ContainsAny(key, "confirmar", "confirm", "guardar", "save", "aplicar cambios",
            "accept", "crear", "contratar")) return BistroBuilderUiStyleRole.PrimaryButton;
        if (ContainsAny(key, "más opciones", "mas opciones", "detalles", "ver detalles", "ayuda",
            "información", "informacion")) return BistroBuilderUiStyleRole.ContextualButton;
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

        if (ContainsAny(name, "title", "screenname", "maintitle")) return BistroBuilderUiStyleRole.Title;
        if (ContainsAny(name, "header", "heading", "sectiontitle")) return BistroBuilderUiStyleRole.Heading;
        if (ContainsAny(name, "summary", "caption", "hint", "help", "secondary", "empty", "label"))
            return BistroBuilderUiStyleRole.Caption;
        if (ContainsAny(name, "kpi", "metric", "value", "amount", "money")) return BistroBuilderUiStyleRole.Kpi;
        if (ContainsAny(key, "crítico", "critico", "bloquead", "error", "cancelad")) return BistroBuilderUiStyleRole.StatusCritical;
        if (ContainsAny(key, "atención", "atencion", "saturad", "espera", "en cocina")) return BistroBuilderUiStyleRole.StatusAttention;
        if (ContainsAny(key, "correcto", "fluida", "activo", "confirmad", "servido")) return BistroBuilderUiStyleRole.StatusSuccess;
        return BistroBuilderUiStyleRole.Body;
    }

    private BistroBuilderUiStyleRole ResolveStructuralRole(Image image)
    {
        string name = image.name != null ? image.name.ToLowerInvariant() : string.Empty;
        string key = name + " " + ReadLabel(image.gameObject).ToLowerInvariant();
        if (image.sprite != null && !ContainsAny(name, "panel", "background", "modal", "card", "row", "dock",
            "bar", "badge", "chip", "toast", "progress")) return BistroBuilderUiStyleRole.Auto;
        if (ContainsAny(name, "overlay", "scrim")) return BistroBuilderUiStyleRole.Overlay;
        if (ContainsAny(name, "toast", "notification")) return BistroBuilderUiStyleRole.Toast;
        if (ContainsAny(name, "badge", "chip")) return ResolveSemanticRole(key, true);
        if (ContainsAny(name, "progress", "track")) return BistroBuilderUiStyleRole.ProgressTrack;
        if (ContainsAny(name, "modal", "popup", "dropdown", "tooltip")) return BistroBuilderUiStyleRole.SurfaceElevated;
        if (ContainsAny(name, "card", "detail", "info")) return BistroBuilderUiStyleRole.Card;
        if (ContainsAny(name, "row", "content")) return BistroBuilderUiStyleRole.Row;
        if (ContainsAny(name, "panel", "background", "root", "dock", "bar")) return BistroBuilderUiStyleRole.Panel;
        return BistroBuilderUiStyleRole.Auto;
    }

    private BistroBuilderUiStyleRole ResolveProgressRole(GameObject target)
    {
        BistroBuilderUiStyleTag tag = target.GetComponent<BistroBuilderUiStyleTag>();
        if (tag != null && (tag.Role == BistroBuilderUiStyleRole.ProgressSuccess ||
            tag.Role == BistroBuilderUiStyleRole.ProgressAttention ||
            tag.Role == BistroBuilderUiStyleRole.ProgressInfo)) return tag.Role;
        string key = ((target.name ?? string.Empty) + " " + ReadLabel(target)).ToLowerInvariant();
        if (ContainsAny(key, "satisf", "success", "éxito", "exito")) return BistroBuilderUiStyleRole.ProgressSuccess;
        if (ContainsAny(key, "cocina", "kitchen", "carga", "load", "warning", "atención", "atencion"))
            return BistroBuilderUiStyleRole.ProgressAttention;
        return BistroBuilderUiStyleRole.ProgressInfo;
    }

    private BistroBuilderUiStyleRole ResolveSemanticRole(string key, bool badge)
    {
        if (ContainsAny(key, "cancel", "crítico", "critico", "error"))
            return badge ? BistroBuilderUiStyleRole.BadgeCritical : BistroBuilderUiStyleRole.StatusCritical;
        if (ContainsAny(key, "cocina", "espera", "atención", "atencion"))
            return badge ? BistroBuilderUiStyleRole.BadgeAttention : BistroBuilderUiStyleRole.StatusAttention;
        if (ContainsAny(key, "servido", "éxito", "exito", "correcto", "confirm"))
            return badge ? BistroBuilderUiStyleRole.BadgeSuccess : BistroBuilderUiStyleRole.StatusSuccess;
        if (ContainsAny(key, "nuevo", "nueva", "info"))
            return badge ? BistroBuilderUiStyleRole.BadgeInfo : BistroBuilderUiStyleRole.StatusInfo;
        return badge ? BistroBuilderUiStyleRole.BadgeNeutral : BistroBuilderUiStyleRole.Body;
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
            default: return TextPrimary;
        }
    }

    private void ApplyTextSizePolicy(TMP_Text text, BistroBuilderUiStyleRole role)
    {
        text.fontSize = ResolveTextSize(role);
        if (role == BistroBuilderUiStyleRole.Title) text.fontStyle |= FontStyles.Bold;
        else if (role == BistroBuilderUiStyleRole.Heading) text.fontStyle |= FontStyles.Bold;
    }

    private static float ResolveTextSize(BistroBuilderUiStyleRole role)
    {
        switch (role)
        {
            case BistroBuilderUiStyleRole.Title: return BistroBuilderUiTokens.FontH1;
            case BistroBuilderUiStyleRole.Heading: return BistroBuilderUiTokens.FontH3;
            case BistroBuilderUiStyleRole.Caption: return BistroBuilderUiTokens.FontCaption;
            case BistroBuilderUiStyleRole.Kpi: return BistroBuilderUiTokens.FontKpi;
            default: return BistroBuilderUiTokens.FontBody;
        }
    }

    private void ApplyBodyFont(TMP_Text text)
    {
        if (theme != null && theme.BodyFont != null) text.font = theme.BodyFont;
    }

    private static void EnsureControlHeight(GameObject target, float height)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout != null) layout.minHeight = Mathf.Max(layout.minHeight, height);
    }

    private void EnsureOutline(GameObject target)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.AddComponent<Outline>();
        outline.effectColor = BorderSubtle;
        outline.effectDistance = new Vector2(BistroBuilderUiTokens.PanelOutline, -BistroBuilderUiTokens.PanelOutline);
        outline.useGraphicAlpha = true;
    }

    private static void EnsureShadow(GameObject target)
    {
        Shadow[] shadows = target.GetComponents<Shadow>();
        Shadow shadow = null;
        for (int i = 0; i < shadows.Length; i++)
            if (!(shadows[i] is Outline)) { shadow = shadows[i]; break; }
        if (shadow == null) shadow = target.AddComponent<Shadow>();
        shadow.effectColor = BistroBuilderUiTokens.Shadow;
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;
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
    private Color SecondaryAction => theme != null ? theme.SecondaryAction : BistroBuilderUiTokens.SecondaryAction;
    private Color Success => theme != null ? theme.Success : BistroBuilderUiTokens.Success;
    private Color Attention => theme != null ? theme.Attention : BistroBuilderUiTokens.Attention;
    private Color Critical => theme != null ? theme.Critical : BistroBuilderUiTokens.Critical;
    private Color Info => theme != null ? theme.Info : BistroBuilderUiTokens.Info;
    private Color BorderSubtle => theme != null ? theme.BorderSubtle : BistroBuilderUiTokens.BorderSubtle;
}
