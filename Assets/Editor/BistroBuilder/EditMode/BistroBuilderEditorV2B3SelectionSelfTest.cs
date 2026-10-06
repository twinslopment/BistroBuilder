using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BistroBuilder.ConstructionAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BistroBuilderEditorV2FakeSelectionSource :
    MonoBehaviour,
    IBistroBuilderEditorV2SelectionSource
{
    public BistroBuilderEditorV2ToolFamily family;
    public BistroBuilderEditorV2Selection current = BistroBuilderEditorV2Selection.None;
    public int readCount;
    public int clearCount;

    public BistroBuilderEditorV2ToolFamily Family => family;

    public bool TryReadSelection(out BistroBuilderEditorV2Selection selection)
    {
        readCount++;
        selection = current;
        return selection.IsValid;
    }

    public bool ClearSelection()
    {
        clearCount++;
        bool hadSelection = current.IsValid;
        current = BistroBuilderEditorV2Selection.None;
        return hadSelection;
    }
}

public static class BistroBuilderEditorV2B3SelectionSelfTest
{
    private const string ReportName = "EditorV2_B3_Selection_Report.txt";
    private static readonly List<string> Lines = new List<string>();
    private static int pass;
    private static int fail;

    [MenuItem("Tools/Bistro Builder/Edit Mode/Editor V2/B3 - Selection Self Test", false, 18102)]
    public static void RunFromMenu() => Run(false);

