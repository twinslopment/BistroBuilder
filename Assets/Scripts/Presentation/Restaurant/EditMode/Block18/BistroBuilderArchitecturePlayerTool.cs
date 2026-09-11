using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum BistroBuilderArchitecturePlayerToolMode
{
    None = 0,
    Wall = 1,
    RoomRectangle = 2
}

public enum BistroBuilderArchitectureRoomPurpose
{
    Dining = 0,
    Kitchen = 1,
    Bathroom = 2
}

/// <summary>
/// Herramienta runtime de construcción arquitectónica para el jugador.
/// Convierte clics sobre el plano del local en comandos del documento Block 18.
/// La geometría provisional se materializa sin publicar BBSIS/Navigation hasta commit.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Block 18/Architecture Player Tool")]
public sealed class BistroBuilderArchitecturePlayerTool : MonoBehaviour
{
    [SerializeField] private BistroBuilderEditRuntimeCoordinator coordinator;
    [SerializeField] private BistroBuilderEditDocumentRuntimeService documentService;
    [SerializeField] private BistroBuilderArchitectureRuntimeMaterializer materializer;
    [SerializeField] private RestaurantEditModeService editModeService;
    [SerializeField] private RestaurantEditInteractionController placeableController;
    [SerializeField] private Camera interactionCamera;
    [Header("Construcción")]
    [SerializeField, Min(0.05f)] private float gridSize = 0.25f;
    [SerializeField, Min(0.05f)] private float defaultWallThickness = 0.12f;
    [SerializeField, Min(1f)] private float defaultWallHeight = 2.8f;
    [SerializeField, Min(0.25f)] private float minimumRoomSide = 1f;

    private BistroBuilderArchitecturePlayerToolMode mode;
    private BistroBuilderArchitectureRoomPurpose roomPurpose = BistroBuilderArchitectureRoomPurpose.Dining;
    private bool hasFirstPoint;
    private Vector3 firstPoint;
    private string statusMessage = "Selecciona Pared o Habitación para construir.";
    private bool placeableControllerWasEnabled;
    private bool placeableInputSuspended;

    public event Action Changed;
    public BistroBuilderArchitecturePlayerToolMode Mode => mode;
    public BistroBuilderArchitectureRoomPurpose RoomPurpose => roomPurpose;
    public bool HasFirstPoint => hasFirstPoint;
    public string StatusMessage => statusMessage;
    public bool HasDraftSession => coordinator != null && coordinator.HasSession;
    public bool HasDraftChanges => coordinator != null && coordinator.IsDirty;
    public bool CanUndo => coordinator != null && coordinator.CanUndo;
    public bool CanRedo => coordinator != null && coordinator.CanRedo;
    public int DraftWallCount => coordinator != null && coordinator.Session != null
        ? coordinator.Session.Draft.walls.Count : 0;
    public int DraftRoomCount => coordinator != null && coordinator.Session != null
        ? coordinator.Session.RoomProjections.Count : 0;

    private void Awake() => CacheDependencies();
    private void OnDisable()
    {
        RestorePlaceableInput();
        hasFirstPoint = false;
        mode = BistroBuilderArchitecturePlayerToolMode.None;
    }

