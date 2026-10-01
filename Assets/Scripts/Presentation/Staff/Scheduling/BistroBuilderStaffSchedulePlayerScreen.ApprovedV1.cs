using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Horarios uses the SAME visible PERSONAL family, not the obsolete dark 5E
/// panel. Commands stay delegated to the canonical 5E facade and schedule.
/// </summary>
public sealed partial class BistroBuilderStaffSchedulePlayerScreen
{
    private bool approvedScheduleReady;
    private BistroBuilderStaffPlayerScreen returnToPersonal;
    private TMP_Text approvedPayrollText;
    private readonly List<GameObject> approvedScheduleGroupHeadings =
        new List<GameObject>();

    public void ShowFromPersonal(BistroBuilderStaffPlayerScreen source)
    {
        returnToPersonal = source;
        Show();
    }

    private void HandleApprovedClose()
    {
        BistroBuilderStaffPlayerScreen source = returnToPersonal;
        Hide();
        if (source != null)
        {
            source.Show();
            source.ShowStaff();
        }
    }

    private void ReturnToPersonal(bool showCandidates)
    {
        BistroBuilderStaffPlayerScreen source = returnToPersonal;
        Hide();
        if (source == null) return;
        source.Show();
        if (showCandidates) source.ShowCandidates();
        else source.ShowStaff();
    }

