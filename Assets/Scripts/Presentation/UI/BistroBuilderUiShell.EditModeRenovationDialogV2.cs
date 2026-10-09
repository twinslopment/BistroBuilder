using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Symbol = BistroBuilderEditChromeSymbol;

/// <summary>
/// Transaction confirmation UI. There is no second renovation model:
/// B5 decides whether Apply or Discard succeeds.
/// </summary>
public sealed partial class BistroBuilderUiShell
{
    private enum EditV2Confirmation { Apply, Discard, Exit }
    private RectTransform editV2ConfirmationRoot;
    private TMP_Text editV2ConfirmationTitle;
    private TMP_Text editV2ConfirmationDetail;
    private Button editV2ConfirmationPrimary;
    private Button editV2ConfirmationSecondary;
    private Button editV2ConfirmationCancel;
    private TMP_Text editV2ConfirmationPrimaryText;
    private TMP_Text editV2ConfirmationSecondaryText;
    private EditV2Confirmation activeEditV2Confirmation;
    private bool exitAfterConfirmedDiscard;

    private void EnsureEditV2Confirmation()
    {
        if (editV2ConfirmationRoot != null || shellRoot == null)
            return;

        var root = NewUi("BB_EditorV2_ConfirmReform", shellRoot)
            .GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        var blockInput = root.gameObject.AddComponent<Image>();
        blockInput.color = new Color32(31, 22, 13, 180);
        blockInput.raycastTarget = true;

        var panel = NewUi("ReformConfirmationCard", root)
            .GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
        panel.pivot = new Vector2(.5f, .5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(468, 314);
        var background = panel.gameObject.AddComponent<Image>();
        background.sprite = EditChromeRoundedSprite();
        background.type = Image.Type.Sliced;
        background.color = new Color32(249, 238, 219, 255);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(176, 122, 55, 255);
        outline.effectDistance = new Vector2(2, -2);
        var shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .31f);
        shadow.effectDistance = new Vector2(0, -7);

        var eyebrow = ChromeText(panel, "Context", "EDITOR V2  /  REFORMA",
            12, EditChromeMuted);
        ChromeBox(eyebrow.rectTransform, 27, 21, 414, 24);
        editV2ConfirmationTitle = ChromeText(panel, "Title", "",
            26, EditChromeText);
        editV2ConfirmationTitle.font = BistroBuilderTypography.Title;
        ChromeBox(editV2ConfirmationTitle.rectTransform, 27, 55, 414, 40);
        editV2ConfirmationDetail = ChromeText(panel, "Details", "",
            14, EditChromeText);
        editV2ConfirmationDetail.textWrappingMode = TextWrappingModes.Normal;
        editV2ConfirmationDetail.overflowMode = TextOverflowModes.Ellipsis;
        editV2ConfirmationDetail.alignment = TextAlignmentOptions.TopLeft;
        ChromeBox(editV2ConfirmationDetail.rectTransform, 27, 107, 414, 112);

        editV2ConfirmationCancel = ChromeButton(panel, "EditV2Cancel",
            Symbol.Undo, "Seguir editando", 142, 54,
            "Conservar todos los cambios y continuar",
            CloseEditV2Confirmation);
        ChromeBox(editV2ConfirmationCancel.GetComponent<RectTransform>(),
            20, 237, 142, 54);
        editV2ConfirmationSecondary = ChromeButton(panel,
            "EditV2Secondary", Symbol.Delete, "Descartar", 135, 54,
            "Descartar toda la reforma", ConfirmEditV2Secondary, true);
        ChromeBox(editV2ConfirmationSecondary.GetComponent<RectTransform>(),
            169, 237, 135, 54);
        editV2ConfirmationPrimary = ChromeButton(panel, "EditV2Primary",
            Symbol.Play, "Aplicar", 143, 54,
            "Confirmar solamente si la reforma es válida",
            ConfirmEditV2Primary);
        ChromeBox(editV2ConfirmationPrimary.GetComponent<RectTransform>(),
            310, 237, 143, 54);
        editV2ConfirmationPrimaryText =
            editV2ConfirmationPrimary.GetComponentInChildren<TMP_Text>();
        editV2ConfirmationSecondaryText =
            editV2ConfirmationSecondary.GetComponentInChildren<TMP_Text>();

        editV2ConfirmationRoot = root;
        root.gameObject.SetActive(false);
    }

