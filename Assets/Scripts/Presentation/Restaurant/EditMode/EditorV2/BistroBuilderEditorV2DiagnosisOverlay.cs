using UnityEngine;

/// <summary>
/// Lightweight, opt-in B11 diagnostic HUD. F8 toggles; no polling or scans
/// while closed. Final Galeria Viva skin can replace this view without
/// touching its read-only DiagnosisService.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEditorV2DiagnosisOverlay : MonoBehaviour
{
    [SerializeField] private BistroBuilderEditorV2DiagnosisService diagnosis;
    [SerializeField] private RestaurantEditModeService editMode;
    private bool visible;
    private Vector2 scroll;
    private BistroBuilderEditorV2DiagnosisLayer layers =
        BistroBuilderEditorV2DiagnosisLayer.All;
    private string status = string.Empty;
    private Rect window = new Rect(0, 100, 370, 520);

    public bool IsVisible => visible;
    public BistroBuilderEditorV2DiagnosisLayer ActiveLayers => layers;

    public void Configure(
        BistroBuilderEditorV2DiagnosisService diagnosticService,
        RestaurantEditModeService mode)
    {
        diagnosis = diagnosticService;
        editMode = mode;
    }

    public void SetVisible(bool show)
    {
        bool next = show && editMode != null && editMode.IsEditModeActive;
        if (!next && visible)
            diagnosis?.CancelPendingRouteScan();
        visible = next;
        if (visible) Refresh();
    }

    public void SelectLayers(BistroBuilderEditorV2DiagnosisLayer next)
    {
        layers = next & BistroBuilderEditorV2DiagnosisLayer.All;
    }

    public bool Refresh()
    {
        if (diagnosis == null)
        {
            status = "No se ha instalado B11.";
            return false;
        }
        bool ok = diagnosis.TryScan(layers, out _, out status);
        return ok;
    }

    private void OnGUI()
    {
        if (editMode == null || !editMode.IsEditModeActive)
        {
            if (visible) SetVisible(false);
            return;
        }
        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.F8)
        {
            SetVisible(!visible);
            Event.current.Use();
        }

        if (!visible)
        {
            Rect launcher = new Rect(Mathf.Max(8, Screen.width - 204), 82, 190, 35);
            if (GUI.Button(launcher, "Diagnosticar  [F8]"))
                SetVisible(true);
            return;
        }

        window.x = Mathf.Clamp(window.x, 8f,
            Mathf.Max(8f, Screen.width - window.width - 8f));
        window.y = Mathf.Clamp(window.y, 82f,
            Mathf.Max(82f, Screen.height - 180f));
        window.height = Mathf.Min(560f, Screen.height - 162f);
        window = GUI.Window(112108, window, DrawWindow,
            "Diagnóstico del restaurante");
        DrawWorldIndicators();
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Label("Lectura del restaurante • no modifica muebles");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Analizar", GUILayout.Height(30)))
            Refresh();
        if (GUILayout.Button("Cerrar", GUILayout.Width(73),
            GUILayout.Height(30))) SetVisible(false);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        LayerButton("Rutas", BistroBuilderEditorV2DiagnosisLayer.Circulation);
        LayerButton("Acceso", BistroBuilderEditorV2DiagnosisLayer.Accessibility);
        LayerButton("Plazas", BistroBuilderEditorV2DiagnosisLayer.Capacity);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        LayerButton("Uso", BistroBuilderEditorV2DiagnosisLayer.Interaction);
        LayerButton("Diseño", BistroBuilderEditorV2DiagnosisLayer.Layout);
        if (GUILayout.Button("Todas"))
        {
            layers = BistroBuilderEditorV2DiagnosisLayer.All;
            Refresh();
        }
        GUILayout.EndHorizontal();

        if (!string.IsNullOrWhiteSpace(status))
            GUILayout.Label(status);
        var report = diagnosis != null ? diagnosis.LastReport : null;
        if (report != null)
        {
            GUILayout.Label("Bloqueos: " + report.blocking +
                "    Avisos: " + report.warnings +
                "    Objetos: " + report.scannedObjects +
                "    Rutas: " + report.scannedRoutes);
            if (report.navigationPending)
                GUILayout.Label("Comprobando rutas en varios pasos; resultados provisionales.");
            else if (!report.complete)
                GUILayout.Label("Diagnóstico parcial: revisa los sistemas no evaluados.");
            scroll = GUILayout.BeginScrollView(scroll);
            if (report.findings.Count == 0)
                GUILayout.Label("Sin incidencias detectadas por las capas evaluadas.");
            for (int i = 0; i < report.findings.Count; i++)
            {
                var finding = report.findings[i];
                GUILayout.Space(5f);
                GUILayout.Label("[" + finding.severity + "] " + finding.title);
                GUILayout.Label(finding.explanation);
                GUILayout.Label("Sugerencia: " + finding.recommendation);
                if (finding.hasWorldPosition)
                    GUILayout.Label("Localización: " +
                        finding.worldPosition.x.ToString("0.0") + " / " +
                        finding.worldPosition.z.ToString("0.0") + " m");
            }
            GUILayout.EndScrollView();
        }
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0, 0, window.width, 25));
    }

    private void LayerButton(string label, BistroBuilderEditorV2DiagnosisLayer bit)
    {
        bool selected = (layers & bit) != 0;
        if (GUILayout.Button((selected ? "✓ " : "○ ") + label))
        {
            layers ^= bit;
            if (layers == BistroBuilderEditorV2DiagnosisLayer.None)
                layers = bit;
            Refresh();
        }
    }

    private void DrawWorldIndicators()
    {
        var report = diagnosis != null ? diagnosis.LastReport : null;
        var camera = Camera.main;
        if (report == null || camera == null) return;
        for (int i = 0; i < report.findings.Count && i < 90; i++)
        {
            var finding = report.findings[i];
            if (!finding.hasWorldPosition) continue;
            Vector3 screen = camera.WorldToScreenPoint(finding.worldPosition +
                Vector3.up * 0.5f);
            if (screen.z <= 0f) continue;
            Rect marker = new Rect(screen.x - 9f,
                Screen.height - screen.y - 9f, 18f, 18f);
            if (marker.Overlaps(window) ||
                screen.x < 0f || screen.x > Screen.width) continue;
            GUI.Label(marker,
                finding.severity == BistroBuilderEditorV2DiagnosisSeverity.Blocking
                    ? "!" : "•");
        }
    }
}
