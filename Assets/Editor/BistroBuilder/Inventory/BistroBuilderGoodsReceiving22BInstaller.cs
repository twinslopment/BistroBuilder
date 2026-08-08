using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Instalador acumulativo e idempotente de 2.2B.
///
/// Añade el servicio autoritativo de recepción y una ruta visual mínima con
/// un único acceso de suministros y un único punto de descarga del almacén.
/// No crea proveedores, vehículos, empleados ni almacenes adicionales.
/// </summary>
public static class BistroBuilderGoodsReceiving22BInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Install or Repair 2.2B Goods Receiving and Basic Delivery Visual";

    private const string RouteRootName = "BB_2.2B_GoodsReceiving";
    private const string SupplyPointName = "SupplyAccessPoint_Primary";
    private const string WarehousePointName = "WarehouseDropPoint_Primary";

    [MenuItem(MenuPath, false, 370)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 2.2B.",
                "Aceptar"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda la escena principal antes de instalar 2.2B.",
                "Aceptar"
            );
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el instalador 2.2B.",
                "Aceptar"
            );
            return;
        }

        AssetDatabase.SaveAssets();
        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);

        try
        {
            GameObject gameSystems =
                BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(
                    scene
                );
            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa."
                );
            }

            BistroBuilderInventoryService inventory =
                Require<BistroBuilderInventoryService>(gameSystems);
            BistroBuilderGeneralGameStateService generalState =
                Require<BistroBuilderGeneralGameStateService>(gameSystems);

            Undo.RegisterCompleteObjectUndo(
                gameSystems,
                "Instalar recepción de mercancía 2.2B"
            );

            BistroBuilderGoodsReceivingService receiving =
                GetOrAdd<BistroBuilderGoodsReceivingService>(gameSystems);
            BistroBuilderGoodsReceivingPresentation presentation =
                GetOrAdd<BistroBuilderGoodsReceivingPresentation>(gameSystems);
            BistroBuilderGoodsReceivingRoute route = EnsureSingleRoute(scene);

            SetReference(receiving, "inventoryService", inventory);
            SetReference(
                receiving,
                "generalGameStateService",
                generalState
            );
            SetReference(presentation, "receivingService", receiving);
            SetReference(presentation, "route", route);

            string error = string.Empty;
            if (!inventory.ValidateConfiguration(out error) ||
                !receiving.ValidateConfiguration(out error) ||
                !route.ValidateConfiguration(out error) ||
                !presentation.ValidateConfiguration(out error))
            {
                throw new InvalidOperationException(error);
            }

            EditorUtility.SetDirty(receiving);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(route);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena activa."
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderGoodsReceiving22BValidationResult result =
                BistroBuilderGoodsReceiving22BValidator.ValidateCurrentProject();
            if (result.ErrorCount > 0)
            {
                throw new InvalidOperationException(result.BuildReport());
            }

            Debug.Log(
                "BISTRO BUILDER - 2.2B INSTALADO\n" + result.BuildReport()
            );
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "2.2B instalado correctamente.\n\n" +
                "Correctos: " + result.CorrectCount +
                "\nAdvertencias: " + result.WarningCount +
                "\nErrores: " + result.ErrorCount,
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 2.2B falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar"
            );
        }
    }

    private static BistroBuilderGoodsReceivingRoute EnsureSingleRoute(
        Scene scene
    )
    {
        BistroBuilderGoodsReceivingRoute[] routes =
            Object.FindObjectsByType<BistroBuilderGoodsReceivingRoute>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        BistroBuilderGoodsReceivingRoute existing = null;
        for (int index = 0; index < routes.Length; index++)
        {
            if (routes[index] != null &&
                routes[index].gameObject.scene == scene)
            {
                if (existing != null)
                {
                    throw new InvalidOperationException(
                        "La escena contiene más de una ruta de recepción 2.2B."
                    );
                }

                existing = routes[index];
            }
        }

        if (existing != null)
        {
            EnsureRouteReferences(existing);
            return existing;
        }

        var root = new GameObject(RouteRootName);
        Undo.RegisterCreatedObjectUndo(root, "Crear ruta de recepción 2.2B");
        SceneManager.MoveGameObjectToScene(root, scene);

        var supply = new GameObject(SupplyPointName);
        Undo.RegisterCreatedObjectUndo(supply, "Crear acceso de suministros");
        supply.transform.SetParent(root.transform, false);

        var warehouse = new GameObject(WarehousePointName);
        Undo.RegisterCreatedObjectUndo(
            warehouse,
            "Crear punto del almacén genérico"
        );
        warehouse.transform.SetParent(root.transform, false);

        ResolveDefaultRoutePositions(
            out Vector3 supplyPosition,
            out Vector3 warehousePosition
        );
        supply.transform.position = supplyPosition;
        warehouse.transform.position = warehousePosition;

        BistroBuilderGoodsReceivingRoute route =
            Undo.AddComponent<BistroBuilderGoodsReceivingRoute>(root);
        SetReference(route, "supplyAccessPoint", supply.transform);
        SetReference(route, "warehouseDropPoint", warehouse.transform);
        return route;
    }

    private static void EnsureRouteReferences(
        BistroBuilderGoodsReceivingRoute route
    )
    {
        if (route == null)
        {
            throw new ArgumentNullException(nameof(route));
        }

        Transform supply = route.SupplyAccessPoint;
        Transform warehouse = route.WarehouseDropPoint;

        if (supply == null)
        {
            Transform found = route.transform.Find(SupplyPointName);
            if (found == null)
            {
                var go = new GameObject(SupplyPointName);
                Undo.RegisterCreatedObjectUndo(go, "Reparar acceso de suministros");
                go.transform.SetParent(route.transform, false);
                found = go.transform;
            }
            supply = found;
            SetReference(route, "supplyAccessPoint", supply);
        }

        if (warehouse == null)
        {
            Transform found = route.transform.Find(WarehousePointName);
            if (found == null)
            {
                var go = new GameObject(WarehousePointName);
                Undo.RegisterCreatedObjectUndo(go, "Reparar almacén genérico");
                go.transform.SetParent(route.transform, false);
                found = go.transform;
            }
            warehouse = found;
            SetReference(route, "warehouseDropPoint", warehouse);
        }

        Vector3 delta = warehouse.position - supply.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.25f)
        {
            ResolveDefaultRoutePositions(
                out Vector3 supplyPosition,
                out Vector3 warehousePosition
            );
            supply.position = supplyPosition;
            warehouse.position = warehousePosition;
        }
    }

    private static void ResolveDefaultRoutePositions(
        out Vector3 supplyPosition,
        out Vector3 warehousePosition
    )
    {
        RestaurantArea kitchen = FindAreaByType("kitchen");
        RestaurantArea dining = FindAreaByType("dining");

        if (kitchen == null || !TryGetAreaBounds(kitchen, out Bounds kitchenBounds))
        {
            supplyPosition = new Vector3(-3f, 0f, 0f);
            warehousePosition = new Vector3(0f, 0f, 0f);
            return;
        }

        Vector3 kitchenCenter = kitchenBounds.center;
        Vector3 outward = dining != null &&
            TryGetAreaBounds(dining, out Bounds diningBounds)
                ? kitchenCenter - diningBounds.center
                : -kitchen.transform.forward;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.0001f)
        {
            outward = Vector3.back;
        }
        outward.Normalize();

        float edgeDistance =
            Mathf.Abs(outward.x) * kitchenBounds.extents.x +
            Mathf.Abs(outward.z) * kitchenBounds.extents.z;
        edgeDistance = Mathf.Max(0.75f, edgeDistance);

        float floorY = kitchen.transform.position.y;
        warehousePosition = kitchenCenter + outward * (edgeDistance * 0.30f);
        supplyPosition = kitchenCenter + outward * (edgeDistance + 1.0f);
        warehousePosition.y = floorY;
        supplyPosition.y = floorY;
    }

    private static RestaurantArea FindAreaByType(string areaTypeId)
    {
        RestaurantArea[] areas = Object.FindObjectsByType<RestaurantArea>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        Scene activeScene = SceneManager.GetActiveScene();
        for (int index = 0; index < areas.Length; index++)
        {
            RestaurantArea area = areas[index];
            if (area != null && area.gameObject.scene == activeScene &&
                area.Definition != null &&
                string.Equals(
                    area.Definition.AreaTypeId,
                    areaTypeId,
                    StringComparison.Ordinal
                ))
            {
                return area;
            }
        }

        return null;
    }

    private static bool TryGetAreaBounds(
        RestaurantArea area,
        out Bounds bounds
    )
    {
        bounds = default;
        if (area == null || area.BoundaryColliders == null)
        {
            return false;
        }

        bool hasBounds = false;
        for (int index = 0; index < area.BoundaryColliders.Count; index++)
        {
            Collider collider = area.BoundaryColliders[index];
            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    internal static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + propertyName + "."
            );
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T Require<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            throw new InvalidOperationException(
                "GameSystems necesita " + typeof(T).Name + "."
            );
        }
        return component;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static void RestoreScene(
        string scenePath,
        string absoluteScenePath,
        byte[] backup
    )
    {
        try
        {
            File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.ImportAsset(
                scenePath,
                ImportAssetOptions.ForceUpdate
            );
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception restoreException)
        {
            Debug.LogException(restoreException);
        }
    }
}
