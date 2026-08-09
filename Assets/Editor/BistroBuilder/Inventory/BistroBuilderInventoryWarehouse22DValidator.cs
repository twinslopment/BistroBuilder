using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BistroBuilderInventoryWarehouse22DValidationResult
{
    private readonly List<string> lines = new List<string>(80);

    public int CorrectCount { get; private set; }
    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }

    public void Ok(string text)
    {
        CorrectCount++;
        lines.Add("- OK: " + text);
    }

    public void Warn(string text)
    {
        WarningCount++;
        lines.Add("- ADVERTENCIA: " + text);
    }

    public void Error(string text)
    {
        ErrorCount++;
        lines.Add("- ERROR: " + text);
    }

    public string BuildReport()
    {
        var builder = new StringBuilder(12288);
        builder.AppendLine("BISTRO BUILDER - 2.2D UI DEFINITIVA DE INVENTARIO / ALMACÉN");
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);
        for (int index = 0; index < lines.Count; index++)
        {
            builder.AppendLine(lines[index]);
        }
        return builder.ToString().TrimEnd();
    }
}

/// <summary>
/// Validador estructural de 2.2D. Verifica que la nueva UI sea una vista
/// sobre las autoridades 2.2A/B/C, que no aparezcan inventarios/almacenes
/// paralelos y que los ScrollRect conserven RectMask2D.
/// </summary>
public static class BistroBuilderInventoryWarehouse22DValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Validate 2.2D Definitive Inventory Warehouse UI";

    [MenuItem(MenuPath, false, 391)]
    private static void ValidateMenu()
    {
        BistroBuilderInventoryWarehouse22DValidationResult result =
            ValidateCurrentProject();
        string report = result.BuildReport();
        if (result.ErrorCount > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }
        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderInventoryWarehouse22DValidationResult
        ValidateCurrentProject()
    {
        var result = new BistroBuilderInventoryWarehouse22DValidationResult();
        Scene scene = SceneManager.GetActiveScene();

        Check(result, scene.IsValid() && scene.isLoaded &&
            !string.IsNullOrWhiteSpace(scene.path),
            "La escena principal está abierta y guardada.");

        GameObject gameSystems =
            BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(scene);
        Check(result, gameSystems != null, "GameSystems existe en la escena.");

        BistroBuilderInventoryService inventory = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderInventoryService>()
            : null;
        Check(result, inventory != null, "Existe el inventario canónico.");
        Check(result, CountSceneComponents<BistroBuilderInventoryService>(scene) == 1,
            "La escena conserva una única autoridad de inventario.");
        Check(result, BistroBuilderInventoryRuntimeSnapshot.CurrentSchemaVersion == 2,
            "inventory.canonical permanece en schema v2.");

        BistroBuilderInventorySaveSectionProvider canonicalProvider =
            gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderInventorySaveSectionProvider>()
                : null;
        Check(result, canonicalProvider != null &&
            canonicalProvider.SectionId ==
                BistroBuilderInventorySaveSectionProvider.StableSectionId &&
            canonicalProvider.SectionVersion == 2,
            "La persistencia física continúa en inventory.canonical v2.");

        BistroBuilderInventoryPlanningService planning = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderInventoryPlanningService>()
            : null;
        Check(result, planning != null,
            "2.2C sigue instalado como autoridad de mínimos, alertas y previsión.");

        BistroBuilderInventoryPolicySaveSectionProvider policy =
            gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderInventoryPolicySaveSectionProvider>()
                : null;
        Check(result, policy != null && policy.SectionId == "inventory.policy" &&
            policy.SectionVersion == 1,
            "Los mínimos continúan persistiendo en inventory.policy v1.");

        BistroBuilderGoodsReceivingService receiving = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderGoodsReceivingService>()
            : null;
        Check(result, receiving != null,
            "2.2B sigue instalado como autoridad de recepción de mercancía.");
        Check(result, receiving != null && ReferenceEquals(receiving.InventoryService, inventory),
            "Las recepciones siguen escribiendo en el mismo inventario canónico.");

        BistroBuilderRecipeCatalogService recipes = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderRecipeCatalogService>()
            : null;
        Check(result, recipes != null,
            "El catálogo canónico de ingredientes está disponible.");

        BistroBuilderInventoryWarehouseService[] warehouseServices =
            Object.FindObjectsByType<BistroBuilderInventoryWarehouseService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        Check(result, CountInScene(warehouseServices, scene) == 1,
            "Existe una única fachada de aplicación de almacén 2.2D.");
        BistroBuilderInventoryWarehouseService warehouse =
            FindInScene(warehouseServices, scene);

        Check(result, warehouse != null &&
            ReferenceEquals(warehouse.InventoryService, inventory),
            "2.2D lee y ajusta exclusivamente el inventario canónico.");
        Check(result, warehouse != null &&
            ReferenceEquals(warehouse.PlanningService, planning),
            "2.2D reutiliza la planificación 2.2C sin recalcular estados en Presentation.");
        Check(result, warehouse != null &&
            ReferenceEquals(warehouse.RecipeCatalogService, recipes),
            "2.2D reutiliza el catálogo canónico para nombres y unidades.");
        Check(result, warehouse != null &&
            ReferenceEquals(warehouse.GoodsReceivingService, receiving),
            "2.2D consume el contrato de recepciones 2.2B.");

        string error = string.Empty;
        Check(result, warehouse != null && warehouse.ValidateConfiguration(out error),
            "La fachada 2.2D tiene dependencias válidas.", error);

        Check(result, typeof(BistroBuilderInventoryWarehouseService).GetMethod(
                "TryAdjustStock",
                BindingFlags.Instance | BindingFlags.Public
            ) != null,
            "Los ajustes manuales pasan por un comando de Application.");
        Check(result, typeof(BistroBuilderInventoryService).GetMethod(
                "TryCorrectOnHand",
                BindingFlags.Instance | BindingFlags.Public
            ) != null,
            "El comando 2.2D reutiliza la corrección canónica existente.");
        Check(result, typeof(BistroBuilderInventoryWarehouseService).GetEvent(
                "DataChanged",
                BindingFlags.Instance | BindingFlags.Public
            ) != null,
            "La UI puede reaccionar por eventos sin sondeo por frame.");

        BistroBuilderInventoryWarehouseRuntimeView[] views =
            Object.FindObjectsByType<BistroBuilderInventoryWarehouseRuntimeView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        Check(result, CountInScene(views, scene) == 1,
            "La escena contiene una única UI definitiva de inventario 2.2D.");
        BistroBuilderInventoryWarehouseRuntimeView view = FindInScene(views, scene);
        Check(result, view != null && ReferenceEquals(view.WarehouseService, warehouse),
            "Presentation está enlazada únicamente con la fachada 2.2D.");
        error = string.Empty;
        Check(result, view != null && view.ValidateConfiguration(out error),
            "La UI 2.2D tiene configuración válida.", error);

        Transform uiRoot = FindSceneTransform(
            scene,
            BistroBuilderInventoryWarehouse22DInstaller.UiRootName
        );
        Check(result, uiRoot != null && uiRoot.GetComponent<RectTransform>() != null &&
            uiRoot.GetComponent<BistroBuilderInventoryWarehouseRuntimeView>() != null,
            "La UI definitiva está aislada en un RectTransform propio del HUD.");

        BistroBuilderInventoryPlanningRuntimeView legacyView =
            FindSingleSceneComponent<BistroBuilderInventoryPlanningRuntimeView>(scene);
        Check(result, legacyView != null,
            "La UI provisional 2.2C se conserva como infraestructura compatible.");
        Check(result, legacyView == null || !ReadSerializedBool(legacyView, "showOpenButton", true),
            "El acceso provisional 2.2C queda oculto para evitar dos botones de inventario.");

        Check(result, ValidateFactoryScrollMask(out error),
            "Los ScrollRect de 2.2D usan RectMask2D y no Mask transparente.", error);

        Check(result, Enum.GetValues(typeof(BistroBuilderInventoryWarehouseFilter)).Length == 4,
            "Los filtros jugables permanecen limitados a cuatro opciones prácticas.");
        Check(result, Enum.GetValues(typeof(BistroBuilderInventoryWarehouseSort)).Length == 4,
            "La ordenación se limita a nombre, stock, estado y caducidad.");
        Check(result, Enum.GetValues(typeof(BistroBuilderInventoryWarehouseSection)).Length == 4,
            "La navegación se limita a Existencias, Alertas, Movimientos y Recepciones.");
        Check(result, Enum.GetValues(typeof(BistroBuilderInventoryManualAdjustmentReason)).Length == 4,
            "Los ajustes manuales usan cuatro motivos administrativos sencillos.");

        BistroBuilderGoodsReceivingRoute[] routes =
            Object.FindObjectsByType<BistroBuilderGoodsReceivingRoute>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        Check(result, CountInScene(routes, scene) == 1,
            "2.2D no crea almacenes ni rutas de suministro adicionales.");

        BistroBuilderSaveGameService saveService = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderSaveGameService>()
            : null;
        if (saveService != null)
        {
            saveService.RefreshExtensions();
        }
        Check(result, saveService != null &&
            saveService.HasProvider(BistroBuilderInventorySaveSectionProvider.StableSectionId) &&
            saveService.HasProvider("inventory.policy"),
            "Guardado/carga conserva inventario canónico y política sin sección paralela 2.2D.");

        BistroBuilderInventoryLots22AValidationResult validation22A =
            BistroBuilderInventoryLots22AValidator.ValidateCurrentProject();
        Check(result, validation22A.ErrorCount == 0,
            "Regresión estructural 2.2A superada.");

        BistroBuilderGoodsReceiving22BValidationResult validation22B =
            BistroBuilderGoodsReceiving22BValidator.ValidateCurrentProject();
        Check(result, validation22B.ErrorCount == 0,
            "Regresión estructural 2.2B superada.");

        BistroBuilderInventoryPlanning22CValidationResult validation22C =
            BistroBuilderInventoryPlanning22CValidator.ValidateCurrentProject();
        Check(result, validation22C.ErrorCount == 0,
            "Regresión estructural 2.2C superada.");

        Check(result, typeof(BistroBuilderInventoryWarehouseRuntimeView).GetMethod(
                "TryValidateVisibleContent",
                BindingFlags.Instance | BindingFlags.Public
            ) != null,
            "La vista expone validación runtime de filas y viewport.");

        Check(result, typeof(BistroBuilderInventoryWarehouseService).GetMethod(
                "CopyReceiptsTo",
                BindingFlags.Instance | BindingFlags.Public
            ) != null &&
            typeof(BistroBuilderInventoryWarehouseService).GetMethod(
                "CopyMovementsTo",
                BindingFlags.Instance | BindingFlags.Public
            ) != null,
            "Recepciones y movimientos se consultan mediante Application, no desde Presentation.");

        return result;
    }

    private static bool ValidateFactoryScrollMask(out string error)
    {
        error = string.Empty;
        GameObject root = null;
        try
        {
            // El validador vive en Assembly-CSharp-Editor y la fábrica de UI
            // es interna de Assembly-CSharp. Invocamos la construcción visual
            // real de 2.2D por reflexión para validar el mismo ScrollRect que
            // utilizará el jugador sin romper el encapsulamiento entre assemblies.
            root = new GameObject(
                "BB_22D_MaskValidation",
                typeof(RectTransform)
            );
            root.hideFlags = HideFlags.HideAndDontSave;

            BistroBuilderInventoryWarehouseRuntimeView view =
                root.AddComponent<BistroBuilderInventoryWarehouseRuntimeView>();
            MethodInfo ensureVisualTree =
                typeof(BistroBuilderInventoryWarehouseRuntimeView).GetMethod(
                    "EnsureVisualTree",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
            if (ensureVisualTree == null)
            {
                error = "No se encuentra el constructor visual interno de 2.2D.";
                return false;
            }

            ensureVisualTree.Invoke(view, null);

            ScrollRect[] scrolls = root.GetComponentsInChildren<ScrollRect>(true);
            if (scrolls == null || scrolls.Length == 0)
            {
                error = "La vista 2.2D no creó ningún ScrollRect.";
                return false;
            }

            for (int index = 0; index < scrolls.Length; index++)
            {
                ScrollRect scroll = scrolls[index];
                if (scroll == null || scroll.viewport == null ||
                    scroll.content == null)
                {
                    error = "La vista 2.2D contiene un ScrollRect incompleto.";
                    return false;
                }
                if (scroll.viewport.GetComponent<RectMask2D>() == null)
                {
                    error = "Un viewport 2.2D no contiene RectMask2D.";
                    return false;
                }
                if (scroll.viewport.GetComponent<Mask>() != null)
                {
                    error = "Un viewport 2.2D contiene un Mask clásico.";
                    return false;
                }
            }

            return true;
        }
        catch (TargetInvocationException exception)
        {
            Exception inner = exception.InnerException;
            error = inner != null ? inner.Message : exception.Message;
            return false;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static bool ReadSerializedBool(
        Object target,
        string propertyName,
        bool fallback
    )
    {
        if (target == null)
        {
            return fallback;
        }
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        return property != null ? property.boolValue : fallback;
    }

    private static int CountSceneComponents<T>(Scene scene) where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        return CountInScene(components, scene);
    }

    private static int CountInScene<T>(T[] components, Scene scene)
        where T : Component
    {
        int count = 0;
        if (components == null)
        {
            return count;
        }
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index] != null &&
                components[index].gameObject.scene == scene)
            {
                count++;
            }
        }
        return count;
    }

    private static T FindInScene<T>(T[] components, Scene scene)
        where T : Component
    {
        if (components == null)
        {
            return null;
        }
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index] != null &&
                components[index].gameObject.scene == scene)
            {
                return components[index];
            }
        }
        return null;
    }

    private static T FindSingleSceneComponent<T>(Scene scene)
        where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        return FindInScene(components, scene);
    }

    private static Transform FindSceneTransform(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform found = FindRecursive(roots[index].transform, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static Transform FindRecursive(Transform root, string name)
    {
        if (string.Equals(root.name, name, StringComparison.Ordinal))
        {
            return root;
        }
        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindRecursive(root.GetChild(index), name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static void Check(
        BistroBuilderInventoryWarehouse22DValidationResult result,
        bool condition,
        string success,
        string detail = ""
    )
    {
        if (condition)
        {
            result.Ok(success);
        }
        else
        {
            result.Error(
                string.IsNullOrWhiteSpace(detail)
                    ? success
                    : success + " Detalle: " + detail
            );
        }
    }
}
