using System;
using System.Collections.Generic;
using BistroBuilder.ConstructionAuthoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Compact construction catalogue and inspector on the existing HUD canvas.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderConstructionPlayerPanel : MonoBehaviour
{
    public static BistroBuilderConstructionPlayerPanel Instance { get; private set; }
    private BistroBuilderConstructionAuthoringRuntimeTool tool;
    private RestaurantEditModeService editMode;
    private RestaurantEditInteractionController furniture;
    private BistroBuilderNewGameOpeningService opening;
    private BistroBuilderUiShell shell;
    private RectTransform root, inspector, moduleControls, openingControls, initialControls, modal;
    private TMP_Text status, selection, dimensions, moduleLabel, summary;
    private Button undo, redo, copy, remove, apply, saveInitial, completeInitial;
    private readonly Dictionary<BistroBuilderConstructionRuntimeMode, Button> modes = new Dictionary<BistroBuilderConstructionRuntimeMode, Button>();
    private bool bypassExit;
    private bool exitRequested;
    private float nextRefresh;
    private string actionStatus;
    private string lastToolStatus;

    public bool IsReady => root != null;
    public bool BlocksWorldInput => (modal != null && modal.gameObject.activeInHierarchy) || (shell != null && shell.HasManagementScreenOpen);
    public static bool InterceptExit()
    {
        if (Instance == null || Instance.bypassExit || !Instance.IsReady) return false;
        if (Instance.opening != null && Instance.opening.IsInitialDesignPhase)
        {
            Instance.actionStatus = "Termina el diseño con Validar restaurante y continuar."; return true;
        }
        if (Instance.tool != null && Instance.tool.HasDraftChanges)
        { Instance.exitRequested = true; Instance.modal.gameObject.SetActive(true); return true; }
        return false;
    }

    private void Awake() { Instance = this; Cache(); }
    private void Cache()
    {
        if (tool == null) tool = GetComponent<BistroBuilderConstructionAuthoringRuntimeTool>();
        if (editMode == null) editMode = FindFirstObjectByType<RestaurantEditModeService>();
        if (furniture == null) furniture = FindFirstObjectByType<RestaurantEditInteractionController>();
        if (opening == null) opening = FindFirstObjectByType<BistroBuilderNewGameOpeningService>();
        if (shell == null) shell = FindFirstObjectByType<BistroBuilderUiShell>();
    }
    private void LateUpdate()
    {
        Cache();
        if (root == null) Build();
        if (root == null) return;
        bool editing = editMode != null && editMode.IsEditModeActive;
        root.gameObject.SetActive(editing && (shell == null || !shell.HasManagementScreenOpen));
        if (!root.gameObject.activeSelf) return;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.12f;
        if (lastToolStatus != tool.StatusMessage) { actionStatus = null; lastToolStatus = tool.StatusMessage; }
        foreach (var entry in modes)
        {
            Color normal = entry.Key == tool.Mode ? BistroBuilderUiTokens.Primary : BistroBuilderUiTokens.Surface2;
            entry.Value.GetComponent<Image>().color = Color.white;
            entry.Value.colors = BistroBuilderUiTokens.ButtonColors(normal,Color.Lerp(normal,Color.white,0.1f),Color.Lerp(normal,Color.black,0.15f));
        }
        bool furnitureMode = tool.Mode == BistroBuilderConstructionRuntimeMode.Furniture;
        inspector.gameObject.SetActive(!furnitureMode);
        moduleControls.gameObject.SetActive(tool.Mode == BistroBuilderConstructionRuntimeMode.WallModule);
        openingControls.gameObject.SetActive(tool.SelectedKind == EntityKind.Opening);
        selection.text = tool.SelectionDescription();
        dimensions.text = tool.DimensionsText;
        moduleLabel.text = "Módulo  " + tool.ModuleLength.ToString("0.00") + " m · " + tool.ModuleAngle.ToString("0") + "°";
        summary.text = "CONSTRUCCIÓN\n" + tool.WallCount + " paredes · " + tool.RoomCount + " habitaciones";
        status.text = string.IsNullOrEmpty(actionStatus) ? tool.StatusMessage : actionStatus;
        undo.interactable = tool.CanUndo; redo.interactable = tool.CanRedo;
        copy.interactable = remove.interactable = tool.SelectedKind == EntityKind.Wall || tool.SelectedKind == EntityKind.Opening;
        apply.interactable = tool.HasDraftChanges;
        bool initial = opening != null && opening.IsInitialDesignPhase;
        initialControls.gameObject.SetActive(initial);
        if (initial) saveInitial.interactable = completeInitial.interactable = !opening.IsSaveBusy;
    }

    private void Build()
    {
        var canvas = shell != null ? shell.GetComponentInParent<Canvas>() : null;
        if (canvas == null || tool == null) return;
        root = Node("BB_ConstructionWorkspace", canvas.transform);
        Stretch(root);
        var catalogue = Panel("Catálogo de construcción", root, new Vector2(0,1), new Vector2(12,-82), new Vector2(252,760));
        Text(catalogue, "Diseña tu local", 25, 40);
        Text(catalogue, "HERRAMIENTAS", 12, 24);
        Mode(catalogue, "Seleccionar", BistroBuilderConstructionRuntimeMode.Select, "select");
        Mode(catalogue, "Pared continua", BistroBuilderConstructionRuntimeMode.Wall, "wall");
        Mode(catalogue, "Módulo de pared", BistroBuilderConstructionRuntimeMode.WallModule, "module");
        Mode(catalogue, "Habitación por arrastre", BistroBuilderConstructionRuntimeMode.Room, "room");
        var apertures = Row(catalogue);
        Mode(apertures, "Puerta", BistroBuilderConstructionRuntimeMode.Door, "door");
        Mode(apertures, "Ventana", BistroBuilderConstructionRuntimeMode.Window, "window");
        Text(catalogue, "TIPO DE ESPACIO", 12, 24);
        var zoneRow = Row(catalogue);
        Zone(zoneRow, "Salón", "zone.dining"); Zone(zoneRow, "Cocina", "zone.kitchen");
        var otherZones = Row(catalogue);
        Zone(otherZones, "Baño", "zone.bathroom"); Zone(otherZones, "Barra", "zone.bar"); Zone(otherZones, "Terraza", "zone.terrace");
        moduleControls = Column("Módulos", catalogue);
        moduleLabel = Text(moduleControls, "", 14, 26);
        var lengths = Row(moduleControls);
        foreach (float length in new[] {0.5f, 1f, 2f, 4f})
        { float captured = length; Button(lengths, length + " m", () => tool.ConfigureModule(captured, tool.ModuleAngle)); }
        Button(moduleControls, "Girar módulo 90°", () => tool.ConfigureModule(tool.ModuleLength, tool.ModuleAngle + 90));
        Mode(catalogue, "Abrir mobiliario", BistroBuilderConstructionRuntimeMode.Furniture, "furniture");
        summary = Text(catalogue, "", 14, 48);

        inspector = Panel("Inspector de construcción", root, new Vector2(1,1), new Vector2(-12,-82), new Vector2(274,420));
        Text(inspector, "Inspector", 25, 40);
        selection = Text(inspector, "", 16, 165);
        var editRow = Row(inspector);
        copy = Button(editRow, "Copiar", () => { tool.TryCopySelection(out var error); actionStatus = error; });
        remove = Button(editRow, "Eliminar", () => { tool.TryDeleteSelection(out var error); actionStatus = error; });
        openingControls = Column("Editar abertura", inspector);
        var movement = Row(openingControls);
        Button(movement, "← 0,25 m", () => AdjustOpening(-0.25f));
        Button(movement, "0,25 m →", () => AdjustOpening(0.25f));

        initialControls = Panel("Diseño inicial", root, new Vector2(1,0), new Vector2(-12,200), new Vector2(274,160));
        Text(initialControls, "Diseño inicial", 20, 28);
        saveInitial = Button(initialControls, "Guardar recuperación", SaveInitial);
        completeInitial = Button(initialControls, "Validar restaurante y continuar", CompleteInitial, 52);

        var bottom = Panel("Acciones de construcción", root, new Vector2(0.5f,0), new Vector2(0,82), new Vector2(990,104));
        status = Text(bottom, "", 15, 30);
        var actions = Row(bottom);
        dimensions = Text(actions, "", 13, 38);
        dimensions.GetComponent<LayoutElement>().preferredWidth = 360;
        undo = Button(actions, "Deshacer", () => { tool.TryUndo(out var e); actionStatus = e; });
        redo = Button(actions, "Rehacer", () => { tool.TryRedo(out var e); actionStatus = e; });
        Button(actions, "Descartar", () => { exitRequested = false; modal.gameObject.SetActive(true); });
        apply = Button(actions, "Aplicar cambios", Apply);
        Button(actions, "Salir", RequestExit);
        BuildModal();
    }

    private void BuildModal()
    {
        modal = Node("Cambios pendientes", root); Stretch(modal);
        modal.gameObject.AddComponent<Image>().color = BistroBuilderUiTokens.Overlay;
        var dialog = Panel("Confirmación", modal, new Vector2(0.5f,0.5f), Vector2.zero, new Vector2(460,290));
        Text(dialog, "Cambios pendientes", 25, 42);
        Text(dialog, "Aplica la construcción o descarta el borrador. También puedes seguir editando.", 16, 62);
        Button(dialog, "Aplicar cambios", () => { Apply(); if (!tool.HasDraftChanges) { modal.gameObject.SetActive(false); if (exitRequested) ExitNow(); } });
        Button(dialog, "Descartar cambios", () => { tool.TryCancelDraft(out var e); actionStatus = e; modal.gameObject.SetActive(false); if (exitRequested) ExitNow(); });
        Button(dialog, "Seguir editando", () => modal.gameObject.SetActive(false));
        modal.gameObject.SetActive(false);
    }
    private void Apply() { actionStatus = tool.TryCommitDraft(out var error) ? "Construcción aplicada." : error; }
    private void AdjustOpening(float delta) { tool.TryAdjustOpening(delta, false, out var error); actionStatus = error; }
    private void RequestExit() { if (!InterceptExit()) ExitNow(); }
    private void ExitNow()
    {
        tool.CancelCurrentGesture();
        bypassExit = true;
        try { if (furniture != null) furniture.TryExitEditMode(false); }
        finally { bypassExit = false; }
    }
    private void SaveInitial()
    {
        if (!tool.TryCommitDraft(out var error)) { actionStatus = error; return; }
        actionStatus = opening.TryRequestInitialSave(out error) ? "Guardando punto de recuperación…" : error;
    }
    private void CompleteInitial()
    {
        if (!tool.TryCommitDraft(out var error) || !opening.TryCompleteInitialDesign(out error))
        { actionStatus = error; return; }
        // Continue through the existing opening flow only after its validation succeeds.
        if (!opening.TryAcknowledgeBriefing(out error) || !opening.TryOpenFirstService(out error) ||
            !opening.TryTransitionToNormalPlay(out error)) { actionStatus = error; return; }
        tool.SetMode(BistroBuilderConstructionRuntimeMode.Furniture);
        FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
        actionStatus = string.Empty;
    }
    private void Mode(Transform parent, string label, BistroBuilderConstructionRuntimeMode mode, string icon)
    {
        var button = Button(parent, label, () => { actionStatus = null; tool.SetMode(mode); }); modes[mode] = button;
        var sprite = Resources.Load<Sprite>("BistroBuilder/Construction/Icons/" + icon);
        if (sprite == null) return;
        var image = Node("Icon", button.transform); image.anchorMin = image.anchorMax = new Vector2(0,0.5f);
        image.anchoredPosition = new Vector2(17,0); image.sizeDelta = new Vector2(22,22);
        image.gameObject.AddComponent<Image>().sprite = sprite;
        image.GetComponent<Image>().raycastTarget = false;
        var text = button.GetComponentInChildren<TMP_Text>(); text.margin = new Vector4(30,0,2,0);
    }
    private void Zone(Transform parent, string label, string id) => Button(parent, label, () => { actionStatus = null; tool.SetRoomZone(id); });

    private static RectTransform Node(string name, Transform parent)
    { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent,false); return go.GetComponent<RectTransform>(); }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = Node(name,parent); rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        rect.gameObject.AddComponent<Image>().color = BistroBuilderUiTokens.Surface1;
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(14,14,12,12); layout.spacing = 7;
        layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
        return rect;
    }
    private static RectTransform Column(string name, Transform parent)
    {
        var rect = Node(name,parent); var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=5;
        layout.childControlHeight = true; layout.childForceExpandHeight = false; return rect;
    }
    private static RectTransform Row(Transform parent)
    {
        var rect = Node("Row",parent); var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing=5;
        layout.childControlHeight=true; layout.childControlWidth=true; layout.childForceExpandWidth=true;
        layout.childForceExpandHeight=false;
        var size=rect.gameObject.AddComponent<LayoutElement>(); size.preferredHeight=36; size.flexibleHeight=0;
        return rect;
    }
    private static TMP_Text Text(Transform parent, string value, int size, float height)
    {
        var rect = Node("Label",parent); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text=value; text.fontSize=size; text.color=BistroBuilderUiTokens.TextPrimary; text.raycastTarget=false;
        text.textWrappingMode=TextWrappingModes.Normal; text.alignment=TextAlignmentOptions.MidlineLeft;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight=height; return text;
    }
    private static Button Button(Transform parent, string label, Action action, float height=36)
    {
        var rect=Node(label,parent); var image=rect.gameObject.AddComponent<Image>(); image.color=BistroBuilderUiTokens.Surface2;
        var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        button.colors=BistroBuilderUiTokens.ButtonColors(Color.white,new Color(1.18f,1.18f,1.18f),new Color(0.8f,0.8f,0.8f));
        button.onClick.AddListener(()=>action());
        var layout=rect.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight=height; layout.minWidth=40; layout.flexibleWidth=1;
        var text=Text(rect,label,14,height); Stretch(text.rectTransform); text.alignment=TextAlignmentOptions.Center;
        text.gameObject.AddComponent<BistroBuilderUiStyleTag>().Configure(BistroBuilderUiStyleRole.Body, false, true);
        return button;
    }
    private void OnDestroy() { if (Instance == this) Instance=null; if(root!=null) Destroy(root.gameObject); }
}
