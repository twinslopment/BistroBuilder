using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class BistroBuilderGoodsReceiving22BValidationResult
{
    private readonly List<string> lines = new List<string>();

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
            "BISTRO BUILDER - 2.2B RECEPCIÓN Y REPARTO VISUAL BÁSICO"
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
/// Validador estructural de 2.2B. Comprueba que el flujo de recepción sigue
/// usando el inventario canónico 2.2A y que la escena solo contiene una ruta
/// de suministro hacia un único almacén genérico.
/// </summary>
public static class BistroBuilderGoodsReceiving22BValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Validate 2.2B Goods Receiving and Basic Delivery Visual";

    [MenuItem(MenuPath, false, 371)]
    private static void ValidateMenu()
    {
        BistroBuilderGoodsReceiving22BValidationResult result =
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

    public static BistroBuilderGoodsReceiving22BValidationResult
        ValidateCurrentProject()
    {
        var result = new BistroBuilderGoodsReceiving22BValidationResult();
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

        string error = string.Empty;
        Check(
            result,
            inventory != null && inventory.ValidateConfiguration(out error),
            "El inventario conserva una configuración válida de 2.2A.",
            error
        );

        Check(
            result,
            BistroBuilderInventoryRuntimeSnapshot.CurrentSchemaVersion == 2,
            "inventory.canonical permanece en schema v2."
        );

        Check(
            result,
            (int)BistroBuilderInventoryTransactionType.Purchase == 1 &&
            (int)BistroBuilderInventoryTransactionType.Expiration == 7,
            "Compra y caducidad mantienen sus contratos de movimiento."
        );

        MethodInfo batchMethod = typeof(BistroBuilderInventoryService)
            .GetMethod(
                "TryReceivePurchaseBatch",
                BindingFlags.Instance | BindingFlags.Public
            );
        Check(
            result,
            batchMethod != null,
            "El inventario expone recepción multilínea atómica e idempotente."
        );

        BistroBuilderGoodsReceivingService receiving = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderGoodsReceivingService>()
            : null;
        Check(
            result,
            receiving != null,
            "GameSystems contiene BistroBuilderGoodsReceivingService."
        );
        Check(
            result,
            receiving != null && ReferenceEquals(receiving.InventoryService, inventory),
            "La recepción utiliza el inventario canónico como única autoridad."
        );
        error = string.Empty;
        Check(
            result,
            receiving != null && receiving.ValidateConfiguration(out error),
            "El servicio de recepción tiene dependencias válidas.",
            error
        );

        Check(
            result,
            BistroBuilderGoodsReceivingIds.PrimaryWarehouse ==
                "warehouse_primary",
            "Existe una única identidad estable de almacén genérico."
        );
        Check(
            result,
            BistroBuilderGoodsReceivingIds.PrimarySupplyAccess ==
                "supply_access_primary",
            "Existe una única identidad estable de acceso de suministros."
        );

        BistroBuilderGoodsReceivingRoute[] routes =
            Object.FindObjectsByType<BistroBuilderGoodsReceivingRoute>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        var sceneRoutes = new List<BistroBuilderGoodsReceivingRoute>();
        for (int index = 0; index < routes.Length; index++)
        {
            if (routes[index] != null && routes[index].gameObject.scene == scene)
            {
                sceneRoutes.Add(routes[index]);
            }
        }
        Check(
            result,
            sceneRoutes.Count == 1,
            "La escena contiene exactamente una ruta de recepción."
        );

        BistroBuilderGoodsReceivingRoute route =
            sceneRoutes.Count == 1 ? sceneRoutes[0] : null;
        Check(
            result,
            route != null && route.SupplyAccessPoint != null,
            "La ruta tiene un acceso de suministros explícito."
        );
        Check(
            result,
            route != null && route.WarehouseDropPoint != null,
            "La ruta tiene un único punto de descarga de almacén."
        );
        Check(
            result,
            route != null && route.SupplyAccessPoint != route.WarehouseDropPoint,
            "Acceso y almacén usan anclajes distintos."
        );
        error = string.Empty;
        Check(
            result,
            route != null && route.ValidateConfiguration(out error),
            "La ruta visual tiene distancia y referencias válidas.",
            error
        );
        Check(
            result,
            route != null && route.WarehouseId ==
                BistroBuilderGoodsReceivingIds.PrimaryWarehouse,
            "La ruta apunta al almacén genérico único."
        );
        Check(
            result,
            route != null && route.SupplyAccessId ==
                BistroBuilderGoodsReceivingIds.PrimarySupplyAccess,
            "La ruta apunta al acceso de suministros único."
        );
        Check(
            result,
            route != null && route.gameObject.scene == scene,
            "La ruta pertenece a la escena principal y no a un asset externo."
        );

        BistroBuilderGoodsReceivingPresentation presentation =
            gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderGoodsReceivingPresentation>()
                : null;
        Check(
            result,
            presentation != null,
            "GameSystems contiene la presentación temporal del reparto."
        );
        Check(
            result,
            presentation != null &&
            ReferenceEquals(presentation.ReceivingService, receiving),
            "Presentación escucha el servicio autoritativo de recepción."
        );
        Check(
            result,
            presentation != null && ReferenceEquals(presentation.Route, route),
            "Presentación usa la única ruta de suministro instalada."
        );
        error = string.Empty;
        Check(
            result,
            presentation != null &&
            presentation.ValidateConfiguration(out error),
            "La presentación visual tiene una configuración válida.",
            error
        );

        BistroBuilderSupplyDeliveryVisual[] persistedVisuals =
            Object.FindObjectsByType<BistroBuilderSupplyDeliveryVisual>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        int sceneVisualCount = 0;
        for (int index = 0; index < persistedVisuals.Length; index++)
        {
            if (persistedVisuals[index] != null &&
                persistedVisuals[index].gameObject.scene == scene)
            {
                sceneVisualCount++;
            }
        }
        Check(
            result,
            sceneVisualCount == 0,
            "No hay repartidores persistentes: se crean solo temporalmente."
        );

        Check(
            result,
            typeof(BistroBuilderGoodsReceivingService)
                .GetEvent("ReceiptAccepted") != null,
            "Recepción y presentación están desacopladas mediante evento."
        );
        Check(
            result,
            typeof(BistroBuilderGoodsReceivingPresentation)
                .GetEvent("VisualStateChanged") != null,
            "La secuencia visual es observable para diagnóstico y pruebas."
        );

        Check(
            result,
            Enum.GetValues(typeof(BistroBuilderGoodsReceivingVisualState)).Length ==
                7,
            "El flujo visual se limita a entrada, descarga y salida."
        );

        return result;
    }

    private static void Check(
        BistroBuilderGoodsReceiving22BValidationResult result,
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
