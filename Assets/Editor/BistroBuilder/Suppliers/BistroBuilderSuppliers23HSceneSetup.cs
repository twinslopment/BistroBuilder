#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BistroBuilderSuppliers23HSceneSetup
{
    private const string RootName = "BB_SupplierDeliveryAnchors";

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3H - Crear/actualizar anclajes de escena")]
    public static void CreateOrUpdateAnchors()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Los anclajes 2.3H deben prepararse fuera de Play Mode.");
            return;
        }

        BistroBuilderSupplierDeliverySceneAnchors existing =
            Object.FindFirstObjectByType<BistroBuilderSupplierDeliverySceneAnchors>();
        GameObject root;
        if (existing != null)
        {
            root = existing.gameObject;
            Undo.RecordObject(existing, "Actualizar anclajes 2.3H");
        }
        else
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Crear anclajes 2.3H");
            existing = root.AddComponent<BistroBuilderSupplierDeliverySceneAnchors>();
        }

        Vector3 basePosition = ResolveBasePosition();
        Transform entry = EnsurePoint(root.transform, "VehicleEntry", basePosition + new Vector3(-12f, 0f, 0f));
        Transform entryWp = EnsurePoint(root.transform, "VehicleEntryWaypoint_01", basePosition + new Vector3(-6f, 0f, 0f));
        Transform parking = EnsurePoint(root.transform, "VehicleParking", basePosition);
        Transform exitWp = EnsurePoint(root.transform, "VehicleExitWaypoint_01", basePosition + new Vector3(6f, 0f, 0f));
        Transform exit = EnsurePoint(root.transform, "VehicleExit", basePosition + new Vector3(12f, 0f, 0f));
        Transform driverExit = EnsurePoint(root.transform, "DriverExitPoint", basePosition + new Vector3(-1.2f, 0f, -1.3f));
        Transform driverWp = EnsurePoint(root.transform, "DriverWarehouseWaypoint_01", basePosition + new Vector3(-1.2f, 0f, -4.0f));
        Transform warehouseDoor = EnsurePoint(root.transform, "WarehouseDoor", basePosition + new Vector3(-1.2f, 0f, -6.5f));
        Transform warehouseDropoff = EnsurePoint(root.transform, "WarehouseDropoff", basePosition + new Vector3(-1.2f, 0f, -8.0f));

        existing.vehicleEntry = entry;
        existing.vehicleParking = parking;
        existing.vehicleExit = exit;
        existing.driverExitPoint = driverExit;
        existing.warehouseDoor = warehouseDoor;
        existing.warehouseDropoff = warehouseDropoff;
        existing.vehicleEntryWaypoints.Clear();
        existing.vehicleEntryWaypoints.Add(entryWp);
        existing.vehicleExitWaypoints.Clear();
        existing.vehicleExitWaypoints.Add(exitWp);
        existing.driverToWarehouseWaypoints.Clear();
        existing.driverToWarehouseWaypoints.Add(driverWp);

        EditorUtility.SetDirty(existing);
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log(
            "2.3H anclajes de escena creados/actualizados. IMPORTANTE: coloca VehicleEntry/VehicleParking/VehicleExit en el acceso de suministros real y WarehouseDoor/WarehouseDropoff en el almacén real antes de la prueba visual definitiva.");
    }

    private static Vector3 ResolveBasePosition()
    {
        if (Selection.activeTransform != null) return Selection.activeTransform.position;
        string[] preferredNames = { "Warehouse", "Almacen", "Almacén", "Supply", "Suministros", "Receiving" };
        for (int n = 0; n < preferredNames.Length; n++)
        {
            GameObject found = GameObject.Find(preferredNames[n]);
            if (found != null) return found.transform.position + new Vector3(6f, 0f, 6f);
        }
        return Vector3.zero;
    }

    private static Transform EnsurePoint(Transform root, string name, Vector3 defaultPosition)
    {
        Transform found = root.Find(name);
        if (found != null) return found;
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Crear punto " + name);
        go.transform.SetParent(root, true);
        go.transform.position = defaultPosition;
        return go.transform;
    }
}
#endif