    private void EnsureApprovedSchedulePresentation()
    {
        if (approvedScheduleReady || panelRoot == null || employeeContent == null)
            return;
        approvedScheduleReady = true;
        if (panelRoot.GetComponent<BistroBuilderUiStyleIsolation>() == null)
            panelRoot.AddComponent<BistroBuilderUiStyleIsolation>();
        Image existing = panelRoot.GetComponent<Image>();
        if (existing == null) existing = panelRoot.AddComponent<Image>();
        existing.color = BistroBuilderStaffVisuals.Ivory;
        existing.raycastTarget = true;
        BistroBuilderStaffVisuals.Frame(panelRoot.transform, "ApprovedScheduleFrame");
        RectTransform paper = BistroBuilderStaffVisuals.Panel(
            "ApprovedSchedulePaper", panelRoot.transform,
            .009f, .016f, .991f, .988f, BistroBuilderStaffVisuals.Ivory, false);
        paper.SetSiblingIndex(1);

        Transform title = panelRoot.transform.Find("Title");
        if (title is RectTransform titleRect)
        {
            BistroBuilderStaffVisuals.Place(titleRect,
                .034f, .923f, .366f, .982f);
            BistroBuilderStaffVisuals.TextStyle(title.GetComponent<TMP_Text>(),
                35f, true);
        }
        BistroBuilderStaffVisuals.Place(headerText.rectTransform,
            .632f, .930f, .951f, .980f);
        BistroBuilderStaffVisuals.TextStyle(headerText, 21f, true,
            TextAlignmentOptions.Right);
        BistroBuilderStaffVisuals.Separator("ScheduleHeaderRule",
            panelRoot.transform, .025f, .908f, .975f);

        Button plantilla = BistroBuilderStaffVisuals.NewButton(
            "GoToPlantilla", panelRoot.transform, "Plantilla",
            .026f, .858f, .158f, .905f);
        plantilla.onClick.AddListener(() => ReturnToPersonal(false));
        Button candidates = BistroBuilderStaffVisuals.NewButton(
            "GoToCandidates", panelRoot.transform, "Candidatos",
            .164f, .858f, .296f, .905f);
        candidates.onClick.AddListener(() => ReturnToPersonal(true));
        BistroBuilderStaffVisuals.Panel("ActiveScheduleTab",
            panelRoot.transform, .302f, .858f, .434f, .905f,
            BistroBuilderStaffVisuals.Amber);
        BistroBuilderStaffVisuals.Label("ActiveScheduleLabel",
            panelRoot.transform, "Horarios", 16f, .307f, .858f,
            .428f, .905f, true, TextAlignmentOptions.Center);
        BistroBuilderStaffVisuals.Place(closeButton.transform as RectTransform,
            .957f, .949f, .981f, .982f);
        BistroBuilderStaffVisuals.ButtonStyle(closeButton);
        TMP_Text close = closeButton.GetComponentInChildren<TMP_Text>(true);
        if (close != null) close.text = "×";

        BistroBuilderStaffVisuals.Label("ScheduleDayCaption",
            panelRoot.transform, "PLANIFICACIÓN DE TURNOS", 17f,
            .025f, .797f, .288f, .847f, true);
        BistroBuilderStaffVisuals.Place(previousDayButton.transform as RectTransform,
            .300f, .789f, .426f, .845f);
        BistroBuilderStaffVisuals.Place(nextDayButton.transform as RectTransform,
            .433f, .789f, .560f, .845f);
        BistroBuilderStaffVisuals.Place(lunchButton.transform as RectTransform,
            .588f, .789f, .689f, .845f);
        BistroBuilderStaffVisuals.Place(dinnerButton.transform as RectTransform,
            .695f, .789f, .796f, .845f);
        BistroBuilderStaffVisuals.Place(autoFillButton.transform as RectTransform,
            .802f, .789f, .970f, .845f);
        foreach (Button b in new[] {
            previousDayButton, nextDayButton, lunchButton, dinnerButton, autoFillButton
        })
            BistroBuilderStaffVisuals.ButtonStyle(b);
        TMP_Text minLabel = autoFillButton.GetComponentInChildren<TMP_Text>(true);
        if (minLabel != null) minLabel.text = "Cobertura mínima Sala";

        BistroBuilderStaffVisuals.Place(coverageText.rectTransform,
            .029f, .713f, .973f, .774f);
        BistroBuilderStaffVisuals.TextStyle(coverageText, 19f, false,
            TextAlignmentOptions.MidlineLeft);
        BistroBuilderStaffVisuals.Place(feedbackText.rectTransform,
            .025f, .124f, .770f, .165f);
        BistroBuilderStaffVisuals.TextStyle(feedbackText, 16f, false,
            TextAlignmentOptions.MidlineLeft,
            BistroBuilderStaffVisuals.Danger);

        RectTransform scroll = employeeContent.parent != null &&
            employeeContent.parent.parent != null
            ? employeeContent.parent.parent as RectTransform : null;
        if (scroll != null)
        {
            BistroBuilderStaffVisuals.Place(scroll,
                .025f, .186f, .975f, .709f);
            Image background = scroll.GetComponent<Image>();
            if (background == null) background = scroll.gameObject.AddComponent<Image>();
            background.color = BistroBuilderStaffVisuals.Paper;
            background.raycastTarget = false;
            Outline border = scroll.GetComponent<Outline>();
            if (border == null) border = scroll.gameObject.AddComponent<Outline>();
            border.effectColor = BistroBuilderStaffVisuals.Border;
            border.effectDistance = new Vector2(1.1f, -1.1f);
            var vertical = employeeContent.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
            {
                vertical.spacing = 2f;
                vertical.padding = new RectOffset(12, 12, 7, 7);
                vertical.childForceExpandHeight = false;
            }
            ScrollRect s = scroll.GetComponent<ScrollRect>();
            if (s != null) s.scrollSensitivity = 23f;
        }
        BistroBuilderStaffVisuals.Place(emptyStateText.rectTransform,
            .15f, .30f, .85f, .60f);
        BistroBuilderStaffVisuals.TextStyle(emptyStateText, 21f, true,
            TextAlignmentOptions.Center);
        BistroBuilderStaffVisuals.Place(copyPreviousButton.transform as RectTransform,
            .779f, .111f, .974f, .168f);
        BistroBuilderStaffVisuals.ButtonStyle(copyPreviousButton);

        var footer = BistroBuilderStaffVisuals.Panel(
            "ApprovedPayrollFooter", panelRoot.transform,
            .025f, .027f, .975f, .101f,
            BistroBuilderStaffVisuals.Inset);
        approvedPayrollText = BistroBuilderStaffVisuals.Label(
            "PayrollSummary", footer, string.Empty,
            19f, .020f, .04f, .979f, .96f, true,
            TextAlignmentOptions.Center);
        BistroBuilderStaffVisuals.Frame(footer, "PayrollInnerFrame");
    }

    private GameObject CreateScheduleDepartmentHeading(string department, int count)
    {
        RectTransform r = BistroBuilderStaffVisuals.Node(
            "ScheduleDept_" + department, employeeContent, 0f, 0f, 1f, 1f);
        var layout = r.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 86f;
        layout.preferredHeight = 86f;
        var band = BistroBuilderStaffVisuals.Panel("Band", r,
            .005f, .41f, .995f, .985f, BistroBuilderStaffVisuals.Inset);
        BistroBuilderStaffVisuals.Label("Title", band, department, 23f,
            .024f, .04f, .35f, .97f, true);
        BistroBuilderStaffVisuals.Label("Count", band,
            count + " EN PLANTILLA", 15f,
            .74f, .04f, .979f, .97f, true,
            TextAlignmentOptions.Right);
        var columns = BistroBuilderStaffVisuals.Panel("Columns", r,
            .005f, .005f, .995f, .41f, BistroBuilderStaffVisuals.Paper);
        string[] names = {"EMPLEADO", "ROL", "DISPONIBILIDAD",
            "SALARIO / SERVICIO", "TURNO"};
        float[] x = {.031f, .31f, .46f, .608f, .810f, .978f};
        for (int i = 0; i < names.Length; i++)
            BistroBuilderStaffVisuals.Label("Column_" + i, columns, names[i],
                13f, x[i], 0f, x[i+1], 1f, true,
                TextAlignmentOptions.Center).color = BistroBuilderStaffVisuals.Muted;
        approvedScheduleGroupHeadings.Add(r.gameObject);
        return r.gameObject;
    }

