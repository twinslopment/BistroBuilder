using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderInteractionV1SelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/Interaction & Reservation v1/Ejecutar autotests")]
    public static void Run()
    {
        int ok = 0;
        int fail = 0;
        var report = new StringBuilder("BB INTERACTION & RESERVATION v1 - AUTOTEST\n");
        void Check(bool condition, string label)
        {
            if (condition) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        GameObject root = new GameObject("__BBInteractionV1SelfTest__");
        BistroBuilderInteractionService service = root.AddComponent<BistroBuilderInteractionService>();
        service.SetEditorSimulationTime(0d);
        BistroBuilderInteractionTargetDefinition definition = null;
        GameObject targetObject = null;
        try
        {
            Check(service.ValidateConfiguration(out _), "Kernel configura sin targets");

            Submit(service, "r.same.b", "actor:b", "resource:same", 10);
            Submit(service, "r.same.a", "actor:a", "resource:same", 10);
            service.ResolveArbitrationEpoch();
            Check(IsGranted(service, "r.same.a") && IsWaiting(service, "r.same.b"),
                "Dos solicitudes mismo frame se resuelven determinísticamente");

            BistroBuilderInteractionGrantHandle firstWinner = DecisionHandle(service, "r.same.a");
            service.ResetTransientRuntimeStateAfterLoad();
            Submit(service, "r.same.a", "actor:a", "resource:same", 10);
            Submit(service, "r.same.b", "actor:b", "resource:same", 10);
            service.ResolveArbitrationEpoch();
            Check(IsGranted(service, "r.same.a") && IsWaiting(service, "r.same.b"),
                "Permutar input no cambia el ganador");
            Check(!service.ValidateCurrentGrant(firstWinner),
                "Handle anterior queda stale tras reconciliación");

            service.ResetTransientRuntimeStateAfterLoad();
            definition = CreateCapacityDefinition(2);
            targetObject = new GameObject("__BBInteractionTarget2__");
            BistroBuilderInteractionTarget target =
                targetObject.AddComponent<BistroBuilderInteractionTarget>();
            target.ConfigureForEditor("target:work2", definition, null);
            Check(service.RegisterTarget(target), "Target genérico se registra");
            SubmitTarget(service, "r.cap.c", "actor:c", "target:work2", 10);
            SubmitTarget(service, "r.cap.a", "actor:a", "target:work2", 10);
            SubmitTarget(service, "r.cap.b", "actor:b", "target:work2", 10);
            service.ResolveArbitrationEpoch();
            int capGranted = new[] { "r.cap.a", "r.cap.b", "r.cap.c" }
                .Count(id => IsGranted(service, id));
            Check(capGranted == 2 && IsWaiting(service, "r.cap.c"),
                "3 actores / capacidad 2 nunca sobreasigna");
            Check(service.ValidateRuntimeInvariants(out _),
                "Capacity N mantiene invariantes");

            service.ResetTransientRuntimeStateAfterLoad();
            Submit(service, "r.low", "actor:low", "resource:stable", 1);
            service.ResolveArbitrationEpoch();
            BistroBuilderInteractionGrantHandle lowHandle = DecisionHandle(service, "r.low");
            Submit(service, "r.high", "actor:high", "resource:stable", 100);
            service.ResolveArbitrationEpoch();
            Check(service.ValidateCurrentGrant(lowHandle) && IsWaiting(service, "r.high"),
                "No existe preemption automática por prioridad posterior");

            service.ResetTransientRuntimeStateAfterLoad();
            Submit(service, "r.timeout", "actor:t", "resource:timeout", 1,
                BistroBuilderInteractionGrantKind.UsePermit, 1f);
            service.ResolveArbitrationEpoch();
            BistroBuilderInteractionGrantHandle timeoutHandle = DecisionHandle(service, "r.timeout");
            service.SetEditorSimulationTime(2d);
            service.ResolveArbitrationEpoch();
            Check(!service.ValidateCurrentGrant(timeoutHandle),
                "Use Permit no-engaged expira sin reserva eterna");
            service.SetEditorSimulationTime(0d);
            service.ResetTransientRuntimeStateAfterLoad();
            Submit(service, "r.block", "actor:block", "resource:b", 1);
            service.ResolveArbitrationEpoch();
            var bundle = new BistroBuilderInteractionBundleRequest
            {
                bundleId = "bundle:partial",
                requests = new List<BistroBuilderInteractionAcquisitionRequest>
                {
                    Request("bundle:a", "actor:x", "resource:a", 1),
                    Request("bundle:b", "actor:x", "resource:b", 1)
                }
            };
            service.SubmitBundle(bundle);
            service.ResolveArbitrationEpoch();
            Check(service.TryGetBundleDecision("bundle:partial", out var bundleDecision) &&
                  bundleDecision.outcome != BistroBuilderInteractionRequestOutcome.Granted &&
                  service.GetGrantsForResource("resource:a").Count == 0,
                "Bundle parcial hace rollback all-or-none");

            service.ResetTransientRuntimeStateAfterLoad();
            Submit(service, "r.custody", "station:pass", "plate:1", 1,
                BistroBuilderInteractionGrantKind.Custody, 0f,
                BistroBuilderInteractionHolderKind.Station);
            service.ResolveArbitrationEpoch();
            BistroBuilderInteractionGrantHandle custodyOld = DecisionHandle(service, "r.custody");
            bool transferred = service.TryTransferCustody(
                custodyOld, BistroBuilderInteractionHolderKind.Actor,
                "waiter:1", out BistroBuilderInteractionGrantHandle custodyNew);
            Check(transferred && !service.ValidateCurrentGrant(custodyOld) &&
                  service.ValidateCurrentGrant(custodyNew),
                "Transferencia Custody es atómica y fencea el owner anterior");
            service.ResetTransientRuntimeStateAfterLoad();
            Submit(service, "r.tray", "actor:carrier", "tray:1", 1,
                BistroBuilderInteractionGrantKind.Custody, 0f,
                BistroBuilderInteractionHolderKind.Actor);
            Submit(service, "r.plate", "tray:1", "plate:2", 1,
                BistroBuilderInteractionGrantKind.Custody, 0f,
                BistroBuilderInteractionHolderKind.Container);
            service.ResolveArbitrationEpoch();
            BistroBuilderInteractionGrantHandle trayHandle = DecisionHandle(service, "r.tray");
            bool cycleAccepted = service.TryTransferCustody(
                trayHandle, BistroBuilderInteractionHolderKind.Container,
                "plate:2", out _);
            Check(!cycleAccepted && service.ValidateRuntimeInvariants(out _),
                "Custody jerárquica rechaza ciclos");
            service.ResetTransientRuntimeStateAfterLoad();
            GameObject waiterObject = new GameObject("__BBInteractionDishWaiter__");
            try
            {
                Waiter dishWaiter = waiterObject.AddComponent<Waiter>();
                BistroBuilderDishCustodyCoordinator dishCustody =
                    root.AddComponent<BistroBuilderDishCustodyCoordinator>();
                dishCustody.ConfigureForEditor(service);
                Check(dishCustody.EnsureReadyDishCustody(
                        "line:dish-test", "kitchen:test", out _),
                    "Plato ReadyForPickup obtiene Custody única en el pass");
                Check(dishCustody.TryPickupByWaiter(
                        "line:dish-test", dishWaiter, out _),
                    "Recogida transfiere Custody Pass -> Waiter");
                Check(dishCustody.TryReturnToOriginPass(
                        "line:dish-test", out _) && service.CustodyCount == 1,
                    "Rollback devuelve Custody Waiter -> Pass sin duplicarla");
                Check(dishCustody.TryReleaseDishCustody("line:dish-test") &&
                      service.CustodyCount == 0,
                    "Cancelación física libera Custody sin huérfanos");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(waiterObject);
            }

            service.ResetTransientRuntimeStateAfterLoad();
            Submit(service, "r.q2", "guest:2", "queue:host", 1,
                BistroBuilderInteractionGrantKind.WaitTicket);
            Submit(service, "r.q1", "guest:1", "queue:host", 1,
                BistroBuilderInteractionGrantKind.WaitTicket);
            service.ResolveArbitrationEpoch();
            Check(service.TryCallNextWaitTicket("queue:host", out var called) &&
                  service.TryGetGrant(called, out var calledGrant) &&
                  calledGrant.holderId == "guest:1",
                "WaitTicket conserva orden determinista sin poseer el servicio");

            service.ResetTransientRuntimeStateAfterLoad();
            Submit(service, "r.assign", "guest:1", "seat:1", 1,
                BistroBuilderInteractionGrantKind.Assignment);
            Submit(service, "r.use.ephemeral", "guest:2", "counter:1", 1,
                BistroBuilderInteractionGrantKind.UsePermit, 30f);
            service.ResolveArbitrationEpoch();
            BistroBuilderInteractionCanonicalSnapshot snapshot =
                service.CaptureCanonicalSnapshot();
            Check(snapshot.grants.Any(g => g.kind == BistroBuilderInteractionGrantKind.Assignment) &&
                  snapshot.grants.All(g => g.kind != BistroBuilderInteractionGrantKind.UsePermit),
                "Save canónico excluye Use Permits efímeros");
            Check(service.TryRestoreCanonicalSnapshot(snapshot, out _),
                "Reservation Reconciliation restaura snapshot válido");
            Check(service.AssignmentCount == 1 && service.UsePermitCount == 0,
                "Load reconstruye derechos duraderos y descarta locks runtime");

            service.ResetTransientRuntimeStateAfterLoad();
            bool callbackTriggered = false;
            service.GrantChanged += OnGrantChanged;
            Submit(service, "r.reentrant", "actor:r", "resource:r", 1);
            service.ResolveArbitrationEpoch();
            service.ResolveArbitrationEpoch();
            Check(callbackTriggered && service.GetGrantsForResource("resource:r").Count == 0,
                "Mutación reentrante se difiere al punto seguro");
            service.GrantChanged -= OnGrantChanged;

            void OnGrantChanged(BistroBuilderInteractionGrantRecord grant)
            {
                if (grant == null || grant.resourceId != "resource:r") return;
                callbackTriggered = true;
                service.ReleaseGrant(grant.Handle);
            }

            Check(service.ValidateRuntimeInvariants(out _),
                "Cierre del stress funcional mantiene invariantes");
        }
        finally
        {
            if (targetObject != null) UnityEngine.Object.DestroyImmediate(targetObject);
            if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(root);
        }
        LastPassed = ok;
        LastFailed = fail;
        report.AppendLine("Resultado: " + ok + " OK / " + fail + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (fail > 0) throw new InvalidOperationException(LastReport);
    }

    private static void Submit(
        BistroBuilderInteractionService service,
        string requestId,
        string holderId,
        string resourceId,
        int priority,
        BistroBuilderInteractionGrantKind kind = BistroBuilderInteractionGrantKind.TaskClaim,
        float engageSeconds = 0f,
        BistroBuilderInteractionHolderKind holderKind = BistroBuilderInteractionHolderKind.Actor)
    {
        service.SubmitAcquisition(Request(
            requestId, holderId, resourceId, priority,
            kind, engageSeconds, holderKind));
    }

    private static BistroBuilderInteractionAcquisitionRequest Request(
        string requestId,
        string holderId,
        string resourceId,
        int priority,
        BistroBuilderInteractionGrantKind kind = BistroBuilderInteractionGrantKind.TaskClaim,
        float engageSeconds = 0f,
        BistroBuilderInteractionHolderKind holderKind = BistroBuilderInteractionHolderKind.Actor)
    {
        return new BistroBuilderInteractionAcquisitionRequest
        {
            requestId = requestId,
            grantKind = kind,
            holderKind = holderKind,
            holderId = holderId,
            interactionId = "test.interaction",
            taskPriorityClass = priority,
            engageWithinSeconds = engageSeconds,
            candidates = new List<BistroBuilderInteractionCandidate>
            {
                new BistroBuilderInteractionCandidate
                {
                    resourceId = resourceId,
                    suitability = 10,
                    travelCostHint = 10
                }
            }
        };
    }

    private static void SubmitTarget(
        BistroBuilderInteractionService service,
        string requestId,
        string holderId,
        string targetId,
        int priority)
    {
        service.SubmitAcquisition(new BistroBuilderInteractionAcquisitionRequest
        {
            requestId = requestId,
            grantKind = BistroBuilderInteractionGrantKind.UsePermit,
            holderId = holderId,
            interactionId = "test.use",
            taskPriorityClass = priority,
            engageWithinSeconds = 30f,
            candidates = new List<BistroBuilderInteractionCandidate>
            {
                new BistroBuilderInteractionCandidate
                {
                    targetId = targetId,
                    resourceId = targetId,
                    channelId = "work",
                    suitability = 10,
                    travelCostHint = 10
                }
            }
        });
    }

    private static BistroBuilderInteractionTargetDefinition CreateCapacityDefinition(int capacity)
    {
        var definition = ScriptableObject.CreateInstance<BistroBuilderInteractionTargetDefinition>();
        definition.ConfigureForEditor(
            "test.definition.capacity",
            null,
            new[]
            {
                new BistroBuilderInteractionChannelDefinition
                {
                    channelId = "work",
                    capacity = capacity,
                    interactionIds = new List<string> { "test.use" }
                }
            });
        return definition;
    }
    private static bool IsGranted(BistroBuilderInteractionService service, string requestId)
    {
        return service.TryGetDecision(requestId, out var decision) &&
               decision.outcome == BistroBuilderInteractionRequestOutcome.Granted;
    }

    private static bool IsWaiting(BistroBuilderInteractionService service, string requestId)
    {
        return service.TryGetDecision(requestId, out var decision) &&
               decision.outcome == BistroBuilderInteractionRequestOutcome.Pending &&
               decision.reason != BistroBuilderInteractionReasonCode.None;
    }

    private static bool IsDenied(BistroBuilderInteractionService service, string requestId)
    {
        return service.TryGetDecision(requestId, out var decision) &&
               decision.outcome != BistroBuilderInteractionRequestOutcome.Granted &&
               decision.outcome != BistroBuilderInteractionRequestOutcome.Pending;
    }

    private static BistroBuilderInteractionGrantHandle DecisionHandle(
        BistroBuilderInteractionService service,
        string requestId)
    {
        return service.TryGetDecision(requestId, out var decision)
            ? decision.handle
            : default;
    }

    public static void RunFromCommandLine()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
