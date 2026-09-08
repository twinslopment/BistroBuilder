using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pantalla jugable del bloque 11 para revisar y resolver cambios de comanda.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedOrderPlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderAdvancedOrderPlayerFacade facade;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button previousLineButton;
    [SerializeField] private Button nextLineButton;
    [SerializeField] private Button previousReplacementButton;
    [SerializeField] private Button nextReplacementButton;
    [SerializeField] private Button correctButton;
    [SerializeField] private Button repeatButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button incidentButton;
    [SerializeField] private Button replaceButton;
    [SerializeField] private Button courtesyButton;
    [SerializeField] private Button returnButton;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text listText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text replacementText;
    [SerializeField] private TMP_Text feedbackText;

    private BistroBuilderAdvancedOrderPlayerSnapshot snapshot;
    private int selectedLineIndex;
    private int selectedReplacementIndex;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
    public BistroBuilderAdvancedOrderPlayerLine SelectedLine =>
        snapshot != null && snapshot.lines.Count > 0
            ? snapshot.lines[Mathf.Clamp(selectedLineIndex, 0, snapshot.lines.Count - 1)]
            : null;

    private void Awake()
    {
        WireButtons();
        if (panelRoot != null) panelRoot.SetActive(false);
    }
    private void OnEnable()
    {
        if (facade != null) facade.Changed += Refresh;
    }
    private void OnDisable()
    {
        if (facade != null) facade.Changed -= Refresh;
    }

    public bool ValidateConfiguration(out string error)
    {
        if (facade == null || panelRoot == null || canvasGroup == null ||
            closeButton == null || previousLineButton == null || nextLineButton == null ||
            previousReplacementButton == null || nextReplacementButton == null ||
            correctButton == null || repeatButton == null || cancelButton == null ||
            incidentButton == null || replaceButton == null || courtesyButton == null ||
            returnButton == null || summaryText == null || listText == null ||
            detailText == null || replacementText == null || feedbackText == null)
        {
            error = "La pantalla 11 no tiene todos sus controles enlazados.";
            return false;
        }
        return facade.ValidateConfiguration(out error);
    }

    public void Show()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        selectedLineIndex = 0;
        selectedReplacementIndex = 0;
        feedbackText.text = string.Empty;
        Refresh();
    }
    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
    public void Refresh()
    {
        if (!IsOpen || facade == null) return;
        if (!facade.TryBuildSnapshot(out snapshot, out string error))
        {
            feedbackText.text = error;
            return;
        }
        selectedLineIndex = Mathf.Clamp(selectedLineIndex, 0,
            Mathf.Max(0, snapshot.lines.Count - 1));
        selectedReplacementIndex = Mathf.Clamp(selectedReplacementIndex, 0,
            Mathf.Max(0, snapshot.replacements.Count - 1));
        Render();
    }

    private void Render()
    {
        summaryText.text = snapshot.activeOrderCount + " comandas activas  ·  " +
            snapshot.changedLineCount + " cambios  ·  " + snapshot.incidentCount +
            " incidencias  ·  Cuenta actual: " + Money(snapshot.totalPriceCents);
        if (snapshot.lines.Count == 0)
        {
            listText.text = "No hay líneas de comanda activas.";
            detailText.text = string.Empty;
            replacementText.text = string.Empty;
            SetActions(null);
            return;
        }

        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < snapshot.lines.Count; i++)
        {
            BistroBuilderAdvancedOrderPlayerLine row = snapshot.lines[i];
            builder.Append(i == selectedLineIndex ? "▶ " : "   ");
            builder.Append(row.dishName).Append("  ·  ").Append(row.stateLabel);
            if (row.origin != BistroBuilderAdvancedOrderLineOriginKind.Original)
                builder.Append("  [").Append(row.originLabel).Append(']');
            if (row.incidentKind != BistroBuilderAdvancedOrderIncidentKind.None)
                builder.Append("  ⚠ ").Append(row.incidentLabel);
            if (!row.billingLabel.Equals("Facturable", StringComparison.Ordinal))
                builder.Append("  ·  ").Append(row.billingLabel);
            builder.AppendLine();
        }
        listText.text = builder.ToString();

        BistroBuilderAdvancedOrderPlayerLine selected = SelectedLine;
        detailText.text = "<b>" + selected.dishName + "</b>\n" +
            selected.stateLabel + "  ·  " + Money(selected.priceCents) + "  ·  " +
            selected.billingLabel + "\nOrigen: " + selected.originLabel +
            (string.IsNullOrWhiteSpace(selected.incidentLabel)
                ? string.Empty : "\nIncidencia: " + selected.incidentLabel) +
            (string.IsNullOrWhiteSpace(selected.changeReason)
                ? string.Empty : "\nMotivo: " + selected.changeReason) +
            "\n\n" + selected.actions.restrictionLabel;

        if (snapshot.replacements.Count > 0)
        {
            BistroBuilderAdvancedOrderReplacementOption option =
                snapshot.replacements[selectedReplacementIndex];
            replacementText.text = "Plato para corrección/reposición: <b>" +
                option.displayName + "</b>  ·  " + Money(option.priceCents);
        }
        else replacementText.text = "No hay platos disponibles para sustituir.";
        SetActions(selected);
    }

    private void SetActions(BistroBuilderAdvancedOrderPlayerLine line)
    {
        bool replacementAvailable = snapshot != null && snapshot.replacements.Count > 0;
        bool valid = line != null && line.actions != null;
        correctButton.interactable = valid && replacementAvailable && line.actions.canCorrect;
        repeatButton.interactable = valid && line.actions.canRepeat;
        cancelButton.interactable = valid && line.actions.canCancel;
        incidentButton.interactable = valid && !line.state.Equals(BistroBuilderCanonicalOrderLineState.Consumed);
        replaceButton.interactable = valid && replacementAvailable && line.actions.canReplace;
        courtesyButton.interactable = replaceButton.interactable;
        returnButton.interactable = valid && line.actions.canReturn;
        previousLineButton.interactable = snapshot != null && snapshot.lines.Count > 1;
        nextLineButton.interactable = previousLineButton.interactable;
        previousReplacementButton.interactable = snapshot != null && snapshot.replacements.Count > 1;
        nextReplacementButton.interactable = previousReplacementButton.interactable;
    }

    private void WireButtons()
    {
        closeButton?.onClick.AddListener(Hide);
        previousLineButton?.onClick.AddListener(() => MoveLine(-1));
        nextLineButton?.onClick.AddListener(() => MoveLine(1));
        previousReplacementButton?.onClick.AddListener(() => MoveReplacement(-1));
        nextReplacementButton?.onClick.AddListener(() => MoveReplacement(1));
        correctButton?.onClick.AddListener(() => Execute("Corregir", () =>
            facade.Correct(SelectedLine, SelectedReplacementDishId())));
        repeatButton?.onClick.AddListener(() => Execute("Repetir", () => facade.Repeat(SelectedLine)));
        cancelButton?.onClick.AddListener(() => Execute("Cancelar", () => facade.Cancel(SelectedLine)));
        incidentButton?.onClick.AddListener(() => Execute("Incidencia", () => facade.ReportIncident(SelectedLine)));
        replaceButton?.onClick.AddListener(() => Execute("Reponer", () =>
            facade.Replace(SelectedLine, SelectedReplacementDishId(), false)));
        courtesyButton?.onClick.AddListener(() => Execute("Cortesía", () =>
            facade.Replace(SelectedLine, SelectedReplacementDishId(), true)));
        returnButton?.onClick.AddListener(() => Execute("Devolver", () => facade.Return(SelectedLine)));
    }

    private void MoveLine(int delta)
    {
        if (snapshot == null || snapshot.lines.Count == 0) return;
        selectedLineIndex = (selectedLineIndex + delta + snapshot.lines.Count) % snapshot.lines.Count;
        feedbackText.text = string.Empty;
        Render();
    }
    private void MoveReplacement(int delta)
    {
        if (snapshot == null || snapshot.replacements.Count == 0) return;
        selectedReplacementIndex = (selectedReplacementIndex + delta + snapshot.replacements.Count) %
            snapshot.replacements.Count;
        Render();
    }
    private string SelectedReplacementDishId()
    {
        return snapshot != null && snapshot.replacements.Count > 0
            ? snapshot.replacements[selectedReplacementIndex].dishId : string.Empty;
    }
    private void Execute(
        string action,
        Func<BistroBuilderAdvancedOrderMutationResult> operation)
    {
        BistroBuilderAdvancedOrderMutationResult result = operation();
        feedbackText.text = result.succeeded
            ? "✓ " + result.message : "✕ " + result.message;
        if (result.succeeded) Refresh();
    }
    private static string Money(int cents) => (cents / 100.0).ToString("0.00") + " €";
}
