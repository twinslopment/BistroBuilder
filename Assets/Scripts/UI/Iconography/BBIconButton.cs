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
        [SerializeField] private Outline hoverOutline;
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
        private float hoverStartedAt;
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

            float animatedGlowAlpha = targetGlowAlpha;
            Vector3 animatedScale = targetScale;
            Vector2 animatedPosition = targetAnchoredPosition;
            Quaternion animatedRotation = targetRotation;

            if (!reduced && navigationSurface && pointerInside && interactable)
            {
                float blend = GetNavigationHoverBlend();
                GetNavigationHoverPose(out float rotation, out float scale, out Vector2 offset, out _);
                animatedScale = Vector3.Lerp(baseScale, baseScale * scale, blend);
                animatedPosition = Vector2.Lerp(baseAnchoredPosition, baseAnchoredPosition + offset, blend);
                animatedRotation = Quaternion.Slerp(baseRotation,
                    baseRotation * Quaternion.Euler(0f, 0f, rotation), blend);
                animatedGlowAlpha = Mathf.Lerp(targetGlowAlpha * 0.72f, targetGlowAlpha, blend);
            }

            if (hoverGlow != null)
            {
                var glow = hoverGlow.color;
                var targetGlow = new Color(BBIconDesignTokens.SelectedSoft.r, BBIconDesignTokens.SelectedSoft.g, BBIconDesignTokens.SelectedSoft.b, animatedGlowAlpha);
                hoverGlow.color = Color.Lerp(glow, targetGlow, t);
            }

            if (hoverOutline != null)
            {
                var outlineColor = hoverOutline.effectColor;
                float outlineAlpha = Mathf.Clamp01(animatedGlowAlpha * 2.0f);
                var targetOutline = new Color(BBIconDesignTokens.Selected.r, BBIconDesignTokens.Selected.g, BBIconDesignTokens.Selected.b, outlineAlpha);
                hoverOutline.effectColor = Color.Lerp(outlineColor, targetOutline, t);
            }

            if (iconRect != null)
            {
                iconRect.localScale = Vector3.Lerp(iconRect.localScale, reduced ? baseScale : animatedScale, t);
                iconRect.anchoredPosition = Vector2.Lerp(iconRect.anchoredPosition, reduced ? baseAnchoredPosition : animatedPosition, t);
                iconRect.localRotation = Quaternion.Slerp(iconRect.localRotation, reduced ? baseRotation : animatedRotation, t);
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

        public void ConfigureNavigationSurface(Image background, Image underline, TMP_Text label, Image glow = null, Outline outline = null)
        {
            navigationSurface = true;
            backgroundImage = background;
            selectionUnderline = underline;
            navigationLabel = label;
            hoverGlow = glow;
            hoverOutline = outline;
            normalBackground = new Color(0.067f, 0.094f, 0.106f, 0f);
            hoverBackground = new Color(0.24f, 0.19f, 0.10f, 0.42f);
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
            if (!pointerInside)
                hoverStartedAt = Time.unscaledTime;
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
            if (!pointerInside)
                hoverStartedAt = Time.unscaledTime;
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
                targetGlowAlpha = selected ? 0.20f : 0.17f;
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
            if (hoverOutline != null)
            {
                var outline = BBIconDesignTokens.Selected;
                outline.a = Mathf.Clamp01(targetGlowAlpha * 2.0f);
                hoverOutline.effectColor = outline;
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
            GetNavigationHoverPose(out float rotation, out float scale, out Vector2 offset, out _);
            targetScale = baseScale * scale;
            targetAnchoredPosition = baseAnchoredPosition + offset;
            targetRotation = baseRotation * Quaternion.Euler(0f, 0f, rotation);
        }

        private float GetNavigationHoverBlend()
        {
            GetNavigationHoverPose(out _, out _, out _, out float duration);
            float elapsed = Mathf.Max(0f, Time.unscaledTime - hoverStartedAt);
            float pingPong = Mathf.PingPong(elapsed / Mathf.Max(0.01f, duration), 1f);
            float eased = pingPong * pingPong * (3f - 2f * pingPong);
            // Mantiene una pose mínima visible durante todo el hover y oscila sobre ella.
            return Mathf.Lerp(0.30f, 1f, eased);
        }

        private void GetNavigationHoverPose(out float rotation, out float scale, out Vector2 offset, out float duration)
        {
            rotation = 0f;
            scale = BBIconDesignTokens.HoverScale;
            offset = Vector2.up * BBIconDesignTokens.HoverLift;
            duration = 0.90f;

            switch (iconId)
            {
                case BBIconId.NavActivity:
                    rotation = -2.4f; scale = 1.065f; offset = new Vector2(0f, 6f); duration = 0.82f; break;
                case BBIconId.NavStaff:
                    rotation = 1.7f; scale = 1.055f; offset = new Vector2(0f, 5f); duration = 0.90f; break;
                case BBIconId.NavMenu:
                    rotation = -3.0f; scale = 1.060f; offset = new Vector2(2f, 5f); duration = 0.88f; break;
                case BBIconId.NavInventory:
                    rotation = 2.1f; scale = 1.055f; offset = new Vector2(0f, 5f); duration = 0.86f; break;
                case BBIconId.NavSuppliers:
                    rotation = 0f; scale = 1.050f; offset = new Vector2(7f, 2f); duration = 0.92f; break;
                case BBIconId.NavReservations:
                    rotation = -2.6f; scale = 1.060f; offset = new Vector2(-2f, 5f); duration = 0.95f; break;
                case BBIconId.NavEconomy:
                    rotation = 2.5f; scale = 1.060f; offset = new Vector2(2f, 5f); duration = 0.86f; break;
                case BBIconId.NavMarketing:
                    rotation = -4.0f; scale = 1.065f; offset = new Vector2(4f, 4f); duration = 0.82f; break;
                case BBIconId.NavReputation:
                    rotation = 1.8f; scale = 1.060f; offset = new Vector2(0f, 6f); duration = 0.90f; break;
                case BBIconId.NavOptions:
                    rotation = 8.0f; scale = 1.050f; offset = new Vector2(0f, 3f); duration = 1.20f; break;
            }
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