    private void Update()
    {
        if (mode == BistroBuilderArchitecturePlayerToolMode.None)
            return;
        if (editModeService == null || !editModeService.IsEditModeActive)
        {
            RestorePlaceableInput();
            hasFirstPoint = false;
            mode = BistroBuilderArchitecturePlayerToolMode.None;
            Changed?.Invoke();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (hasFirstPoint)
            {
                hasFirstPoint = false;
                SetStatus("Punto inicial cancelado. La herramienta sigue activa.");
            }
            else
            {
                SetMode(BistroBuilderArchitecturePlayerToolMode.None);
            }
            return;
        }

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || PointerHitsUi())
            return;
        if (!TryGetPlanPoint(out Vector3 point))
        {
            SetStatus("No se pudo proyectar el clic sobre el plano del local.");
            return;
        }
        HandlePlanClick(point);
    }
    public void SetMode(BistroBuilderArchitecturePlayerToolMode next)
    {
        CacheDependencies();
        if (next != BistroBuilderArchitecturePlayerToolMode.None &&
            (editModeService == null || !editModeService.IsEditModeActive))
        {
            SetStatus("Activa el modo edición antes de construir.");
            return;
        }

        hasFirstPoint = false;
        mode = next;
        if (mode == BistroBuilderArchitecturePlayerToolMode.None)
        {
            RestorePlaceableInput();
            SetStatus("Herramienta de construcción cerrada.");
        }
        else
        {
            SuspendPlaceableInput();
            SetStatus(mode == BistroBuilderArchitecturePlayerToolMode.Wall
                ? "Pared: haz clic en el punto inicial y después en el final. Escape cancela."
                : RoomPurposeLabel(roomPurpose) + ": haz clic en dos esquinas opuestas. Escape cancela.");
        }
        Changed?.Invoke();
    }

    public void SetRoomMode(BistroBuilderArchitectureRoomPurpose purpose)
    {
        roomPurpose = purpose;
        SetMode(BistroBuilderArchitecturePlayerToolMode.RoomRectangle);
        if (mode == BistroBuilderArchitecturePlayerToolMode.RoomRectangle)
            SetStatus(RoomPurposeLabel(roomPurpose) + ": haz clic en dos esquinas opuestas. Escape cancela.");
    }
    public bool TryUndo(out string error)
    {
        error = string.Empty;
        if (coordinator == null || !coordinator.HasSession || !coordinator.TryUndo(out error)) return false;
        RebuildDraftPreview();
        SetStatus("Último cambio de construcción deshecho.");
        return true;
    }
    public bool TryRedo(out string error)
    {
        error = string.Empty;
        if (coordinator == null || !coordinator.HasSession || !coordinator.TryRedo(out error)) return false;
        RebuildDraftPreview();
        SetStatus("Último cambio de construcción rehecho.");
        return true;
    }

    public bool TryCommitDraft(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (coordinator == null || !coordinator.HasSession)
        {
            SetStatus("No hay cambios arquitectónicos pendientes.");
            return true;
        }
        if (!coordinator.IsDirty)
        {
            coordinator.CancelSession();
            SetStatus("No había cambios arquitectónicos pendientes.");
            return true;
        }
        if (!coordinator.TryReview(out var diagnostics))
        {
            error = BuildDiagnosticSummary(diagnostics);
            SetStatus(error);
            return false;
        }
        if (!coordinator.TryCommit(out _, out diagnostics, out error))
        {
            if (string.IsNullOrWhiteSpace(error)) error = BuildDiagnosticSummary(diagnostics);
            SetStatus(error);
            return false;
        }
        hasFirstPoint = false;
        SetStatus("Construcción aplicada al restaurante.");
        return true;
    }
    public bool TryCancelDraft(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (coordinator == null || !coordinator.HasSession)
        {
            SetStatus("No hay construcción pendiente que descartar.");
            return true;
        }
        if (!coordinator.CancelSession())
        {
            error = "No se pudo descartar la construcción pendiente.";
            SetStatus(error);
            return false;
        }
        hasFirstPoint = false;
        if (materializer != null && documentService != null)
            materializer.Rebuild(documentService.GetCommittedSnapshot());
        SetStatus("Construcción pendiente descartada.");
        return true;
    }

    private void HandlePlanClick(Vector3 point)
    {
        if (!hasFirstPoint)
        {
            firstPoint = point;
            hasFirstPoint = true;
            SetStatus(mode == BistroBuilderArchitecturePlayerToolMode.Wall
                ? "Punto inicial fijado. Haz clic donde termina la pared."
                : "Primera esquina de " + RoomPurposeLabel(roomPurpose).ToLowerInvariant() + " fijada. Haz clic en la esquina opuesta.");
            return;
        }
        if (mode == BistroBuilderArchitecturePlayerToolMode.Wall)
            CreateWall(firstPoint, point, true);
        else if (mode == BistroBuilderArchitecturePlayerToolMode.RoomRectangle)
            CreateRectangle(firstPoint, point);
    }
    private void CreateWall(Vector3 start, Vector3 end, bool continueChain)
    {
        Vector2 a = new Vector2(start.x, start.z);
        Vector2 b = new Vector2(end.x, end.z);
        if (Vector2.Distance(a, b) < Mathf.Max(0.05f, gridSize))
        {
            SetStatus("La pared es demasiado corta.");
            return;
        }
        if (!EnsureDraftSession(out string error))
        {
            SetStatus(error);
            return;
        }
        var wall = new BistroBuilderWallRecord
        {
            wallId = BistroBuilderEditId.NewId(),
            buildPlaneId = "default",
            axisStart = a,
            axisEnd = b,
            baseElevation = 0f,
            height = defaultWallHeight,
            thickness = defaultWallThickness,
            wallDefinitionId = "wall.default"
        };
        if (!coordinator.TryExecute(new BistroBuilderCreateWallCommand(wall), out _, out error))
        {
            SetStatus(error);
            return;
        }
        RebuildDraftPreview();
        if (continueChain)
        {
            firstPoint = end;
            hasFirstPoint = true;
            SetStatus("Pared creada. Continúa desde el último punto o pulsa Escape.");
        }
        else
        {
            hasFirstPoint = false;
            SetStatus("Pared creada.");
        }
    }

    private void CreateRectangle(Vector3 first, Vector3 opposite)
    {
        float minX = Mathf.Min(first.x, opposite.x);
        float maxX = Mathf.Max(first.x, opposite.x);
        float minZ = Mathf.Min(first.z, opposite.z);
        float maxZ = Mathf.Max(first.z, opposite.z);
        if (maxX - minX < minimumRoomSide || maxZ - minZ < minimumRoomSide)
        {
            SetStatus("La habitación necesita al menos " + minimumRoomSide.ToString("0.##") + " m por lado.");
            return;
        }
        if (!EnsureDraftSession(out string error))
        {
            SetStatus(error);
            return;
        }

        Vector3[] points =
        {
            new Vector3(minX, 0f, minZ),
            new Vector3(maxX, 0f, minZ),
            new Vector3(maxX, 0f, maxZ),
            new Vector3(minX, 0f, maxZ)
        };
        int created = 0;
        for (int i = 0; i < 4; i++)
        {
            Vector3 start = points[i];
            Vector3 end = points[(i + 1) % 4];
            var wall = new BistroBuilderWallRecord
            {
                wallId = BistroBuilderEditId.NewId(),
                buildPlaneId = "default",
                axisStart = new Vector2(start.x, start.z),
                axisEnd = new Vector2(end.x, end.z),
                baseElevation = 0f,
                height = defaultWallHeight,
                thickness = defaultWallThickness,
                wallDefinitionId = "wall.default"
            };
            if (!coordinator.TryExecute(new BistroBuilderCreateWallCommand(wall), out _, out error))
            {
                for (int undo = 0; undo < created; undo++) coordinator.TryUndo(out _);
                SetStatus("No se pudo crear la habitación: " + error);
                RebuildDraftPreview();
                return;
            }
            created++;
        }
        var zone = new BistroBuilderFunctionalZoneRecord
        {
            zoneId = BistroBuilderEditId.NewId(),
            zoneDefinitionId = RoomPurposeZoneId(roomPurpose)
        };
        zone.explicitRegion.Add(new Vector2(minX, minZ));
        zone.explicitRegion.Add(new Vector2(maxX, minZ));
        zone.explicitRegion.Add(new Vector2(maxX, maxZ));
        zone.explicitRegion.Add(new Vector2(minX, maxZ));
        if (!coordinator.TryExecute(new BistroBuilderSetFunctionalZoneCommand(zone), out _, out error))
        {
            for (int undo = 0; undo < created; undo++) coordinator.TryUndo(out _);
            SetStatus("No se pudo clasificar la habitaciÃ³n: " + error);
            RebuildDraftPreview();
            return;
        }
        hasFirstPoint = false;
        RebuildDraftPreview();
        SetStatus(RoomPurposeLabel(roomPurpose) + " creada. Puedes seguir construyendo o aplicar los cambios.");
    }
    private static string RoomPurposeZoneId(BistroBuilderArchitectureRoomPurpose purpose)
    {
        switch (purpose)
        {
            case BistroBuilderArchitectureRoomPurpose.Kitchen: return "kitchen";
            case BistroBuilderArchitectureRoomPurpose.Bathroom: return "bathroom";
            default: return "dining";
        }
    }

    private static string RoomPurposeLabel(BistroBuilderArchitectureRoomPurpose purpose)
    {
        switch (purpose)
        {
            case BistroBuilderArchitectureRoomPurpose.Kitchen: return "Cocina";
            case BistroBuilderArchitectureRoomPurpose.Bathroom: return "BaÃ±o";
            default: return "SalÃ³n";
        }
    }
    private bool EnsureDraftSession(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (coordinator == null)
        {
            error = "No está disponible el coordinador de construcción.";
            return false;
        }
        if (coordinator.HasSession) return true;
        return coordinator.TryBeginSession(out error);
    }

    private void RebuildDraftPreview()
    {
        if (materializer == null || coordinator == null || coordinator.Session == null) return;
        materializer.Rebuild(coordinator.Session.Draft);
        Changed?.Invoke();
    }

    private bool TryGetPlanPoint(out Vector3 point)
    {
        point = default;
        CacheDependencies();
        if (interactionCamera == null || Mouse.current == null) return false;
        Vector2 pointer = Mouse.current.position.ReadValue();
        Ray ray = interactionCamera.ScreenPointToRay(pointer);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float distance)) return false;
        point = ray.GetPoint(distance);
        point.x = Snap(point.x);
        point.y = 0f;
        point.z = Snap(point.z);
        return true;
    }
    private bool PointerHitsUi()
    {
        if (Mouse.current == null) return true;
        Vector2 pointer = Mouse.current.position.ReadValue();
        if (BistroBuilderRuntimePointerUiGuard.IsPointerBlocked(pointer)) return true;
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private float Snap(float value)
    {
        float size = Mathf.Max(0.01f, gridSize);
        return Mathf.Round(value / size) * size;
    }

    private void SuspendPlaceableInput()
    {
        if (placeableInputSuspended) return;
        CacheDependencies();
        if (placeableController == null) return;
        placeableControllerWasEnabled = placeableController.enabled;
        placeableInputSuspended = true;
        if (placeableControllerWasEnabled) placeableController.enabled = false;
    }

    private void RestorePlaceableInput()
    {
        if (!placeableInputSuspended) return;
        CacheDependencies();
        if (placeableController != null && placeableControllerWasEnabled)
            placeableController.enabled = true;
        placeableControllerWasEnabled = false;
        placeableInputSuspended = false;
    }

    private void SetStatus(string message)
    {
        statusMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        Changed?.Invoke();
    }
    private static string BuildDiagnosticSummary(System.Collections.Generic.IReadOnlyList<BistroBuilderEditDiagnostic> diagnostics)
    {
        if (diagnostics == null || diagnostics.Count == 0)
            return "La construcción no pudo validarse.";
        for (int i = 0; i < diagnostics.Count; i++)
        {
            BistroBuilderEditDiagnostic diagnostic = diagnostics[i];
            if (diagnostic != null && diagnostic.severity == BistroBuilderEditDiagnosticSeverity.Blocking)
                return string.IsNullOrWhiteSpace(diagnostic.message)
                    ? "La construcción contiene un bloqueo." : diagnostic.message;
        }
        return "La construcción necesita revisión antes de aplicarse.";
    }

    private void CacheDependencies()
    {
        if (coordinator == null) coordinator = FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
        if (documentService == null) documentService = FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>();
        if (materializer == null) materializer = FindFirstObjectByType<BistroBuilderArchitectureRuntimeMaterializer>();
        if (editModeService == null) editModeService = FindFirstObjectByType<RestaurantEditModeService>();
        if (placeableController == null) placeableController = FindFirstObjectByType<RestaurantEditInteractionController>();
        if (interactionCamera == null) interactionCamera = Camera.main != null
            ? Camera.main : FindFirstObjectByType<Camera>();
    }
}
