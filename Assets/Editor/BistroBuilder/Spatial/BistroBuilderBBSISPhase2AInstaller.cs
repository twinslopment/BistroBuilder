using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fase 2A: materializa BBSIS sobre mesas, sillas y puertas reales.
/// No altera reglas de seating, apertura ni navegación.
/// </summary>
public static class BistroBuilderBBSISPhase2AInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ContractFolder =
        "Assets/Resources/BistroBuilder/Spatial/Contracts";

    [MenuItem("Bistro Builder/BBSIS/Fase 2A/Instalar")]
    public static void Install()
    {
        BistroBuilderBBSISPhase1Installer.Install();
        EnsureFolder(ContractFolder);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null)
            throw new InvalidOperationException("BBSIS 2A: falta GameSystems.");

        Dictionary<string, BistroBuilderSpatialContractDefinition> seatContracts =
            BuildSeatContracts();
        Dictionary<string, BistroBuilderSpatialContractDefinition> tableContracts =
            BuildTableContracts();
        BistroBuilderSpatialContractDefinition doorContract = BuildDoorContract();

        BindCurrentSeats(seatContracts);
        BindCurrentTables(tableContracts);
        BindCurrentDoors(doorContract);

        BistroBuilderSpatialAssessmentService assessment =
            EnsureComponent<BistroBuilderSpatialAssessmentService>(systems);
        BistroBuilderSpatialRuntimeBinder binder =
            EnsureComponent<BistroBuilderSpatialRuntimeBinder>(systems);
        binder.ConfigureForEditor(
            ToBindings(seatContracts),
            ToBindings(tableContracts),
            doorContract);

        BistroBuilderSpatialInteractionService service =
            systems.GetComponent<BistroBuilderSpatialInteractionService>();
        if (service == null)
            throw new InvalidOperationException("BBSIS 2A: falta autoridad espacial de Fase 1.");
        service.RebuildSubjects();
        assessment.EvaluateCurrentLayout();

        EditorUtility.SetDirty(assessment);
        EditorUtility.SetDirty(binder);
        EditorUtility.SetDirty(systems);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Bistro Builder/BBSIS/Fase 2A/Instalar y validar")]
    public static void InstallAndValidate()
    {
        Install();
        BistroBuilderBBSISPhase2AValidator.Run();
        BistroBuilderBBSISPhase2ASelfTest.Run();
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BBSIS FASE 2A - INSTALACION PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static Dictionary<string, BistroBuilderSpatialContractDefinition>
        BuildSeatContracts()
    {
        var result = new Dictionary<string, BistroBuilderSpatialContractDefinition>(
            StringComparer.Ordinal);
        string[] guids = AssetDatabase.FindAssets("t:RestaurantSeatUseProfileDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RestaurantSeatUseProfileDefinition profile =
                AssetDatabase.LoadAssetAtPath<RestaurantSeatUseProfileDefinition>(path);
            if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileId)) continue;
            string key = profile.ProfileId;
            BistroBuilderSpatialContractDefinition contract = GetOrCreateContract(
                "Seat_" + Sanitize(key));
            contract.ConfigureForEditor(
                "spatial.contract.seat." + key,
                "seating.chair",
                BistroBuilderAdaptiveSpatialProxyMode.Articulated,
                new[] { "seating.chair", "dynamic.sweep", "seat.bay", "interaction.approach" });
            contract.SetContractVersionForEditor(2);
            contract.ClearSemanticGeometryForEditor();
            contract.AddPortForEditor(new BistroBuilderSpatialPortDefinition
            {
                portId = "seat",
                kind = BistroBuilderSpatialPortKind.SeatBay,
                radius = Mathf.Max(0.18f, profile.CustomerApproachRadius),
                conflictMode = BistroBuilderSpatialConflictMode.Reservable
            });
            contract.AddPortForEditor(new BistroBuilderSpatialPortDefinition
            {
                portId = "approach",
                kind = BistroBuilderSpatialPortKind.Interaction,
                radius = profile.CustomerApproachRadius,
                conflictMode = BistroBuilderSpatialConflictMode.Degrade
            });
            EditorUtility.SetDirty(contract);
            result[key] = contract;
        }
        return result;
    }

    private static Dictionary<string, BistroBuilderSpatialContractDefinition>
        BuildTableContracts()
    {
        var result = new Dictionary<string, BistroBuilderSpatialContractDefinition>(
            StringComparer.Ordinal);
        RestaurantTableSeatingConfiguration[] sceneTables =
            UnityEngine.Object.FindObjectsByType<RestaurantTableSeatingConfiguration>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        string[] guids = AssetDatabase.FindAssets(
            "t:RestaurantTableSeatingConfigurationDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RestaurantTableSeatingConfigurationDefinition definition =
                AssetDatabase.LoadAssetAtPath<RestaurantTableSeatingConfigurationDefinition>(path);
            if (definition == null || string.IsNullOrWhiteSpace(definition.ConfigurationId))
                continue;
            string key = definition.ConfigurationId;
            BistroBuilderSpatialContractDefinition contract = GetOrCreateContract(
                "Table_" + Sanitize(key));
            contract.ConfigureForEditor(
                "spatial.contract.table." + key,
                "seating.table",
                BistroBuilderAdaptiveSpatialProxyMode.Layered,
                new[] { "seating.table", "seat.bays", "service.table" });
            contract.SetContractVersionForEditor(2);
            contract.ClearSemanticGeometryForEditor();

            RestaurantTableSeatingConfiguration representative = null;
            for (int tableIndex = 0; tableIndex < sceneTables.Length; tableIndex++)
            {
                if (sceneTables[tableIndex] != null &&
                    ReferenceEquals(sceneTables[tableIndex].Definition, definition))
                {
                    representative = sceneTables[tableIndex];
                    break;
                }
            }
            if (representative != null)
                AddTablePorts(contract, representative);
            EditorUtility.SetDirty(contract);
            result[key] = contract;
        }
        return result;
    }

    private static void AddTablePorts(
        BistroBuilderSpatialContractDefinition contract,
        RestaurantTableSeatingConfiguration table)
    {
        var slots = new List<RestaurantTableSeatSlot>(16);
        table.WriteCurrentSlots(slots);
        for (int i = 0; i < slots.Count; i++)
        {
            RestaurantTableSeatSlot slot = slots[i];
            Vector3 localForward = table.transform.InverseTransformDirection(
                slot.FacingDirection);
            contract.AddPortForEditor(new BistroBuilderSpatialPortDefinition
            {
                portId = "seat." + slot.SlotIndex,
                kind = BistroBuilderSpatialPortKind.SeatBay,
                localPosition = table.transform.InverseTransformPoint(
                    slot.AssociationPosition),
                localForward = localForward.sqrMagnitude > 0.000001f
                    ? localForward.normalized
                    : Vector3.forward,
                radius = 0.28f,
                conflictMode = BistroBuilderSpatialConflictMode.Reservable
            });
        }
    }

    private static BistroBuilderSpatialContractDefinition BuildDoorContract()
    {
        BistroBuilderSpatialContractDefinition contract = GetOrCreateContract("Door_Standard");
        contract.ConfigureForEditor(
            "spatial.contract.door.standard",
            "architecture.door",
            BistroBuilderAdaptiveSpatialProxyMode.Simple,
            new[] { "architecture.door", "dynamic.sweep", "traversal.gate" });
        contract.SetContractVersionForEditor(2);
        contract.ClearSemanticGeometryForEditor();
        contract.AddGateForEditor(new BistroBuilderSpatialGateDefinition
        {
            gateId = "door.passage",
            localStart = Vector3.left * 0.45f,
            localEnd = Vector3.right * 0.45f,
            minimumWidth = 0.75f,
            criticalRoute = true
        });
        EditorUtility.SetDirty(contract);
        return contract;
    }

    private static void BindCurrentSeats(
        Dictionary<string, BistroBuilderSpatialContractDefinition> contracts)
    {
        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < seats.Length; i++)
        {
            RestaurantSeat seat = seats[i];
            if (seat == null || seat.UseProfile == null) continue;
            RemoveMissingScripts(seat.gameObject);
            RestaurantPlaceableObject placeable =
                seat.GetComponent<RestaurantPlaceableObject>();
            string subjectId = ResolveSubjectId(
                seat.gameObject,
                placeable,
                "spatial.seat.");
            if (!contracts.TryGetValue(seat.UseProfile.ProfileId, out var contract))
                throw new InvalidOperationException(
                    "Falta Spatial Contract para SeatUseProfile " + seat.UseProfile.ProfileId + ".");
            BistroBuilderSpatialBindingUtility.BindSeat(
                seat,
                contract,
                subjectId);
            MarkSpatialComponentsDirty(seat.gameObject);
        }
    }

    private static void BindCurrentTables(
        Dictionary<string, BistroBuilderSpatialContractDefinition> contracts)
    {
        RestaurantTableSeatingConfiguration[] tables =
            UnityEngine.Object.FindObjectsByType<RestaurantTableSeatingConfiguration>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        for (int i = 0; i < tables.Length; i++)
        {
            RestaurantTableSeatingConfiguration table = tables[i];
            if (table == null || table.Definition == null) continue;
            RemoveMissingScripts(table.gameObject);
            RestaurantPlaceableObject placeable =
                table.GetComponent<RestaurantPlaceableObject>();
            string subjectId = ResolveSubjectId(
                table.gameObject,
                placeable,
                "spatial.table.");
            if (!contracts.TryGetValue(table.Definition.ConfigurationId, out var contract))
                throw new InvalidOperationException(
                    "Falta Spatial Contract para TableConfiguration " +
                    table.Definition.ConfigurationId + ".");
            BistroBuilderSpatialBindingUtility.BindTable(
                table,
                contract,
                subjectId);
            MarkSpatialComponentsDirty(table.gameObject);
        }
    }

    private static void BindCurrentDoors(BistroBuilderSpatialContractDefinition contract)
    {
        BistroBuilderNavigableDoor[] doors =
            UnityEngine.Object.FindObjectsByType<BistroBuilderNavigableDoor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        for (int i = 0; i < doors.Length; i++)
        {
            BistroBuilderNavigableDoor door = doors[i];
            if (door == null) continue;
            RemoveMissingScripts(door.gameObject);
            BistroBuilderSpatialSubject existing =
                door.GetComponent<BistroBuilderSpatialSubject>();
            string subjectId = existing != null ? existing.SubjectId : string.Empty;
            if (string.IsNullOrWhiteSpace(subjectId))
                subjectId = "spatial.door." + Guid.NewGuid().ToString("N");
            BistroBuilderSpatialBindingUtility.BindDoor(door, contract, subjectId);
            MarkSpatialComponentsDirty(door.gameObject);
        }
    }

    private static string ResolveSubjectId(
        GameObject gameObject,
        RestaurantPlaceableObject placeable,
        string fallbackPrefix)
    {
        BistroBuilderSpatialSubject existing =
            gameObject.GetComponent<BistroBuilderSpatialSubject>();
        if (existing != null && !string.IsNullOrWhiteSpace(existing.SubjectId))
            return existing.SubjectId;
        if (placeable != null && placeable.HasInstanceId)
            return "spatial.placeable." + placeable.InstanceId;
        return fallbackPrefix + Guid.NewGuid().ToString("N");
    }
    private static List<BistroBuilderSpatialContractBinding> ToBindings(
        Dictionary<string, BistroBuilderSpatialContractDefinition> contracts)
    {
        var keys = new List<string>(contracts.Keys);
        keys.Sort(StringComparer.Ordinal);
        var result = new List<BistroBuilderSpatialContractBinding>(keys.Count);
        for (int i = 0; i < keys.Count; i++)
        {
            result.Add(new BistroBuilderSpatialContractBinding
            {
                key = keys[i],
                contract = contracts[keys[i]]
            });
        }
        return result;
    }

    private static BistroBuilderSpatialContractDefinition GetOrCreateContract(string suffix)
    {
        string path = ContractFolder + "/BB_SpatialContract_" + suffix + ".asset";
        BistroBuilderSpatialContractDefinition contract =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(path);
        if (contract != null) return contract;
        contract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
        AssetDatabase.CreateAsset(contract, path);
        return contract;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void RemoveMissingScripts(GameObject gameObject)
    {
        if (gameObject == null) return;
        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
    }

    private static void MarkSpatialComponentsDirty(GameObject gameObject)
    {
        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component is BistroBuilderSpatialSubject ||
                component is BistroBuilderAdaptiveSpatialProxy ||
                component is BistroBuilderSpatialPortAnchors ||
                component is BistroBuilderSeatSpatialAdapter ||
                component is BistroBuilderTableSpatialAdapter ||
                component is BistroBuilderDoorSpatialAdapter)
                EditorUtility.SetDirty(component);
        }
        EditorUtility.SetDirty(gameObject);
    }

    private static string Sanitize(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value)
            ? "unnamed"
            : value.Trim().ToLowerInvariant();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            normalized = normalized.Replace(invalid, '_');
        return normalized.Replace('.', '_').Replace(' ', '_');
    }

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace('\\', '/');
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
