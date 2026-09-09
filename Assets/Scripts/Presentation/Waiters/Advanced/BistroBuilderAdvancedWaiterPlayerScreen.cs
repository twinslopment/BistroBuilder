using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI compacta del Bloque 13. Se crea en runtime y permanece plegada por
/// defecto para no competir con el HUD principal.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedWaiterPlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderAdvancedWaiterService service;
    private GameObject panel;
    private TMP_Text body;
    private Button toggle;
    private bool open;

    private void Awake()
    {
        if (service == null) service = GetComponent<BistroBuilderAdvancedWaiterService>();
        BuildUi();
    }

    private void OnEnable()
    {
        if (service != null) service.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (service != null) service.Changed -= Refresh;
    }

    public bool ValidateConfiguration(out string error)
    {
        if (service == null) service = GetComponent<BistroBuilderAdvancedWaiterService>();
        if (service == null) { error = "Falta BistroBuilderAdvancedWaiterService."; return false; }
        if (!service.ValidateConfiguration(out error)) return false;
        error = string.Empty;
        return true;
    }

    private void BuildUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject root = new GameObject("BistroBuilderAdvancedWaiter13UI", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-18f, -18f);
        rootRect.sizeDelta = new Vector2(340f, 380f);

        GameObject buttonObject = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root.transform, false);
        RectTransform br = (RectTransform)buttonObject.transform;
        br.anchorMin = new Vector2(1f, 1f); br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(1f, 1f); br.sizeDelta = new Vector2(138f, 34f);
        toggle = buttonObject.GetComponent<Button>();
        toggle.onClick.AddListener(Toggle);
        TMP_Text label = CreateText(buttonObject.transform, "CAMAREROS", 14);
        label.alignment = TextAlignmentOptions.Center;

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        RectTransform pr = (RectTransform)panel.transform;
        pr.anchorMin = new Vector2(0f, 0f); pr.anchorMax = new Vector2(1f, 1f);
        pr.offsetMin = Vector2.zero; pr.offsetMax = new Vector2(0f, -42f);
        body = CreateText(panel.transform, string.Empty, 13);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        panel.SetActive(false);
    }

    private static TMP_Text CreateText(Transform parent, string value, float size)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 6f); rect.offsetMax = new Vector2(-10f, -6f);
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

    private void Refresh()
    {
        if (!open || body == null || service == null) return;
        Waiter[] waiters = FindObjectsByType<Waiter>(FindObjectsSortMode.None);
        System.Array.Sort(waiters, (a, b) => a.WaiterId.CompareTo(b.WaiterId));
        var lines = new List<string>(waiters.Length + 2) { "CAMAREROS - IA OPERATIVA" };
        for (int i = 0; i < waiters.Length; i++)
        {
            if (!service.TryBuildSnapshot(waiters[i], out BistroBuilderAdvancedWaiterSnapshot snapshot)) continue;
            lines.Add("#" + snapshot.waiterId + "  " + snapshot.saturation +
                "  tareas " + snapshot.plannedTaskCount + "/" + snapshot.planCapacity +
                "  zona " + snapshot.primaryZoneId);
            if (snapshot.plans.Count > 0)
            {
                BistroBuilderAdvancedWaiterTaskPlan plan = snapshot.plans[0];
                lines.Add("  > " + plan.taskType + " / " + plan.responsibility +
                    " / ruta " + plan.estimatedRouteMeters.ToString("0.0") + " m" +
                    (plan.contextualAction != BistroBuilderWaiterContextActionKind.None
                        ? " / " + plan.contextualAction : string.Empty));
            }
        }
        body.text = string.Join("\n", lines);
    }
}
