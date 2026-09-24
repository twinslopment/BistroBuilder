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

        TestExplanationMitigation(ref failures, logResult);
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
            billWaitSeconds = 210f,
            billDelayExplanationMitigationBasisPoints = 1500
        });

        BistroBuilderReputationRuntimeSnapshot clone = snapshot.DeepClone();
        Expect(
            clone.visits.Count == 1 &&
            clone.visits[0].billDelayExplanationMitigationBasisPoints == 1500,
            "reputation.runtime debe conservar Explicar demora en snapshot/rehidratación.",
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
