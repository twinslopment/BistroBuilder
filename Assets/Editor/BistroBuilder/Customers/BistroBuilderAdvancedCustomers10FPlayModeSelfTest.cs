using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedCustomers10FPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Customers.10F.Play.Stage";
    private const string SuccessKey = "BB.Customers.10F.Play.Success";
    private const string GroupKey = "BB.Customers.10F.Play.Group";
    private const string ReportPath = "Customers10FPlayModeReport.txt";
    private static double startedAt;

    static BistroBuilderAdvancedCustomers10FPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Customers/10F - PlayMode real", false, 10053)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 10F ya está ejecutándose.");
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
        if (stage.StartsWith("wait_", StringComparison.Ordinal))
            Poll(commandLine);
    }

    private static void Initialize(bool commandLine)
    {
        SessionState.SetString(StageKey, commandLine ? "running_cli" : "running_menu");
        var assignment = UnityEngine.Object.FindFirstObjectByType<TableAssignmentSystem>();
        var profiles = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderAdvancedCustomerProfileService>();
        var behavior = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderAdvancedCustomerBehaviorService>();
        var tracking = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderCustomerExperienceTrackingService>();
        string error = string.Empty;
        if (assignment == null || profiles == null || behavior == null || tracking == null ||
            !profiles.ValidateConfiguration(out error) ||
            !behavior.ValidateConfiguration(out error))
        {
            Finish(false, "10F: faltan autoridades runtime. " + error, commandLine);
            return;
        }

        int groupId = FindDiagnosticGroupId(assignment);
        GameObject host = new GameObject("BB_10F_DiagnosticGroup");
        CustomerGroup group = host.AddComponent<CustomerGroup>();
        if (!group.Initialize(groupId, 2) ||
            !assignment.RegisterCustomerGroup(group))
        {
            UnityEngine.Object.Destroy(host);
            Finish(false, "10F: no pudo registrarse el grupo diagnóstico.", commandLine);
            return;
        }

        group.SetState(CustomerGroupState.WaitingForWaiter);
        if (!profiles.TryRefreshGroupProfile(group, out error))
        {
            assignment.UnregisterCustomerGroup(group);
            UnityEngine.Object.Destroy(host);
            Finish(false, "10F: no pudo generarse el perfil real. " + error, commandLine);
            return;
        }

        SessionState.SetInt(GroupKey, groupId);
        Time.timeScale = 60f;
        startedAt = EditorApplication.timeSinceStartup;
        SessionState.SetString(StageKey, commandLine ? "wait_cli" : "wait_menu");
    }

    private static void Poll(bool commandLine)
    {
        int groupId = SessionState.GetInt(GroupKey, 0);
        var assignment = UnityEngine.Object.FindFirstObjectByType<TableAssignmentSystem>();
        var behavior = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderAdvancedCustomerBehaviorService>();
        var tracking = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderCustomerExperienceTrackingService>();
        CustomerGroup group = FindGroup(groupId);

        if (assignment == null || behavior == null || tracking == null || group == null)
        {
            Finish(false, "10F: se perdió el fixture runtime.", commandLine);
            return;
        }

        if (behavior.TryGetBehavior(groupId, out var state) && state != null &&
            tracking.TryGetRuntimeVisit(groupId, out var visit) && visit != null &&
            state.individuals != null && state.individuals.Count == 2 &&
            state.dominantReason == BistroBuilderCustomerBehaviorReason.WaiterWait &&
            state.maximumPressureBasisPoints > 0 &&
            state.dominantMood != BistroBuilderCustomerBehaviorMood.Calm &&
            visit.waiterWaitSeconds > 0f)
        {
            bool individual = state.individuals[0].customerId != state.individuals[1].customerId &&
                state.individuals[0].patiencePressureBasisPoints !=
                state.individuals[1].patiencePressureBasisPoints;
            Cleanup(assignment, group);
            Finish(individual,
                individual
                    ? "PASS — la espera real de camarero consume paciencia y dos clientes reaccionan individualmente."
                    : "10F: la reacción runtime perdió individualidad.",
                commandLine);
            return;
        }

        if (EditorApplication.timeSinceStartup - startedAt > 8d)
        {
            Cleanup(assignment, group);
            Finish(false, "10F: timeout esperando presión de paciencia real.", commandLine);
        }
    }

    private static int FindDiagnosticGroupId(TableAssignmentSystem assignment)
    {
        int candidate = 910000;
        IReadOnlyList<CustomerGroup> groups = assignment.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
            if (groups[i] != null && groups[i].GroupId >= candidate)
                candidate = groups[i].GroupId + 1;
        return candidate;
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

    private static void Cleanup(TableAssignmentSystem assignment, CustomerGroup group)
    {
        Time.timeScale = 1f;
        if (assignment != null && group != null)
            assignment.UnregisterCustomerGroup(group);
        if (group != null) UnityEngine.Object.Destroy(group.gameObject);
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (!EditorApplication.isPlaying) return;
        Time.timeScale = 1f;
        string report = "=== BISTRO BUILDER — 10F / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
