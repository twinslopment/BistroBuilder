using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BistroBuilderEditorV2FakeAdapter :
    MonoBehaviour,
    IBistroBuilderEditorV2ToolAdapter
{
    public BistroBuilderEditorV2ToolFamily family;
    public bool available = true;
    public bool activationSucceeds = true;
    public bool cancelSucceeds = true;
    public bool activeOperation;
    public int activationCount;
    public int cancellationCount;
    public int deactivationCount;
    private bool active;
    private string toolId = string.Empty;

    public BistroBuilderEditorV2ToolFamily Family => family;
    public bool IsAvailable => available;
    public bool IsActive => active;
    public bool HasActiveOperation => activeOperation;
    public string ActiveToolId => toolId;

    public bool TryActivate(string nextToolId, out string error)
    {
        activationCount++;
        if (!activationSucceeds)
        {
            error = "Injected activation failure.";
            return false;
        }
        active = true;
        toolId = nextToolId ?? string.Empty;
        error = string.Empty;
        return true;
    }

    public bool TryCancelActiveOperation(out string error)
    {
        cancellationCount++;
        if (!cancelSucceeds)
        {
            error = "Injected cancellation failure.";
            return false;
        }
        activeOperation = false;
        error = string.Empty;
        return true;
    }

    public void Deactivate()
    {
        deactivationCount++;
        active = false;
        toolId = string.Empty;
    }
}

public static class BistroBuilderEditorV2B2CoordinatorSelfTest
{
    private const string ReportName = "EditorV2_B2_Coordinator_Report.txt";
    private static readonly List<string> Lines = new List<string>();
    private static int pass;
    private static int fail;

    [MenuItem("Tools/Bistro Builder/Edit Mode/Editor V2/B2 - Coordinator Self Test", false, 18101)]
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

        TestCoordinatorCore();
        TestRealSceneAdapters();

        string report =
            "EDITOR V2 - B2 COORDINATOR SELF TEST\n" +
            string.Join("\n", Lines) +
            $"\nResultado: {pass} OK / {fail} fallos.";

        string root = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(Path.Combine(root, ReportName), report);
        Debug.Log(report);

