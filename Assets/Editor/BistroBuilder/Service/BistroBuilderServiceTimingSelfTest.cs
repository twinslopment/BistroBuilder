using UnityEditor;
using UnityEngine;

public static class BistroBuilderServiceTimingSelfTest
{
    [MenuItem("Bistro Builder/Servicio/Timing contextual/Autotest")]
    public static void RunFromMenu()
    {
        Run(true);
    }

    public static bool RunBatch()
    {
        bool pass = Run(true);
        if (!pass)
            throw new System.InvalidOperationException(
                "Service Timing contextual self-test failed."
            );
        return true;
    }

    public static bool Run(bool logResult)
    {
        int failures = 0;

        BistroBuilderServiceTimingCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderServiceTimingCatalog>(
                BistroBuilderServiceTimingInstaller.AssetPath
            );

        if (catalog == null ||
            !catalog.TryGetProfile(
                BistroBuilderServiceTimingPhase.BillDelivery,
                out BistroBuilderServiceTimingProfile profile))
        {
            Fail("No se pudo cargar BillDelivery.", ref failures, logResult);
        }
        else
        {
            ExpectState(profile, 0f, BistroBuilderServiceTimingState.Normal, ref failures, logResult);
            ExpectState(profile, 119.99f, BistroBuilderServiceTimingState.Normal, ref failures, logResult);
            ExpectState(profile, 120f, BistroBuilderServiceTimingState.Attention, ref failures, logResult);
            ExpectState(profile, 209.99f, BistroBuilderServiceTimingState.Attention, ref failures, logResult);
            ExpectState(profile, 210f, BistroBuilderServiceTimingState.Delay, ref failures, logResult);
            ExpectState(profile, 299.99f, BistroBuilderServiceTimingState.Delay, ref failures, logResult);
            ExpectState(profile, 300f, BistroBuilderServiceTimingState.Incident, ref failures, logResult);
            ExpectState(profile, 420f, BistroBuilderServiceTimingState.Critical, ref failures, logResult);
        }

        if (catalog == null ||
            !catalog.TryGetProfile(
                BistroBuilderServiceTimingPhase.TakeOrder,
                out BistroBuilderServiceTimingProfile takeOrder))
        {
            Fail("No se pudo cargar TakeOrder.", ref failures, logResult);
        }
        else
        {
            ExpectState(takeOrder, 19.99f, BistroBuilderServiceTimingState.Normal, ref failures, logResult);
            ExpectState(takeOrder, 20f, BistroBuilderServiceTimingState.Attention, ref failures, logResult);
            ExpectState(takeOrder, 35f, BistroBuilderServiceTimingState.Delay, ref failures, logResult);
            ExpectState(takeOrder, 50f, BistroBuilderServiceTimingState.Incident, ref failures, logResult);
            ExpectState(takeOrder, 70f, BistroBuilderServiceTimingState.Critical, ref failures, logResult);
        }

        BistroBuilderFoodTimingPolicy food =
            catalog != null ? catalog.FoodTimingPolicy : null;
        if (food == null)
        {
            Fail("No se pudo cargar FoodDelivery dinámico.", ref failures, logResult);
        }
        else
        {
            food.GetThresholds(
                20f,
                out float foodTarget,
                out float foodAttention,
                out float foodDelay,
                out float foodIncident,
                out float foodCritical
            );
            Expect(
                Mathf.Approximately(foodTarget, 20f) &&
                Mathf.Approximately(foodAttention, 23f) &&
                Mathf.Approximately(foodDelay, 31f) &&
                Mathf.Approximately(foodIncident, 40f) &&
                Mathf.Approximately(foodCritical, 90f),
                "FoodDelivery 20s debe derivar 20/23/31/40/90.",
                ref failures,
                logResult
            );
            Expect(
                food.Evaluate(20f, 22.99f) == BistroBuilderServiceTimingState.Normal &&
                food.Evaluate(20f, 23f) == BistroBuilderServiceTimingState.Attention &&
                food.Evaluate(20f, 31f) == BistroBuilderServiceTimingState.Delay &&
                food.Evaluate(20f, 40f) == BistroBuilderServiceTimingState.Incident &&
                food.Evaluate(20f, 90f) == BistroBuilderServiceTimingState.Critical,
                "FoodDelivery debe respetar todos sus límites dinámicos.",
                ref failures,
                logResult
            );
        }

        Expect(
            !BistroBuilderTableContextActionService.CanAccelerateBill(
                true, true, false, WaiterTaskPriority.High,
                BistroBuilderServiceTimingState.Normal),
            "Normal no debe permitir Agilizar cuenta.",
            ref failures,
            logResult
        );
        Expect(
            BistroBuilderTableContextActionService.CanAccelerateBill(
                true, true, false, WaiterTaskPriority.High,
                BistroBuilderServiceTimingState.Attention),
            "Atención debe permitir Agilizar cuenta.",
            ref failures,
            logResult
        );
        Expect(
            BistroBuilderTableContextActionService.CanAccelerateBill(
                true, true, false, WaiterTaskPriority.High,
                BistroBuilderServiceTimingState.Delay),
            "Demora debe permitir Agilizar cuenta.",
            ref failures,
            logResult
        );
        Expect(
            !BistroBuilderTableContextActionService.CanAccelerateBill(
                true, true, false, WaiterTaskPriority.Urgent,
                BistroBuilderServiceTimingState.Delay),
            "Una cuenta ya Urgent no debe poder agilizarse otra vez.",
            ref failures,
            logResult
        );
        Expect(
            !BistroBuilderTableContextActionService.CanAccelerateBill(
                true, false, true, WaiterTaskPriority.High,
                BistroBuilderServiceTimingState.Delay),
            "Una cuenta ya asignada no debe mostrar Agilizar cuenta.",
            ref failures,
            logResult
        );
        Expect(
            !BistroBuilderTableContextActionService.CanExplainBillDelay(
                true, false, BistroBuilderServiceTimingState.Attention),
            "Atención no debe permitir Explicar demora.",
            ref failures,
            logResult
        );
        Expect(
            BistroBuilderTableContextActionService.CanExplainBillDelay(
                true, false, BistroBuilderServiceTimingState.Delay),
            "Demora debe permitir Explicar demora.",
            ref failures,
            logResult
        );
        Expect(
            !BistroBuilderTableContextActionService.CanExplainBillDelay(
                true, true, BistroBuilderServiceTimingState.Incident),
            "Explicar demora debe ser de una sola aplicación.",
            ref failures,
            logResult
        );
        Expect(
            !BistroBuilderTableContextActionService.CanApologize(
                false, false, 0),
            "Sin incidencia no debe aparecer Disculpa.",
            ref failures,
            logResult
        );
        Expect(
            BistroBuilderTableContextActionService.CanApologize(
                true, false, 0),
            "Incidencia temporal debe permitir Disculpa.",
            ref failures,
            logResult
        );
        Expect(
            !BistroBuilderTableContextActionService.CanApologize(
                true, true, 0),
            "La misma incidencia temporal no debe admitir dos disculpas.",
            ref failures,
            logResult
        );
        Expect(
            BistroBuilderTableContextActionService.CanApologize(
                false, true, 1),
            "Un fallo explícito pendiente debe permitir Disculpa.",
            ref failures,
            logResult
        );
        Expect(
            BistroBuilderCustomerExperienceTrackingService
                .IsRecoverableServiceIncidentKind(
                    BistroBuilderAdvancedOrderIncidentKind.WrongDish),
            "WrongDish debe considerarse una incidencia recuperable.",
            ref failures,
            logResult
        );
        Expect(
            !BistroBuilderCustomerExperienceTrackingService
                .IsRecoverableServiceIncidentKind(
                    BistroBuilderAdvancedOrderIncidentKind.CustomerChange),
            "CustomerChange no debe tratarse como fallo del restaurante.",
            ref failures,
            logResult
        );

        TestExplanationMitigation(ref failures, logResult);
        TestApologyRecovery(ref failures, logResult);
        TestCoordinatorPriority(ref failures, logResult);
        TestSaveRecordContract(ref failures, logResult);

        if (logResult)
            Debug.Log(
                failures == 0
                    ? "SERVICE TIMING SELFTEST: PASS"
                    : "SERVICE TIMING SELFTEST: FAIL (" + failures + ")"
            );

        return failures == 0;
    }

