using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedCustomers10DPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Customers.10D.Play.Stage";
    private const string SuccessKey = "BB.Customers.10D.Play.Success";
    private const string ReportPath = "Customers10DPlayModeReport.txt";

    private static BistroBuilderAdvancedCustomerHistoryService history;
    private static BistroBuilderAdvancedCustomerHistorySnapshot originalHistory;
    private static TableAssignmentSystem assignment;
    private static RestaurantTableRegistry registry;
    private static CustomerGroup group;
    private static RestaurantTable premiumTable;
    private static RestaurantTable standardTable;

    static BistroBuilderAdvancedCustomers10DPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Customers/10D - PlayMode real", false, 10033)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 10D ya está ejecutándose.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
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
            if (cli) EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("init_", StringComparison.Ordinal) || Time.frameCount < 4)
            return;
        RunScenario(stage.EndsWith("cli", StringComparison.Ordinal));
    }

    private static void RunScenario(bool commandLine)
    {
        SessionState.SetString(StageKey, commandLine ? "running_cli" : "running_menu");
        try
        {
            var seating = Find<BistroBuilderAdvancedCustomerSeatingPreferenceService>();
            var profiles = Find<BistroBuilderAdvancedCustomerProfileService>();
            history = Find<BistroBuilderAdvancedCustomerHistoryService>();
            assignment = Find<TableAssignmentSystem>();
            registry = Find<RestaurantTableRegistry>();
            if (seating == null || profiles == null || history == null ||
                assignment == null || registry == null)
            {
                Finish(false, "10D: faltan autoridades runtime.", commandLine);
                return;
            }
            string configurationError = string.Empty;
            if (!seating.ValidateConfiguration(out configurationError))
            {
                Finish(false, "10D: configuración inválida. " + configurationError,
                    commandLine);
                return;
            }

            originalHistory = history.CreateSnapshot();
            if (!TryFindAccessibleCohort(profiles, out string cohortId))
            {
                Finish(false, "10D: no pudo construir identidad accesible determinista.",
                    commandLine);
                return;
            }
            if (!InstallVipHistoryFixture(cohortId, out string historyError))
            {
                Finish(false, "10D: no pudo preparar historial VIP. " + historyError,
                    commandLine);
                return;
            }

            if (!CreateFixtureTables(out string tableError))
            {
                Finish(false, "10D: no pudo preparar mesas de prueba. " + tableError,
                    commandLine);
                return;
            }
            if (!CreateFixtureGroup(cohortId, out string groupError))
            {
                Finish(false, "10D: no pudo preparar grupo VIP. " + groupError,
                    commandLine);
                return;
            }

            if (!seating.TryBuildPreference(group, out var preference, out string prefError) ||
                preference == null || !preference.isVip ||
                (preference.specialNeeds &
                 BistroBuilderCustomerSpecialNeed.AccessibleSeating) == 0)
            {
                Finish(false,
                    "10D: el grupo real no conserva VIP + accesibilidad. " + prefError,
                    commandLine);
                return;
            }

            if (!assignment.TryGetPreferredTable(group, out RestaurantTable reserved) ||
                !ReferenceEquals(reserved, premiumTable))
            {
                Finish(false,
                    "10D: el hook previo no reservó la mesa premium/accesible.",
                    commandLine);
                return;
            }

            group.SetState(CustomerGroupState.WaitingForTable);
            if (!group.HasAssignedTable || !ReferenceEquals(group.AssignedTable, premiumTable) ||
                ReferenceEquals(group.AssignedTable, standardTable))
            {
                Finish(false,
                    "10D: TableAssignment no respetó la preferencia canónica.",
                    commandLine);
                return;
            }

            Finish(true,
                "PASS — un cliente habitual VIP con necesidad accesible reserva y recibe " +
                "la mesa premium/accesible mediante TableAssignmentSystem; la mesa estándar " +
                "más cercana queda descartada.",
                commandLine);
        }
        catch (Exception exception)
        {
            Finish(false, "10D lanzó excepción: " + exception.Message, commandLine);
        }
    }

    private static bool TryFindAccessibleCohort(
        BistroBuilderAdvancedCustomerProfileService profiles,
        out string cohortId)
    {
        cohortId = string.Empty;
        if (profiles?.ProfileCatalog == null || profiles.GeneralGameStateService == null)
            return false;
        for (int i = 1; i <= 1000; i++)
        {
            string candidate = "guest_cohort_10d_" + i.ToString("D4");
            var acquisition = CreateReturningAcquisition(candidate);
            if (!BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
                    990010, 1, profiles.GeneralGameStateService.DayIndex,
                    acquisition, profiles.ProfileCatalog.Archetypes,
                    out var built, out _) || built?.members == null ||
                built.members.Count != 1)
                continue;
            if ((built.members[0].specialNeeds &
                 BistroBuilderCustomerSpecialNeed.AccessibleSeating) == 0)
                continue;
            cohortId = candidate;
            return true;
        }
        return false;
    }

    private static bool InstallVipHistoryFixture(string cohortId, out string error)
    {
        var snapshot = BistroBuilderAdvancedCustomerHistoryEngine.CreateEmptySnapshot();
        snapshot.revision = 1;
        snapshot.customers.Add(new BistroBuilderAdvancedCustomerHistoryRecord
        {
            cohortId = cohortId,
            segmentId = "highvalue",
            firstVisitDay = 1,
            lastVisitDay = 6,
            visitCount = 6,
            lifetimeSpendCents = 42000,
            satisfactionTotalBasisPoints = 48000,
            loyaltyTier = BistroBuilderCustomerLoyaltyTier.Vip,
            recentVisits = new List<BistroBuilderAdvancedCustomerVisitHistoryRecord>()
        });
        return history.TryRestoreSnapshot(snapshot, out error);
    }

    private static bool CreateFixtureTables(out string error)
    {
        error = string.Empty;
        GameObject premiumGo = new GameObject("BB10D_PremiumAccessibleTable");
        premiumGo.transform.position = new Vector3(12f, 0f, 0f);
        premiumTable = premiumGo.AddComponent<RestaurantTable>();
        premiumTable.AssignTableId(990001);
        var premiumProfile = premiumGo.AddComponent<BistroBuilderAdvancedCustomerTableProfile>();
        if (!premiumProfile.TryConfigure(
                BistroBuilderCustomerTableFeature.Accessible |
                BistroBuilderCustomerTableFeature.Quiet |
                BistroBuilderCustomerTableFeature.VipPreferred,
                new[] { "premium", "vip", "quiet", "accessible" }, out error))
            return false;

        GameObject standardGo = new GameObject("BB10D_StandardTable");
        standardGo.transform.position = Vector3.zero;
        standardTable = standardGo.AddComponent<RestaurantTable>();
        standardTable.AssignTableId(990002);
        var standardProfile = standardGo.AddComponent<BistroBuilderAdvancedCustomerTableProfile>();
        if (!standardProfile.TryConfigure(
                BistroBuilderCustomerTableFeature.None,
                new[] { "dining" }, out error))
            return false;

        if (!registry.RegisterTable(premiumTable))
        {
            error = "TableRegistry rechazó la mesa premium.";
            return false;
        }
        if (!registry.RegisterTable(standardTable))
        {
            error = "TableRegistry rechazó la mesa estándar.";
            return false;
        }
        return true;
    }

    private static bool CreateFixtureGroup(string cohortId, out string error)
    {
        error = string.Empty;
        GameObject go = new GameObject("BB10D_VipAccessibleGroup");
        go.transform.position = Vector3.zero;
        group = go.AddComponent<CustomerGroup>();
        if (!group.Initialize(990010, 1, BistroBuilderServiceMode.TableService))
        {
            error = "CustomerGroup rechazó el fixture.";
            return false;
        }
        var tag = go.AddComponent<BistroBuilderCustomerAcquisitionTag>();
        if (!tag.TryConfigure(CreateReturningAcquisition(cohortId), out error))
            return false;
        if (!assignment.RegisterCustomerGroup(group))
        {
            error = "TableAssignment rechazó el grupo fixture.";
            return false;
        }
        return true;
    }

    private static BistroBuilderCustomerAcquisitionProfile CreateReturningAcquisition(
        string cohortId)
    {
        return new BistroBuilderCustomerAcquisitionProfile
        {
            segmentId = "highvalue",
            sourceSystemId = "guest_relations.state",
            sourceReferenceId = cohortId,
            discoverySourceId = "returning_guest",
            returningVisit = true,
            guestRelationsReferenceId = cohortId,
            preferredGroupSize = 1
        };
    }

    private static void CleanupFixture()
    {
        if (group != null)
        {
            if (group.HasAssignedTable) group.ClearAssignedTable();
            assignment?.UnregisterCustomerGroup(group);
            UnityEngine.Object.Destroy(group.gameObject);
            group = null;
        }
        CleanupTable(ref premiumTable);
        CleanupTable(ref standardTable);
        if (history != null && originalHistory != null)
            history.TryRestoreSnapshot(originalHistory, out _);
        originalHistory = null;
    }

    private static void CleanupTable(ref RestaurantTable table)
    {
        if (table == null) return;
        registry?.UnregisterTable(table);
        UnityEngine.Object.Destroy(table.gameObject);
        table = null;
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        if (!EditorApplication.isPlaying) return;
        CleanupFixture();
        string report = "=== BISTRO BUILDER — 10D / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }

    private static T Find<T>() where T : UnityEngine.Object =>
        UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
}
