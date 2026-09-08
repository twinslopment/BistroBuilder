using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Panel jugable compacto para cierre y resumen de jornada.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEndOfDayPlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderEndOfDayService service;
    private GameObject panel;
    private TMP_Text body;
    private Button closeButton;
    private TMP_Text closeLabel;
    private Button nextDayButton;
    private TMP_Text feedback;
    private Button toggle;
    private bool open;
    private float refreshTimer;

    private void Awake()
    {
        if (service == null) service = GetComponent<BistroBuilderEndOfDayService>();
        BuildUi();
    }

    private void OnEnable()
    {
        if (service != null)
        {
            service.PhaseChanged += HandlePhaseChanged;
            service.SummaryReady += HandleSummaryReady;
            service.HistoryChanged += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (service != null)
        {
            service.PhaseChanged -= HandlePhaseChanged;
            service.SummaryReady -= HandleSummaryReady;
            service.HistoryChanged -= Refresh;
        }
    }

    private void Update()
    {
        if (!open || service == null) return;
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.5f;
            Refresh();
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        if (service == null) service = GetComponent<BistroBuilderEndOfDayService>();
        if (service == null)
        {
            error = "Falta BistroBuilderEndOfDayService.";
            return false;
        }
        return service.ValidateConfiguration(out error);
    }

    private void BuildUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject root = new GameObject("BistroBuilderEndOfDay15UI", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rr = (RectTransform)root.transform;
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(0f, 1f);
        rr.pivot = new Vector2(0f, 1f);
        rr.anchoredPosition = new Vector2(18f, -18f);
        rr.sizeDelta = new Vector2(470f, 610f);

        GameObject toggleGo = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        toggleGo.transform.SetParent(root.transform, false);
        RectTransform tr = (RectTransform)toggleGo.transform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0f, 1f);
        tr.sizeDelta = new Vector2(155f, 36f);
        toggle = toggleGo.GetComponent<Button>();
        toggle.onClick.AddListener(Toggle);
        TMP_Text tl = CreateText(toggleGo.transform, "CIERRE DEL DIA", 13);
        tl.alignment = TextAlignmentOptions.Center;

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        RectTransform pr = (RectTransform)panel.transform;
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = new Vector2(0f, -44f);

        body = CreateText(panel.transform, string.Empty, 13);
        RectTransform br = body.rectTransform;
        br.anchorMin = new Vector2(0f, 0.20f); br.anchorMax = new Vector2(1f, 1f);
        br.offsetMin = new Vector2(12f, 8f); br.offsetMax = new Vector2(-12f, -12f);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;

        closeButton = CreateButton(panel.transform, "INICIAR CIERRE", new Vector2(0.02f, 0.08f), new Vector2(0.48f, 0.18f));
        closeLabel = closeButton.GetComponentInChildren<TMP_Text>();
        closeButton.onClick.AddListener(BeginClose);
        nextDayButton = CreateButton(panel.transform, "SIGUIENTE DIA", new Vector2(0.52f, 0.08f), new Vector2(0.98f, 0.18f));
        nextDayButton.onClick.AddListener(NextDay);
        feedback = CreateText(panel.transform, string.Empty, 11);
        RectTransform fr = feedback.rectTransform;
        fr.anchorMin = new Vector2(0.02f, 0f); fr.anchorMax = new Vector2(0.98f, 0.07f);
        fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        feedback.alignment = TextAlignmentOptions.Center;

        panel.SetActive(false);
    }

    private static Button CreateButton(Transform parent, string label, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = min; r.anchorMax = max; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        TMP_Text text = CreateText(go.transform, label, 12);
        text.alignment = TextAlignmentOptions.Center;
        return go.GetComponent<Button>();
    }

    private static TMP_Text CreateText(Transform parent, string value, float size)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 4f); rect.offsetMax = new Vector2(-8f, -4f);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = size;
        return text;
    }

    private void Toggle()
    {
        open = !open;
        if (panel != null) panel.SetActive(open);
        if (open) Refresh();
    }

    private void BeginClose()
    {
        if (service == null) return;
        if (!service.TryBeginEndOfService(out string error))
            feedback.text = error;
        else feedback.text = "Cierre iniciado. Finalizando clientes y tareas activas.";
        Refresh();
    }

    private void NextDay()
    {
        if (service == null) return;
        if (!service.TryAdvanceToNextDay(out string error)) feedback.text = error;
        else feedback.text = "Siguiente jornada preparada.";
        Refresh();
    }

    private void HandlePhaseChanged(BistroBuilderEndOfDayPhase _) => Refresh();
    private void HandleSummaryReady(BistroBuilderEndOfDaySummary _) => Refresh();

    private void Refresh()
    {
        if (!open || body == null || service == null) return;
        var lines = new List<string>(28)
        {
            "FIN DE SERVICIO Y FIN DE DIA",
            "Estado: " + service.Phase,
            "Pendientes: " + service.RemainingGuestCount + " grupos / " + service.RemainingOrderCount + " comandas"
        };

        if (service.TryGetLatestSummary(out BistroBuilderEndOfDaySummary s))
        {
            lines.Add("");
            lines.Add("Dia " + s.dayIndex + " - " + s.calendarDay.ToString("D2") + "/" + s.calendarMonth.ToString("D2") + "/" + s.calendarYear);
            lines.Add("Ventas: " + Money(s.revenueCents) + "   Gastos: " + Money(s.totalExpensesCents));
            lines.Add("Resultado: " + Money(s.operatingResultCents) + "   Rendimiento: " + (s.performanceBasisPoints / 100f).ToString("0.0") + "%");
            lines.Add("Comandas cobradas: " + s.paidOrderCount + "   Grupos: " + s.servedGroupCount);
            lines.Add("Satisfaccion: " + (s.averageSatisfactionBasisPoints / 100f).ToString("0.0") + "%");
            lines.Add("Reputacion: " + (s.reputationAfterBasisPoints / 100f).ToString("0.0") + "% (" + Signed(s.reputationDeltaBasisPoints) + ")");
            lines.Add("Incidencias: " + s.incidentCount + "   Abandonos: " + s.abandonedGroupCount);
            lines.Add("Consumo inventario: " + s.inventoryConsumedCanonicalMilliUnits + " milunidades");
            if (s.hasPreviousDayComparison)
                lines.Add("Vs. dia anterior: ventas " + SignedMoney(s.revenueDeltaVsPreviousCents) +
                    " / rendimiento " + Signed(s.performanceDeltaVsPreviousBasisPoints));
            Append(lines, "Destacados", s.highlights);
            Append(lines, "Problemas", s.problems);
            Append(lines, "Oportunidades", s.opportunities);
            Append(lines, "Siguiente dia", s.recommendations);
        }
        else lines.Add("Aun no hay un cierre consolidado.");

        body.text = string.Join("\n", lines);
        closeButton.interactable = service.Phase == BistroBuilderEndOfDayPhase.ServiceOpen;
        nextDayButton.interactable = service.Phase == BistroBuilderEndOfDayPhase.SummaryReady;
        if (closeLabel != null)
            closeLabel.text = service.Phase == BistroBuilderEndOfDayPhase.Closing ? "CERRANDO..." : "INICIAR CIERRE";
    }

    private static void Append(List<string> lines, string title, List<string> source)
    {
        if (source == null || source.Count == 0) return;
        lines.Add(""); lines.Add(title + ":");
        for (int i = 0; i < source.Count && i < 3; i++) lines.Add("- " + source[i]);
    }

    private static string Money(long cents) => (cents / 100.0).ToString("0.00") + " EUR";
    private static string SignedMoney(long cents) => (cents >= 0 ? "+" : "") + Money(cents);
    private static string Signed(int basisPoints) => (basisPoints >= 0 ? "+" : "") + (basisPoints / 100f).ToString("0.0") + "%";
}
