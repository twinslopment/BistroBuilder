using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Feedback jugable compacto de entrada/sala/barra. Muestra estado operativo,
/// cola priorizada y permite enviar manualmente el primer grupo a barra.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedFrontOfHousePlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderAdvancedFrontOfHouseService service;
    private readonly List<BistroBuilderFrontOfHouseQueueEntry> queue = new(24);
    private GameObject panel;
    private TMP_Text body;
    private Button barButton;
    private bool open;

    private void Awake()
    {
        if (service == null) service = GetComponent<BistroBuilderAdvancedFrontOfHouseService>();
        BuildUi();
    }

    private void OnEnable()
    {
        if (service != null)
        {
            service.QueueChanged += Refresh;
            service.OperationalStateChanged += HandleStateChanged;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (service != null)
        {
            service.QueueChanged -= Refresh;
            service.OperationalStateChanged -= HandleStateChanged;
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        if (service == null) service = GetComponent<BistroBuilderAdvancedFrontOfHouseService>();
        if (service == null)
        {
            error = "Falta BistroBuilderAdvancedFrontOfHouseService.";
            return false;
        }
        return service.ValidateConfiguration(out error);
    }

    private void BuildUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject root = new GameObject("BistroBuilderAdvancedFrontOfHouse14UI", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rr = (RectTransform)root.transform;
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(0f, 1f);
        rr.pivot = new Vector2(0f, 1f);
        rr.anchoredPosition = new Vector2(18f, -18f);
        rr.sizeDelta = new Vector2(410f, 430f);

        Button toggle = CreateButton(root.transform, "Toggle", "SALA", new Vector2(0f, 0f), new Vector2(124f, 34f));
        toggle.onClick.AddListener(Toggle);

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        RectTransform pr = (RectTransform)panel.transform;
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 1f);
        pr.offsetMin = Vector2.zero;
        pr.offsetMax = new Vector2(0f, -42f);

        GameObject bodyObject = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyObject.transform.SetParent(panel.transform, false);
        RectTransform br = (RectTransform)bodyObject.transform;
        br.anchorMin = new Vector2(0f, 0.16f);
        br.anchorMax = new Vector2(1f, 1f);
        br.offsetMin = new Vector2(10f, 4f);
        br.offsetMax = new Vector2(-10f, -8f);
        body = bodyObject.GetComponent<TextMeshProUGUI>();
        body.fontSize = 13f;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;

        barButton = CreateButton(panel.transform, "SendFirstToBar", "1º -> BARRA",
            new Vector2(10f, 10f), new Vector2(150f, 36f));
        barButton.onClick.AddListener(SendFirstToBar);
        panel.SetActive(false);
    }

    private static Button CreateButton(
        Transform parent, string name, string label, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Button button = go.GetComponent<Button>();
        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = (RectTransform)textGo.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label; text.fontSize = 13f; text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void Toggle()
    {
        open = !open;
        if (panel != null) panel.SetActive(open);
        if (open) Refresh();
    }

    private void SendFirstToBar()
    {
        if (service == null) return;
        service.CopyQueueSnapshot(queue);
        if (queue.Count == 0) return;
        service.TrySendGroupToBar(queue[0].groupId, out _);
        Refresh();
    }

    private void HandleStateChanged(BistroBuilderFrontOfHouseOperationalState _) => Refresh();

    private void Refresh()
    {
        if (!open || body == null || service == null) return;
        service.CopyQueueSnapshot(queue);
        var lines = new List<string>(queue.Count + 4)
        {
            "ENTRADA / SALA / BARRA",
            "Estado: " + service.OperationalState,
            "Cola: " + queue.Count
        };
        for (int i = 0; i < queue.Count; i++)
        {
            BistroBuilderFrontOfHouseQueueEntry entry = queue[i];
            lines.Add((i + 1) + ". Grupo " + entry.groupId + " (" + entry.partySize + ")  " +
                entry.reason + "  espera " + entry.waitingSeconds.ToString("0") + "s  " +
                Mathf.RoundToInt(entry.pressure01 * 100f) + "%" +
                (entry.occupyingBar ? "  [BARRA]" : string.Empty));
        }
        body.text = string.Join("\n", lines);
        if (barButton != null) barButton.interactable = queue.Count > 0 && !queue[0].occupyingBar;
    }
}
