using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedCustomers10APlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Customers.10A.Play.Stage";
    private const string SuccessKey = "BB.Customers.10A.Play.Success";
    private const string GroupKey = "BB.Customers.10A.Play.Group";
    private const string ReportPath = "Customers10APlayModeReport.txt";
    private static double startedAt;

    static BistroBuilderAdvancedCustomers10APlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Customers/10A - PlayMode real", false, 10003)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 10A ya está ejecutándose.");
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
        var serviceState = UnityEngine.Object.FindFirstObjectByType<
            RestaurantServiceStateService>();
        var spawner = UnityEngine.Object.FindFirstObjectByType<CustomerGroupSpawner>();
        var profiles = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderAdvancedCustomerProfileService>();
        string profileError = string.Empty;
        if (serviceState == null || spawner == null || profiles == null ||
            !profiles.ValidateConfiguration(out profileError))
        {
            Finish(false, "10A: faltan autoridades runtime. " + profileError, commandLine);
            return;
        }

        if (!serviceState.TryOpenService())
        {
            Finish(false, "10A: no pudo abrirse el servicio real.", commandLine);
            return;
        }

        if (!spawner.TrySpawnExternalTableServiceGroup(
                3, out CustomerGroup group, out string spawnError) || group == null)
        {
            Finish(false, "10A: no pudo materializar un grupo real. " + spawnError,
                commandLine);
            return;
        }

        var acquisitionTag = group.GetComponent<BistroBuilderCustomerAcquisitionTag>();
        if (acquisitionTag == null)
            acquisitionTag = group.gameObject.AddComponent<BistroBuilderCustomerAcquisitionTag>();
        var acquisition = BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        acquisition.segmentId = "workers";
        acquisition.sourceSystemId = "customers.10a.playmode";
        acquisition.sourceReferenceId = "group." + group.GroupId;
        string acquisitionError = string.Empty;
        string initialRefreshError = string.Empty;
        if (!acquisitionTag.TryConfigure(acquisition, out acquisitionError) ||
            !profiles.TryRefreshGroupProfile(group, out initialRefreshError))
        {
            Finish(false, "10A: no pudo aplicar perfil a grupo real. " +
                acquisitionError + " " + initialRefreshError, commandLine);
            return;
        }

        SessionState.SetInt(GroupKey, group.GroupId);
        startedAt = EditorApplication.timeSinceStartup;
        SessionState.SetString(StageKey, commandLine ? "wait_cli" : "wait_menu");
    }

    private static void Poll(bool commandLine)
    {
        int groupId = SessionState.GetInt(GroupKey, 0);
        var profiles = UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderAdvancedCustomerProfileService>();
        CustomerGroup group = FindGroup(groupId);
        if (profiles == null || group == null)
        {
            Finish(false, "10A: se perdió el grupo o el servicio de perfiles.", commandLine);
            return;
        }

        if (!profiles.TryGetGroupProfile(groupId, out var profile) || profile == null)
        {
            if (EditorApplication.timeSinceStartup - startedAt > 8d)
                Finish(false, "10A: timeout esperando perfil individual runtime.", commandLine);
            return;
        }

        bool cardinality = profile.members != null && profile.members.Count == group.GroupSize;
        bool identities = cardinality && profile.members[0].customerId != profile.members[1].customerId;
        bool heterogeneous = cardinality &&
            (Math.Abs(profile.members[0].tableWaitToleranceSeconds -
                      profile.members[1].tableWaitToleranceSeconds) > 0.001f ||
             profile.members[0].archetypeId != profile.members[1].archetypeId);
        if (!cardinality || !identities || !heterogeneous || profile.segmentId != "workers")
        {
            Finish(false, "10A: el grupo real no recibió perfiles individuales coherentes.",
                commandLine);
            return;
        }

        var tag = group.GetComponent<BistroBuilderCustomerAcquisitionTag>();
        var returning = tag != null
            ? tag.CreateSnapshot()
            : BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        returning.segmentId = "localresidents";
        returning.returningVisit = true;
        returning.guestRelationsReferenceId = "guest_cohort_000777";
        returning.sourceSystemId = "guest_relations.state";
        returning.sourceReferenceId = "guest_cohort_000777";
        returning.discoverySourceId = "returning_guest";

        string tagError = string.Empty;
        string refreshError = string.Empty;
        if (tag == null || !tag.TryConfigure(returning, out tagError) ||
            !profiles.TryRefreshGroupProfile(group, out refreshError) ||
            !profiles.TryGetGroupProfile(groupId, out var returningProfile) ||
            returningProfile == null || !returningProfile.returningVisit ||
            returningProfile.returningReferenceId != "guest_cohort_000777" ||
            returningProfile.members[0].customerId != "guest_cohort_000777.member01")
        {
            Finish(false, "10A: el perfil habitual no conservó identidad estable. " +
                tagError + " " + refreshError, commandLine);
            return;
        }

        Finish(true,
            "PASS — CustomerGroup real de 3 personas recibe 3 perfiles individuales " +
            "heterogéneos y una cohorte habitual conserva identidad estable.",
            commandLine);
    }

    private static CustomerGroup FindGroup(int groupId)
    {
        CustomerGroup[] groups = UnityEngine.Object.FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < groups.Length; i++)
            if (groups[i] != null && groups[i].GroupId == groupId)
                return groups[i];
        return null;
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (!EditorApplication.isPlaying) return;
        string report = "=== BISTRO BUILDER — 10A / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