    private static void StyleScheduleEmployeeRow(
        GameObject row, BistroBuilderStaffSchedulePlayerRow data)
    {
        if (row == null || data == null) return;
        LayoutElement layout = row.GetComponent<LayoutElement>();
        if (layout != null) { layout.minHeight = 62f; layout.preferredHeight = 62f; }
        Image bg = row.GetComponent<Image>();
        if (bg != null)
            bg.color = data.scheduled ? BistroBuilderStaffVisuals.Amber
                : BistroBuilderStaffVisuals.Paper;
        Button button = row.GetComponent<Button>();
        if (button != null)
        {
            BistroBuilderStaffVisuals.ButtonStyle(button, false, data.scheduled);
            ColorBlock color = button.colors;
            color.normalColor = data.scheduled ? BistroBuilderStaffVisuals.Amber
                : BistroBuilderStaffVisuals.Paper;
            color.highlightedColor = Color.Lerp(color.normalColor,
                BistroBuilderStaffVisuals.Paper, .23f);
            button.colors = color;
        }
        if (bg != null) bg.color = data.scheduled
            ? BistroBuilderStaffVisuals.Amber : BistroBuilderStaffVisuals.Paper;
        void Style(string name, float a, float b, float size)
        {
            RectTransform rect = row.transform.Find(name) as RectTransform;
            if (rect == null) return;
            BistroBuilderStaffVisuals.Place(rect, a, 0f, b, 1f);
            BistroBuilderStaffVisuals.TextStyle(
                rect.GetComponent<TMP_Text>(), size);
        }
        Style("Name", .032f, .309f, 17f);
        Style("Availability", .462f, .601f, 16f);
        Style("Salary", .611f, .809f, 16f);
        Style("Scheduled", .812f, .974f, 16f);
        var role = BistroBuilderStaffVisuals.Label("Role", row.transform,
            data.roleName, 16f, .318f, 0f, .457f, 1f);
        role.color = BistroBuilderStaffVisuals.Muted;
        TMP_Text status = row.transform.Find("Scheduled")?.GetComponent<TMP_Text>();
        if (status != null) status.color = data.scheduled
            ? BistroBuilderStaffVisuals.Green
            : BistroBuilderStaffVisuals.Muted;
        TMP_Text availability =
            row.transform.Find("Availability")?.GetComponent<TMP_Text>();
        if (availability != null) availability.color = data.available
            ? BistroBuilderStaffVisuals.Green : BistroBuilderStaffVisuals.Muted;
    }

    private static string Money(long cents)
    {
        return (cents / 100m).ToString("0.00") + " €";
    }

    private void RenderApprovedScheduleTotals(
        BistroBuilderStaffSchedulePlayerSnapshot snapshot)
    {
        if (!approvedScheduleReady || snapshot == null) return;
        var coverage = snapshot.coverage;
        int minimum = coverage != null ? coverage.minimumRecommendedWaiters : 0;
        coverageText.text = "SALA   " + snapshot.scheduledWaiters + " / " +
            minimum + " camareros recomendados   ·   COCINA   " +
            snapshot.scheduledCooks + " cocineros programados";
        coverageText.color = BistroBuilderStaffVisuals.Ink;
        if (approvedPayrollText != null)
            approvedPayrollText.text = "Coste previsto por servicio: Sala " +
                Money(snapshot.projectedWaiterSalaryCents) +
                "   +   Cocina " + Money(snapshot.projectedCookSalaryCents) +
                "   =   TOTAL " + Money(snapshot.projectedTotalSalaryCents);
    }

    private void SetApprovedMealButtonState(Button button, bool selected)
    {
        if (button == null) return;
        BistroBuilderStaffVisuals.ButtonStyle(button, false, selected);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.color = BistroBuilderStaffVisuals.Ink;
    }
}
