using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// B10 visual adapter for the existing catalogue + contextual inspector.
/// All business rules, Finance, seat topology and Undo/Redo remain in
/// BistroBuilderEditorV2GroupOperationService / canonical authorities.
/// </summary>
public sealed partial class RestaurantPlaceableInspectorPanel
{
    private static readonly Color ReplacementHoney =
        new Color32(157, 112, 45, 255);
    private static readonly Color ReplacementIvory =
        new Color32(248, 243, 234, 255);
    private static readonly CultureInfo ReplacementCurrencyCulture =
        CultureInfo.GetCultureInfo("es-ES");

    private BistroBuilderEditorV2GroupOperationService replacementOperations;
    private BistroBuilderEditorV2SelectionCoordinator replacementSelection;
    private RestaurantPlaceableItemDefinition currentReplacementDefinition;
    private GameObject replacementSection;
    private TMP_Text replacementCostText;
    private TMP_Text replacementButtonText;
    private Button replacementButton;
    private long quotedReplacementCents;
    private long observedSelectionRevision = long.MinValue;
    private bool quotedReplacementValid;
    private bool observedPlacementActive;

    private void CacheReplacementDependencies()
    {
        if (replacementOperations == null)
            replacementOperations = FindFirstObjectByType<
                BistroBuilderEditorV2GroupOperationService>(
                    FindObjectsInactive.Include);
        if (replacementSelection == null)
            replacementSelection = FindFirstObjectByType<
                BistroBuilderEditorV2SelectionCoordinator>(
                    FindObjectsInactive.Include);
    }

    private void BuildReplacementAction()
    {
        if (replacementSection != null) return;
        RectTransform section = CreateLayoutRow("ReplaceSelection", 104f);
        replacementSection = section.gameObject;
        Image backing = section.gameObject.AddComponent<Image>();
        backing.color = ReplacementIvory;
        ApplyRounded(backing, 12);

        replacementCostText = CreateTmp(
            "ReplacementQuote", section,
            "Selecciona los objetos que quieres sustituir.",
            regularFont, 13f, TextPrimary,
            TextAlignmentOptions.MidlineLeft);
        replacementCostText.textWrappingMode = TextWrappingModes.Normal;
        replacementCostText.overflowMode = TextOverflowModes.Ellipsis;
        SetAnchors(replacementCostText.rectTransform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(12f, -49f), new Vector2(-12f, -4f));

        replacementButton = CreateTextButton(
            "ConfirmReplacement", section, "Sustituir selección",
            315f, 43f, ReplacementHoney, Color.white, 14f);
        RectTransform action = replacementButton.GetComponent<RectTransform>();
        action.anchorMin = new Vector2(0f, 0f);
        action.anchorMax = new Vector2(1f, 0f);
        action.pivot = new Vector2(0.5f, 0f);
        action.anchoredPosition = new Vector2(0f, 8f);
        action.sizeDelta = new Vector2(-20f, 43f);
        replacementButtonText =
            replacementButton.GetComponentInChildren<TMP_Text>(true);
        replacementButton.onClick.AddListener(ConfirmReplacementSelection);

        replacementSection.SetActive(false);
    }

    private void RefreshReplacementIfNeeded()
    {
        if (replacementSection == null || currentData == null) return;
        CacheReplacementDependencies();
        long currentRevision = replacementSelection != null
            ? replacementSelection.SelectionSetRevision : -1L;
        bool placementActive = interactionController != null &&
                               interactionController.HasActivePlacement;
        if (currentRevision != observedSelectionRevision ||
            placementActive != observedPlacementActive)
            RefreshReplacementAction();
    }

    private void RefreshReplacementAction()
    {
        if (replacementSection == null) return;
        CacheReplacementDependencies();
        observedSelectionRevision = replacementSelection != null
            ? replacementSelection.SelectionSetRevision : -1L;
        observedPlacementActive = interactionController != null &&
                                  interactionController.HasActivePlacement;
        bool visible = currentData != null &&
            currentReplacementDefinition != null &&
            editModeService != null && editModeService.IsEditModeActive &&
            replacementOperations != null &&
            replacementOperations.SelectedPlaceableCount > 0 &&
            !observedPlacementActive;
        replacementSection.SetActive(visible);
        quotedReplacementValid = false;
        if (!visible) return;

        int count = replacementOperations.SelectedPlaceableCount;
        long quote;
        string error;
        quotedReplacementValid =
            replacementOperations.TryQuoteReplacementSelection(
                currentReplacementDefinition, out quote, out error);
        quotedReplacementCents = quote;
        replacementButton.interactable = quotedReplacementValid;
        if (quotedReplacementValid)
        {
            replacementCostText.color = TextPrimary;
            replacementCostText.text = count +
                (count == 1 ? " artículo" : " artículos") +
                " · " + FormatReplacementQuote(quote);
            replacementButtonText.text = "Sustituir selección";
        }
        else
        {
            replacementCostText.color = ReplacementHoney;
            replacementCostText.text = string.IsNullOrWhiteSpace(error)
                ? "Esta sustitución no está disponible."
                : error;
            replacementButtonText.text = "Sustitución no disponible";
        }
    }

    private static string FormatReplacementQuote(long netCents)
    {
        if (netCents == 0L) return "Sin coste adicional";
        decimal amount = Math.Abs((decimal)netCents) / 100m;
        string money = amount.ToString("N2", ReplacementCurrencyCulture) + " €";
        return netCents < 0L ? "Recuperas " + money :
            "Coste neto " + money;
    }

    private void ConfirmReplacementSelection()
    {
        if (!quotedReplacementValid ||
            currentReplacementDefinition == null ||
            replacementOperations == null) return;
        if (replacementSelection != null &&
            replacementSelection.SelectionSetRevision != observedSelectionRevision)
        {
            RefreshReplacementAction();
            SetStatus(true, "Selección actualizada",
                "Revisa de nuevo los objetos y el importe.");
            return;
        }

        // Re-quote from authoritative selection and Finance on every click;
        // never apply a price that has changed since it was displayed.
        long actualQuote;
        string error;
        if (!replacementOperations.TryQuoteReplacementSelection(
                currentReplacementDefinition, out actualQuote, out error))
        {
            RefreshReplacementAction();
            SetStatus(false, "No se puede sustituir", error);
            return;
        }
        if (actualQuote != quotedReplacementCents)
        {
            RefreshReplacementAction();
            SetStatus(false, "Coste actualizado",
                "Revisa el nuevo importe y pulsa de nuevo.");
            return;
        }

        if (!replacementOperations.TryReplaceSelection(
                currentReplacementDefinition, out var created, out error))
        {
            RefreshReplacementAction();
            SetStatus(false, "No se pudo sustituir", error);
            return;
        }

        RefreshReplacementAction();
        SetStatus(true, "Sustitución completada",
            (created != null ? created.Count : 0) +
            " artículo(s) sustituidos. Puedes deshacer la operación.");
    }
}
