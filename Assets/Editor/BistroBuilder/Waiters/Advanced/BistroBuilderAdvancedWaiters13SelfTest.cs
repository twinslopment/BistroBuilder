using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderAdvancedWaiters13SelfTest
{
    [MenuItem("Tools/Bistro Builder/Waiters/13 - Autotest", false, 13002)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0; failed = 0;
        var lines = new List<string>();
        GameObject tableObject = null;
        GameObject profileObject = null;
        GameObject routeObject = null;
        GameObject groupObject = null;
        GameObject waiterObject = null;
        try
        {
            tableObject = new GameObject("BB13_TestTable") { hideFlags = HideFlags.HideAndDontSave };
            RestaurantTable table = tableObject.AddComponent<RestaurantTable>();
            WaiterTask take = new WaiterTask(1, WaiterTaskType.TakeOrder, WaiterTaskPriority.Normal, table, null, 0);
            groupObject = new GameObject("BB13_TestGroup") { hideFlags = HideFlags.HideAndDontSave };
            CustomerGroup group = groupObject.AddComponent<CustomerGroup>();
            waiterObject = new GameObject("BB13_TestWaiter") { hideFlags = HideFlags.HideAndDontSave };
            Waiter testWaiter = waiterObject.AddComponent<Waiter>();
            RestaurantOrder testOrder = new RestaurantOrder(13001, table, group, testWaiter);
            WaiterTask food = new WaiterTask(2, WaiterTaskType.DeliverFood, WaiterTaskPriority.High, table,
                testOrder, "order_line_bb13test", 1);
            WaiterTask bill = new WaiterTask(3, WaiterTaskType.DeliverBill, WaiterTaskPriority.Normal, table, null, 2);
            WaiterTask urgent = new WaiterTask(4, WaiterTaskType.TakeOrder, WaiterTaskPriority.Urgent, table, null, 3);

            Check(BistroBuilderAdvancedWaiterPolicy.PriorityScore(WaiterTaskPriority.Urgent) >
                  BistroBuilderAdvancedWaiterPolicy.PriorityScore(WaiterTaskPriority.High), "Prioridades jerarquicas", ref passed, ref failed, lines);
            Check(BistroBuilderAdvancedWaiterPolicy.ResolveSaturation01(1, 2, 3) >= 0.99f,
                "Saturacion individual acumulativa", ref passed, ref failed, lines);
            Check(BistroBuilderAdvancedWaiterPolicy.ResolveSaturation(0f) == BistroBuilderWaiterSaturationLevel.Free,
                "Estado libre", ref passed, ref failed, lines);
            Check(BistroBuilderAdvancedWaiterPolicy.ResolveSaturation(1f) == BistroBuilderWaiterSaturationLevel.Saturated,
                "Estado saturado", ref passed, ref failed, lines);
            Check(BistroBuilderAdvancedWaiterPolicy.ResolveContextAction(take, 8f, 0.2f) == BistroBuilderWaiterContextActionKind.Apologize,
                "Accion disculparse", ref passed, ref failed, lines);
            Check(BistroBuilderAdvancedWaiterPolicy.ResolveContextAction(take, 15f, 0.2f) == BistroBuilderWaiterContextActionKind.CalmCustomer,
                "Accion calmar cliente", ref passed, ref failed, lines);
            Check(BistroBuilderAdvancedWaiterPolicy.ResolveContextAction(food, 6f, 0.2f) == BistroBuilderWaiterContextActionKind.ExplainDelay,
                "Accion explicar retraso", ref passed, ref failed, lines);
            Check(BistroBuilderAdvancedWaiterPolicy.ResolveContextAction(bill, 5f, 0.2f) == BistroBuilderWaiterContextActionKind.AccelerateBill,
                "Accion acelerar cuenta", ref passed, ref failed, lines);
            Check(BistroBuilderAdvancedWaiterPolicy.ResolveContextAction(urgent, 0f, 0.2f) == BistroBuilderWaiterContextActionKind.ReviewOrder,
                "Accion revisar pedido", ref passed, ref failed, lines);
            Check(BistroBuilderAdvancedWaiterPolicy.ResolveContextAction(take, 1f, 0.1f) == BistroBuilderWaiterContextActionKind.SuggestiveSale,
                "Venta sugerida contextual", ref passed, ref failed, lines);

            profileObject = new GameObject("BB13_TestProfile") { hideFlags = HideFlags.HideAndDontSave };
            BistroBuilderAdvancedWaiterProfile profile = profileObject.AddComponent<BistroBuilderAdvancedWaiterProfile>();
            profile.SupportsZone("dining", out BistroBuilderWaiterResponsibilityKind primary);
            profile.SupportsZone("bar", out BistroBuilderWaiterResponsibilityKind secondary);
            profile.SupportsZone("terrace", out BistroBuilderWaiterResponsibilityKind support);
            Check(primary == BistroBuilderWaiterResponsibilityKind.Primary, "Responsabilidad principal", ref passed, ref failed, lines);
            Check(secondary == BistroBuilderWaiterResponsibilityKind.Secondary, "Responsabilidad secundaria", ref passed, ref failed, lines);
            Check(support == BistroBuilderWaiterResponsibilityKind.Support, "Apoyo entre zonas", ref passed, ref failed, lines);
            Check(profile.SimultaneousPlanCapacity >= 3, "Multiples tareas simultaneas planificables", ref passed, ref failed, lines);
            bool restoredProfile = profile.TryRestorePersistentSettings(
                "bar", new List<string> { "dining", "terrace", "bar" }, 4, 1.1f, out _);
            Check(restoredProfile && profile.PrimaryZoneId == "bar" &&
                  profile.SimultaneousPlanCapacity == 4 &&
                  Mathf.Approximately(profile.ServiceEfficiency, 1.1f),
                "Persistencia de responsabilidad y capacidad", ref passed, ref failed, lines);
            profile.SupportsZone("dining", out BistroBuilderWaiterResponsibilityKind restoredSecondary);
            Check(restoredSecondary == BistroBuilderWaiterResponsibilityKind.Secondary,
                "Zonas secundarias persistentes", ref passed, ref failed, lines);

            var visit = new BistroBuilderReputationVisitRuntimeRecord
            {
                groupId = 1,
                partySize = 2,
                waiterWaitSeconds = 30f,
                foodWaitSeconds = 40f,
                billWaitSeconds = 20f,
                expectedFoodSeconds = 12f,
                foodQualityPotentialBasisPoints = 7000,
                ambienceScoreBasisPoints = 5000
            };
            BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
                visit, 1, out BistroBuilderCustomerExperienceRecord beforeCare, out _);
            visit.waiterCareCreditSeconds = 7f;
            visit.foodCareCreditSeconds = 5f;
            visit.billCareCreditSeconds = 5f;
            visit.waiterContextActionCount = 3;
            bool careEvaluated = BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
                visit, 1, out BistroBuilderCustomerExperienceRecord afterCare, out _);
            Check(careEvaluated && afterCare.overallSatisfactionBasisPoints > beforeCare.overallSatisfactionBasisPoints,
                "Acciones contextuales mitigan espera percibida", ref passed, ref failed, lines);

            var saveRecord = new BistroBuilderWaiterRuntimeSaveRecord
            {
                waiterId = 13,
                worldPosition = new BistroBuilderSaveVector3(Vector3.zero),
                worldRotation = new BistroBuilderSaveQuaternion(Quaternion.identity),
                hasAdvancedWaiterProfile = true,
                primaryZoneId = profile.PrimaryZoneId,
                simultaneousPlanCapacity = profile.SimultaneousPlanCapacity,
                serviceEfficiency = profile.ServiceEfficiency
            };
            profile.CopySecondaryZones(saveRecord.secondaryZoneIds);
            Check(saveRecord.TryValidate(out _),
                "Perfil avanzado valido en service.runtime", ref passed, ref failed, lines);

            routeObject = new GameObject("BB13_TestRouting") { hideFlags = HideFlags.HideAndDontSave };
            BistroBuilderWaiterRoutingService routing = routeObject.AddComponent<BistroBuilderWaiterRoutingService>();
            var points = new List<Vector3>();
            bool routeOk = routing.TryBuildRoute(Vector3.zero, new Vector3(3f, 0f, 4f), points, out float meters);
            Check(routeOk && points.Count > 0 && meters > 0f, "Motor de mejores rutas con fallback seguro", ref passed, ref failed, lines);
        }
        catch (Exception exception)
        {
            failed++;
            lines.Add("ERROR - Excepcion: " + exception.Message);
        }
        finally
        {
            if (tableObject != null) UnityEngine.Object.DestroyImmediate(tableObject);
            if (profileObject != null) UnityEngine.Object.DestroyImmediate(profileObject);
            if (routeObject != null) UnityEngine.Object.DestroyImmediate(routeObject);
            if (waiterObject != null) UnityEngine.Object.DestroyImmediate(waiterObject);
            if (groupObject != null) UnityEngine.Object.DestroyImmediate(groupObject);
        }

        var builder = new StringBuilder("BLOQUE 13 - AUTOTEST\n");
        for (int i = 0; i < lines.Count; i++) builder.AppendLine(lines[i]);
        builder.Append("Resultado: ").Append(passed).Append(" OK / ").Append(failed).Append(" fallos.");
        report = builder.ToString();
        return failed == 0;
    }

    private static void Check(bool condition, string name, ref int passed, ref int failed, List<string> lines)
    {
        if (condition) passed++; else failed++;
        lines.Add((condition ? "OK - " : "ERROR - ") + name);
    }
}
