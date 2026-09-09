using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderAdvancedKitchen12SelfTest
{
    [MenuItem("Tools/Bistro Builder/Kitchen/12 - Autotest", false, 12002)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder("BLOQUE 12 — AUTOTEST\n");

        Check(BistroBuilderAdvancedKitchenPolicy.ResolveLoad(1, 0, 2, false) ==
            BistroBuilderKitchenLoadState.Fluid, "Carga fluida", ref passed, ref failed, log);
        Check(BistroBuilderAdvancedKitchenPolicy.ResolveLoad(2, 1, 2, false) ==
            BistroBuilderKitchenLoadState.Loaded, "Carga cargada", ref passed, ref failed, log);
        Check(BistroBuilderAdvancedKitchenPolicy.ResolveLoad(2, 5, 2, false) ==
            BistroBuilderKitchenLoadState.Saturated, "Carga saturada", ref passed, ref failed, log);
        Check(BistroBuilderAdvancedKitchenPolicy.ResolveLoad(1, 1, 2, true) ==
            BistroBuilderKitchenLoadState.Blocked, "Carga bloqueada", ref passed, ref failed, log);
        Check(BistroBuilderAdvancedKitchenPolicy.ApplySpeed(10f, 20000) < 5.01f,
            "Equipo rápido reduce tiempo", ref passed, ref failed, log);
        Check(BistroBuilderAdvancedKitchenPolicy.StableRoll("kitchen12", 10000) ==
            BistroBuilderAdvancedKitchenPolicy.StableRoll("kitchen12", 10000),
            "Incidencias deterministas", ref passed, ref failed, log);

        var station = new BistroBuilderKitchenStationDefinition
        {
            stationId = "station.test",
            displayName = "Test",
            kind = BistroBuilderKitchenStationKind.Range,
            baseCapacity = 2,
            speedBasisPoints = 10000,
            reliabilityBasisPoints = 9950,
            equipmentTier = 2
        };
        Check(station.TryValidate(out _), "Definición de estación", ref passed, ref failed, log);

        var cook = new BistroBuilderEmployeeRecord
        {
            employeeId = "emp_11111111111111111111111111111111",
            roleId = "cook",
            experiencePoints = 900,
            skills = new BistroBuilderEmployeeSkillSet
            {
                speed = 80, attentiveness = 80, organization = 75, hospitality = 50
            }
        };
        int skilledQuality = BistroBuilderAdvancedKitchenPolicy.ResolveQuality(
            cook, station, BistroBuilderKitchenLoadState.Fluid, BistroBuilderKitchenIncidentKind.None);
        int baseQuality = BistroBuilderAdvancedKitchenPolicy.ResolveQuality(
            null, station, BistroBuilderKitchenLoadState.Fluid, BistroBuilderKitchenIncidentKind.None);
        Check(skilledQuality > baseQuality, "Cocinero experto mejora calidad", ref passed, ref failed, log);
        int saturatedQuality = BistroBuilderAdvancedKitchenPolicy.ResolveQuality(
            cook, station, BistroBuilderKitchenLoadState.Saturated, BistroBuilderKitchenIncidentKind.None);
        Check(saturatedQuality < skilledQuality, "Saturación penaliza calidad", ref passed, ref failed, log);

        BistroBuilderKitchenStationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderKitchenStationCatalog>(
                BistroBuilderAdvancedKitchen12Seed.CatalogPath);
        Check(catalog != null && catalog.TryValidate(out _),
            "Catálogo canónico válido", ref passed, ref failed, log);
        if (catalog != null)
        {
            var route = new List<string>();
            bool routeOk = catalog.Routes.Count > 0 &&
                catalog.TryResolveRoute(catalog.Routes[0].dishId, route) && route.Count > 0;
            Check(routeOk, "Rutas de preparación resolubles", ref passed, ref failed, log);
        }
        else
        {
            Check(false, "Rutas de preparación resolubles", ref passed, ref failed, log);
        }

        BistroBuilderKitchenRuntimeSnapshot multi = BuildAdvancedSnapshot(false);
        Check(multi.TryValidate(out _), "Persistencia multicapacidad válida", ref passed, ref failed, log);
        BistroBuilderKitchenRuntimeSnapshot duplicate = BuildAdvancedSnapshot(true);
        Check(!duplicate.TryValidate(out _), "Persistencia rechaza slots duplicados", ref passed, ref failed, log);

        Check(Enum.IsDefined(typeof(BistroBuilderKitchenIntakeMode), BistroBuilderKitchenIntakeMode.Paused),
            "Modo de pausa definido", ref passed, ref failed, log);
        Check(Enum.IsDefined(typeof(BistroBuilderKitchenIncidentKind), BistroBuilderKitchenIncidentKind.EquipmentFailure),
            "Averías definidas", ref passed, ref failed, log);

        report = log.Append("Resultado: ").Append(passed).Append(" OK / ")
            .Append(failed).Append(" fallos.").ToString();
        return failed == 0;
    }

    private static BistroBuilderKitchenRuntimeSnapshot BuildAdvancedSnapshot(bool duplicateSlot)
    {
        var snapshot = new BistroBuilderKitchenRuntimeSnapshot
        {
            kitchenId = "kitchen_test",
            nextSequence = 2,
            advancedEnabled = true,
            advancedIntakeMode = (int)BistroBuilderKitchenIntakeMode.Normal
        };
        snapshot.workItems.Add(Work("line_12_a", 0, 0));
        snapshot.workItems.Add(Work("line_12_b", 1, duplicateSlot ? 0 : 1));
        snapshot.stationStates.Add(new BistroBuilderKitchenStationRuntimeSaveData
        {
            stationId = "station.range",
            blockedSeconds = 0f,
            lastIncidentKind = 0
        });
        return snapshot;
    }

    private static BistroBuilderKitchenLineWorkSaveData Work(
        string lineId,
        long sequence,
        int slot)
    {
        return new BistroBuilderKitchenLineWorkSaveData
        {
            canonicalOrderId = "order_12_test",
            orderLineId = lineId,
            dishId = "dish_12_test",
            legacyOrderId = 12,
            sequence = sequence,
            totalDurationSeconds = 2f,
            remainingDurationSeconds = 1f,
            wasActive = true,
            advanced = true,
            stationId = "station.range",
            stationSlotIndex = slot,
            stageIndex = 0,
            stageCount = 1,
            priority = (int)BistroBuilderKitchenPriorityKind.Normal,
            qualityBasisPoints = 7000,
            incidentKind = 0
        };
    }

    private static void Check(
        bool condition,
        string name,
        ref int passed,
        ref int failed,
        StringBuilder log)
    {
        if (condition) { passed++; log.Append("OK — ").AppendLine(name); }
        else { failed++; log.Append("FAIL — ").AppendLine(name); }
    }
}
