using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Opening/New Game Opening Player Screen")]
public sealed class BistroBuilderNewGameOpeningPlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderNewGameOpeningService openingService;
    [SerializeField] private bool visibleOnStart = true;
    [SerializeField] private BistroBuilderConstructionAuthoringRuntimeTool constructionTool;

    private string restaurantName = "Mi restaurante";
    private BistroBuilderStartingPremisesProfile premises = BistroBuilderStartingPremisesProfile.Balanced;
    private string statusMessage = string.Empty;
    private Vector2 scroll;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle boxStyle;
    private EventSystem blockedEventSystem;
    private bool blockedEventSystemWasEnabled;
    private bool initialEditEntryAttempted;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (openingService == null) TryGetComponent(out openingService);
        if (constructionTool == null) constructionTool = FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        IsVisible = visibleOnStart;
    }

    public bool ValidateConfiguration(out string error)
    {
        if (openingService == null) TryGetComponent(out openingService);
        if (openingService == null)
        {
            error = "La UI de nueva partida no tiene servicio de apertura.";
            return false;
        }
        return openingService.ValidateConfiguration(out error);
    }

    public void Show()
    {
        IsVisible = true;
        initialEditEntryAttempted = false;
        ApplyModalInputState();
    }

    public void Hide()
    {
        IsVisible = false;
        RestoreModalInputState();
    }

    private void OnDisable() => RestoreModalInputState();

    private void OnGUI()
    {
        if (!Application.isPlaying || openingService == null) return;
        if (openingService.Phase == BistroBuilderNewGamePhase.InitialSetup)
        {
            IsVisible = false;
            RestoreModalInputState();
            EnsureStyles();
            if (!openingService.IsInitialEditModeActive && !initialEditEntryAttempted)
            {
                initialEditEntryAttempted = true;
                if (openingService.TryEnterInitialEditMode(out statusMessage)) statusMessage = string.Empty;
            }
            DrawInitialDesignOverlay();
            return;
        }
        if (!IsVisible) return;
        ApplyModalInputState();
        initialEditEntryAttempted = false;
        if (openingService.Phase == BistroBuilderNewGamePhase.NormalPlay)
        {
            Hide();
            return;
        }

        EnsureStyles();
        if (openingService.Phase == BistroBuilderNewGamePhase.StartMenu)
        {
            GUI.depth = -1000;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.blackTexture, ScaleMode.StretchToFill);
        }
        float width = Mathf.Min(640f, Screen.width - 40f);
        float height = Mathf.Min(720f, Screen.height - 40f);
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, GUIContent.none, boxStyle);
        GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, panel.height - 40f));
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("BISTRO BUILDER", titleStyle);
        GUILayout.Space(4f);
        GUILayout.Label(PhaseTitle(), titleStyle);
        GUILayout.Space(14f);

        switch (openingService.Phase)
        {
            case BistroBuilderNewGamePhase.StartMenu:
                DrawStartMenu();
                break;
            case BistroBuilderNewGamePhase.Briefing:
                DrawBriefing();
                break;
            case BistroBuilderNewGamePhase.ReadyToOpen:
                DrawReadyToOpen();
                break;
            case BistroBuilderNewGamePhase.FirstService:
                DrawFirstService();
                break;
        }

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            GUILayout.Space(14f);
            GUILayout.Label(statusMessage, textStyle);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawStartMenu()
    {
        GUILayout.Label("Empieza una nueva historia o continua tu ultima partida.", textStyle);
        GUILayout.Space(14f);
        GUILayout.Label("Nombre del restaurante", textStyle);
        restaurantName = GUILayout.TextField(restaurantName, 80, GUILayout.Height(32f));
        GUILayout.Space(8f);
        GUILayout.Label("Tipo de local inicial: " + PremisesLabel(premises), textStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Vacío", GUILayout.Height(34f))) premises = BistroBuilderStartingPremisesProfile.Empty;
        if (GUILayout.Button("Compacto", GUILayout.Height(34f))) premises = BistroBuilderStartingPremisesProfile.Compact;
        if (GUILayout.Button("Equilibrado", GUILayout.Height(34f))) premises = BistroBuilderStartingPremisesProfile.Balanced;
        if (GUILayout.Button("Amplio", GUILayout.Height(34f))) premises = BistroBuilderStartingPremisesProfile.Spacious;
        GUILayout.EndHorizontal();
        GUILayout.Label(PremisesDescription(premises), textStyle);
        GUILayout.Space(16f);
        if (GUILayout.Button("CREAR NUEVA PARTIDA", GUILayout.Height(46f)))
        {
            if (!openingService.TryCreateNewGame(restaurantName, premises, out statusMessage))
                return;
            statusMessage = string.Empty;
            Hide();
        }
        GUI.enabled = openingService.CanContinue;
        if (GUILayout.Button("CONTINUAR", GUILayout.Height(42f)))
        {
            if (openingService.TryContinue(out statusMessage)) statusMessage = "Cargando partida...";
        }
        GUI.enabled = true;
    }

    private void DrawInitialDesignOverlay()
    {
        if (constructionTool == null)
            constructionTool = FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();

        float width = Mathf.Min(420f, Screen.width - 24f);
        float height = 176f;
        Rect panel = new Rect(Screen.width - width - 12f, 88f, width, height);
        BistroBuilderRuntimePointerUiGuard.PublishBlockedGuiRect(panel);
        GUI.Box(panel, GUIContent.none, boxStyle);
        GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, panel.height - 20f));
        GUILayout.Label("DISENO INICIAL", titleStyle);
        GUILayout.Label(openingService.RestaurantName + " - " + PremisesLabel(openingService.PremisesProfile), textStyle);

        if (!openingService.IsInitialEditModeActive)
        {
            if (GUILayout.Button("ACTIVAR MODO EDICION", GUILayout.Height(32f)))
            {
                initialEditEntryAttempted = false;
                if (openingService.TryEnterInitialEditMode(out statusMessage)) statusMessage = string.Empty;
            }
        }
        else
        {
            string liveStatus = ResolveLiveStatus();
            if (!string.IsNullOrWhiteSpace(liveStatus)) GUILayout.Label(liveStatus, textStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = !openingService.IsSaveBusy;
            if (GUILayout.Button("GUARDAR", GUILayout.Height(34f)))
            {
                if (!TryCommitArchitectureBeforeTransition(out string e)) statusMessage = "BLOQUEO - " + e;
                else statusMessage = openingService.TryRequestInitialSave(out e) ? "Guardado iniciado..." : "ERROR - " + e;
            }
            GUI.enabled = true;
            if (GUILayout.Button("VALIDAR Y CONTINUAR", GUILayout.Height(34f)))
            {
                if (!TryCommitArchitectureBeforeTransition(out string e)) statusMessage = "BLOQUEO - " + e;
                else if (TryValidateAndEnterGame(out e))
                {
                    constructionTool?.SetMode(BistroBuilderConstructionRuntimeMode.Furniture);
                    statusMessage = string.Empty;
                    Hide();
                }
                else statusMessage = "BLOQUEO - " + e;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndArea();
    }
    private bool TryCommitArchitectureBeforeTransition(out string error)
    {
        error = string.Empty;
        if (constructionTool == null || !constructionTool.HasDraftSession) return true;
        return constructionTool.TryCommitDraft(out error);
    }

    private bool TryValidateAndEnterGame(out string error)
    {
        if (!openingService.TryCompleteInitialDesign(out error)) return false;
        if (!openingService.TryAcknowledgeBriefing(out error)) return false;
        if (!openingService.TryOpenFirstService(out error)) return false;
        return openingService.TryTransitionToNormalPlay(out error);
    }

    private string BuildToolHelp()
    {
        if (constructionTool == null)
            return "Mobiliario: usa el catalogo del modo edicion.";
        switch (constructionTool.Mode)
        {
            case BistroBuilderConstructionRuntimeMode.Wall:
                return "PARED activa: clic inicio, clic final; Escape cancela el punto.";
            case BistroBuilderConstructionRuntimeMode.Room:
                return "ESPACIO activo: marca dos esquinas opuestas para crear la habitacion.";
            default:
                return "MOBILIARIO activo: usa el catalogo para colocar, mover o retirar objetos.";
        }
    }
    private string ResolveLiveStatus()
    {
        if (openingService.IsSaveBusy)
            return "Guardando " + Mathf.RoundToInt(openingService.SaveProgress * 100f) + "% · " + openingService.SaveStatusMessage;
        if (constructionTool != null && constructionTool.Mode != BistroBuilderConstructionRuntimeMode.Furniture)
            return constructionTool.StatusMessage;
        if (!string.IsNullOrWhiteSpace(statusMessage)) return statusMessage;
        BistroBuilderSaveOperationResult result = openingService.LastSaveResult;
        if (result != null && result.OperationKind == BistroBuilderSaveOperationKind.Save)
            return result.Succeeded ? "Guardado completado." : "Último guardado falló: " + result.Message;
        return "Modo edición activo.";
    }

    private void DrawBriefing()
    {
        GUILayout.Label(openingService.Briefing, textStyle);
        GUILayout.Space(12f);
        DrawChecks();
        GUILayout.Space(14f);
        if (GUILayout.Button("ENTENDIDO - PREPARAR APERTURA", GUILayout.Height(44f)))
        {
            if (openingService.TryAcknowledgeBriefing(out statusMessage))
                statusMessage = "Validacion superada. Ya puedes abrir.";
        }
    }

    private void DrawReadyToOpen()
    {
        GUILayout.Label("El restaurante esta preparado. La primera apertura iniciara el servicio real.", textStyle);
        GUILayout.Space(12f);
        DrawChecks();
        GUILayout.Space(14f);
        if (GUILayout.Button("ABRIR RESTAURANTE", GUILayout.Height(52f)))
        {
            if (openingService.TryOpenFirstService(out statusMessage))
                statusMessage = "Primer servicio abierto correctamente.";
        }
    }

    private void DrawFirstService()
    {
        GUILayout.Label("Tu primer servicio ya esta en marcha. A partir de aqui funcionan los sistemas normales del restaurante.", textStyle);
        GUILayout.Space(16f);
        if (GUILayout.Button("ENTRAR AL JUEGO", GUILayout.Height(52f)))
        {
            if (openingService.TryTransitionToNormalPlay(out statusMessage)) Hide();
        }
    }

    private void DrawChecks()
    {
        // El preflight puede ser costoso y OnGUI se invoca varias veces por frame.
        // Solo mostramos el Ãºltimo resultado calculado por las transiciones del servicio.
        BistroBuilderOpeningPreflightReport report = openingService.LastPreflight;
        if (report == null || report.checks == null || report.checks.Count == 0)
        {
            GUILayout.Label("Validacion previa pendiente. Se comprobara al continuar.", textStyle);
            return;
        }
        GUILayout.Label("Validacion previa: " + report.passedCount + " OK / " +
                        report.warningCount + " avisos / " + report.blockerCount + " bloqueos", textStyle);
        if (report.checks == null) return;
        for (int i = 0; i < report.checks.Count; i++)
        {
            BistroBuilderOpeningCheck check = report.checks[i];
            if (check == null) continue;
            string prefix = check.level == BistroBuilderOpeningCheckLevel.Passed ? "[OK] " :
                check.level == BistroBuilderOpeningCheckLevel.Warning ? "[AVISO] " : "[BLOQUEO] ";
            GUILayout.Label(prefix + check.label + " - " + check.message, textStyle);
        }
    }

    private static string PremisesLabel(BistroBuilderStartingPremisesProfile value)
    {
        switch (value)
        {
            case BistroBuilderStartingPremisesProfile.Empty: return "Vacío";
            case BistroBuilderStartingPremisesProfile.Compact: return "Compacto";
            case BistroBuilderStartingPremisesProfile.Spacious: return "Amplio";
            default: return "Equilibrado";
        }
    }

    private static string PremisesDescription(BistroBuilderStartingPremisesProfile value)
    {
        if (value == BistroBuilderStartingPremisesProfile.Empty)
            return "Local vacío: conserva la envolvente y la entrada; tú distribuyes salón, cocina y baño y colocas el equipamiento.";
        return "Local preparado como base editable. Puedes modificar paredes, espacios, mobiliario y equipamiento antes de validar.";
    }

    private string PhaseTitle()
    {
        switch (openingService.Phase)
        {
            case BistroBuilderNewGamePhase.StartMenu: return "Nueva partida";
            case BistroBuilderNewGamePhase.InitialSetup: return "Diseño inicial";
            case BistroBuilderNewGamePhase.Briefing: return "Briefing inicial";
            case BistroBuilderNewGamePhase.ReadyToOpen: return "Listo para abrir";
            case BistroBuilderNewGamePhase.FirstService: return "Primera apertura";
            default: return "Preparacion inicial";
        }
    }

    private void ApplyModalInputState()
    {
        if (!IsVisible || blockedEventSystem != null) return;
        blockedEventSystem = EventSystem.current;
        if (blockedEventSystem == null) return;
        blockedEventSystemWasEnabled = blockedEventSystem.enabled;
        if (blockedEventSystemWasEnabled) blockedEventSystem.enabled = false;
    }

    private void RestoreModalInputState()
    {
        if (blockedEventSystem != null && blockedEventSystemWasEnabled)
            blockedEventSystem.enabled = true;
        blockedEventSystem = null;
        blockedEventSystemWasEnabled = false;
    }
    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            wordWrap = true
        };
        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(18, 18, 18, 18)
        };
    }
}
