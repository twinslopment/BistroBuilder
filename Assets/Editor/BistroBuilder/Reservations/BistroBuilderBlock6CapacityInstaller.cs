using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Amplía la sala base para que Reservas se pruebe con capacidad real.
/// Reutiliza exclusivamente mesas, sillas y Waiter canónicos existentes.
/// </summary>
public static class BistroBuilderBlock6CapacityInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Reservations/6X - Expand dining capacity";
    private const string MainScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string TablePrefabPath =
        "Assets/Prefabs/Restaurant/Furniture/Table_Basic.prefab";
    private const string ChairPrefabPath =
        "Assets/Prefabs/Restaurant/Generated/Furniture/SillaBistroDeMadera.prefab";
    private const string ConfigFolder =
        "Assets/Data/Restaurant/Seating/TableConfigurations";
    public const string FourSeatConfigPath =
        ConfigFolder + "/TableSeatingConfiguration_TableBasic4.asset";
    public const string ExpansionRootName = "BB_Block6_DiningExpansion";

    private static readonly TableSpec[] TableSpecs =
    {        new TableSpec(5, 4, new Vector3(-5.5f, 0.5f, -2.0f)),
        new TableSpec(6, 4, new Vector3(-2.25f, 0.5f, -2.0f)),
        new TableSpec(7, 2, new Vector3(1.0f, 0.5f, -2.0f)),
        new TableSpec(8, 2, new Vector3(3.75f, 0.5f, -2.0f)),
        new TableSpec(9, 4, new Vector3(-4.0f, 0.5f, 1.1f)),
        new TableSpec(10, 4, new Vector3(0.0f, 0.5f, 1.1f))
    };

    private static readonly WaiterSpec[] WaiterSpecs =
    {
        new WaiterSpec(3, new Vector3(-4.0f, 0f, 0.0f)),
        new WaiterSpec(4, new Vector3(-1.0f, 0f, 0.0f))
    };

    [MenuItem(MenuPath, false, 600)]
    public static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — Bloque 6", report, "Aceptar");
            return;
        }

        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Bloque 6", report, "Aceptar");
    }

    /// <summary>
    /// Entrada determinista para CI/batchmode. Abre siempre la escena principal
    /// y reutiliza exactamente el mismo instalador transaccional del menú.
    /// </summary>
    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            throw new InvalidOperationException(report);
        }

        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de ampliar la sala.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar.";
            return false;
        }

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);
        bool configExisted = File.Exists(Path.GetFullPath(FourSeatConfigPath));
        byte[] configBackup = configExisted
            ? File.ReadAllBytes(Path.GetFullPath(FourSeatConfigPath))
            : null;
        string configMetaPath = FourSeatConfigPath + ".meta";
        bool metaExisted = File.Exists(Path.GetFullPath(configMetaPath));
        byte[] metaBackup = metaExisted
            ? File.ReadAllBytes(Path.GetFullPath(configMetaPath))
            : null;
        try
        {
            GameObject tablePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabPath);
            GameObject chairPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ChairPrefabPath);
            if (tablePrefab == null || chairPrefab == null)
                throw new InvalidOperationException(
                    "No están disponibles los prefabs canónicos de mesa/silla.");

            RestaurantArea diningArea = FindDiningArea(scene);
            if (diningArea == null)
                throw new InvalidOperationException(
                    "No existe exactamente un área dining_main válida.");

            RestaurantTableSeatingConfigurationDefinition twoSeatConfig =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantTableSeatingConfigurationDefinition>(
                    "Assets/Data/Restaurant/Seating/TableConfigurations/" +
                    "TableSeatingConfiguration_TableBasic2.asset");
            if (twoSeatConfig == null)
                throw new InvalidOperationException(
                    "No existe la configuración canónica de mesa de 2 plazas.");

            RestaurantTableSeatingConfigurationDefinition fourSeatConfig =
                EnsureFourSeatConfiguration();
            Transform root = EnsureExpansionRoot(scene).transform;
            foreach (TableSpec spec in TableSpecs)
            {
                EnsureTableGroup(
                    scene,
                    root,
                    diningArea,
                    tablePrefab,
                    chairPrefab,
                    spec,
                    spec.Capacity == 4 ? fourSeatConfig : twoSeatConfig);
            }

            EnsureWaiters(scene, root, diningArea);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la ampliación.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderBlock6CapacityValidation result =
                BistroBuilderBlock6CapacityValidator.ValidateCurrentScene();
            if (result.Errors > 0)
                throw new InvalidOperationException(result.BuildReport());

            report = "Capacidad Bloque 6 instalada.\n" + result.BuildReport();
            return true;
        }
        catch (Exception exception)
        {            File.WriteAllBytes(absoluteScene, sceneBackup);
            RestoreOptionalAsset(FourSeatConfigPath, configExisted, configBackup);
            RestoreOptionalAsset(configMetaPath, metaExisted, metaBackup);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            report = "La ampliación falló y fue restaurada. " + exception.Message;
            Debug.LogException(exception);
            return false;
        }
    }

    private static RestaurantTableSeatingConfigurationDefinition
        EnsureFourSeatConfiguration()
    {
        RestaurantTableSeatingConfigurationDefinition config =
            AssetDatabase.LoadAssetAtPath<
                RestaurantTableSeatingConfigurationDefinition>(FourSeatConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<
                RestaurantTableSeatingConfigurationDefinition>();
            AssetDatabase.CreateAsset(config, FourSeatConfigPath);
        }

        SerializedObject serialized = new SerializedObject(config);
        SetString(serialized, "configurationId", "table_basic_4_rectangular");
        SetString(serialized, "displayName", "Mesa básica rectangular de 4 clientes");
        SetInt(serialized, "maximumCustomers", 4);        SetInt(serialized, "shape", (int)RestaurantTableSeatingShape.Rectangular);
        SetBool(serialized, "usePlacementFootprintDimensions", true);
        SetInt(serialized, "positiveZSeats", 2);
        SetInt(serialized, "negativeZSeats", 2);
        SetInt(serialized, "positiveXSeats", 0);
        SetInt(serialized, "negativeXSeats", 0);
        SetFloat(serialized, "sideEndInset", 0.10f);
        SetFloat(serialized, "minimumSpacePerCustomer", 0.55f);
        SetFloat(serialized, "parkedGapFromTableEdge", 0.10f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
    }

    private static GameObject EnsureExpansionRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, ExpansionRootName, StringComparison.Ordinal))
                return root;
        }

        GameObject created = new GameObject(ExpansionRootName);
        SceneManager.MoveGameObjectToScene(created, scene);
        created.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return created;
    }
    private static void EnsureTableGroup(
        Scene scene,
        Transform root,
        RestaurantArea diningArea,
        GameObject tablePrefab,
        GameObject chairPrefab,
        TableSpec spec,
        RestaurantTableSeatingConfigurationDefinition seatingDefinition)
    {
        string tableName = "BB_B6_Table_" + spec.TableId.ToString("00");
        RestaurantTable table = FindTable(scene, spec.TableId);
        if (table == null)
        {
            GameObject tableObject = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab, scene);
            tableObject.name = tableName;
            tableObject.transform.SetParent(root, true);
            table = tableObject.GetComponent<RestaurantTable>();
        }
        if (table == null)
            throw new InvalidOperationException(tableName + " no expone RestaurantTable.");

        table.gameObject.name = tableName;
        table.transform.SetPositionAndRotation(spec.Position, Quaternion.identity);
        table.AssignTableId(spec.TableId);
        SerializedObject tableSerialized = new SerializedObject(table);
        SetInt(tableSerialized, "capacity", spec.Capacity);
        tableSerialized.ApplyModifiedPropertiesWithoutUndo();

        RestaurantAreaMember tableArea = table.GetComponent<RestaurantAreaMember>();
        tableArea?.SetArea(diningArea);
        RestaurantPlaceableObject tablePlaceable =
            table.GetComponent<RestaurantPlaceableObject>();
        tablePlaceable?.AssignInstanceId("b6_table_" + spec.TableId.ToString("00"));

        RestaurantTableSeatingConfiguration configuration =
            table.GetComponent<RestaurantTableSeatingConfiguration>();
        RestaurantPlacementFootprint footprint =
            table.GetComponent<RestaurantPlacementFootprint>();
        if (configuration == null || footprint == null || tablePlaceable == null)
            throw new InvalidOperationException(tableName + " carece de configuración colocable/seating.");

        SerializedObject configSerialized = new SerializedObject(configuration);
        SetObject(configSerialized, "table", table);
        SetObject(configSerialized, "placementFootprint", footprint);
        SetObject(configSerialized, "definition", seatingDefinition);
        SetObject(configSerialized, "seatingCenter", tablePlaceable.PlacementAnchor);
        configSerialized.ApplyModifiedPropertiesWithoutUndo();

        if (!configuration.ValidateConfiguration(out string configError))
            throw new InvalidOperationException(tableName + ": " + configError);

        var slots = new List<RestaurantTableSeatSlot>(spec.Capacity);
        if (configuration.WriteCurrentSlots(slots) != spec.Capacity)
            throw new InvalidOperationException(tableName + " no genera todas sus plazas.");

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            EnsureChair(scene, root, diningArea, chairPrefab, table, slots[slotIndex]);
    }
    private static void EnsureChair(
        Scene scene,
        Transform root,
        RestaurantArea diningArea,
        GameObject chairPrefab,
        RestaurantTable table,
        RestaurantTableSeatSlot slot)
    {
        string chairName = "BB_B6_Chair_T" + table.TableId.ToString("00") +
                           "_S" + slot.SlotIndex.ToString("00");
        RestaurantSeat seat = FindSeat(scene, chairName);
        if (seat == null)
        {
            GameObject chairObject = (GameObject)PrefabUtility.InstantiatePrefab(chairPrefab, scene);
            chairObject.name = chairName;
            chairObject.transform.SetParent(root, true);
            seat = chairObject.GetComponent<RestaurantSeat>();
        }
        if (seat == null)
            throw new InvalidOperationException(chairName + " no expone RestaurantSeat.");

        Quaternion rotation = seat.CalculateRootRotationForFacingDirection(slot.FacingDirection);
        Vector3 position = seat.CalculateRootPositionForAssociationAtPose(
            slot.AssociationPosition, rotation);
        seat.transform.SetPositionAndRotation(position, rotation);
        seat.gameObject.name = chairName;

        RestaurantAreaMember area = seat.GetComponent<RestaurantAreaMember>();
        area?.SetArea(diningArea);
        seat.PlaceableObject?.AssignInstanceId(
            "b6_chair_t" + table.TableId.ToString("00") +
            "_s" + slot.SlotIndex.ToString("00"));
        if (!seat.ValidateConfiguration(out string seatError) ||
            !table.GetComponent<RestaurantTableSeatingConfiguration>()
                .TryEvaluateSeatAgainstSlot(
                    seat, seat.transform.position, seat.transform.rotation, slot, out _))
            throw new InvalidOperationException(chairName + ": " + seatError);
    }

    private static void EnsureWaiters(
        Scene scene,
        Transform root,
        RestaurantArea diningArea)
    {
        Waiter source = null;
        Waiter[] existing = FindSceneComponents<Waiter>(scene);
        foreach (Waiter waiter in existing)
        {
            if (waiter != null && waiter.WaiterId == 2) { source = waiter; break; }
        }
        if (source == null && existing.Length > 0) source = existing[0];
        if (source == null)
            throw new InvalidOperationException("No existe Waiter canónico que reutilizar.");

        foreach (WaiterSpec spec in WaiterSpecs)
        {
            Waiter waiter = FindWaiter(scene, spec.WaiterId);
            if (waiter == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(clone, scene);
                clone.transform.SetParent(root, true);
                waiter = clone.GetComponent<Waiter>();
            }            if (waiter == null)
                throw new InvalidOperationException("El clon operativo no contiene Waiter.");

            waiter.gameObject.name = "BB_B6_Waiter_" + spec.WaiterId.ToString("00");
            waiter.transform.SetPositionAndRotation(spec.Position, Quaternion.identity);
            SerializedObject waiterSerialized = new SerializedObject(waiter);
            SetInt(waiterSerialized, "waiterId", spec.WaiterId);
            waiterSerialized.ApplyModifiedPropertiesWithoutUndo();
            RestaurantAreaMember area = waiter.GetComponent<RestaurantAreaMember>();
            area?.SetArea(diningArea);
        }
    }

    private static RestaurantArea FindDiningArea(Scene scene)
    {
        RestaurantArea found = null;
        foreach (RestaurantArea area in FindSceneComponents<RestaurantArea>(scene))
        {
            if (area == null || !string.Equals(
                    area.AreaId, "dining_main", StringComparison.Ordinal)) continue;
            if (found != null) return null;
            found = area;
        }
        return found;
    }

    private static RestaurantTable FindTable(Scene scene, int tableId)
    {
        foreach (RestaurantTable table in FindSceneComponents<RestaurantTable>(scene))
            if (table != null && table.TableId == tableId) return table;
        return null;
    }
    private static RestaurantSeat FindSeat(Scene scene, string name)
    {
        foreach (RestaurantSeat seat in FindSceneComponents<RestaurantSeat>(scene))
            if (seat != null && string.Equals(seat.name, name, StringComparison.Ordinal))
                return seat;
        return null;
    }

    private static Waiter FindWaiter(Scene scene, int waiterId)
    {
        foreach (Waiter waiter in FindSceneComponents<Waiter>(scene))
            if (waiter != null && waiter.WaiterId == waiterId) return waiter;
        return null;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] values = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < values.Length; index++)
                if (values[index] != null) result.Add(values[index]);
        }
        return result.ToArray();
    }

    private static void RestoreOptionalAsset(
        string path, bool existed, byte[] backup)
    {
        string absolute = Path.GetFullPath(path);
        if (existed) File.WriteAllBytes(absolute, backup);
        else if (File.Exists(absolute)) File.Delete(absolute);
    }
    private static SerializedProperty Require(SerializedObject serialized, string name)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            throw new InvalidOperationException(
                serialized.targetObject.name + " no expone " + name + ".");
        return property;
    }

    private static void SetString(SerializedObject s, string n, string v) =>
        Require(s, n).stringValue = v;
    private static void SetInt(SerializedObject s, string n, int v) =>
        Require(s, n).intValue = v;
    private static void SetFloat(SerializedObject s, string n, float v) =>
        Require(s, n).floatValue = v;
    private static void SetBool(SerializedObject s, string n, bool v) =>
        Require(s, n).boolValue = v;
    private static void SetObject(SerializedObject s, string n, UnityEngine.Object v) =>
        Require(s, n).objectReferenceValue = v;

    private readonly struct TableSpec
    {
        public readonly int TableId;
        public readonly int Capacity;
        public readonly Vector3 Position;
        public TableSpec(int tableId, int capacity, Vector3 position)
        {
            TableId = tableId;
            Capacity = capacity;
            Position = position;
        }
    }
    private readonly struct WaiterSpec
    {
        public readonly int WaiterId;
        public readonly Vector3 Position;
        public WaiterSpec(int waiterId, Vector3 position)
        {
            WaiterId = waiterId;
            Position = position;
        }
    }
}