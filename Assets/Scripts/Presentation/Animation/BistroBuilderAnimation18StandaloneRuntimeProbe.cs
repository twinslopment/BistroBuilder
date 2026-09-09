#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Probe temporal de Play Mode real para BB18.
/// Sólo existe en Editor y nunca entra en builds del juego.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAnimation18StandaloneRuntimeProbe : MonoBehaviour
{
    [SerializeField] private bool runOnStart;
    private bool started;

    private BistroBuilderInteractionPresentationService service;
    private GameObject actor;
    private GameObject probeRoot;
    private Transform sourceSocket;
    private Transform destinationSocket;
    private BistroBuilderTransferableVisual transferable;
    private RestaurantSeat seat;
    private BistroBuilderNavigableDoor door;
    private BistroBuilderDoorCirculationEnvelope doorEnvelope;
    private Exception stepException;

    public void ConfigureForEditor(bool shouldRun)
    {
        runOnStart = shouldRun;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapRuntimeProbe()
    {
        BistroBuilderAnimation18StandaloneRuntimeProbe[] probes =
            UnityEngine.Object.FindObjectsByType<
                BistroBuilderAnimation18StandaloneRuntimeProbe>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
        for (int i = 0; i < probes.Length; i++)
            probes[i]?.BeginProbeIfRequested();
    }

    private void Start()
    {
        BeginProbeIfRequested();
    }

    private void BeginProbeIfRequested()
    {
        if (!runOnStart || started) return;
        started = true;
        Debug.Log("BB18_RUNTIME_PROBE_START");
        StartCoroutine(RunProbe());
    }

    private IEnumerator RunProbe()
    {
        Exception setupException = null;
        try
        {
            service = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderInteractionPresentationService>();
            if (service == null)
                throw new InvalidOperationException(
                    "Falta la autoridad BB18 en runtime.");
            SetupTransferProbe();
        }
        catch (Exception exception)
        {
            setupException = exception;
        }

        if (setupException != null)
        {
            FinishAndExit(false, "BB18 PlayMode: " + setupException.Message);
            yield break;
        }

        yield return ExecuteStep(RunCommittedTransfer());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunCancelledTransfer());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunSeatSit());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunSeatStand());
        if (!ContinueAfterStep()) yield break;
        yield return ExecuteStep(RunPortal());
        if (!ContinueAfterStep()) yield break;

        FinishAndExit(
            true,
            "PASS - Transfer commit, cancelacion pre-commit, " +
            "Seat/Stand real, Commit Frontier, reconciliacion y Portal funcionan.");
    }

    private void SetupTransferProbe()
    {
        probeRoot = new GameObject("__BB18_RuntimeProbe__");
        actor = new GameObject("Actor");
        actor.transform.SetParent(probeRoot.transform, false);
        GameObject source = new GameObject("SourceSocket");
        source.transform.SetParent(probeRoot.transform, false);
        GameObject destination = new GameObject("DestinationSocket");
        destination.transform.SetParent(probeRoot.transform, false);
        sourceSocket = source.transform;
        destinationSocket = destination.transform;

        GameObject prop = new GameObject("TransferableProp");
        prop.transform.SetParent(sourceSocket, false);
        transferable = prop.AddComponent<BistroBuilderTransferableVisual>();
        transferable.CaptureHomeIfNeeded();
        service.CommitRequested += ConfirmCommit;
    }

    private IEnumerator RunCommittedTransfer()
    {
        BistroBuilderResolvedInteractionPlan plan = CreateTransferPlan(
            "play18:transfer:commit",
            0.12f,
            0.35f);
        if (!service.TryStartResolvedInteraction(
                plan,
                out BistroBuilderInteractionHandle handle,
                out string error))
            throw new InvalidOperationException(
                "No comenzo Transfer: " + error);

        yield return WaitForTerminal(handle, 3f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed)
            throw new InvalidOperationException(
                "Transfer termino como " + handle.Status + ": " +
                handle.FailureReason);
        if (transferable.transform.parent != destinationSocket)
            throw new InvalidOperationException(
                "Transfer no reconcilio al socket destino.");
    }

    private IEnumerator RunCancelledTransfer()
    {
        transferable.ReconcileVisualTo(sourceSocket);
        BistroBuilderResolvedInteractionPlan plan = CreateTransferPlan(
            "play18:transfer:cancel",
            0.55f,
            0.82f);
        if (!service.TryStartResolvedInteraction(
                plan,
                out BistroBuilderInteractionHandle handle,
                out string error))
            throw new InvalidOperationException(
                "No comenzo Transfer cancelable: " + error);

        float deadline = Time.unscaledTime + 3f;
        bool cancelRequested = false;
        while (!handle.IsTerminal && Time.unscaledTime < deadline)
        {
            if (!cancelRequested &&
                handle.Phase == BistroBuilderInteractionPhase.Operate &&
                handle.CommitState ==
                    BistroBuilderInteractionCommitState.PreCommit)
            {
                cancelRequested = handle.RequestCancel();
            }
            yield return null;
        }
        if (!handle.IsTerminal)
            throw new TimeoutException(
                "Cancelacion pre-commit supero watchdog.");
        if (handle.Status != BistroBuilderInteractionStatus.Cancelled)
            throw new InvalidOperationException(
                "Cancelacion pre-commit termino como " + handle.Status + ".");
        if (transferable.transform.parent != sourceSocket)
            throw new InvalidOperationException(
                "Cancelacion pre-commit no restauro socket origen.");
    }

    private IEnumerator RunSeatSit()
    {
        seat = FindAvailableSeat();
        if (seat == null)
            throw new InvalidOperationException(
                "No hay silla real disponible para Seat.");
        if (!seat.TryReserve("play18:seat"))
            throw new InvalidOperationException(
                "Gameplay no pudo reservar la silla de prueba.");

        actor.transform.position = seat.CustomerApproachPoint.position;
        actor.transform.rotation = seat.CustomerApproachPoint.rotation;
        BistroBuilderResolvedInteractionPlan plan = CreateSeatPlan(
            "play18:seat:sit",
            BistroBuilderInteractionOperation.Sit);
        if (!service.TryStartResolvedInteraction(
                plan,
                out BistroBuilderInteractionHandle handle,
                out string error))
            throw new InvalidOperationException(
                "No comenzo Sit: " + error);

        yield return WaitForTerminal(handle, 5f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed)
            throw new InvalidOperationException(
                "Sit termino como " + handle.Status + ": " +
                handle.FailureReason);
        if (seat.OperationalState != RestaurantSeatOperationalState.Occupied)
            throw new InvalidOperationException(
                "La silla no termino Occupied.");
        if ((actor.transform.position - seat.SeatPoint.position).sqrMagnitude >
            0.0004f)
            throw new InvalidOperationException(
                "Actor no quedo reconciliado al SeatFrame.");
    }

    private IEnumerator RunSeatStand()
    {
        BistroBuilderResolvedInteractionPlan plan = CreateSeatPlan(
            "play18:seat:stand",
            BistroBuilderInteractionOperation.Stand);
        if (!service.TryStartResolvedInteraction(
                plan,
                out BistroBuilderInteractionHandle handle,
                out string error))
            throw new InvalidOperationException(
                "No comenzo Stand: " + error);

        yield return WaitForTerminal(handle, 5f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed)
            throw new InvalidOperationException(
                "Stand termino como " + handle.Status + ": " +
                handle.FailureReason);
        if (seat.OperationalState != RestaurantSeatOperationalState.Parked)
            throw new InvalidOperationException(
                "La silla no regreso a Parked.");
        if ((actor.transform.position - seat.CustomerApproachPoint.position)
                .sqrMagnitude > 0.0004f)
            throw new InvalidOperationException(
                "Actor no quedo reconciliado al ExitFrame.");
        if (!seat.ReleaseReservation("play18:seat"))
            throw new InvalidOperationException(
                "Gameplay no pudo liberar la reserva tras Stand.");
    }

    private IEnumerator RunPortal()
    {
        GameObject doorObject = new GameObject("RuntimeDoor");
        doorObject.transform.SetParent(probeRoot.transform, false);
        doorObject.transform.position = actor.transform.position +
            Vector3.right * 20f;
        doorEnvelope = doorObject.AddComponent<
            BistroBuilderDoorCirculationEnvelope>();
        // API runtime: configura el barrido real sin depender de helpers de Editor.
        doorEnvelope.ConfigureSweepForAngle(90f);
        door = doorObject.AddComponent<BistroBuilderNavigableDoor>();

        BistroBuilderResolvedInteractionPlan plan =
            new BistroBuilderResolvedInteractionPlan
            {
                interactionId = "play18:portal:open",
                ownerId = "play18:portal",
                family = BistroBuilderInteractionFamily.Portal,
                operation = BistroBuilderInteractionOperation.Open,
                actor = actor,
                target = doorObject,
                door = door,
                requiresCommitConfirmation = false,
                operationTimeoutSeconds = 3f
            };

        if (!service.TryStartResolvedInteraction(
                plan,
                out BistroBuilderInteractionHandle handle,
                out string error))
            throw new InvalidOperationException(
                "No comenzo Portal: " + error);

        yield return WaitForTerminal(handle, 4f);
        if (handle.Status != BistroBuilderInteractionStatus.Completed ||
            !door.IsOpen)
            throw new InvalidOperationException(
                "Portal no termino abierto correctamente.");
        if (doorEnvelope.IsActive)
            throw new InvalidOperationException(
                "Barrido espacial de puerta retenido tras completar.");
    }

    private BistroBuilderResolvedInteractionPlan CreateTransferPlan(
        string id,
        float duration,
        float commitTime)
    {
        return new BistroBuilderResolvedInteractionPlan
        {
            interactionId = id,
            ownerId = id,
            family = BistroBuilderInteractionFamily.Transfer,
            operation = BistroBuilderInteractionOperation.Pickup,
            actor = actor,
            target = transferable.gameObject,
            transferable = transferable,
            sourceSocket = sourceSocket,
            destinationSocket = destinationSocket,
            fallbackMotionDuration = duration,
            commitNormalizedTime = commitTime,
            operationTimeoutSeconds = 2f,
            commitTimeoutSeconds = 1f,
            requiresCommitConfirmation = true
        };
    }

    private BistroBuilderResolvedInteractionPlan CreateSeatPlan(
        string id,
        BistroBuilderInteractionOperation operation)
    {
        return new BistroBuilderResolvedInteractionPlan
        {
            interactionId = id,
            ownerId = "play18:seat",
            family = BistroBuilderInteractionFamily.Seat,
            operation = operation,
            actor = actor,
            target = seat.gameObject,
            interactionFrame = seat.CustomerApproachPoint,
            seatFrame = seat.SeatPoint,
            exitFrame = seat.CustomerApproachPoint,
            seat = seat,
            fallbackMotionDuration = 0.12f,
            commitNormalizedTime = 0.35f,
            operationTimeoutSeconds = 3f,
            commitTimeoutSeconds = 1f,
            requiresCommitConfirmation = true
        };
    }

    private static RestaurantSeat FindAvailableSeat()
    {
        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < seats.Length; i++)
        {
            RestaurantSeat candidate = seats[i];
            if (candidate != null && candidate.IsAvailableForReservation &&
                candidate.CustomerApproachPoint != null &&
                candidate.SeatPoint != null)
                return candidate;
        }
        return null;
    }

    private static IEnumerator WaitForTerminal(
        BistroBuilderInteractionHandle handle,
        float timeoutSeconds)
    {
        float deadline = Time.unscaledTime + Mathf.Max(0.1f, timeoutSeconds);
        while (handle != null && !handle.IsTerminal &&
               Time.unscaledTime < deadline)
            yield return null;

        if (handle == null)
            throw new InvalidOperationException("Handle nulo durante probe runtime.");
        if (!handle.IsTerminal)
            throw new TimeoutException(
                "Interaccion supero watchdog: " + handle.InteractionId);
    }

    private IEnumerator ExecuteStep(IEnumerator root)
    {
        stepException = null;
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            IEnumerator currentRoutine = stack.Peek();
            bool moved;
            object yielded = null;
            try
            {
                moved = currentRoutine.MoveNext();
                if (moved) yielded = currentRoutine.Current;
            }
            catch (Exception exception)
            {
                stepException = exception;
                yield break;
            }

            if (!moved)
            {
                stack.Pop();
                continue;
            }
            if (yielded is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return yielded;
        }
    }

    private bool ContinueAfterStep()
    {
        if (stepException == null) return true;
        FinishAndExit(false, "BB18 PlayMode: " + stepException.Message);
        return false;
    }

    private static void FinishAndExit(bool success, string message)
    {
        string report =
            "=== BISTRO BUILDER - BLOQUE 18 / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(
            ResolveReportPath(),
            report);
        if (success) Debug.Log(report);
        else Debug.LogError(report);
        Application.Quit(success ? 0 : 1);
    }

    private static string ResolveReportPath()
    {
        string configured = Environment.GetEnvironmentVariable("BB18_REPORT_PATH");
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(Application.persistentDataPath, "Animation18StandaloneReport.txt");
    }
    private static void ConfirmCommit(
        BistroBuilderInteractionHandle requestedHandle,
        BistroBuilderResolvedInteractionPlan plan)
    {
        requestedHandle?.ConfirmCommit();
    }

    private void OnDestroy()
    {
        if (service != null)
            service.CommitRequested -= ConfirmCommit;
        if (seat != null && !string.IsNullOrWhiteSpace(seat.ReservationOwnerId))
            seat.ReleaseReservation("play18:seat", true);
        if (probeRoot != null)
            UnityEngine.Object.Destroy(probeRoot);
    }
}
#endif
