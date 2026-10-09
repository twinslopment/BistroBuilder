using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Verifies that the historical exhaustive dock algorithm and optimized
/// exact algorithm produce the same reachable state, route kind, length
/// and ordered world-space waypoints, in actual Unity Play Mode.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2NavigationEquivalencePlayModeSelfTest
{
    private const string Stage = "BB.B11.NavEquivalence.Stage";
    private const string Result = "BB.B11.NavEquivalence.Result";
    public const string ReportFile = "EditorV2_B11_NavigationEquivalence_Report.txt";

    static BistroBuilderEditorV2NavigationEquivalencePlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandleState;
        EditorApplication.playModeStateChanged += HandleState;
    }

    [MenuItem("Bistro Builder/QA/Editor V2/B11 Navigation Old vs Optimized")]
    public static void RunFromMenu() => Start(false);

    public static void RunFromCommandLine() => Start(true);

    private static void Start(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Already in Play Mode.");
        File.WriteAllText(Path.GetFullPath(ReportFile),
            "B11 Navigation equivalence started.\n");
        SessionState.SetBool(Result, false);
        SessionState.SetString(Stage,
            commandLine ? "enter_cli" : "enter_manual");
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandleState(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(Stage, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var host = new GameObject("__B11_NavigationEquivalenceRunner");
            host.AddComponent<BistroBuilderEditorV2NavEquivalenceDriver>();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(Stage);
            if (stage.EndsWith("cli", StringComparison.Ordinal))
                EditorApplication.Exit(SessionState.GetBool(Result, false) ? 0 : 1);
        }
    }

    public static void End(bool passed, IList<string> lines)
    {
        File.WriteAllLines(Path.GetFullPath(ReportFile), lines);
        SessionState.SetBool(Result, passed);
        string stage = SessionState.GetString(Stage, string.Empty);
        SessionState.SetString(Stage,
            stage.EndsWith("cli", StringComparison.Ordinal)
                ? "exit_cli" : "exit_manual");
        if (passed)
            UnityEngine.Debug.Log("[B11 EQUIVALENCE] PASS " +
                lines[lines.Count - 1]);
        else
            UnityEngine.Debug.LogError("[B11 EQUIVALENCE] FAIL " +
                lines[lines.Count - 1]);
        EditorApplication.ExitPlaymode();
    }
}

public sealed class BistroBuilderEditorV2NavEquivalenceDriver : MonoBehaviour
{
    private readonly struct Case
    {
        public readonly string id;
        public readonly Vector3 from, to;
        public readonly BistroBuilderNavigationAgentMask mask;
        public readonly float radius;

        public Case(string id, Vector3 from, Vector3 to,
            BistroBuilderNavigationAgentMask mask, float radius)
        {
            this.id = id; this.from = from; this.to = to;
            this.mask = mask; this.radius = radius;
        }
    }

    private static void AddCase(List<Case> cases,
        string id, Vector3 from, Vector3 to,
        BistroBuilderNavigationAgentMask agent, float radius = 0.28f)
    {
        cases.Add(new Case(id, from, to, agent, radius));
    }