    private static void TestExplanationMitigation(
        ref int failures,
        bool logResult)
    {
        int raw = 0;
        int mitigated =
            BistroBuilderCustomerExperienceEvaluator.ApplyBillDelayExplanationMitigation(
                raw,
                1500
            );

        Expect(
            mitigated == 1500,
            "Una explicación al 15% debe recuperar 1500 pb cuando la penalización de cuenta es máxima.",
            ref failures,
            logResult
        );

        var snapshot = new BistroBuilderReputationRuntimeSnapshot();
        snapshot.visits.Add(new BistroBuilderReputationVisitRuntimeRecord
        {
            groupId = 77,
            partySize = 2,
            segmentId = "general",
            waiterWaitSeconds = 35f,
            foodWaitSeconds = 31f,
            expectedFoodSeconds = 20f,
            billWaitSeconds = 210f,
            waiterDelayExplanationMitigationBasisPoints = 1500,
            waiterIncidentApologyMitigationBasisPoints = 2500,
            foodDelayExplanationMitigationBasisPoints = 1500,
            foodIncidentApologyMitigationBasisPoints = 2500,
            billDelayExplanationMitigationBasisPoints = 1500
        });

        BistroBuilderReputationRuntimeSnapshot clone = snapshot.DeepClone();
        Expect(
            clone.visits.Count == 1 &&
            clone.visits[0].waiterDelayExplanationMitigationBasisPoints == 1500 &&
            clone.visits[0].waiterIncidentApologyMitigationBasisPoints == 2500 &&
            clone.visits[0].foodDelayExplanationMitigationBasisPoints == 1500 &&
            clone.visits[0].foodIncidentApologyMitigationBasisPoints == 2500 &&
            clone.visits[0].billDelayExplanationMitigationBasisPoints == 1500,
            "reputation.runtime debe conservar las recuperaciones de camarero, comida y cuenta.",
            ref failures,
            logResult
        );
        Expect(
            BistroBuilderCustomerExperienceTrackingService.TryValidateRuntimeSnapshot(
                clone,
                out _
            ),
            "El snapshot con Explicar demora debe validar.",
            ref failures,
            logResult
        );
    }

