using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderNewGameOpeningPlayerScreen
{
    private Canvas initialActionsCanvas;
    private TMP_Text initialStatus;
    private Button initialSave;
    private Button initialContinue;

    private static readonly Color InitialRibbonSurface =
        new Color32(250, 247, 240, 252);

    private static readonly Color InitialRibbonBorder =
        new Color32(211, 201, 184, 255);

    private static readonly Color InitialRibbonMuted =
        new Color32(104, 101, 92, 255);

    private static readonly Color InitialRibbonPrimary =
        new Color32(103, 128, 70, 255);

    private static readonly Color InitialRibbonPrimaryHover =
        new Color32(118, 145, 83, 255);

    private void RefreshInitialActions()
    {
        bool show =
            openingService != null &&
            openingService.Phase ==
                BistroBuilderNewGamePhase.InitialSetup;

        if (show &&
            initialActionsCanvas == null)
        {
            BuildInitialDesignRibbon();
        }

        if (initialActionsCanvas == null)
            return;

        initialActionsCanvas.gameObject.SetActive(show);

        if (!show)
            return;

        initialSave.interactable =
            initialContinue.interactable =
                !openingService.IsSaveBusy;

        initialStatus.text =
            ResolveLiveStatus();
    }

    private void BuildInitialDesignRibbon()
    {
        if (roundedIvory == null)
            roundedIvory = CreateIvoryRound();

        GameObject root =
            new GameObject(
                "InitialDesignActions",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        initialActionsCanvas =
            root.GetComponent<Canvas>();

        initialActionsCanvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        initialActionsCanvas.sortingOrder =
            15000;

        CanvasScaler scaler =
            root.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        scaler.matchWidthOrHeight =
            0.5f;

        Image panel =
            Surface(
                root.transform,
                "InitialDesignRibbon",
                InitialRibbonSurface);

        RectTransform rect =
            panel.rectTransform;

        rect.anchorMin =
            rect.anchorMax =
                new Vector2(0.5f, 0f);

        rect.pivot =
            new Vector2(0.5f, 0f);

        rect.anchoredPosition =
            new Vector2(0f, 104f);

        rect.sizeDelta =
            new Vector2(760f, 76f);

        Outline outline =
            panel.gameObject.AddComponent<Outline>();

        outline.effectColor =
            InitialRibbonBorder;

        outline.effectDistance =
            new Vector2(1f, -1f);

        Shadow shadow =
            panel.gameObject.AddComponent<Shadow>();

        shadow.effectColor =
            new Color(0f, 0f, 0f, 0.10f);

        shadow.effectDistance =
            new Vector2(0f, -3f);

        TMP_Text heading =
            Label(
                rect,
                "Heading",
                "Diseño inicial",
                20f,
                10f,
                210f,
                28f,
                20f,
                true);

        heading.color = Ink;

        TMP_Text restaurant =
            Label(
                rect,
                "Restaurant",
                openingService.RestaurantName,
                20f,
                38f,
                210f,
                22f,
                13f);

        restaurant.color =
            InitialRibbonMuted;

        initialStatus =
            Label(
                rect,
                "Status",
                string.Empty,
                236f,
                14f,
                188f,
                48f,
                12.5f,
                false,
                TextAlignmentOptions.MidlineLeft);

        initialStatus.color =
            InitialRibbonMuted;

        initialSave =
            ActionButton(
                rect,
                "SaveRecovery",
                "Guardar",
                436f,
                16f,
                126f,
                44f,
                HandleInitialSave);

        initialContinue =
            ActionButton(
                rect,
                "ValidateAndContinue",
                "Validar y continuar",
                572f,
                16f,
                168f,
                44f,
                HandleInitialContinue);

        StyleInitialSecondaryButton(
            initialSave);

        StyleInitialPrimaryButton(
            initialContinue);

        initialSave
            .GetComponentInChildren<TMP_Text>()
            .fontSize = 15f;

        initialContinue
            .GetComponentInChildren<TMP_Text>()
            .fontSize = 15f;
    }

    private void HandleInitialSave()
    {
        if (!TryCommitArchitectureBeforeTransition(
                out string error))
        {
            statusMessage =
                error;

            return;
        }

        statusMessage =
            openingService.TryRequestInitialSave(
                out error)
                ? "Guardando…"
                : error;
    }

    private void HandleInitialContinue()
    {
        if (!TryCommitArchitectureBeforeTransition(
                out string error) ||
            !TryValidateAndEnterGame(
                out error))
        {
            statusMessage =
                error;

            return;
        }

        statusMessage =
            string.Empty;

        Hide();
    }

    private static void StyleInitialSecondaryButton(
        Button button)
    {
        if (button == null)
            return;

        ColorBlock colors =
            button.colors;

        colors.normalColor =
            new Color32(247, 242, 232, 255);

        colors.highlightedColor =
            new Color32(255, 248, 233, 255);

        colors.selectedColor =
            colors.highlightedColor;

        colors.pressedColor =
            new Color32(229, 218, 199, 255);

        button.colors =
            colors;

        TMP_Text label =
            button.GetComponentInChildren<TMP_Text>();

        if (label != null)
            label.color = Ink;
    }

    private static void StyleInitialPrimaryButton(
        Button button)
    {
        if (button == null)
            return;

        ColorBlock colors =
            button.colors;

        colors.normalColor =
            InitialRibbonPrimary;

        colors.highlightedColor =
            InitialRibbonPrimaryHover;

        colors.selectedColor =
            InitialRibbonPrimaryHover;

        colors.pressedColor =
            new Color32(86, 108, 58, 255);

        colors.disabledColor =
            new Color32(171, 180, 156, 255);

        button.colors =
            colors;

        TMP_Text label =
            button.GetComponentInChildren<TMP_Text>();

        if (label != null)
            label.color = Color.white;
    }
}
