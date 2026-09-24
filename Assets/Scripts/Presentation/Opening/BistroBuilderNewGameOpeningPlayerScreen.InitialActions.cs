using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BistroBuilderNewGameOpeningPlayerScreen
{
    private Canvas initialActionsCanvas;
    private TMP_Text initialStatus;
    private Button initialSave, initialContinue;

    private void RefreshInitialActions()
    {
        bool show = openingService != null && openingService.Phase == BistroBuilderNewGamePhase.InitialSetup;
        if (show && initialActionsCanvas == null)
        {
            if (roundedIvory == null) roundedIvory = CreateIvoryRound();
            var root = new GameObject("InitialDesignActions", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            initialActionsCanvas = root.GetComponent<Canvas>();
            initialActionsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            initialActionsCanvas.sortingOrder = 15000;
            var panel = Surface(root.transform, "InitialDesignPanel", new Color32(48, 42, 33, 255));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, 110); rect.sizeDelta = new Vector2(350, 184);
            Label(rect, "Heading", "Diseño inicial", 16, 12, 318, 30, 22, true).color = Ivory;
            initialSave = ActionButton(rect, "SaveRecovery", "Guardar recuperación", 16, 49, 318, 44, () =>
            {
                if (!TryCommitArchitectureBeforeTransition(out string error)) statusMessage = error;
                else statusMessage = openingService.TryRequestInitialSave(out error) ? "Guardando…" : error;
            });
            initialContinue = ActionButton(rect, "ValidateAndContinue", "Validar y continuar", 16, 101, 318, 44, () =>
            {
                if (!TryCommitArchitectureBeforeTransition(out string error) || !TryValidateAndEnterGame(out error)) statusMessage = error;
                else { statusMessage = string.Empty; Hide(); }
            });
            initialSave.GetComponentInChildren<TMP_Text>().fontSize = 18;
            initialContinue.GetComponentInChildren<TMP_Text>().fontSize = 18;
            initialStatus = Label(rect, "Status", "", 16, 150, 318, 28, 14); initialStatus.color = Ivory;
        }
        if (initialActionsCanvas == null) return;
        initialActionsCanvas.gameObject.SetActive(show);
        if (!show) return;
        initialActionsCanvas.scaleFactor = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), .7f, 1.4f);
        initialSave.interactable = initialContinue.interactable = !openingService.IsSaveBusy;
        initialStatus.text = ResolveLiveStatus();
    }
}