    private static void TestApologyRecovery(
        ref int failures,
        bool logResult)
    {
        int billRecovered =
            BistroBuilderCustomerExperienceEvaluator.ApplyBillRecoveryMitigations(
                0,
                1500,
                2500
            );

        Expect(
            billRecovered == 3625,
            "Explicar 15% + Disculpa 25% deben recuperar secuencialmente 3625 pb desde una penalización máxima.",
            ref failures,
            logResult
        );

        Expect(
            BistroBuilderCustomerExperienceEvaluator.ApplyServiceIncidentImpact(
                7000,
                1000,
                0
            ) == 6000,
            "Una incidencia explícita provisional debe restar 1000 pb.",
            ref failures,
            logResult
        );

        Expect(
            BistroBuilderCustomerExperienceEvaluator.ApplyServiceIncidentImpact(
                7000,
                1000,
                500
            ) == 6500,
            "Disculpa debe recuperar 500 pb de una incidencia explícita provisional.",
            ref failures,
            logResult
        );

        var snapshot = new BistroBuilderReputationRuntimeSnapshot();
        snapshot.visits.Add(new BistroBuilderReputationVisitRuntimeRecord
        {
            groupId = 78,
            partySize = 2,
            segmentId = "general",
            billWaitSeconds = 300f,
            billIncidentApologyMitigationBasisPoints = 2500,
            recoverableServiceIncidentCount = 1,
            apologizedServiceIncidentCount = 1,
            serviceIncidentPenaltyBasisPoints = 1000,
            serviceIncidentApologyRecoveryBasisPoints = 500
        });

        BistroBuilderReputationRuntimeSnapshot clone = snapshot.DeepClone();
        Expect(
            clone.visits.Count == 1 &&
            clone.visits[0].billIncidentApologyMitigationBasisPoints == 2500 &&
            clone.visits[0].recoverableServiceIncidentCount == 1 &&
            clone.visits[0].apologizedServiceIncidentCount == 1 &&
            clone.visits[0].serviceIncidentApologyRecoveryBasisPoints == 500,
            "reputation.runtime debe conservar Disculpa e incidencias en snapshot/rehidratación.",
            ref failures,
            logResult
        );
        Expect(
            BistroBuilderCustomerExperienceTrackingService.TryValidateRuntimeSnapshot(
                clone,
                out _
            ),
            "El snapshot con Disculpa debe validar.",
            ref failures,
            logResult
        );
    }