    private void RequestEditV2Confirmation(EditV2Confirmation kind)
    {
        ResolveEditChrome();
        EnsureEditV2Confirmation();
        if (editV2ConfirmationRoot == null)
            return;

        if (editChromeRenovation == null)
        {
            ChromeMessage("La sesión de reforma B5 no está disponible.");
            return;
        }

        if (!editChromeRenovation.HasPendingChanges)
        {
            if (kind == EditV2Confirmation.Exit)
                HandleEditModeClicked();
            else
                ChromeMessage("No hay cambios pendientes en la reforma.");
            return;
        }

        activeEditV2Confirmation = kind;
        editV2ConfirmationRoot.gameObject.SetActive(true);
        editV2ConfirmationRoot.SetAsLastSibling();

        editV2ConfirmationPrimary.gameObject.SetActive(true);
        editV2ConfirmationSecondary.gameObject.SetActive(true);

        // B5 is the single source for counts and financial estimates.
        string summary = "Operaciones pendientes.";
        if (editChromeRenovation.TryGetSnapshot(out var snapshot,
                out string quotationError))
        {
            summary = snapshot.operationCount + " operaciones · Coste neto: " +
                BistroBuilderFinanceUiFormat.Money(snapshot.totalSignedCostCents) +
                ". Caja: " +
                BistroBuilderFinanceUiFormat.Money(snapshot.currentBalanceCents) + ".";
        }
        else if (!string.IsNullOrWhiteSpace(quotationError))
        {
            summary = "Presupuesto pendiente de validación: " + quotationError;
        }

        if (kind == EditV2Confirmation.Discard)
        {
            editV2ConfirmationTitle.text = "¿Descartar toda la reforma?";
            editV2ConfirmationDetail.text =
                "Se restaurará el estado previo a la reforma, incluidos " +
                "mobiliario, construcción y superficies. Esta acción requiere " +
                "confirmación expresa.\n" + summary;
            editV2ConfirmationPrimary.gameObject.SetActive(false);
            editV2ConfirmationSecondaryText.text = "Sí, descartar";
        }
        else
        {
            editV2ConfirmationTitle.text = kind == EditV2Confirmation.Exit
                ? "Hay una reforma pendiente" : "Revisar antes de aplicar";
            editV2ConfirmationDetail.text = kind == EditV2Confirmation.Exit
                ? "Para salir debes aplicar los cambios o descartar la reforma. " +
                  "Puedes continuar editando sin perder nada.\n" + summary
                : "Se aplicará la reforma completa tras validarla.\n" + summary;
            editV2ConfirmationPrimaryText.text =
                kind == EditV2Confirmation.Exit ? "Aplicar y salir" : "Aplicar";
            editV2ConfirmationSecondaryText.text =
                kind == EditV2Confirmation.Exit ? "Descartar y salir" : "Descartar";
        }
    }

    private void CloseEditV2Confirmation()
    {
        exitAfterConfirmedDiscard = false;
        if (editV2ConfirmationRoot != null)
            editV2ConfirmationRoot.gameObject.SetActive(false);
    }

    private void ConfirmEditV2Primary()
    {
        string error = string.Empty;
        if (editChromeRenovation == null ||
            !editChromeRenovation.TryApplyChanges(out error))
        {
            editV2ConfirmationDetail.text = string.IsNullOrWhiteSpace(error)
                ? "No ha sido posible aplicar la reforma." : error;
            return;
        }

        bool exit = activeEditV2Confirmation == EditV2Confirmation.Exit;
        CloseEditV2Confirmation();
        ChromeMessage("Reforma aplicada.");
        if (exit)
            HandleEditModeClicked();
    }

    private void ConfirmEditV2Secondary()
    {
        // A secondary path from Apply / Exit is not itself confirmation.
        // Explicitly ask again in the destructive Discard dialog.
        if (activeEditV2Confirmation != EditV2Confirmation.Discard)
        {
            bool exitRequested = activeEditV2Confirmation == EditV2Confirmation.Exit;
            RequestEditV2Confirmation(EditV2Confirmation.Discard);
            exitAfterConfirmedDiscard = exitRequested;
            return;
        }

        string error = string.Empty;
        if (editChromeRenovation == null ||
            !editChromeRenovation.TryDiscardChanges(out error))
        {
            editV2ConfirmationDetail.text = string.IsNullOrWhiteSpace(error)
                ? "No ha sido posible descartar la reforma." : error;
            return;
        }

        bool exit = exitAfterConfirmedDiscard;
        CloseEditV2Confirmation();
        ChromeMessage("Reforma descartada. Estado inicial restaurado.");
        if (exit)
            HandleEditModeClicked();
    }

    private bool TickEditV2ConfirmationInput()
    {
        if (editV2ConfirmationRoot == null ||
            !editV2ConfirmationRoot.gameObject.activeSelf)
            return false;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return false;
        CloseEditV2Confirmation();
        return true;
    }

    private void HideEditV2ConfirmationIfInactive(bool editing)
    {
        if (!editing)
            CloseEditV2Confirmation();
    }
}