        if (fail > 0)
            throw new InvalidOperationException(report);
    }

    private static void TestCoordinatorCore()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var serviceGo = new GameObject("B2_Core_Service");
        var adaptersGo = new GameObject("B2_Core_Adapters");
        try
        {
            serviceGo.AddComponent<RestaurantPlacementValidationService>();
            serviceGo.AddComponent<RestaurantPlacementTransactionService>();
            var editMode = serviceGo.AddComponent<RestaurantEditModeService>();
            var coordinator = serviceGo.AddComponent<BistroBuilderEditorV2Coordinator>();

            var furniture = adaptersGo.AddComponent<BistroBuilderEditorV2FakeAdapter>();
            furniture.family = BistroBuilderEditorV2ToolFamily.Furniture;
            var construction = adaptersGo.AddComponent<BistroBuilderEditorV2FakeAdapter>();
            construction.family = BistroBuilderEditorV2ToolFamily.Construction;
            var surfaces = adaptersGo.AddComponent<BistroBuilderEditorV2FakeAdapter>();
            surfaces.family = BistroBuilderEditorV2ToolFamily.Surfaces;

            coordinator.Configure(editMode, furniture, construction, surfaces);

            Check(!coordinator.TryActivateTool(
                    BistroBuilderEditorV2ToolFamily.Furniture,
                    "furniture",
                    out _),
                "B2 no activa herramientas fuera de modo edición");

            Check(editMode.TryEnterEditMode(out _, out _),
                "B2 autoridad existente abre modo edición");
            Check(coordinator.RegisteredAdapterCount == 3,
                "B2 registra exactamente Furniture/Construction/Surfaces");

            Check(coordinator.TryActivateTool(
                    BistroBuilderEditorV2ToolFamily.Furniture,
                    "furniture",
                    out _) &&
                  coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.Furniture &&
                  coordinator.State == BistroBuilderEditorV2OperationState.ToolActive,
                "B2 activa mobiliario como estado coordinado");

            furniture.activeOperation = true;
            Check(coordinator.TryActivateTool(
                    BistroBuilderEditorV2ToolFamily.Construction,
                    "wall",
                    out _) &&
                  furniture.cancellationCount == 1 &&
                  !furniture.activeOperation &&
                  coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.Construction,
                "B2 cancela operación provisional antes de cambiar de familia");

            construction.activeOperation = true;
            construction.cancelSucceeds = false;
            Check(!coordinator.TryActivateTool(
                    BistroBuilderEditorV2ToolFamily.Surfaces,
                    "floor",
                    out _) &&
                  coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.Construction &&
                  construction.IsActive,
                "B2 impide dos operaciones incompatibles si la activa no puede cancelarse");

            construction.cancelSucceeds = true;
            Check(coordinator.TryActivateTool(
                    BistroBuilderEditorV2ToolFamily.Surfaces,
                    "floor",
                    out _) &&
                  coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.Surfaces,
                "B2 cambia a superficies cuando la operación previa converge");

            long sequence = coordinator.TransitionSequence;
            Check(coordinator.TryActivateTool(
                    BistroBuilderEditorV2ToolFamily.Surfaces,
                    "floor",
                    out _) &&
                  coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.Surfaces &&
                  coordinator.TransitionSequence == sequence + 1,
                "B2 activación repetida es idempotente y observable");

            Check(editMode.TryExitEditMode(true, out _) &&
                  coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.None &&
                  coordinator.State == BistroBuilderEditorV2OperationState.Inactive,
                "B2 limpia coordinación al cerrar modo edición");

            var duplicate = adaptersGo.AddComponent<BistroBuilderEditorV2FakeAdapter>();
            duplicate.family = BistroBuilderEditorV2ToolFamily.Furniture;
            coordinator.Configure(editMode, furniture, construction, surfaces, duplicate);
            Check(!coordinator.RebuildAdapterRegistry(out string duplicateError) &&
                  !string.IsNullOrWhiteSpace(duplicateError),
                "B2 rechaza dos adaptadores como autoridad de la misma familia");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(adaptersGo);
            UnityEngine.Object.DestroyImmediate(serviceGo);
        }
    }

    private static void TestRealSceneAdapters()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);

        // Construction Authoring is intentionally runtime-composed, not serialized
        // in Prototype_Restaurant. Use exactly the production bootstrap.
        InvokeBootstrap(typeof(BistroBuilderConstructionAuthoringRuntimeBootstrap));
        InvokeBootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));

        var editMode = UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var furnitureController = UnityEngine.Object.FindFirstObjectByType<RestaurantEditInteractionController>(
            FindObjectsInactive.Include);
        var constructionTool = UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>(
            FindObjectsInactive.Include);
        var coordinator = UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditorV2Coordinator>(
            FindObjectsInactive.Include);

        Check(editMode != null &&
              furnitureController != null &&
              constructionTool != null &&
              coordinator != null,
            "B2 bootstrap compone Coordinator sobre autoridades existentes " +
            $"(editMode={editMode != null}, furniture={furnitureController != null}, " +
            $"construction={constructionTool != null}, coordinator={coordinator != null})");
        if (editMode == null ||
            furnitureController == null ||
            constructionTool == null ||
            coordinator == null)
            return;

        int coordinatorCount =
            UnityEngine.Object.FindObjectsByType<BistroBuilderEditorV2Coordinator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
        InvokeBootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));
        int coordinatorCountAfterSecondInstall =
            UnityEngine.Object.FindObjectsByType<BistroBuilderEditorV2Coordinator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
        Check(coordinatorCount == 1 && coordinatorCountAfterSecondInstall == 1,
            "B2 bootstrap es idempotente: un único Coordinator");

        string rejection = string.Empty;
        bool entered = editMode.IsEditModeActive ||
            editMode.TryEnterEditMode(out _, out rejection);
        Check(entered,
            "B2 entra en modo edición mediante RestaurantEditModeService" +
            (entered ? string.Empty : ": " + rejection));
        if (!entered)
            return;

        Check(coordinator.RegisteredAdapterCount == 3,
            "B2 runtime registra tres adaptadores sin añadir autoridades de dominio");

        Check(coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Furniture,
                "furniture",
                out _) &&
              constructionTool.Mode == BistroBuilderConstructionRuntimeMode.Furniture &&
              !furnitureController.IsWorldInputSuppressed,
            "B2 real: Furniture delega al controlador existente");

        Check(coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Construction,
                "wall",
                out _) &&
              constructionTool.Mode == BistroBuilderConstructionRuntimeMode.Wall &&
              furnitureController.IsWorldInputSuppressed,
            "B2 real: Wall delega a Construction Authoring y suspende input de mobiliario");

        Check(coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Surfaces,
                "floor",
                out _) &&
              constructionTool.Mode == BistroBuilderConstructionRuntimeMode.Select &&
              furnitureController.IsWorldInputSuppressed,
            "B2 real: Surfaces reutiliza selección de Construction sin nueva autoridad");

        Check(coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Construction,
                "door",
                out _) &&
              constructionTool.Mode == BistroBuilderConstructionRuntimeMode.Door,
            "B2 real: cambio Surface → Door llega al especialista correcto");

        Check(coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Furniture,
                "furniture",
                out _) &&
              constructionTool.Mode == BistroBuilderConstructionRuntimeMode.Furniture &&
              !furnitureController.IsWorldInputSuppressed,
            "B2 real: volver a Furniture restaura input existente");

        Check(!coordinator.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Construction,
                "invented-tool",
                out _) &&
              coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.Furniture &&
              !furnitureController.IsWorldInputSuppressed,
            "B2 real: herramienta inválida hace rollback al especialista anterior");

        Check(coordinator.TryClearActiveTool(true, out _) &&
              coordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.None,
            "B2 real: limpiar herramienta no cancela ni inventa Draft");

        if (editMode.IsEditModeActive)
            Check(editMode.TryExitEditMode(true, out _),
                "B2 sale por la autoridad existente de modo edición");
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
