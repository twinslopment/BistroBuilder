using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        private Vector3 targetScale = Vector3.one;
        private Vector2 targetAnchoredPosition;
        private Color targetIconColor;
        private Color targetBackgroundColor;
        private Color targetBorderColor;
        private float targetUnderlineAlpha;

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
            var t = 1f - Mathf.Exp(-BBIconDesignTokens.TransitionSpeed * Time.unscaledDeltaTime);

            if (iconImage != null)
                iconImage.color = Color.Lerp(iconImage.color, targetIconColor, t);

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

            if (iconRect != null)
            {
                iconRect.localScale = Vector3.Lerp(iconRect.localScale, targetScale, t);
                iconRect.anchoredPosition = Vector2.Lerp(iconRect.anchoredPosition, targetAnchoredPosition, t);
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
            interactable = runtimeButton == null || runtimeButton.interactable;
            Configure(newIconId, semanticColor);
        }

        public void SetToggleSelectionOnClick(bool value)
        {
            toggleSelectionOnClick = value;
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
            semanticRole = definition.semanticRole;
        }

        private void CacheTransform()
        {
            if (iconImage == null)
                return;

            iconRect = iconImage.rectTransform;
            baseScale = iconRect.localScale;
            baseAnchoredPosition = iconRect.anchoredPosition;
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
            targetUnderlineAlpha = 0f;

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
            if (iconRect != null)
            {
                iconRect.localScale = targetScale;
                iconRect.anchoredPosition = targetAnchoredPosition;
            }
        }
    }
}
