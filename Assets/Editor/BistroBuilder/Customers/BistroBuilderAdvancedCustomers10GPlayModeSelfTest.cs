using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedCustomers10GPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string PrefabPath = "Assets/Prefabs/Customers/CustomerGroupPrefab.prefab";
    private const string StageKey = "BB.Customers.10G.Play.Stage";
    private const string SuccessKey = "BB.Customers.10G.Play.Success";
    private const string GroupKey = "BB.Customers.10G.Play.Group";
    private const string ReportPath = "Customers10GPlayModeReport.txt";
    private static double startedAt;

    static BistroBuilderAdvancedCustomers10GPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Customers/10G - PlayMode real", false, 10063)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 10G ya está ejecutándose.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetInt(GroupKey, 0);
        SessionState.SetString(StageKey, commandLine ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stage)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "init_cli" : "init_menu");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool success = SessionState.GetBool(SuccessKey, false);
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            SessionState.EraseString(StageKey);
            SessionState.EraseInt(GroupKey);
            if (cli) EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
        if (stage.StartsWith("init_", StringComparison.Ordinal) && Time.frameCount >= 4)
        {
            Initialize(commandLine);
            return;
        }
        if (stage.StartsWith("wait_live_", StringComparison.Ordinal))
            PollLiveUpdate(commandLine);
    }

    private static void Initialize(bool commandLine)
    {
        SessionState.SetString(StageKey, commandLine ? "running_cli" : "running_menu");
        var assignment = UnityEngine.Object.FindFirstObjectByType<TableAssignmentSystem>();
        var profiles = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderAdvancedCustomerProfileService>();
        var inspection = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderAdvancedCustomerInspectionService>();
        var controller = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderAdvancedCustomerInspectionController>();
        Camera camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
        string error = string.Empty;
        if (assignment == null || profiles == null || inspection == null ||
            controller == null || camera == null ||
            !controller.ValidateConfiguration(out error))
        {
            Finish(false, "10G: faltan autoridades de inspección. " + error, commandLine);
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Finish(false, "10G: no existe CustomerGroupPrefab.", commandLine);
            return;
        }

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.name = "BB_10G_DiagnosticGroup";
        CustomerGroup group = instance.GetComponent<CustomerGroup>();
        int groupId = FindDiagnosticGroupId(assignment);
        if (group == null || !group.Initialize(groupId, 3))
        {
            UnityEngine.Object.Destroy(instance);
            Finish(false, "10G: no pudo inicializarse el grupo visual.", commandLine);
            return;
        }

        DisableAutonomousFlows(instance);
        instance.transform.position = camera.transform.position +
            camera.transform.forward * 8f;
        if (!assignment.RegisterCustomerGroup(group))
        {
            UnityEngine.Object.Destroy(instance);
            Finish(false, "10G: no pudo registrarse el grupo visual.", commandLine);
            return;
        }
        group.SetState(CustomerGroupState.WaitingForWaiter);
        if (!profiles.TryRefreshGroupProfile(group, out error))
        {
            Cleanup(assignment, group, controller);
            Finish(false, "10G: no pudo generar perfiles. " + error, commandLine);
            return;
        }

        var visuals = instance.GetComponent<
            BistroBuilderAdvancedCustomerMemberVisualGroup>();
        if (visuals == null || !visuals.EnsureVisuals() || visuals.VisualCount != 3)
        {
            Cleanup(assignment, group, controller);
            Finish(false, "10G: no se materializaron tres clientes visuales.", commandLine);
            return;
        }

        var target1 = visuals.GetHitTarget(1);
        var target2 = visuals.GetHitTarget(2);
        var target3 = visuals.GetHitTarget(3);
        if (target1 == null || target2 == null || target3 == null ||
            target1.transform.position == target2.transform.position ||
            target2.transform.position == target3.transform.position ||
            !target2.ValidateConfiguration(out error))
        {
            Cleanup(assignment, group, controller);
            Finish(false, "10G: los destinos de selección individual son inválidos. " +
                error, commandLine);
            return;
        }

        if (!inspection.TryBuildSnapshot(group, 1, out var first, out error) ||
            !inspection.TryBuildSnapshot(group, 2, out var second, out error) ||
            first.displayName == second.displayName)
        {
            Cleanup(assignment, group, controller);
            Finish(false, "10G: los nombres individuales no son distinguibles. " +
                error, commandLine);
            return;
        }

        Physics.SyncTransforms();
        Vector3 screen = camera.WorldToScreenPoint(target2.transform.position);
        if (screen.z <= 0f || !controller.TryInspectAtScreenPoint(
                new Vector2(screen.x, screen.y), out error) ||
            !controller.IsOpen || controller.CurrentSnapshot == null ||
            controller.CurrentSnapshot.memberIndex != 2 ||
            controller.CurrentDisplayName != second.displayName)
        {
            Cleanup(assignment, group, controller);
            Finish(false, "10G: el raycast real no abrió la ficha del miembro 2. " +
                error, commandLine);
            return;
        }

        group.SetState(CustomerGroupState.WaitingForFood);
        SessionState.SetInt(GroupKey, groupId);
        startedAt = EditorApplication.timeSinceStartup;
        SessionState.SetString(
            StageKey, commandLine ? "wait_live_cli" : "wait_live_menu");
    }

    private static void PollLiveUpdate(bool commandLine)
    {
        int groupId = SessionState.GetInt(GroupKey, 0);
        var assignment = UnityEngine.Object.FindFirstObjectByType<TableAssignmentSystem>();
        var controller = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderAdvancedCustomerInspectionController>();
        CustomerGroup group = FindGroup(groupId);
        if (assignment == null || controller == null || group == null)
        {
            Finish(false, "10G: se perdió el fixture durante la actualización viva.",
                commandLine);
            return;
        }

        var snapshot = controller.CurrentSnapshot;
        if (controller.IsOpen && snapshot != null &&
            snapshot.memberIndex == 2 &&
            snapshot.serviceState == CustomerGroupState.WaitingForFood &&
            snapshot.serviceStateLabel.Contains("esperando comida",
                StringComparison.OrdinalIgnoreCase))
        {
            Cleanup(assignment, group, controller);
            Finish(true,
                "PASS — tres clientes visuales son seleccionables individualmente; " +
                "el clic por raycast abre al miembro correcto y la ficha cambia en vivo " +
                "de espera de camarero a pedido realizado/esperando comida.",
                commandLine);
            return;
        }

        if (EditorApplication.timeSinceStartup - startedAt > 4d)
        {
            Cleanup(assignment, group, controller);
            Finish(false, "10G: la ficha no actualizó el estado de servicio en vivo.",
                commandLine);
        }
    }

    private static int FindDiagnosticGroupId(TableAssignmentSystem assignment)
    {
        int candidate = 920000;
        IReadOnlyList<CustomerGroup> groups = assignment.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
            if (groups[i] != null && groups[i].GroupId >= candidate)
                candidate = groups[i].GroupId + 1;
        return candidate;
    }

    private static void DisableAutonomousFlows(GameObject instance)
    {
        if (instance == null) return;
        CustomerArrivalFlow arrival = instance.GetComponent<CustomerArrivalFlow>();
        CustomerSeatingFlow seating = instance.GetComponent<CustomerSeatingFlow>();
        CustomerDiningFlow dining = instance.GetComponent<CustomerDiningFlow>();
        CustomerExitFlow exit = instance.GetComponent<CustomerExitFlow>();
        CustomerMovementView movement = instance.GetComponent<CustomerMovementView>();
        if (arrival != null) arrival.enabled = false;
        if (seating != null) seating.enabled = false;
        if (dining != null) dining.enabled = false;
        if (exit != null) exit.enabled = false;
        if (movement != null) movement.enabled = false;
    }

    private static CustomerGroup FindGroup(int groupId)
    {
        CustomerGroup[] groups = UnityEngine.Object.FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < groups.Length; i++)
            if (groups[i] != null && groups[i].GroupId == groupId)
                return groups[i];
        return null;
    }

    private static void Cleanup(
        TableAssignmentSystem assignment,
        CustomerGroup group,
        BistroBuilderAdvancedCustomerInspectionController controller)
    {
        controller?.Close();
        if (assignment != null && group != null)
            assignment.UnregisterCustomerGroup(group);
        if (group != null) UnityEngine.Object.Destroy(group.gameObject);
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (!EditorApplication.isPlaying) return;
        string report = "=== BISTRO BUILDER — 10G / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