    public static void RunFromCommandLine()
    {
        try
        {
            Run(true);
            EditorApplication.Exit(fail == 0 ? 0 : 1);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    private static void Run(bool cli)
    {
        pass = 0;
        fail = 0;
        Lines.Clear();

        TestSelectionCore();
        TestRealSceneSelection();

        string report =
            "EDITOR V2 - B3 COMMON SELECTION SELF TEST\n" +
            string.Join("\n", Lines) +
            $"\nResultado: {pass} OK / {fail} fallos.";

        string root = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(Path.Combine(root, ReportName), report);
        Debug.Log(report);

        if (fail > 0)
            throw new InvalidOperationException(report);
    }

    private static void TestSelectionCore()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var serviceGo = new GameObject("B3_Core_Service");
        var adaptersGo = new GameObject("B3_Core_Adapters");
        var sourcesGo = new GameObject("B3_Core_SelectionSources");
        try
        {
            serviceGo.AddComponent<RestaurantPlacementValidationService>();
            serviceGo.AddComponent<RestaurantPlacementTransactionService>();
            var editMode = serviceGo.AddComponent<RestaurantEditModeService>();
            var coordinator = serviceGo.AddComponent<BistroBuilderEditorV2Coordinator>();
            var selectionCoordinator =
                serviceGo.AddComponent<BistroBuilderEditorV2SelectionCoordinator>();

            var furnitureAdapter = adaptersGo.AddComponent<BistroBuilderEditorV2FakeAdapter>();
            furnitureAdapter.family = BistroBuilderEditorV2ToolFamily.Furniture;
            var constructionAdapter = adaptersGo.AddComponent<BistroBuilderEditorV2FakeAdapter>();
            constructionAdapter.family = BistroBuilderEditorV2ToolFamily.Construction;
            var surfacesAdapter = adaptersGo.AddComponent<BistroBuilderEditorV2FakeAdapter>();
            surfacesAdapter.family = BistroBuilderEditorV2ToolFamily.Surfaces;

            var furniture = sourcesGo.AddComponent<BistroBuilderEditorV2FakeSelectionSource>();
            furniture.family = BistroBuilderEditorV2ToolFamily.Furniture;
            var construction = sourcesGo.AddComponent<BistroBuilderEditorV2FakeSelectionSource>();
            construction.family = BistroBuilderEditorV2ToolFamily.Construction;
            var surfaces = sourcesGo.AddComponent<BistroBuilderEditorV2FakeSelectionSource>();
            surfaces.family = BistroBuilderEditorV2ToolFamily.Surfaces;

            coordinator.Configure(editMode, furnitureAdapter, constructionAdapter, surfacesAdapter);
            selectionCoordinator.Configure(
                editMode,
                coordinator,
                furniture,
                construction,
                surfaces);

            Check(selectionCoordinator.RegisteredSourceCount == 3,
                "B3 registra exactamente una fuente de selección por familia");
            Check(editMode.TryEnterEditMode(out _, out _),
                "B3 usa la autoridad existente para entrar en modo edición");

            var furnitureCaps =
                BistroBuilderEditorV2SelectionCapability.Inspect |
                BistroBuilderEditorV2SelectionCapability.Move |
                BistroBuilderEditorV2SelectionCapability.Rotate |
                BistroBuilderEditorV2SelectionCapability.Delete |
                BistroBuilderEditorV2SelectionCapability.Duplicate;
            furniture.current = MakeSelection(
                BistroBuilderEditorV2ToolFamily.Furniture,
                BistroBuilderEditorV2SelectionKind.Furniture,
                "chair-001",
                "Silla",
                furnitureCaps);

            bool furnitureActivated = coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Furniture,
                "furniture",
                out _);
            selectionCoordinator.RefreshSelection();
            Check(furnitureActivated &&
                  selectionCoordinator.Current.kind == BistroBuilderEditorV2SelectionKind.Furniture,
                "B3 proyecta mobiliario sin crear una segunda selección");

            int clearsBeforeRead = furniture.clearCount;
            long revisionAfterSelection = selectionCoordinator.Revision;
            bool readChanged = selectionCoordinator.RefreshSelection();
            Check(!readChanged &&
                  furniture.clearCount == clearsBeforeRead &&
                  selectionCoordinator.Revision == revisionAfterSelection,
                "B3 leer la selección es idempotente y no modifica la autoridad");

            Check(selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Move) &&
                  selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Rotate) &&
                  selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Delete) &&
                  selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Duplicate) &&
                  !selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.ApplySurface),
                "B3 expone únicamente las capacidades válidas de mobiliario");

            Check(coordinator.TryActivateTool(
                    BistroBuilderEditorV2ToolFamily.Construction,
                    "select",
                    out _) &&
                  furniture.clearCount == clearsBeforeRead + 1 &&
                  !selectionCoordinator.HasSelection,
                "B3 limpia la selección de mobiliario al abandonar su autoridad");

            var wallCaps =
                BistroBuilderEditorV2SelectionCapability.Inspect |
                BistroBuilderEditorV2SelectionCapability.Move |
                BistroBuilderEditorV2SelectionCapability.Rotate |
                BistroBuilderEditorV2SelectionCapability.Delete |
                BistroBuilderEditorV2SelectionCapability.Duplicate;
            construction.current = MakeSelection(
                BistroBuilderEditorV2ToolFamily.Construction,
                BistroBuilderEditorV2SelectionKind.Wall,
                "wall-001",
                "Pared",
                wallCaps);
            selectionCoordinator.RefreshSelection();

            Check(selectionCoordinator.Current.kind == BistroBuilderEditorV2SelectionKind.Wall &&
                  selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Move) &&
                  !selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.ApplySurface),
                "B3 representa pared con capacidades arquitectónicas y no de superficie");

            surfaces.current = MakeSelection(
                BistroBuilderEditorV2ToolFamily.Surfaces,
                BistroBuilderEditorV2SelectionKind.Surface,
                "room-001",
                "Superficie de habitación",
                BistroBuilderEditorV2SelectionCapability.Inspect |
                BistroBuilderEditorV2SelectionCapability.ApplySurface);
            int constructionClears = construction.clearCount;
            int surfaceClears = surfaces.clearCount;

            Check(coordinator.TryActivateTool(
                    BistroBuilderEditorV2ToolFamily.Surfaces,
                    "floor",
                    out _) &&
                  construction.clearCount == constructionClears &&
                  surfaces.clearCount == surfaceClears &&
                  selectionCoordinator.Current.kind == BistroBuilderEditorV2SelectionKind.Surface &&
                  selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.ApplySurface),
                "B3 conserva una selección arquitectónica compatible al pasar a Superficies");

            coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Construction,
                "select",
                out _);
            construction.current = MakeSelection(
                BistroBuilderEditorV2ToolFamily.Construction,
                BistroBuilderEditorV2SelectionKind.Wall,
                "wall-002",
                "Pared",
                wallCaps);
            selectionCoordinator.RefreshSelection();
            surfaces.current = BistroBuilderEditorV2Selection.None;
            int invalidSurfaceClears = surfaces.clearCount;

            Check(coordinator.TryActivateTool(
                    BistroBuilderEditorV2ToolFamily.Surfaces,
                    "floor",
                    out _) &&
                  surfaces.clearCount == invalidSurfaceClears + 1 &&
                  !selectionCoordinator.HasSelection,
                "B3 descarta una selección incompatible al cambiar de familia");

            var duplicate = sourcesGo.AddComponent<BistroBuilderEditorV2FakeSelectionSource>();
            duplicate.family = BistroBuilderEditorV2ToolFamily.Furniture;
            selectionCoordinator.Configure(
                editMode,
                coordinator,
                furniture,
                construction,
                surfaces,
                duplicate);
            Check(!selectionCoordinator.RebuildSourceRegistry(out string duplicateError) &&
                  !string.IsNullOrWhiteSpace(duplicateError),
                "B3 rechaza dos fuentes comunes para la misma familia");

            selectionCoordinator.Configure(
                editMode,
                coordinator,
                furniture,
                construction,
                surfaces);
            Check(editMode.TryExitEditMode(true, out _) &&
                  !selectionCoordinator.HasSelection,
                "B3 limpia la proyección común al salir de modo edición");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourcesGo);
            UnityEngine.Object.DestroyImmediate(adaptersGo);
            UnityEngine.Object.DestroyImmediate(serviceGo);
        }
    }

    private static void TestRealSceneSelection()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);

        InvokeBootstrap(typeof(BistroBuilderConstructionAuthoringRuntimeBootstrap));
        InvokeBootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));

        var editMode = UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var coordinator = UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditorV2Coordinator>(
            FindObjectsInactive.Include);
        var selectionCoordinator =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>(
                FindObjectsInactive.Include);
        var furnitureController =
            UnityEngine.Object.FindFirstObjectByType<RestaurantEditInteractionController>(
                FindObjectsInactive.Include);
        var constructionTool =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>(
                FindObjectsInactive.Include);
        var constructionCoordinator =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>(
                FindObjectsInactive.Include);

        Check(editMode != null &&
              coordinator != null &&
              selectionCoordinator != null &&
              furnitureController != null &&
              constructionTool != null &&
              constructionCoordinator != null,
            "B3 bootstrap compone selección común sobre autoridades reales");
        if (editMode == null ||
            coordinator == null ||
            selectionCoordinator == null ||
            furnitureController == null ||
            constructionTool == null ||
            constructionCoordinator == null)
            return;

        int selectionCount =
            UnityEngine.Object.FindObjectsByType<BistroBuilderEditorV2SelectionCoordinator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
        InvokeBootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));
        int selectionCountAfterSecondInstall =
            UnityEngine.Object.FindObjectsByType<BistroBuilderEditorV2SelectionCoordinator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
        Check(selectionCount == 1 && selectionCountAfterSecondInstall == 1,
            "B3 bootstrap es idempotente: un único Selection Coordinator");

        bool entered = editMode.IsEditModeActive ||
            editMode.TryEnterEditMode(out _, out _);
        Check(entered, "B3 real entra en modo edición");
        if (!entered)
            return;

        Check(selectionCoordinator.RegisteredSourceCount == 3,
            "B3 runtime registra Furniture/Construction/Surfaces como proyecciones");

        Check(coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Furniture,
                "furniture",
                out _),
            "B3 real activa Furniture mediante Editor V2 Coordinator");

        RestaurantPlaceableObject placeable = FindEditablePlaceable();
        Check(placeable != null,
            "B3 real encuentra un colocable editable canónico");
        if (placeable != null)
        {
            RestaurantEditableObject editable = placeable.GetComponent<RestaurantEditableObject>();
            Vector3 positionBefore = placeable.transform.position;
            Quaternion rotationBefore = placeable.transform.rotation;

            bool selected = furnitureController.TrySelectPlaceable(placeable);
            selectionCoordinator.RefreshSelection();

            Check(selected &&
                  selectionCoordinator.Current.kind == BistroBuilderEditorV2SelectionKind.Furniture &&
                  !string.IsNullOrWhiteSpace(selectionCoordinator.Current.stableId),
                "B3 real proyecta la selección de mobiliario existente");

            Check(!furnitureController.HasActivePlacement &&
                  Approximately(positionBefore, placeable.transform.position) &&
                  Approximately(rotationBefore, placeable.transform.rotation),
                "B3 real seleccionar mobiliario no mueve, rota ni abre transacción");

            Check(selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Inspect) &&
                  selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Delete) &&
                  selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Move) == editable.CanMove &&
                  selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Rotate) == editable.CanRotate,
                "B3 real deriva capacidades desde RestaurantEditableObject");
        }

        Check(coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Construction,
                "select",
                out _) &&
              !furnitureController.HasSelection &&
              !selectionCoordinator.HasSelection,
            "B3 real Furniture -> Construction elimina selección incompatible");

        string beginError = string.Empty;
        bool sessionReady = constructionCoordinator.HasSession ||
            constructionCoordinator.TryBeginSession(out beginError);
        Check(sessionReady,
            "B3 real abre Draft arquitectónico sin publicarlo" +
            (sessionReady ? string.Empty : ": " + beginError));

        if (sessionReady && constructionCoordinator.Session != null)
        {
            BistroBuilderEditId wallId = default;
            if (constructionCoordinator.Session.Draft.walls.Count > 0)
            {
                wallId = constructionCoordinator.Session.Draft.walls[0].wallId;
            }
            else
            {
                var probeWall = new BistroBuilderWallRecord
                {
                    wallId = new BistroBuilderEditId("b3_selection_probe_wall"),
                    buildPlaneId = "default",
                    axisStart = new Vector2(100f, 100f),
                    axisEnd = new Vector2(101f, 100f),
                    baseElevation = 0f,
                    height = 2.5f,
                    thickness = 0.12f,
                    wallDefinitionId = "wall.default"
                };
                bool probeCreated = constructionCoordinator.TryExecute(
                    new BistroBuilderCreateWallCommand(probeWall),
                    out _,
                    out string probeError);
                Check(probeCreated,
                    "B3 real prepara una pared temporal únicamente dentro del Draft" +
                    (probeCreated ? string.Empty : ": " + probeError));
                if (probeCreated)
                    wallId = probeWall.wallId;
            }

            if (wallId.IsValid)
            {
                string fingerprintBefore =
                    constructionCoordinator.Session.Draft.ComputeFingerprint();
                long draftRevisionBefore =
                    constructionCoordinator.Session.DraftRevision;
                bool wasDirtyBeforeSelection = constructionCoordinator.IsDirty;

                bool architectureSelected =
                    constructionTool.TrySelectArchitecture(
                        EntityKind.Wall,
                        wallId,
                        out string architectureError);
                selectionCoordinator.RefreshSelection();

                Check(architectureSelected &&
                      constructionTool.SelectedKind == EntityKind.Wall &&
                      selectionCoordinator.Current.kind == BistroBuilderEditorV2SelectionKind.Wall,
                    "B3 real selecciona pared mediante la autoridad Construction" +
                    (architectureSelected ? string.Empty : ": " + architectureError));

                Check(constructionCoordinator.IsDirty == wasDirtyBeforeSelection &&
                      constructionCoordinator.Session.DraftRevision == draftRevisionBefore &&
                      string.Equals(
                          constructionCoordinator.Session.Draft.ComputeFingerprint(),
                          fingerprintBefore,
                          StringComparison.Ordinal),
                    "B3 real seleccionar arquitectura no modifica Draft ni revisión");

                Check(selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Move) &&
                      selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Rotate) &&
                      selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Delete) &&
                      selectionCoordinator.Current.Supports(BistroBuilderEditorV2SelectionCapability.Duplicate),
                    "B3 real inspector común expone acciones válidas para pared");

                Check(coordinator.TryActivateTool(
                        BistroBuilderEditorV2ToolFamily.Surfaces,
                        "floor",
                        out _) &&
                      constructionTool.SelectedKind == EntityKind.None &&
                      !selectionCoordinator.HasSelection,
                    "B3 real Construction -> Surfaces limpia pared incompatible");
            }
            else
            {
                Check(false,
                    "B3 real obtiene una pared seleccionable dentro del Draft");
            }
        }
        else
        {
            Check(false,
                "B3 real dispone de sesión arquitectónica para validar selección");
        }

        constructionTool.TryCancelDraft(out _);
        if (editMode.IsEditModeActive)
            Check(editMode.TryExitEditMode(true, out _) &&
                  !selectionCoordinator.HasSelection,
                "B3 real sale limpio del modo edición");
    }

    private static RestaurantPlaceableObject FindEditablePlaceable()
    {
        RestaurantPlaceableObject[] placeables =
            UnityEngine.Object.FindObjectsByType<RestaurantPlaceableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        for (int i = 0; i < placeables.Length; i++)
        {
            RestaurantPlaceableObject candidate = placeables[i];
            if (candidate != null &&
                candidate.HasValidDefinition &&
                candidate.TryGetComponent(out RestaurantEditableObject editable) &&
                editable.EditingEnabled &&
                editable.HasValidDefinition)
            {
                return candidate;
            }
        }

        return null;
    }

    private static BistroBuilderEditorV2Selection MakeSelection(
        BistroBuilderEditorV2ToolFamily family,
        BistroBuilderEditorV2SelectionKind kind,
        string id,
        string name,
        BistroBuilderEditorV2SelectionCapability capabilities)
    {
        return new BistroBuilderEditorV2Selection
        {
            family = family,
            kind = kind,
            stableId = id,
            displayName = name,
            capabilities = capabilities,
            persistentIdentity = true
        };
    }

    private static void InvokeBootstrap(Type bootstrapType)
    {
        MethodInfo install = bootstrapType.GetMethod(
            "Install",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (install == null)
            throw new MissingMethodException(bootstrapType.FullName, "Install");
        install.Invoke(null, null);
    }

    private static bool Approximately(Vector3 a, Vector3 b) =>
        (a - b).sqrMagnitude <= 0.0000001f;

    private static bool Approximately(Quaternion a, Quaternion b) =>
        Quaternion.Angle(a, b) <= 0.0001f;

    private static void Check(bool condition, string label)
    {
        if (condition)
        {
            pass++;
            Lines.Add("OK - " + label);
        }
        else
        {
            fail++;
            Lines.Add("FAIL - " + label);
        }
    }
}