    private static void TestCoordinatorPriority(
        ref int failures,
        bool logResult)
    {
        GameObject root = null;
        GameObject tableGo = null;
        GameObject groupGo = null;

        try
        {
            root = new GameObject("BB_TimingSelfTest_Coordinator");
            root.SetActive(false);
            WaiterTaskCoordinator coordinator =
                root.AddComponent<WaiterTaskCoordinator>();

            SerializedObject coordinatorSo = new SerializedObject(coordinator);
            coordinatorSo.FindProperty("discoverSceneObjectsOnStart").boolValue = false;
            coordinatorSo.FindProperty("manageBillTasks").boolValue = true;
            coordinatorSo.FindProperty("manageTakeOrderTasks").boolValue = false;
            coordinatorSo.FindProperty("manageFoodDeliveryTasks").boolValue = false;
            coordinatorSo.FindProperty("manageCleaningTasks").boolValue = false;
            coordinatorSo.ApplyModifiedPropertiesWithoutUndo();

            tableGo = new GameObject("BB_TimingSelfTest_Table");
            RestaurantTable table = tableGo.AddComponent<RestaurantTable>();

            groupGo = new GameObject("BB_TimingSelfTest_Group");
            CustomerGroup group = groupGo.AddComponent<CustomerGroup>();
            group.Initialize(990001, 2);

            Expect(
                table.TryAssignCustomerGroup(group),
                "La mesa diagnóstica debe aceptar el grupo.",
                ref failures,
                logResult
            );

            table.SetState(TableState.WaitingForBill);
            coordinator.RegisterTable(table);

            Expect(
                coordinator.TryGetActiveTableTask(
                    WaiterTaskType.DeliverBill,
                    table,
                    out WaiterTask task) &&
                task != null &&
                task.Priority == WaiterTaskPriority.High &&
                task.IsPending,
                "WaitingForBill debe crear una tarea DeliverBill High pendiente.",
                ref failures,
                logResult
            );

            Expect(
                coordinator.TryChangePendingTableTaskPriority(
                    WaiterTaskType.DeliverBill,
                    table,
                    WaiterTaskPriority.Urgent,
                    out WaiterTaskPriority previous) &&
                previous == WaiterTaskPriority.High,
                "Agilizar debe elevar High -> Urgent por la autoridad de tareas.",
                ref failures,
                logResult
            );

            Expect(
                coordinator.TryGetActiveTableTask(
                    WaiterTaskType.DeliverBill,
                    table,
                    out task) &&
                task.Priority == WaiterTaskPriority.Urgent,
                "La tarea debe quedar Urgent tras agilizar.",
                ref failures,
                logResult
            );
        }
        finally
        {
            if (groupGo != null) Object.DestroyImmediate(groupGo);
            if (tableGo != null) Object.DestroyImmediate(tableGo);
            if (root != null) Object.DestroyImmediate(root);
        }
    }

    private static void TestSaveRecordContract(
        ref int failures,
        bool logResult)
    {
        var valid = new BistroBuilderTableRuntimeSaveRecord
        {
            tableId = 1,
            state = (int)TableState.WaitingForBill,
            groupId = 1,
            billPriorityBoosted = true
        };

        Expect(
            valid.TryValidate(out _),
            "Una cuenta agilizada debe poder persistirse mientras WaitingForBill.",
            ref failures,
            logResult
        );

        var invalid = new BistroBuilderTableRuntimeSaveRecord
        {
            tableId = 1,
            state = (int)TableState.Eating,
            groupId = 1,
            billPriorityBoosted = true
        };

        Expect(
            !invalid.TryValidate(out _),
            "La prioridad de cuenta no debe persistirse fuera de WaitingForBill.",
            ref failures,
            logResult
        );
    }

    private static void ExpectState(
        BistroBuilderServiceTimingProfile profile,
        float seconds,
        BistroBuilderServiceTimingState expected,
        ref int failures,
        bool logResult)
    {
        BistroBuilderServiceTimingState actual = profile.Evaluate(seconds);
        Expect(
            actual == expected,
            seconds + " s esperaba " + expected + " y devolvió " + actual + ".",
            ref failures,
            logResult
        );
    }

    private static void Expect(
        bool condition,
        string message,
        ref int failures,
        bool logResult)
    {
        if (condition)
            return;

        failures++;
        if (logResult) Debug.LogError("SERVICE TIMING SELFTEST: " + message);
    }

    private static void Fail(
        string message,
        ref int failures,
        bool logResult)
    {
        failures++;
        if (logResult) Debug.LogError("SERVICE TIMING SELFTEST: " + message);
    }
}
