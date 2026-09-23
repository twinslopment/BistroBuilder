using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderActivityRuntimeSelfTest
{
    [MenuItem("Bistro Builder/Tests/Activity/Run Runtime V1 Self-Test")]
    public static void RunFromMenu()
    {
        int passed = 0;
        int failed = 0;
        var log = new List<string>();

        GameObject first = null;
        GameObject second = null;

        try
        {
            Check(ActivityEventCatalog.Validate(out string catalogError),
                "Catálogo válido: " + catalogError, ref passed, ref failed, log);
            Check(ActivityEventCatalog.All.Count == 63,
                "63 eventos V1 activos registrados", ref passed, ref failed, log);

            var families = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ActivityEventCatalog.All.Count; i++)
                families.Add(ActivityEventCatalog.All[i].IconFamilyKey);
            Check(families.Count == 28,
                "28 familias visuales canónicas", ref passed, ref failed, log);

            first = new GameObject("__BB_ACTIVITY_SELF_TEST_A__");
            ActivityFeedService feed = first.AddComponent<ActivityFeedService>();

            var tableOne = new ActivityTargetRef(ActivityTargetType.Table, 1);
            ActivityPublishResult firstBill = feed.PublishAt(
                ActivityEventId.TableBillRequested,
                new ActivityEventPayload().Set("table", 1),
                tableOne,
                1,
                600d);
            ActivityPublishResult duplicateBill = feed.PublishAt(
                ActivityEventId.TableBillRequested,
                new ActivityEventPayload().Set("table", 1),
                tableOne,
                1,
                600.5d);

            Check(firstBill.Accepted && duplicateBill.Deduplicated && feed.Count == 1,
                "Deduplicación EventId + TargetId", ref passed, ref failed, log);

            var ingredient = new ActivityTargetRef(
                ActivityTargetType.Ingredient,
                "tomato");
            ActivityPublishResult low = feed.PublishState(
                ActivityEventId.InventoryLow,
                "inventory.stock",
                ingredient,
                new ActivityEventPayload().Set("ingredient", "Tomate"));
            bool cleared = feed.ClearState("inventory.stock", ingredient);
            ActivityPublishResult lowAgain = feed.PublishState(
                ActivityEventId.InventoryLow,
                "inventory.stock",
                ingredient,
                new ActivityEventPayload().Set("ingredient", "Tomate"));

            Check(low.Accepted && cleared && lowAgain.Accepted,
                "Reentrada real de estado no queda bloqueada por dedup",
                ref passed, ref failed, log);

            feed.PublishAt(
                ActivityEventId.TableWaitingExcessive,
                new ActivityEventPayload().Set("table", 2),
                new ActivityTargetRef(ActivityTargetType.Table, 2),
                1,
                700d);
            feed.PublishAt(
                ActivityEventId.TableWaitingExcessive,
                new ActivityEventPayload().Set("table", 3),
                new ActivityTargetRef(ActivityTargetType.Table, 3),
                1,
                700.5d);

            var rows = new List<ActivityDisplayEntry>();
            new ActivityFeedAggregator().Build(
                feed.Events,
                ActivityFilter.Today,
                1,
                rows);

            ActivityDisplayEntry waitingGroup = rows.Find(
                row => row != null &&
                       row.Definition != null &&
                       row.Definition.EventId == ActivityEventId.TableWaitingExcessive);

            Check(waitingGroup != null &&
                  waitingGroup.AggregatedCount == 2 &&
                  waitingGroup.Targets.Count == 2,
                "Agrupación conserva todos los TargetIds",
                ref passed, ref failed, log);

            ActivityEventDefinition seatedDefinition =
                ActivityEventCatalog.GetRequired(ActivityEventId.TableGroupSeated);
            var seatedInstance = new ActivityEventInstance
            {
                sequence = 999,
                eventId = ActivityEventId.TableGroupSeated.Value,
                target = new ActivityTargetRef(ActivityTargetType.Table, 7),
                payload = new ActivityEventPayload().Set("table", 7),
                dayIndex = 1,
                minuteOfDay = 720d
            };
            var seatedRow = new ActivityDisplayEntry
            {
                Definition = seatedDefinition,
                Primary = seatedInstance
            };
            ActivityFormattedText formatted =
                new ActivityTemplateFormatter().Format(seatedRow);

            Check(formatted.Title == "Mesa 7" &&
                  formatted.Body == "Grupo sentado",
                "Formatter resuelve variables sin texto en gameplay",
                ref passed, ref failed, log);

            ActivityFeedSaveSnapshot snapshot = feed.CaptureSnapshot();
            second = new GameObject("__BB_ACTIVITY_SELF_TEST_B__");
            ActivityFeedService restored =
                second.AddComponent<ActivityFeedService>();

            bool restoredOk = restored.RestoreSnapshot(
                snapshot,
                target => true,
                out string restoreError);

            int beforeStateRepublish = restored.Count;
            ActivityPublishResult restoredState = restored.PublishState(
                ActivityEventId.InventoryLow,
                "inventory.stock",
                ingredient,
                new ActivityEventPayload().Set("ingredient", "Tomate"));

            Check(restoredOk && string.IsNullOrEmpty(restoreError),
                "Snapshot se rehidrata sin efectos",
                ref passed, ref failed, log);
            Check(restored.Count == beforeStateRepublish &&
                  restoredState.Deduplicated,
                "Carga reconstruye estados activos y evita republicación",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.Add("EXCEPTION · " + exception);
        }
        finally
        {
            if (first != null) UnityEngine.Object.DestroyImmediate(first);
            if (second != null) UnityEngine.Object.DestroyImmediate(second);
        }

        string summary =
            "ACTIVITY RUNTIME V1 SELF-TEST · PASS " + passed +
            " · FAIL " + failed + "\n" +
            string.Join("\n", log);

        if (failed == 0)
            Debug.Log(summary);
        else
            Debug.LogError(summary);
    }

    private static void Check(
        bool condition,
        string label,
        ref int passed,
        ref int failed,
        List<string> log)
    {
        if (condition)
        {
            passed++;
            log.Add("PASS · " + label);
        }
        else
        {
            failed++;
            log.Add("FAIL · " + label);
        }
    }
}
