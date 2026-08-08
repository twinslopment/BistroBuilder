using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BistroBuilderInventoryPlanning22CValidationResult
{
    private readonly List<string> lines = new List<string>(64);

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
        var builder = new StringBuilder(8192);
        builder.AppendLine(
            "BISTRO BUILDER - 2.2C STOCK MÍNIMO, ALERTAS Y PREVISIÓN BÁSICA"
        );
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
/// Validador estructural de 2.2C. Comprueba que planificación y política
/// permanecen subordinadas al inventario canónico y que no se introducen
/// almacenes, balances o lotes paralelos.
/// </summary>
public static class BistroBuilderInventoryPlanning22CValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Validate 2.2C Minimum Stock, Alerts and Basic Forecast";

    [MenuItem(MenuPath, false, 381)]
    private static void ValidateMenu()
    {
        BistroBuilderInventoryPlanning22CValidationResult result =
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

    public static BistroBuilderInventoryPlanning22CValidationResult
        ValidateCurrentProject()
    {
        var result = new BistroBuilderInventoryPlanning22CValidationResult();
        Scene scene = SceneManager.GetActiveScene();

        Check(
            result,
            scene.IsValid() && scene.isLoaded &&
            !string.IsNullOrWhiteSpace(scene.path),
            "La escena principal está abierta y guardada."
        );

        GameObject gameSystems =
            BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(scene);
        Check(result, gameSystems != null, "GameSystems existe en la escena.");

        BistroBuilderInventoryService inventory = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderInventoryService>()
            : null;
        Check(result, inventory != null, "Existe el inventario canónico.");

        int inventoryCount = CountSceneComponents<BistroBuilderInventoryService>(scene);
        Check(
            result,
            inventoryCount == 1,
            "La escena conserva exactamente una autoridad de inventario."
        );

        string error = string.Empty;
        Check(
            result,
            inventory != null && inventory.ValidateConfiguration(out error),
            "El inventario canónico mantiene configuración válida.",
            error
        );

        Check(
            result,
            BistroBuilderInventoryRuntimeSnapshot.CurrentSchemaVersion == 2,
            "inventory.canonical permanece en schema v2."
        );

        BistroBuilderInventorySaveSectionProvider canonicalProvider =
            gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderInventorySaveSectionProvider>()
                : null;
        Check(
            result,
            canonicalProvider != null &&
            canonicalProvider.SectionId ==
                BistroBuilderInventorySaveSectionProvider.StableSectionId &&
            canonicalProvider.SectionVersion == 2,
            "La persistencia física sigue siendo inventory.canonical v2."
        );

        BistroBuilderGoodsReceivingService receiving = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderGoodsReceivingService>()
            : null;
        Check(
            result,
            receiving != null,
            "2.2B continúa instalado como flujo de recepción."
        );

        Check(
            result,
            receiving != null && ReferenceEquals(receiving.InventoryService, inventory),
            "Las recepciones continúan usando la misma autoridad de stock."
        );

        BistroBuilderInventoryPlanningService planning = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderInventoryPlanningService>()
            : null;
        Check(
            result,
            planning != null,
            "GameSystems contiene BistroBuilderInventoryPlanningService."
        );

        Check(
            result,
            planning != null && ReferenceEquals(planning.InventoryService, inventory),
            "La planificación lee el inventario canónico sin duplicarlo."
        );

        BistroBuilderRecipeCatalogService recipes = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderRecipeCatalogService>()
            : null;
        Check(
            result,
            planning != null && ReferenceEquals(planning.RecipeCatalogService, recipes),
            "La planificación usa el catálogo canónico de ingredientes."
        );

        BistroBuilderGeneralGameStateService general = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderGeneralGameStateService>()
            : null;
        Check(
            result,
            planning != null && ReferenceEquals(planning.GeneralGameStateService, general),
            "La previsión usa el DayIndex autoritativo de la partida."
        );

        RestaurantServiceStateService serviceState = gameSystems != null
            ? gameSystems.GetComponent<RestaurantServiceStateService>()
            : null;
        Check(
            result,
            planning != null && ReferenceEquals(planning.ServiceStateService, serviceState),
            "La comprobación de apertura está conectada al estado de servicio."
        );

        error = string.Empty;
        Check(
            result,
            planning != null && planning.ValidateConfiguration(out error),
            "El servicio de planificación tiene dependencias válidas.",
            error
        );

        Check(
            result,
            planning != null && planning.CriticalThresholdRatio > 0f &&
            planning.CriticalThresholdRatio < 1f,
            "El umbral crítico está definido como proporción válida del mínimo."
        );

        Check(
            result,
            (int)BistroBuilderInventoryFreshnessState.NearExpiry == 3,
            "Las alertas de caducidad reutilizan el estado NearExpiry canónico de 2.2A."
        );

        Check(
            result,
            planning != null && planning.MinimumHistoryDaysForForecast >= 2,
            "La previsión exige historial antes de mostrar cobertura."
        );

        Check(
            result,
            typeof(RestaurantServiceStateService).GetEvent(
                "ServiceOpeningRequested",
                BindingFlags.Instance | BindingFlags.Public
            ) != null,
            "El estado de servicio publica una señal previa a la apertura."
        );

        Check(
            result,
            typeof(BistroBuilderInventoryPlanningService).GetEvent(
                "AlertActivated"
            ) != null &&
            typeof(BistroBuilderInventoryPlanningService).GetEvent(
                "AlertCleared"
            ) != null,
            "Las alertas exponen activación y recuperación deduplicables."
        );

        Check(
            result,
            typeof(BistroBuilderInventoryPlanningService).GetEvent(
                "OpeningReadinessEvaluated"
            ) != null,
            "La validación previa a apertura es observable sin bloquear."
        );

        BistroBuilderInventoryPolicySaveSectionProvider[] policyProviders =
            Object.FindObjectsByType<BistroBuilderInventoryPolicySaveSectionProvider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        int scenePolicyProviders = CountInScene(policyProviders, scene);
        Check(
            result,
            scenePolicyProviders == 1,
            "Existe un único proveedor persistente de política de inventario."
        );

        BistroBuilderInventoryPolicySaveSectionProvider policyProvider =
            gameSystems != null
                ? gameSystems.GetComponent<
                    BistroBuilderInventoryPolicySaveSectionProvider
                >()
                : null;
        Check(
            result,
            policyProvider != null &&
            policyProvider.SectionId == "inventory.policy" &&
            policyProvider.SectionVersion == 1,
            "Stock mínimo se persiste en inventory.policy v1."
        );

        Check(
            result,
            policyProvider != null &&
            ReferenceEquals(policyProvider.PlanningService, planning),
            "inventory.policy persiste exclusivamente la política de planificación."
        );

        error = string.Empty;
        Check(
            result,
            policyProvider != null &&
            policyProvider.ValidateConfiguration(out error),
            "El proveedor inventory.policy tiene configuración válida.",
            error
        );

        Check(
            result,
            BistroBuilderInventoryPolicySaveData.CurrentSchemaVersion == 1,
            "El payload inventory.policy tiene versión explícita."
        );

        BistroBuilderSaveGameService saveService = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderSaveGameService>()
            : null;
        if (saveService != null)
        {
            saveService.RefreshExtensions();
        }
        Check(
            result,
            saveService != null && saveService.HasProvider("inventory.policy"),
            "La plataforma universal de guardado registra inventory.policy."
        );

        Check(
            result,
            policyProvider != null &&
            policyProvider.StateType == typeof(BistroBuilderInventoryPolicySaveData),
            "inventory.policy declara un estado serializable independiente del stock físico."
        );

        BistroBuilderInventoryPlanningRuntimeView[] views =
            Object.FindObjectsByType<BistroBuilderInventoryPlanningRuntimeView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        int sceneViews = CountInScene(views, scene);
        Check(
            result,
            sceneViews == 1,
            "La escena contiene una única UI jugable de inventario 2.2C."
        );

        BistroBuilderInventoryPlanningRuntimeView view = sceneViews == 1
            ? FindInScene(views, scene)
            : null;
        Check(
            result,
            view != null && ReferenceEquals(view.PlanningService, planning),
            "La UI de almacén consume exclusivamente snapshots de planificación."
        );

        error = string.Empty;
        Check(
            result,
            view != null && view.ValidateConfiguration(out error),
            "La UI de inventario tiene una configuración válida.",
            error
        );

        Transform uiRoot = FindSceneTransform(
            scene,
            BistroBuilderInventoryPlanning22CInstaller.UiRootName
        );
        Check(
            result,
            uiRoot != null && uiRoot.GetComponent<RectTransform>() != null &&
            uiRoot.GetComponent<BistroBuilderInventoryPlanningRuntimeView>() != null,
            "La UI 2.2C está aislada en un único RectTransform del HUD."
        );

        BistroBuilderGoodsReceivingRoute[] routes =
            Object.FindObjectsByType<BistroBuilderGoodsReceivingRoute>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        Check(
            result,
            CountInScene(routes, scene) == 1,
            "2.2C no introduce almacenes ni rutas de suministro adicionales."
        );

        Check(
            result,
            Enum.GetValues(typeof(BistroBuilderInventoryStockLevelState)).Length == 4 &&
            Enum.GetValues(typeof(BistroBuilderInventoryForecastState)).Length == 3,
            "Estados de stock y previsión permanecen compactos y comprensibles."
        );

        Check(
            result,
            (int)BistroBuilderInventoryTransactionType.Consumption == 4 &&
            (int)BistroBuilderInventoryTransactionType.Purchase == 1 &&
            (int)BistroBuilderInventoryTransactionType.Expiration == 7,
            "2.2C conserva los contratos de movimientos existentes."
        );

        Check(
            result,
            typeof(BistroBuilderInventoryPlanningMath).GetMethod(
                "CalculateForecast",
                BindingFlags.Public | BindingFlags.Static
            ) != null,
            "La previsión básica se expresa mediante cálculo puro y testeable."
        );

        return result;
    }

    private static int CountSceneComponents<T>(Scene scene)
        where T : Component
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
        BistroBuilderInventoryPlanning22CValidationResult result,
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