    private IEnumerator Start()
    {
        var rows = new List<string>(96)
        {
            "B11 NAVIGATION EXACT OLD-vs-OPTIMIZED PLAY MODE",
            "Checked dimensions: reachability, kind, length (1 mm), every waypoint (1 mm)."
        };
        for (int f = 0; f < 14; f++) yield return null;

        var nav = Object.FindFirstObjectByType<
            BistroBuilderNavigationService>(FindObjectsInactive.Include);
        var placeables = Object.FindFirstObjectByType<
            RestaurantPlaceableRegistry>(FindObjectsInactive.Include);
        var edit = Object.FindFirstObjectByType<
            RestaurantEditModeService>(FindObjectsInactive.Include);
        string configError = string.Empty;
        if (nav == null || placeables == null || edit == null ||
            !nav.ValidateConfiguration(out configError))
        {
            rows.Add("FAIL missing Navigation, restaurant or configuration: " +
                configError);
            BistroBuilderEditorV2NavigationEquivalencePlayModeSelfTest.End(
                false, rows);
            yield break;
        }
        if (!edit.IsEditModeActive &&
            !edit.TryEnterEditMode(out _, out string enterError))
        {
            rows.Add("FAIL cannot enter edit mode: " + enterError);
            BistroBuilderEditorV2NavigationEquivalencePlayModeSelfTest.End(
                false, rows);
            yield break;
        }

        var specs = new List<Case>(64);
        GameObject door = GameObject.Find("RestaurantEntrancePoint");
        var tables = Object.FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        var kitchens = Object.FindObjectsByType<KitchenSystem>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

        if (door != null)
            foreach (var table in tables)
                if (table != null && table.CustomerApproachPoint != null)
                    AddCase(specs, "customer_" + table.TableId,
                        door.transform.position,
                        table.CustomerApproachPoint.position,
                        BistroBuilderNavigationAgentMask.Customer);

        for (int kitchenIndex = 0; kitchenIndex < kitchens.Length; kitchenIndex++)
        {
            var kitchen = kitchens[kitchenIndex];
            if (kitchen == null || kitchen.PickupPoint == null) continue;
            foreach (var table in tables)
                if (table != null && table.WaiterServicePoint != null)
                    AddCase(specs, "waiter_" + kitchenIndex +
                        "_" + table.TableId,
                        kitchen.PickupPoint.position,
                        table.WaiterServicePoint.position,
                        BistroBuilderNavigationAgentMask.Waiter);
        }

        var receiving = Object.FindFirstObjectByType<
            BistroBuilderGoodsReceivingRoute>();
        if (receiving != null && receiving.SupplyAccessPoint != null &&
            receiving.WarehouseDropPoint != null)
            AddCase(specs, "warehouse",
                receiving.SupplyAccessPoint.position,
                receiving.WarehouseDropPoint.position,
                BistroBuilderNavigationAgentMask.Delivery);

        int standardCount = specs.Count;
        // Adversarial endpoints: nearby but not identical to the configured
        // dock, plus increased mobility radius and blocked-looking offsets.
        int variations = Math.Min(8, standardCount);
        for (int i = 0; i < variations; i++)
        {
            Case example = specs[i];
            AddCase(specs, example.id + "_offset", example.from,
                example.to + new Vector3(0.55f, 0f, -0.37f),
                example.mask, example.radius);
            AddCase(specs, example.id + "_wide",
                example.from + new Vector3(-0.21f, 0f, 0.16f),
                example.to, example.mask, 0.55f);
        }

        var before = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        foreach (var p in placeables.RegisteredPlaceables)
            if (p != null) before[p.InstanceId] = p.transform.position;

        int revision = nav.Revision;
        var legacyPoints = new List<Vector3>(64);
        var optimizedPoints = new List<Vector3>(64);
        int passed = 0, failed = 0;
        long legacyTotalMs = 0, optimizedTotalMs = 0;
        var watch = new Stopwatch();

        for (int i = 0; i < specs.Count; i++)
        {
            Case sample = specs[i];
            try
            {
                nav.B11UseLegacyDockForQA = true;
                legacyPoints.Clear();
                watch.Restart();
                bool legacyOk = nav.TryBuildRoute(
                    "health:eq:" + sample.id, sample.mask,
                    sample.from, sample.to, sample.radius, legacyPoints,
                    out float legacyMeters,
                    out BistroBuilderNavigationRouteKind legacyKind);
                watch.Stop();
                long oldMs = watch.ElapsedMilliseconds;
                legacyTotalMs += oldMs;

                nav.B11UseLegacyDockForQA = false;
                optimizedPoints.Clear();
                watch.Restart();
                bool optimizedOk = nav.TryBuildRoute(
                    "health:eq:" + sample.id, sample.mask,
                    sample.from, sample.to, sample.radius, optimizedPoints,
                    out float optimizedMeters,
                    out BistroBuilderNavigationRouteKind optimizedKind);
                watch.Stop();
                long newMs = watch.ElapsedMilliseconds;
                optimizedTotalMs += newMs;

                bool same = legacyOk == optimizedOk &&
                    legacyKind == optimizedKind &&
                    Mathf.Abs(legacyMeters - optimizedMeters) <= 0.001f &&
                    legacyPoints.Count == optimizedPoints.Count;
                if (same)
                    for (int j = 0; j < legacyPoints.Count; j++)
                        same &= (legacyPoints[j] -
                                 optimizedPoints[j]).sqrMagnitude < 0.000001f;

                if (same) passed++; else failed++;
                rows.Add((same ? "PASS" : "FAIL") + " " + sample.id +
                    " old=" + oldMs + "ms optimized=" + newMs + "ms" +
                    " ok=" + legacyOk + "/" + optimizedOk +
                    " kind=" + legacyKind + "/" + optimizedKind +
                    " meters=" + legacyMeters.ToString("F4") +
                    "/" + optimizedMeters.ToString("F4") +
                    " waypoints=" + legacyPoints.Count +
                    "/" + optimizedPoints.Count);
            }
            catch (Exception ex)
            {
                failed++;
                rows.Add("FAIL " + sample.id + " exception=" +
                    ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                nav.B11UseLegacyDockForQA = false;
            }
            // Allow the Editor to update while ensuring each pair used
            // exactly the same geometry and agent state in a single frame.
            yield return null;
        }

        bool unchanged = nav.Revision == revision &&
            placeables.RegisteredPlaceables.Count == before.Count;
        foreach (var placed in placeables.RegisteredPlaceables)
            if (placed == null || !before.TryGetValue(placed.InstanceId,
                    out var position) || placed.transform.position != position)
                unchanged = false;
        rows.Add("BASE_CASES=" + standardCount +
                 " ADVERSARIAL_CASES=" + (specs.Count - standardCount) +
                 " OLD_MS=" + legacyTotalMs +
                 " OPTIMIZED_MS=" + optimizedTotalMs +
                 " NAVIGATION_UNCHANGED=" + unchanged);
        bool allPassed = failed == 0 && unchanged &&
            standardCount >= 21 && passed == specs.Count;
        rows.Add("RESULT: " + passed + " PASS / " + failed +
                 " FAIL; COMPARISON=" + (allPassed ? "PASS" : "FAIL"));
        BistroBuilderEditorV2NavigationEquivalencePlayModeSelfTest.End(
            allPassed, rows);
    }
}
