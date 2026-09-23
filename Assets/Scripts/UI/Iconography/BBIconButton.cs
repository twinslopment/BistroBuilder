using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace BistroBuilder.UI.Iconography
{
    /// <summary>
    /// Presentación reutilizable de un icono de Bistro Builder.
    /// Gestiona Normal/Hover/Selected/Disabled sin introducir decisiones de gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BBIconButton : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [Header("Icon")]
        [SerializeField] private BBIconId iconId;
        [SerializeField] private BBIconCatalog catalog;
        [SerializeField] private Image iconImage;
        [SerializeField] private bool useSemanticColor;

        [Header("Optional visual targets")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image selectionUnderline;
        [SerializeField] private Image hoverGlow;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("State")]
        [SerializeField] private bool selected;
        [SerializeField] private bool interactable = true;
        [SerializeField] private bool toggleSelectionOnClick;

        [Header("Surface colors")]
        [SerializeField] private Color normalBackground = new Color(0.035f, 0.114f, 0.137f, 1f);
        [SerializeField] private Color hoverBackground = new Color(0.071f, 0.169f, 0.200f, 1f);
        [SerializeField] private Color selectedBackground = new Color(0.120f, 0.155f, 0.125f, 1f);
        [SerializeField] private Color disabledBackground = new Color(0.035f, 0.102f, 0.122f, 1f);

        private BBIconState state;
        private BBIconSemanticRole semanticRole;
        private bool pointerInside;
        private RectTransform iconRect;
        private Vector3 baseScale = Vector3.one;
        private Vector2 baseAnchoredPosition;
        private Quaternion baseRotation = Quaternion.identity;
        private Vector3 targetScale = Vector3.one;
        private Vector2 targetAnchoredPosition;
        private Quaternion targetRotation = Quaternion.identity;
        private Color targetIconColor;
        private Color targetBackgroundColor;
        private Color targetBorderColor;
        private float targetUnderlineAlpha;
        private float targetGlowAlpha;
        private TMP_Text navigationLabel;
        private bool navigationSurface;

        public BBIconId IconId => iconId;
        public BBIconState State => state;
        public bool IsSelected => selected;
        public bool IsInteractable => interactable;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            ResolveIcon();
            CacheTransform();
            RefreshState(true);
        }

        private void OnEnable()
        {
            ResolveIcon();
            CacheTransform();
            RefreshState(true);
        }

        private void Update()
        {
            bool reduced = BistroBuilderOptionsScreen.ReducedMotion;
            var t = reduced ? 1f : 1f - Mathf.Exp(-BBIconDesignTokens.TransitionSpeed * Time.unscaledDeltaTime);

            if (iconImage != null)
                iconImage.color = Color.Lerp(iconImage.color, targetIconColor, t);
            if (navigationLabel != null)
                navigationLabel.color = Color.Lerp(navigationLabel.color,
                    state == BBIconState.Selected ? BBIconDesignTokens.SelectedSoft :
                    state == BBIconState.Hover ? Color.white : BBIconDesignTokens.Muted, t);

            if (backgroundImage != null)
                backgroundImage.color = Color.Lerp(backgroundImage.color, targetBackgroundColor, t);

            if (borderImage != null)
                borderImage.color = Color.Lerp(borderImage.color, targetBorderColor, t);

            if (selectionUnderline != null)
            {
                var c = selectionUnderline.color;
                c = Color.Lerp(c, new Color(BBIconDesignTokens.Selected.r, BBIconDesignTokens.Selected.g, BBIconDesignTokens.Selected.b, targetUnderlineAlpha), t);
                selectionUnderline.color = c;
            }

            if (hoverGlow != null)
            {
                var glow = hoverGlow.color;
                var targetGlow = new Color(BBIconDesignTokens.SelectedSoft.r, BBIconDesignTokens.SelectedSoft.g, BBIconDesignTokens.SelectedSoft.b, targetGlowAlpha);
                hoverGlow.color = Color.Lerp(glow, targetGlow, t);
            }

            if (iconRect != null)
            {
                iconRect.localScale = Vector3.Lerp(iconRect.localScale, reduced ? baseScale : targetScale, t);
                iconRect.anchoredPosition = Vector2.Lerp(iconRect.anchoredPosition, reduced ? baseAnchoredPosition : targetAnchoredPosition, t);
                iconRect.localRotation = Quaternion.Slerp(iconRect.localRotation, reduced ? baseRotation : targetRotation, t);
            }
        }

        public void Configure(BBIconId newIconId, bool semanticColor = false)
        {
            iconId = newIconId;
            useSemanticColor = semanticColor;
            ResolveIcon();
            RefreshState(true);
        }

        public void ConfigureRuntime(BBIconId newIconId, Image runtimeIconImage, Button runtimeButton, bool semanticColor = false)
        {
            iconImage = runtimeIconImage;
            CacheTransform();
            interactable = runtimeButton == null || runtimeButton.interactable;
            Configure(newIconId, semanticColor);
        }

        public void SetToggleSelectionOnClick(bool value)
        {
            toggleSelectionOnClick = value;
        }

        public void ConfigureNavigationSurface(Image background, Image underline, TMP_Text label, Image glow = null)
        {
            navigationSurface = true;
            backgroundImage = background;
            selectionUnderline = underline;
            navigationLabel = label;
            hoverGlow = glow;
            normalBackground = new Color(0.067f, 0.094f, 0.106f, 0f);
            hoverBackground = new Color(0.18f, 0.15f, 0.10f, 0.34f);
            selectedBackground = new Color(0.16f, 0.16f, 0.11f, 0.7f);
            disabledBackground = normalBackground;
            RefreshState(true);
        }

        public void SetSelected(bool value)
        {
            if (selected == value)
                return;

            selected = value;
            RefreshState(false);
        }

        public void SetInteractable(bool value)
        {
            if (interactable == value)
                return;

            interactable = value;
            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = value;

            RefreshState(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            RefreshState(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            RefreshState(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable || !toggleSelectionOnClick || eventData.button != PointerEventData.InputButton.Left)
                return;

            SetSelected(!selected);
        }

        public void OnSelect(BaseEventData eventData)
        {
            pointerInside = true;
            RefreshState(false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            pointerInside = false;
            RefreshState(false);
        }

        private void ResolveIcon()
        {
            if (catalog == null)
                catalog = BBIconCatalog.LoadDefault();

            if (catalog == null || iconImage == null)
                return;

            if (!catalog.TryGet(iconId, out var definition))
                return;

            iconImage.sprite = definition.sprite;
            iconImage.preserveAspect = true;
            iconImage.useSpriteMesh = true;
            semanticRole = definition.semanticRole;
        }

        private void CacheTransform()
        {
            if (iconImage == null)
                return;

            iconRect = iconImage.rectTransform;
            baseScale = iconRect.localScale;
            baseAnchoredPosition = iconRect.anchoredPosition;
            baseRotation = iconRect.localRotation;
        }

        private void RefreshState(bool immediate)
        {
            state = !interactable
                ? BBIconState.Disabled
                : selected
                    ? BBIconState.Selected
                    : pointerInside
                        ? BBIconState.Hover
                        : BBIconState.Normal;

            var semantic = useSemanticColor
                ? BBIconDesignTokens.ForRole(semanticRole)
                : BBIconDesignTokens.Neutral;

            targetScale = baseScale;
            targetAnchoredPosition = baseAnchoredPosition;
            targetRotation = baseRotation;
            targetUnderlineAlpha = 0f;
            targetGlowAlpha = 0f;
            if (navigationSurface) semantic = new Color(0.48f, 0.65f, 0.71f);

            switch (state)
            {
                case BBIconState.Hover:
                    targetIconColor = Color.white;
                    targetBackgroundColor = hoverBackground;
                    targetBorderColor = BBIconDesignTokens.HoverBorder;
                    targetScale = baseScale * BBIconDesignTokens.HoverScale;
                    targetAnchoredPosition = baseAnchoredPosition + Vector2.up * BBIconDesignTokens.HoverLift;
                    break;

                case BBIconState.Selected:
                    targetIconColor = BBIconDesignTokens.SelectedSoft;
                    targetBackgroundColor = selectedBackground;
                    targetBorderColor = BBIconDesignTokens.Selected;
                    targetUnderlineAlpha = 1f;
                    break;

                case BBIconState.Disabled:
                    targetIconColor = BBIconDesignTokens.Disabled;
                    targetBackgroundColor = disabledBackground;
                    targetBorderColor = BBIconDesignTokens.Border;
                    break;

                default:
                    targetIconColor = semantic;
                    targetBackgroundColor = normalBackground;
                    targetBorderColor = BBIconDesignTokens.Border;
                    break;
            }

            if (navigationSurface && pointerInside && interactable)
            {
                ApplyNavigationHoverPose();
                targetGlowAlpha = selected ? 0.115f : 0.095f;
            }

            if (!immediate)
                return;

            if (iconImage != null)
                iconImage.color = targetIconColor;
            if (backgroundImage != null)
                backgroundImage.color = targetBackgroundColor;
            if (borderImage != null)
                borderImage.color = targetBorderColor;
            if (selectionUnderline != null)
            {
                var c = BBIconDesignTokens.Selected;
                c.a = targetUnderlineAlpha;
                selectionUnderline.color = c;
            }
            if (hoverGlow != null)
            {
                var glow = BBIconDesignTokens.SelectedSoft;
                glow.a = targetGlowAlpha;
                hoverGlow.color = glow;
            }
            if (iconRect != null)
            {
                iconRect.localScale = targetScale;
                iconRect.anchoredPosition = targetAnchoredPosition;
                iconRect.localRotation = targetRotation;
            }
        }

        private void ApplyNavigationHoverPose()
        {
            float rotation = 0f;
            float scale = BBIconDesignTokens.HoverScale;
            Vector2 offset = Vector2.up * BBIconDesignTokens.HoverLift;

            switch (iconId)
            {
                case BBIconId.NavActivity:      rotation = -0.7f; scale = 1.050f; offset = new Vector2(0f, 2.2f); break;
                case BBIconId.NavStaff:         rotation =  0.0f; scale = 1.040f; offset = new Vector2(0f, 1.6f); break;
                case BBIconId.NavMenu:          rotation = -1.6f; scale = 1.045f; offset = new Vector2(0.4f, 1.7f); break;
                case BBIconId.NavInventory:     rotation =  1.0f; scale = 1.040f; offset = new Vector2(0f, 1.4f); break;
                case BBIconId.NavSuppliers:     rotation =  0.0f; scale = 1.035f; offset = new Vector2(2.0f, 1.0f); break;
                case BBIconId.NavReservations:  rotation = -1.3f; scale = 1.045f; offset = new Vector2(-0.5f, 1.7f); break;
                case BBIconId.NavEconomy:       rotation =  1.2f; scale = 1.045f; offset = new Vector2(0.5f, 1.6f); break;
                case BBIconId.NavMarketing:     rotation = -2.0f; scale = 1.050f; offset = new Vector2(1.2f, 1.5f); break;
                case BBIconId.NavReputation:    rotation =  0.8f; scale = 1.045f; offset = new Vector2(0f, 2.0f); break;
                case BBIconId.NavOptions:       rotation =  6.0f; scale = 1.035f; offset = new Vector2(0f, 1.0f); break;
            }

            targetScale = baseScale * scale;
            targetAnchoredPosition = baseAnchoredPosition + offset;
            targetRotation = baseRotation * Quaternion.Euler(0f, 0f, rotation);
        }

        private void OnDisable()
        {
            pointerInside = false;
            if (iconRect == null) return;
            iconRect.localScale = baseScale;
            iconRect.anchoredPosition = baseAnchoredPosition;
            iconRect.localRotation = baseRotation;
        }
    }
}
