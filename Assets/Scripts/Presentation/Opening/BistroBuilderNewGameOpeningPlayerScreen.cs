using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Opening/New Game Opening Player Screen")]
public sealed class BistroBuilderNewGameOpeningPlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderNewGameOpeningService openingService;
    [SerializeField] private bool visibleOnStart = true;

    private string restaurantName = "Mi restaurante";
    private BistroBuilderStartingPremisesProfile premises = BistroBuilderStartingPremisesProfile.Balanced;
    private string statusMessage = string.Empty;
    private Vector2 scroll;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle boxStyle;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (openingService == null) TryGetComponent(out openingService);
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

    public void Show() => IsVisible = true;
    public void Hide() => IsVisible = false;

    private void OnGUI()
    {
        if (!Application.isPlaying || !IsVisible || openingService == null) return;
        if (openingService.Phase == BistroBuilderNewGamePhase.NormalPlay)
        {
            IsVisible = false;
            return;
        }

        EnsureStyles();
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
            case BistroBuilderNewGamePhase.InitialSetup:
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
        GUILayout.Label("Tipo de local inicial: " + premises, textStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Compacto", GUILayout.Height(34f))) premises = BistroBuilderStartingPremisesProfile.Compact;
        if (GUILayout.Button("Equilibrado", GUILayout.Height(34f))) premises = BistroBuilderStartingPremisesProfile.Balanced;
        if (GUILayout.Button("Amplio", GUILayout.Height(34f))) premises = BistroBuilderStartingPremisesProfile.Spacious;
        GUILayout.EndHorizontal();
        GUILayout.Space(16f);
        if (GUILayout.Button("CREAR NUEVA PARTIDA", GUILayout.Height(46f)))
        {
            if (!openingService.TryCreateNewGame(restaurantName, premises, out statusMessage))
                return;
            statusMessage = "Nueva partida preparada y guardado inicial solicitado.";
        }
        GUI.enabled = openingService.CanContinue;
        if (GUILayout.Button("CONTINUAR", GUILayout.Height(42f)))
        {
            if (openingService.TryContinue(out statusMessage)) statusMessage = "Cargando partida...";
        }
        GUI.enabled = true;
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
        if (!openingService.TryRunOpeningPreflight(out BistroBuilderOpeningPreflightReport report, out string error))
        {
            GUILayout.Label(error, textStyle);
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

    private string PhaseTitle()
    {
        switch (openingService.Phase)
        {
            case BistroBuilderNewGamePhase.StartMenu: return "Nueva partida";
            case BistroBuilderNewGamePhase.Briefing: return "Briefing inicial";
            case BistroBuilderNewGamePhase.ReadyToOpen: return "Listo para abrir";
            case BistroBuilderNewGamePhase.FirstService: return "Primera apertura";
            default: return "Preparacion inicial";
        }
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
