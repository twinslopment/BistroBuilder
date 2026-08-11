#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2.2D7A3 — Validación runtime completa de Inventario/Almacén.
///
/// OBJETIVO
/// -------
/// Complementa la prueba funcional 2.2D (39/0) con la evidencia que faltaba
/// durante un servicio REAL:
///   - comanda canónica real;
///   - reserva Active real;
///   - coherencia Stock / Reservado / Disponible entre inventario canónico
///     y la fachada 2.2D;
///   - consumo/liberación real de la reserva;
///   - guardado/carga REAL mediante la prueba 368EF ya existente;
///   - coherencia de 2.2D después de restaurar;
///   - ausencia de errores/excepciones durante toda la ejecución.
///
/// DISEÑO
/// ------
/// Esta herramienta es Editor-only. No añade componentes runtime ni modifica
/// los sistemas canónicos. Reutiliza, por reflexión, la prueba real de servicio
/// activo 368EF que ya existe en el proyecto y se limita a observar datos.
///
/// Si no puede demostrar un contrato (por ejemplo, no encuentra una reserva
/// Active con cantidades legibles), la prueba FALLA. No convierte ausencia de
/// evidencia en un aprobado.
/// </summary>
public sealed class BistroBuilderInventoryWarehouse22D7RuntimeValidationWindow : EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/2.2D7A3 - Validación runtime completa";

    private const double TimeoutSeconds = 180.0;
    private const double SnapshotIntervalSeconds = 0.12;
    private const double PostLoadSettleSeconds = 0.35;
    private const double Tolerance = 0.0001;

    private static readonly Regex OrderIdRegex =
        new Regex(@"OrderId:\s*(order_[A-Za-z0-9_]+)", RegexOptions.Compiled);

    private static readonly Regex ConsumedReservationRegex =
        new Regex(@"Reserva\s+(inventory_reservation_[^\s\.]+)\s+consumida",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly List<CheckResult> checks = new List<CheckResult>();
    private readonly List<RuntimeIssue> runtimeIssues = new List<RuntimeIssue>();
    private readonly List<string> invalidTransformDiagnostics = new List<string>();
    private int runtimeIssueOccurrences;
    private bool capturingRuntimeDiagnostics;
    private readonly HashSet<string> observedOrderIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> trackedReservationIds =
        new HashSet<string>(StringComparer.Ordinal);

    private Vector2 scroll;
    private string finalReport = string.Empty;
    private string phaseText = "Preparada.";

    private bool running;
    private bool subscribed;
    private bool captureGuard;
    private bool harnessStarted;
    private bool harnessFailure;
    private bool harnessSuccessMarker;
    private bool restoreSeen;
    private bool postLoadValidated;
    private bool activeReservationCaptured;
    private bool consumedTrackedReservation;
    private bool initialRuntimeStateValidated;
    private bool postConsumptionValidated;
    private bool emittedFinal;

    private double startedAt;
    private double nextSnapshotAt;
    private double restoreSeenAt;

    private RuntimeSnapshot activeSnapshot;
    private RuntimeSnapshot postConsumptionSnapshot;
    private RuntimeSnapshot postLoadSnapshot;

    private Type legacyHarnessType;
    private EditorWindow legacyHarnessWindow;
    private bool legacyRunningStateKnown;
    private bool legacyRunningState;

    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        BistroBuilderInventoryWarehouse22D7RuntimeValidationWindow window =
            GetWindow<BistroBuilderInventoryWarehouse22D7RuntimeValidationWindow>(
                "2.2D7 Runtime"
            );
        window.minSize = new Vector2(660f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        wantsMouseMove = true;
    }

    private void OnDisable()
    {
        StopSubscriptions();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "2.2D7A3 — Validación runtime completa",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Ejecuta esta ventana en Play Mode. La prueba lanza la prueba real " +
            "368EF de servicio/guardado/carga y observa automáticamente la " +
            "comanda, las reservas y la fachada 2.2D. No necesita comparar " +
            "cantidades a mano.",
            MessageType.Info
        );

        EditorGUILayout.LabelField("Estado", phaseText);
        EditorGUILayout.LabelField(
            "Play Mode",
            EditorApplication.isPlaying ? "Sí" : "No"
        );

        using (new EditorGUI.DisabledScope(running || !EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Ejecutar 2.2D7A3 completa", GUILayout.Height(34f)))
            {
                BeginValidation();
            }
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra primero en Play Mode y después pulsa el botón.",
                MessageType.Warning
            );
        }

        if (running)
        {
            if (GUILayout.Button("Cancelar observación"))
            {
                FailAndFinish("Prueba cancelada manualmente.");
            }
        }

        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (checks.Count > 0)
        {
            EditorGUILayout.LabelField("Comprobaciones", EditorStyles.boldLabel);
            for (int i = 0; i < checks.Count; i++)
            {
                CheckResult check = checks[i];
                GUIStyle style = new GUIStyle(EditorStyles.label);
                style.wordWrap = true;
                style.normal.textColor = check.Passed
                    ? new Color(0.20f, 0.72f, 0.32f)
                    : new Color(0.90f, 0.28f, 0.24f);
                EditorGUILayout.LabelField(
                    (check.Passed ? "✓ " : "✗ ") + check.Message,
                    style
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(finalReport))
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Informe final", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(finalReport, GUILayout.MinHeight(220f));
            if (GUILayout.Button("Copiar informe"))
            {
                EditorGUIUtility.systemCopyBuffer = finalReport;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void BeginValidation()
    {
        ResetState();

        if (!EditorApplication.isPlaying)
        {
            FailAndFinish("2.2D7 solo puede ejecutarse en Play Mode.");
            return;
        }

        phaseText = "Preflight de dependencias...";
        Repaint();

        string preflightError;
        if (!RunPreflight(out preflightError))
        {
            FailAndFinish(preflightError);
            return;
        }

        running = true;
        startedAt = EditorApplication.timeSinceStartup;
        nextSnapshotAt = startedAt;
        StartSubscriptions();

        phaseText = "Lanzando servicio real 368EF...";
        Repaint();

        string harnessError;
        if (!StartLegacyHarness(out harnessError))
        {
            FailAndFinish(harnessError);
            return;
        }

        harnessStarted = true;
        AddPass(
            "La prueba real 368EF se ha iniciado desde 2.2D7 sin modificar " +
            "gameplay ni inventario desde esta herramienta."
        );
    }

    private void ResetState()
    {
        StopSubscriptions();
        checks.Clear();
        runtimeIssues.Clear();
        invalidTransformDiagnostics.Clear();
        runtimeIssueOccurrences = 0;
        capturingRuntimeDiagnostics = false;
        observedOrderIds.Clear();
        trackedReservationIds.Clear();

        finalReport = string.Empty;
        phaseText = "Preparada.";

        running = false;
        captureGuard = false;
        harnessStarted = false;
        harnessFailure = false;
        harnessSuccessMarker = false;
        restoreSeen = false;
        postLoadValidated = false;
        activeReservationCaptured = false;
        consumedTrackedReservation = false;
        initialRuntimeStateValidated = false;
        postConsumptionValidated = false;
        emittedFinal = false;

        startedAt = 0.0;
        nextSnapshotAt = 0.0;
        restoreSeenAt = 0.0;

        activeSnapshot = null;
        postConsumptionSnapshot = null;
        postLoadSnapshot = null;

        legacyHarnessType = null;
        legacyHarnessWindow = null;
        legacyRunningStateKnown = false;
        legacyRunningState = false;
    }

    private bool RunPreflight(out string error)
    {
        error = string.Empty;

        Type inventoryType = ReflectionProbe.FindType("BistroBuilderInventoryService");
        if (inventoryType == null)
        {
            error = "No se encontró BistroBuilderInventoryService.";
            return false;
        }

        UnityEngine.Object inventory = ReflectionProbe.FindSceneObject(inventoryType);
        if (inventory == null)
        {
            error = "BistroBuilderInventoryService no está activo en la escena.";
            return false;
        }
        AddPass("BistroBuilderInventoryService real localizado en la escena.");

        UnityEngine.Object facade;
        string facadeDescription;
        if (!ReflectionProbe.TryResolve22DApplicationReadSource(
                inventory, out facade, out facadeDescription))
        {
            error =
                "No se pudo resolver la fuente Application de lectura de 2.2D por " +
                "contrato de datos. Se rechazaron Presentation y fuentes que no " +
                "coinciden con el inventario canónico. " + facadeDescription;
            return false;
        }

        AddPass(
            "Fuente Application de 2.2D localizada por contrato: " +
            facadeDescription + "."
        );

        legacyHarnessType =
            ReflectionProbe.FindType("BistroBuilderActiveServicePersistenceFunctionalTestWindow");
        if (legacyHarnessType == null)
        {
            error =
                "No se encontró BistroBuilderActiveServicePersistenceFunctionalTestWindow. " +
                "2.2D7 necesita esa prueba ya validada para provocar guardado/carga real " +
                "sin duplicar el harness de servicio.";
            return false;
        }
        AddPass("Harness real de servicio/persistencia 368EF localizado.");

        Type canonicalOrders = ReflectionProbe.FindType("BistroBuilderCanonicalOrderService");
        if (canonicalOrders == null || ReflectionProbe.FindSceneObject(canonicalOrders) == null)
        {
            error = "No está disponible BistroBuilderCanonicalOrderService en runtime.";
            return false;
        }
        AddPass("Servicio de comandas canónicas localizado.");

        Type lifecycle = ReflectionProbe.FindType("BistroBuilderOrderInventoryLifecycleService");
        if (lifecycle == null || ReflectionProbe.FindSceneObject(lifecycle) == null)
        {
            error = "No está disponible BistroBuilderOrderInventoryLifecycleService en runtime.";
            return false;
        }
        AddPass("Ciclo de vida real comanda → reserva → consumo localizado.");

        return true;
    }

    private bool StartLegacyHarness(out string error)
    {
        error = string.Empty;

        try
        {
            legacyHarnessWindow = EditorWindow.GetWindow(
                legacyHarnessType,
                false,
                "368EF servicio real",
                false
            );

            MethodInfo begin = ReflectionProbe.FindZeroArgumentMethod(
                legacyHarnessType,
                "BeginTest",
                "StartTest",
                "RunTest"
            );

            if (begin == null)
            {
                error =
                    "Se encontró la ventana 368EF, pero no su método de inicio " +
                    "(BeginTest/StartTest/RunTest).";
                return false;
            }

            begin.Invoke(legacyHarnessWindow, null);
            return true;
        }
        catch (TargetInvocationException ex)
        {
            Exception inner = ex.InnerException ?? ex;
            error = "El harness 368EF no pudo iniciarse: " + inner.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = "El harness 368EF no pudo iniciarse: " + ex.Message;
            return false;
        }
    }

    private void StartSubscriptions()
    {
        if (subscribed)
        {
            return;
        }

        Application.logMessageReceived += HandleLog;
        EditorApplication.update += Observe;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        subscribed = true;
    }

    private void StopSubscriptions()
    {
        if (!subscribed)
        {
            return;
        }

        Application.logMessageReceived -= HandleLog;
        EditorApplication.update -= Observe;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        subscribed = false;
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange change)
    {
        if (!running)
        {
            return;
        }

        if (change == PlayModeStateChange.ExitingPlayMode ||
            change == PlayModeStateChange.EnteredEditMode)
        {
            FailAndFinish(
                "Play Mode terminó antes de completar la validación 2.2D7A3."
            );
        }
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (!running || string.IsNullOrEmpty(condition))
        {
            return;
        }

        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            // 2.2D7A3 NO aborta en el primer error. Registra y deduplica los problemas,
            // pero deja continuar el servicio para separar dos resultados distintos:
            // 1) si la cadena funcional Inventario/2.2D es coherente de extremo a extremo;
            // 2) si el runtime completo permanece limpio.
            // El aprobado global sigue exigiendo CERO Error/Exception/Assert.
            CaptureRuntimeIssue(condition, stackTrace, type);

            if (condition.IndexOf("ValidTRS()", StringComparison.OrdinalIgnoreCase) >= 0 &&
                invalidTransformDiagnostics.Count == 0)
            {
                CaptureInvalidTransformDiagnostics();
            }

            phaseText =
                "Incidencias runtime registradas (" + runtimeIssueOccurrences +
                "); la prueba continúa para completar el diagnóstico funcional...";
        }

        Match orderMatch = OrderIdRegex.Match(condition);
        if (orderMatch.Success)
        {
            observedOrderIds.Add(orderMatch.Groups[1].Value);
        }

        if (condition.IndexOf("reservó ingredientes", StringComparison.OrdinalIgnoreCase) >= 0 ||
            condition.IndexOf("reservo ingredientes", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Este log se publica inmediatamente después de crear la reserva. Capturamos
            // EN ESE MISMO MOMENTO para evitar que una cocina rápida la consuma antes del
            // siguiente EditorApplication.update.
            TryCaptureActiveStateAtReservationCreation();
        }

        Match consumedMatch = ConsumedReservationRegex.Match(condition);
        if (consumedMatch.Success)
        {
            string reservationId = consumedMatch.Groups[1].Value;
            if (trackedReservationIds.Contains(reservationId))
            {
                consumedTrackedReservation = true;
                phaseText = "Reserva real consumida; verificando liberación...";
                TryCapturePostConsumptionState();
            }
        }

        if (condition.IndexOf("368EF service.runtime restaurado", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            restoreSeen = true;
            restoreSeenAt = EditorApplication.timeSinceStartup;
            phaseText = "Carga real completada; esperando estabilización...";
        }

        if (condition.IndexOf("PRUEBA REAL DE SERVICIO ACTIVO 368EF", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (condition.IndexOf("FALL", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                harnessFailure = true;
            }
            if (condition.IndexOf("SUPERAD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                condition.IndexOf("COMPLETAD", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                harnessSuccessMarker = true;
            }
        }
    }

    private void Observe()
    {
        if (!running)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;

        if (harnessFailure)
        {
            FailAndFinish("La prueba real 368EF informó de un fallo.");
            return;
        }

        if (now - startedAt > TimeoutSeconds)
        {
            FailAndFinish(BuildTimeoutReason());
            return;
        }

        ReadLegacyRunningState();

        if (now >= nextSnapshotAt)
        {
            nextSnapshotAt = now + SnapshotIntervalSeconds;

            if (!activeReservationCaptured)
            {
                TryCaptureActiveStateAtReservationCreation();
            }
            else if (consumedTrackedReservation && !postConsumptionValidated)
            {
                TryCapturePostConsumptionState();
            }
        }

        if (restoreSeen && !postLoadValidated &&
            now - restoreSeenAt >= PostLoadSettleSeconds)
        {
            TryCapturePostLoadState();
        }

        if (CanCompleteSuccessfully())
        {
            if (runtimeIssueOccurrences > 0)
            {
                CompleteFunctionalEvidenceWithRuntimeIssues();
            }
            else
            {
                CompleteSuccess();
            }
        }

        Repaint();
    }

    private void ReadLegacyRunningState()
    {
        if (legacyHarnessWindow == null || legacyHarnessType == null)
        {
            return;
        }

        bool value;
        if (ReflectionProbe.TryReadLikelyRunningBoolean(
                legacyHarnessWindow,
                out value
            ))
        {
            legacyRunningStateKnown = true;
            legacyRunningState = value;
        }
    }

    private void TryCaptureActiveStateAtReservationCreation()
    {
        if (captureGuard || activeReservationCaptured || !running)
        {
            return;
        }

        captureGuard = true;
        try
        {
            RuntimeSnapshot snapshot;
            string error;
            if (!RuntimeSnapshot.TryCapture(out snapshot, out error))
            {
                phaseText = "Esperando datos legibles de la reserva Active...";
                return;
            }

            if (snapshot.ActiveReservations.Count == 0)
            {
                phaseText = "Esperando una reserva Active real...";
                return;
            }

            string validationError;
            if (!ValidateSnapshot(snapshot, true, out validationError))
            {
                FailAndFinish("Reserva Active encontrada, pero la coherencia falló: " + validationError);
                return;
            }

            activeSnapshot = snapshot;
            activeReservationCaptured = true;
            initialRuntimeStateValidated = true;

            foreach (ReservationRecord reservation in snapshot.ActiveReservations)
            {
                trackedReservationIds.Add(reservation.ReservationId);
            }

            AddPass(
                "Se capturó al menos una reserva Active REAL antes de que cocina la consumiera."
            );
            AddPass(
                "Stock, Reservado y Disponible coinciden entre inventario canónico y fachada 2.2D."
            );
            AddPass(
                "Disponible = Stock - Reservado y no hay cantidades negativas."
            );
            AddPass(
                "La suma de líneas de reservas Active coincide con el Reservado canónico/2.2D " +
                "para los ingredientes enlazados."
            );

            phaseText = "Reserva Active validada; siguiendo su consumo real...";
        }
        finally
        {
            captureGuard = false;
        }
    }

    private void TryCapturePostConsumptionState()
    {
        if (captureGuard || postConsumptionValidated || !consumedTrackedReservation)
        {
            return;
        }

        captureGuard = true;
        try
        {
            RuntimeSnapshot snapshot;
            string error;
            if (!RuntimeSnapshot.TryCapture(out snapshot, out error))
            {
                phaseText = "Esperando lectura estable tras consumo...";
                return;
            }

            foreach (ReservationRecord active in snapshot.ActiveReservations)
            {
                if (trackedReservationIds.Contains(active.ReservationId))
                {
                    phaseText = "La reserva consumida aún figura Active; esperando siguiente estado...";
                    return;
                }
            }

            string validationError;
            if (!ValidateSnapshot(snapshot, false, out validationError))
            {
                FailAndFinish("Tras consumir la reserva, la coherencia falló: " + validationError);
                return;
            }

            postConsumptionSnapshot = snapshot;
            postConsumptionValidated = true;

            AddPass("La reserva seguida dejó de figurar como Active después del consumo real.");
            AddPass("2.2D permanece coherente con el inventario canónico después del consumo.");
            phaseText = "Consumo validado; esperando guardado/carga real 368EF...";
        }
        finally
        {
            captureGuard = false;
        }
    }

    private void TryCapturePostLoadState()
    {
        if (captureGuard || postLoadValidated || !restoreSeen)
        {
            return;
        }

        captureGuard = true;
        try
        {
            RuntimeSnapshot snapshot;
            string error;
            if (!RuntimeSnapshot.TryCapture(out snapshot, out error))
            {
                phaseText = "Esperando lectura estable después de Load...";
                return;
            }

            string validationError;
            if (!ValidateSnapshot(snapshot, false, out validationError))
            {
                FailAndFinish("Después del Load real, la coherencia falló: " + validationError);
                return;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ReservationRecord reservation in snapshot.ActiveReservations)
            {
                if (!ids.Add(reservation.ReservationId))
                {
                    FailAndFinish(
                        "Después del Load hay una reserva Active duplicada: " +
                        reservation.ReservationId
                    );
                    return;
                }
            }

            postLoadSnapshot = snapshot;
            postLoadValidated = true;

            AddPass("Se observó una restauración REAL de service.runtime mediante 368EF.");
            AddPass("Tras Load, inventario canónico y fachada 2.2D siguen coincidiendo.");
            AddPass("Tras Load no existen ReservationId Active duplicados.");
            phaseText = "Carga real validada; esperando cierre limpio del harness 368EF...";
        }
        finally
        {
            captureGuard = false;
        }
    }

    private bool ValidateSnapshot(
        RuntimeSnapshot snapshot,
        bool requireReservationLineEvidence,
        out string error
    )
    {
        error = string.Empty;

        if (snapshot.InventoryRows.Count == 0)
        {
            error = "no se pudieron leer filas agregadas del inventario canónico.";
            return false;
        }
        if (snapshot.FacadeRows.Count == 0)
        {
            error = "no se pudieron leer filas agregadas desde la fachada 2.2D.";
            return false;
        }

        int compared = 0;
        foreach (KeyValuePair<string, IngredientRecord> pair in snapshot.InventoryRows)
        {
            IngredientRecord canonical = pair.Value;
            IngredientRecord facade;
            if (!snapshot.FacadeRows.TryGetValue(pair.Key, out facade))
            {
                continue;
            }

            compared++;

            if (!canonical.IsInternallyCoherent(Tolerance))
            {
                error = "inventario canónico incoherente para " + pair.Key + ".";
                return false;
            }
            if (!facade.IsInternallyCoherent(Tolerance))
            {
                error = "fachada 2.2D incoherente para " + pair.Key + ".";
                return false;
            }

            if (!AlmostEqual(canonical.Total, facade.Total) ||
                !AlmostEqual(canonical.Available, facade.Available) ||
                !AlmostEqual(canonical.Reserved, facade.Reserved))
            {
                error =
                    "diferencia canónico/2.2D para " + pair.Key +
                    " (canónico: " + canonical.ToQuantityString() +
                    "; 2.2D: " + facade.ToQuantityString() + ").";
                return false;
            }
        }

        if (compared == 0)
        {
            error = "no existe ningún IngredientId comparable entre canónico y fachada 2.2D.";
            return false;
        }

        Dictionary<string, double> activeReserved =
            snapshot.BuildActiveReservedByIngredient();

        if (requireReservationLineEvidence && activeReserved.Count == 0)
        {
            error =
                "hay reserva(s) Active, pero no se pudieron demostrar sus líneas " +
                "IngredientId + cantidad.";
            return false;
        }

        foreach (KeyValuePair<string, double> reservationPair in activeReserved)
        {
            IngredientRecord canonical;
            IngredientRecord facade;
            if (!snapshot.InventoryRows.TryGetValue(reservationPair.Key, out canonical) ||
                !snapshot.FacadeRows.TryGetValue(reservationPair.Key, out facade))
            {
                error =
                    "la reserva Active usa " + reservationPair.Key +
                    " pero ese ingrediente no es comparable en canónico/2.2D.";
                return false;
            }

            if (!AlmostEqual(canonical.Reserved, reservationPair.Value) ||
                !AlmostEqual(facade.Reserved, reservationPair.Value))
            {
                error =
                    "Reservado no coincide con la suma de reservas Active para " +
                    reservationPair.Key + " (Active=" + F(reservationPair.Value) +
                    ", canónico=" + F(canonical.Reserved) +
                    ", 2.2D=" + F(facade.Reserved) + ").";
                return false;
            }
        }

        return true;
    }

    private bool CanCompleteSuccessfully()
    {
        if (!harnessStarted || harnessFailure)
        {
            return false;
        }

        if (observedOrderIds.Count == 0 ||
            !activeReservationCaptured ||
            !initialRuntimeStateValidated ||
            !consumedTrackedReservation ||
            !postConsumptionValidated ||
            !restoreSeen ||
            !postLoadValidated)
        {
            return false;
        }

        if (harnessSuccessMarker)
        {
            return true;
        }

        // Fallback seguro: solo se acepta si podemos demostrar por reflexión que
        // el harness estuvo disponible y ya no está ejecutándose DESPUÉS del Load.
        // Si no podemos demostrarlo, no damos aprobado y esperamos al timeout.
        return legacyRunningStateKnown && !legacyRunningState;
    }

    private void CompleteSuccess()
    {
        if (emittedFinal)
        {
            return;
        }

        AddPass("Se observó al menos una OrderId canónica real durante el servicio.");
        AddPass("La ejecución terminó sin Error, Exception ni Assert capturados por 2.2D7A3.");

        emittedFinal = true;
        running = false;
        phaseText = "SUPERADA.";
        StopSubscriptions();

        finalReport = BuildReport(true, string.Empty);
        Debug.Log(finalReport);
        Repaint();
    }

    private void CompleteFunctionalEvidenceWithRuntimeIssues()
    {
        if (emittedFinal)
        {
            return;
        }

        AddPass("Se observó al menos una OrderId canónica real durante el servicio.");
        AddPass(
            "La cadena funcional 2.2D llegó a completarse pese a las incidencias runtime: " +
            "reserva Active, consumo y Load quedaron observados."
        );

        string reason =
            "La validación funcional de 2.2D se completó, pero el runtime no está limpio: " +
            runtimeIssueOccurrences + " Error/Exception/Assert en " +
            runtimeIssues.Count + " firma(s) única(s).";

        emittedFinal = true;
        running = false;
        phaseText = "FUNCIONAL OK / RUNTIME FALLIDO.";
        StopSubscriptions();

        AddFail(reason);
        finalReport = BuildReport(false, reason);
        Debug.LogError(finalReport);
        Repaint();
    }

    private void FailAndFinish(string reason)
    {
        if (emittedFinal)
        {
            return;
        }

        emittedFinal = true;
        running = false;
        phaseText = "FALLIDA.";
        StopSubscriptions();

        AddFail(reason);
        finalReport = BuildReport(false, reason);
        Debug.LogError(finalReport);
        Repaint();
    }

    private string BuildTimeoutReason()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Timeout sin evidencia suficiente. Pendiente:");
        if (observedOrderIds.Count == 0) sb.Append(" OrderId real;");
        if (!activeReservationCaptured) sb.Append(" reserva Active + cantidades;");
        if (!consumedTrackedReservation) sb.Append(" consumo de reserva seguida;");
        if (!postConsumptionValidated) sb.Append(" coherencia post-consumo;");
        if (!restoreSeen) sb.Append(" Load real 368EF;");
        if (!postLoadValidated) sb.Append(" coherencia post-Load;");
        if (!harnessSuccessMarker && !(legacyRunningStateKnown && !legacyRunningState))
            sb.Append(" cierre demostrable del harness 368EF;");
        return sb.ToString();
    }

    private string BuildReport(bool success, string reason)
    {
        int passed = checks.Count(c => c.Passed);
        int failed = checks.Count(c => !c.Passed);

        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine(success
            ? "PRUEBA RUNTIME COMPLETA 2.2D7A3 SUPERADA"
            : "PRUEBA RUNTIME COMPLETA 2.2D7A3 FALLIDA");
        sb.AppendLine();
        sb.AppendLine("Correctos: " + passed);
        sb.AppendLine("Fallos: " + failed);
        sb.AppendLine("Errores/Excepciones/Asserts capturados: " + runtimeIssueOccurrences);
        sb.AppendLine("Firmas runtime únicas: " + runtimeIssues.Count);
        sb.AppendLine("OrderId reales observadas: " + observedOrderIds.Count);
        sb.AppendLine("Reservas Active capturadas: " +
            (activeSnapshot != null ? activeSnapshot.ActiveReservations.Count : 0));
        sb.AppendLine("Load real observado: " + (restoreSeen ? "sí" : "no"));
        sb.AppendLine();

        for (int i = 0; i < checks.Count; i++)
        {
            CheckResult check = checks[i];
            sb.AppendLine((check.Passed ? "[OK] " : "[FALLO] ") + check.Message);
        }

        if (runtimeIssues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("INCIDENCIAS RUNTIME (DEDUPLICADAS):");
            int count = Math.Min(runtimeIssues.Count, 8);
            for (int i = 0; i < count; i++)
            {
                RuntimeIssue issue = runtimeIssues[i];
                sb.AppendLine(
                    "- [" + issue.Type + "] x" + issue.Count +
                    " | t+" + issue.FirstSeenSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    "s | " + issue.Condition
                );

                string compactStack = CompactStack(issue.StackTrace, 6);
                if (!string.IsNullOrWhiteSpace(compactStack))
                {
                    sb.AppendLine(compactStack);
                }
                else
                {
                    sb.AppendLine("    (Unity no entregó stack trace para esta incidencia)");
                }
            }

            if (invalidTransformDiagnostics.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("DIAGNÓSTICO ValidTRS / TRANSFORMS NO FINITOS:");
                for (int i = 0; i < invalidTransformDiagnostics.Count; i++)
                {
                    sb.AppendLine("- " + invalidTransformDiagnostics[i]);
                }
            }
        }

        if (!success && !string.IsNullOrWhiteSpace(reason))
        {
            sb.AppendLine();
            sb.AppendLine("MOTIVO FINAL:");
            sb.AppendLine(reason);
        }

        sb.AppendLine();
        sb.AppendLine(
            "Nota: la prueba 2.2D7A3 complementa, no sustituye, la prueba funcional " +
            "2.2D de 39/0. Al terminar, salir de Play Mode permite al harness 368EF " +
            "restaurar los cambios temporales de escena."
        );

        return sb.ToString();
    }

    private void CaptureRuntimeIssue(
        string condition,
        string stackTrace,
        LogType type
    )
    {
        runtimeIssueOccurrences++;

        string normalizedCondition = condition ?? string.Empty;
        string normalizedStack = stackTrace ?? string.Empty;
        string signature =
            type + "|" + normalizedCondition + "|" + FirstMeaningfulStackLine(normalizedStack);

        for (int i = 0; i < runtimeIssues.Count; i++)
        {
            if (runtimeIssues[i].Signature == signature)
            {
                runtimeIssues[i].Count++;
                return;
            }
        }

        runtimeIssues.Add(
            new RuntimeIssue(
                signature,
                type,
                normalizedCondition,
                normalizedStack,
                Math.Max(0.0, EditorApplication.timeSinceStartup - startedAt)
            )
        );
    }

    private void CaptureInvalidTransformDiagnostics()
    {
        if (capturingRuntimeDiagnostics)
        {
            return;
        }

        capturingRuntimeDiagnostics = true;
        try
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            int invalidCount = 0;

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null)
                {
                    continue;
                }

                Vector3 position = transform.localPosition;
                Quaternion rotation = transform.localRotation;
                Vector3 scale = transform.localScale;

                if (IsFinite(position) && IsFinite(rotation) && IsFinite(scale))
                {
                    continue;
                }

                invalidCount++;
                if (invalidTransformDiagnostics.Count < 24)
                {
                    invalidTransformDiagnostics.Add(
                        BuildTransformPath(transform) +
                        " | localPosition=" + position +
                        " | localRotation=" + rotation +
                        " | localScale=" + scale
                    );
                }
            }

            if (invalidCount == 0)
            {
                invalidTransformDiagnostics.Add(
                    "No se encontró ningún Transform cargado con componentes locales NaN/Infinity. " +
                    "El origen deberá localizarse con el stack trace capturado por Unity."
                );
            }
            else if (invalidCount > invalidTransformDiagnostics.Count)
            {
                invalidTransformDiagnostics.Add(
                    "... y " + (invalidCount - invalidTransformDiagnostics.Count) +
                    " Transform(s) no finitos adicionales."
                );
            }
        }
        catch (Exception ex)
        {
            invalidTransformDiagnostics.Add(
                "El escaneo diagnóstico de Transform falló: " + ex.Message
            );
        }
        finally
        {
            capturingRuntimeDiagnostics = false;
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) &&
               IsFinite(value.z) && IsFinite(value.w);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string BuildTransformPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        List<string> names = new List<string>();
        Transform current = transform;
        int guard = 0;

        while (current != null && guard++ < 64)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names.ToArray());
    }

    private static string FirstMeaningfulStackLine(string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return string.Empty;
        }

        string[] lines = stackTrace.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line))
            {
                return line;
            }
        }

        return string.Empty;
    }

    private static string CompactStack(string stackTrace, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(stackTrace) || maxLines <= 0)
        {
            return string.Empty;
        }

        string[] lines = stackTrace.Replace("\r", string.Empty).Split('\n');
        StringBuilder sb = new StringBuilder();
        int written = 0;

        for (int i = 0; i < lines.Length && written < maxLines; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            sb.Append("    ");
            sb.AppendLine(line);
            written++;
        }

        return sb.ToString().TrimEnd();
    }

    private void AddPass(string message)
    {
        if (checks.Any(c => c.Passed && c.Message == message))
        {
            return;
        }
        checks.Add(new CheckResult(true, message));
    }

    private void AddFail(string message)
    {
        if (checks.Any(c => !c.Passed && c.Message == message))
        {
            return;
        }
        checks.Add(new CheckResult(false, message));
    }

    private static bool AlmostEqual(double a, double b)
    {
        return Math.Abs(a - b) <= Tolerance;
    }

    private static string F(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private sealed class RuntimeIssue
    {
        public readonly string Signature;
        public readonly LogType Type;
        public readonly string Condition;
        public readonly string StackTrace;
        public readonly double FirstSeenSeconds;
        public int Count;

        public RuntimeIssue(
            string signature,
            LogType type,
            string condition,
            string stackTrace,
            double firstSeenSeconds
        )
        {
            Signature = signature ?? string.Empty;
            Type = type;
            Condition = condition ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            FirstSeenSeconds = firstSeenSeconds;
            Count = 1;
        }
    }

    private sealed class CheckResult
    {
        public readonly bool Passed;
        public readonly string Message;

        public CheckResult(bool passed, string message)
        {
            Passed = passed;
            Message = message ?? string.Empty;
        }
    }

    private sealed class RuntimeSnapshot
    {
        public readonly Dictionary<string, IngredientRecord> InventoryRows;
        public readonly Dictionary<string, IngredientRecord> FacadeRows;
        public readonly List<ReservationRecord> ActiveReservations;

        private RuntimeSnapshot(
            Dictionary<string, IngredientRecord> inventoryRows,
            Dictionary<string, IngredientRecord> facadeRows,
            List<ReservationRecord> activeReservations)
        {
            InventoryRows = inventoryRows;
            FacadeRows = facadeRows;
            ActiveReservations = activeReservations;
        }

        public static bool TryCapture(out RuntimeSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;

            Type inventoryType = ReflectionProbe.FindType("BistroBuilderInventoryService");
            UnityEngine.Object inventory = ReflectionProbe.FindSceneObject(inventoryType);
            if (inventory == null)
            {
                error = "inventario canónico no disponible.";
                return false;
            }

            UnityEngine.Object facade;
            string facadeDescription;
            if (!ReflectionProbe.TryResolve22DApplicationReadSource(
                    inventory, out facade, out facadeDescription))
            {
                error = "fuente Application 2.2D no disponible: " + facadeDescription;
                return false;
            }

            Dictionary<string, IngredientRecord> inventoryRows =
                ReflectionProbe.ExtractIngredientRows(inventory, false);
            Dictionary<string, IngredientRecord> facadeRows =
                ReflectionProbe.ExtractIngredientRows(facade, true);
            List<ReservationRecord> reservations =
                ReflectionProbe.ExtractActiveReservations(inventory);

            if (inventoryRows.Count == 0)
            {
                error = "sin filas agregadas canónicas legibles.";
                return false;
            }
            if (facadeRows.Count == 0)
            {
                error = "sin filas agregadas 2.2D legibles.";
                return false;
            }

            snapshot = new RuntimeSnapshot(inventoryRows, facadeRows, reservations);
            return true;
        }

        public Dictionary<string, double> BuildActiveReservedByIngredient()
        {
            Dictionary<string, double> result =
                new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (ReservationRecord reservation in ActiveReservations)
            {
                foreach (KeyValuePair<string, double> line in reservation.ByIngredient)
                {
                    double current;
                    result.TryGetValue(line.Key, out current);
                    result[line.Key] = current + line.Value;
                }
            }

            return result;
        }
    }

    private sealed class IngredientRecord
    {
        public string IngredientId;
        public double Total;
        public double Available;
        public double Reserved;
        public int Score;
        public string SourceType;

        public bool IsInternallyCoherent(double tolerance)
        {
            if (Total < -tolerance || Available < -tolerance || Reserved < -tolerance)
            {
                return false;
            }
            return Math.Abs(Total - (Available + Reserved)) <= tolerance;
        }

        public string ToQuantityString()
        {
            return "stock=" + F(Total) +
                   ", reservado=" + F(Reserved) +
                   ", disponible=" + F(Available);
        }
    }

    private sealed class ReservationRecord
    {
        public string ReservationId;
        public readonly Dictionary<string, double> ByIngredient =
            new Dictionary<string, double>(StringComparer.Ordinal);
    }

    private static class ReflectionProbe
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly string[] IngredientIdAliases =
        {
            "ingredientid", "ingredientdefinitionid", "canonicalingredientid"
        };

        private static readonly string[] TotalAliases =
        {
            "totalquantity", "stockquantity", "physicalquantity", "onhandquantity",
            "totalstock", "stocktotal", "currentstock", "stock",
            "totalstockquantity", "stocktotalquantity", "quantitytotal",
            "quantityonhand", "onhand", "physicalstock", "totalonhandquantity"
        };

        private static readonly string[] AvailableAliases =
        {
            "availablequantity", "available", "availablestock", "stockavailable",
            "usablequantity", "freequantity", "quantityavailable",
            "unreservedquantity", "availabletouse", "availableamount"
        };

        private static readonly string[] ReservedAliases =
        {
            "reservedquantity", "reserved", "reservedstock", "stockreserved",
            "committedquantity", "quantityreserved", "reservedamount",
            "committed", "committedamount"
        };

        private static readonly string[] ReservationIdAliases =
        {
            "reservationid", "inventoryreservationid"
        };

        private static readonly string[] StateAliases =
        {
            "state", "status", "reservationstate", "reservationstatus"
        };

        private static readonly string[] LineQuantityAliases =
        {
            "reservedquantity", "quantity", "amount", "requiredquantity",
            "requestedquantity", "ingredientquantity"
        };

        public static Type FindType(string simpleOrFullName)
        {
            if (string.IsNullOrWhiteSpace(simpleOrFullName))
            {
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                try
                {
                    Type direct = assembly.GetType(simpleOrFullName, false);
                    if (direct != null)
                    {
                        return direct;
                    }

                    Type[] types = GetTypesSafe(assembly);
                    for (int j = 0; j < types.Length; j++)
                    {
                        Type type = types[j];
                        if (type != null &&
                            string.Equals(type.Name, simpleOrFullName, StringComparison.Ordinal))
                        {
                            return type;
                        }
                    }
                }
                catch
                {
                    // Otra assembly puede contener el tipo.
                }
            }
            return null;
        }

        public static Type FindTypeByNameParts(params string[] parts)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types = GetTypesSafe(assemblies[i]);
                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];
                    if (type == null)
                    {
                        continue;
                    }

                    string name = type.Name;
                    bool matches = true;
                    for (int p = 0; p < parts.Length; p++)
                    {
                        if (name.IndexOf(parts[p], StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            matches = false;
                            break;
                        }
                    }
                    if (matches)
                    {
                        return type;
                    }
                }
            }
            return null;
        }

        public static bool TryResolve22DApplicationReadSource(
            object canonicalInventory,
            out UnityEngine.Object source,
            out string description)
        {
            source = null;
            description = string.Empty;

            if (canonicalInventory == null)
            {
                description = "Inventario canónico nulo.";
                return false;
            }

            Dictionary<string, IngredientRecord> canonicalRows =
                ExtractIngredientRows(canonicalInventory, false);
            if (canonicalRows.Count == 0)
            {
                description = "El inventario canónico no expone filas agregadas legibles.";
                return false;
            }

            /*
             * 2.2D7A3 deja de adivinar el nombre de la fachada.
             * La prueba funcional 2.2D de 39/0 ya compila contra el contrato real,
             * por lo que usamos la metadata de RunAutomaticTest como fuente de verdad:
             * sus variables locales contienen los tipos concretos que la propia prueba
             * utiliza (fachada, UI, planning, recepciones, etc.).
             */
            string contractDetail;
            List<Type> contractTypes =
                Get22DContractTypesFromFunctionalTest(out contractDetail);

            UnityEngine.Object best = null;
            string bestName = string.Empty;
            int bestScore = int.MinValue;
            int exactContractObjects = 0;
            int readableContractObjects = 0;

            for (int i = 0; i < contractTypes.Count; i++)
            {
                Type type = contractTypes[i];
                if (!IsAllowed22DApplicationSourceType(type, false))
                {
                    continue;
                }

                List<UnityEngine.Object> candidates = FindSceneObjects(type);
                for (int c = 0; c < candidates.Count; c++)
                {
                    UnityEngine.Object candidate = candidates[c];
                    if (candidate == null || ReferenceEquals(candidate, canonicalInventory))
                    {
                        continue;
                    }

                    exactContractObjects++;

                    Dictionary<string, IngredientRecord> rows =
                        ExtractIngredientRows(candidate, true);
                    if (rows.Count == 0)
                    {
                        continue;
                    }

                    readableContractObjects++;

                    int overlap;
                    int exact;
                    if (!RowsRepresentCanonicalState(
                            canonicalRows,
                            rows,
                            out overlap,
                            out exact))
                    {
                        continue;
                    }

                    int score = Score22DSourceType(type, overlap, exact, true);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        bestName =
                            (type.FullName ?? type.Name) +
                            " [contrato=RunAutomaticTest, filas=" +
                            rows.Count.ToString(CultureInfo.InvariantCulture) +
                            ", solapadas=" +
                            overlap.ToString(CultureInfo.InvariantCulture) +
                            ", exactas=" +
                            exact.ToString(CultureInfo.InvariantCulture) +
                            "]";
                    }
                }
            }

            if (best != null)
            {
                source = best;
                description = bestName;
                return true;
            }

            /*
             * Fallback defensivo: si Unity no conserva locales suficientes en la
             * metadata del método, recorremos los componentes runtime. A diferencia
             * de 2.2D7A, aquí las consultas de lectura con parámetros/out/buffer sí
             * se ejecutan de forma segura, por lo que una fachada que exponga, por
             * ejemplo, Copy/Query(filter, sort, buffer) también es legible.
             */
            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            int inspectedInventoryComponents = 0;
            int readableCandidates = 0;

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || ReferenceEquals(behaviour, canonicalInventory))
                {
                    continue;
                }
                if (!behaviour.gameObject.scene.IsValid() ||
                    !behaviour.gameObject.scene.isLoaded)
                {
                    continue;
                }

                Type type = behaviour.GetType();
                string fullName = type.FullName ?? type.Name;
                string lower = fullName.ToLowerInvariant();

                if (!lower.Contains("inventory"))
                {
                    continue;
                }
                inspectedInventoryComponents++;

                if (!IsAllowed22DApplicationSourceType(type, true))
                {
                    continue;
                }

                Dictionary<string, IngredientRecord> rows =
                    ExtractIngredientRows(behaviour, true);
                if (rows.Count == 0)
                {
                    continue;
                }
                readableCandidates++;

                int overlap;
                int exact;
                if (!RowsRepresentCanonicalState(
                        canonicalRows,
                        rows,
                        out overlap,
                        out exact))
                {
                    continue;
                }

                int score = Score22DSourceType(type, overlap, exact, false);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = behaviour;
                    bestName =
                        fullName +
                        " [fallback, filas=" +
                        rows.Count.ToString(CultureInfo.InvariantCulture) +
                        ", solapadas=" +
                        overlap.ToString(CultureInfo.InvariantCulture) +
                        ", exactas=" +
                        exact.ToString(CultureInfo.InvariantCulture) +
                        "]";
                }
            }

            if (best == null)
            {
                description =
                    "Contrato 39/0: " + contractDetail +
                    "; objetos runtime de tipos del contrato=" +
                    exactContractObjects.ToString(CultureInfo.InvariantCulture) +
                    ", objetos del contrato con lectura agregada=" +
                    readableContractObjects.ToString(CultureInfo.InvariantCulture) +
                    "; componentes runtime con 'Inventory' inspeccionados=" +
                    inspectedInventoryComponents.ToString(CultureInfo.InvariantCulture) +
                    ", candidatos fallback con filas legibles=" +
                    readableCandidates.ToString(CultureInfo.InvariantCulture) +
                    ".";
                return false;
            }

            source = best;
            description = bestName;
            return true;
        }

        private static List<Type> Get22DContractTypesFromFunctionalTest(
            out string detail)
        {
            List<Type> result = new List<Type>();
            HashSet<Type> seen = new HashSet<Type>();

            Type testType =
                FindType("BistroBuilderInventoryWarehouse22DFunctionalTestWindow");
            if (testType == null)
            {
                detail = "no se encontró BistroBuilderInventoryWarehouse22DFunctionalTestWindow";
                return result;
            }

            MethodInfo run = testType.GetMethod(
                "RunAutomaticTest",
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic
            );

            if (run != null)
            {
                try
                {
                    MethodBody body = run.GetMethodBody();
                    if (body != null)
                    {
                        IList<LocalVariableInfo> locals = body.LocalVariables;
                        for (int i = 0; i < locals.Count; i++)
                        {
                            AddContractTypeTree(
                                locals[i].LocalType,
                                result,
                                seen,
                                0
                            );
                        }
                    }
                }
                catch
                {
                    // Seguimos con campos y firmas; la falta de metadata local
                    // no convierte el preflight en un falso positivo.
                }
            }

            FieldInfo[] fields = testType.GetFields(
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic
            );
            for (int i = 0; i < fields.Length; i++)
            {
                AddContractTypeTree(fields[i].FieldType, result, seen, 0);
            }

            List<string> candidateNames = new List<string>();
            for (int i = 0; i < result.Count && candidateNames.Count < 12; i++)
            {
                Type candidateType = result[i];
                if (!IsAllowed22DApplicationSourceType(candidateType, false))
                {
                    continue;
                }

                candidateNames.Add(candidateType.FullName ?? candidateType.Name);
            }

            detail =
                "tipos extraídos de la prueba funcional=" +
                result.Count.ToString(CultureInfo.InvariantCulture) +
                ", candidatos Application=" +
                candidateNames.Count.ToString(CultureInfo.InvariantCulture) +
                (candidateNames.Count > 0
                    ? " [" + string.Join(", ", candidateNames.ToArray()) + "]"
                    : string.Empty);
            return result;
        }

        private static void AddContractTypeTree(
            Type type,
            List<Type> result,
            HashSet<Type> seen,
            int depth)
        {
            if (type == null || depth > 3 || !seen.Add(type))
            {
                return;
            }

            result.Add(type);

            if (type.IsByRef || type.IsArray || type.IsPointer)
            {
                AddContractTypeTree(
                    type.GetElementType(),
                    result,
                    seen,
                    depth + 1
                );
            }

            if (type.IsGenericType)
            {
                Type[] args = type.GetGenericArguments();
                for (int i = 0; i < args.Length; i++)
                {
                    AddContractTypeTree(args[i], result, seen, depth + 1);
                }
            }
        }

        private static bool IsAllowed22DApplicationSourceType(
            Type type,
            bool requireInventorySemanticName)
        {
            if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return false;
            }

            string lower = (type.FullName ?? type.Name).ToLowerInvariant();
            if (lower.Contains("presentation") || lower.Contains("editor") ||
                lower.Contains("save") || lower.Contains("persistence") ||
                lower.Contains("lifecycle") || lower.Contains("reservation") ||
                lower.Contains("receipt") || lower.Contains("delivery") ||
                lower.Contains("movement") || lower.Contains("policy") ||
                lower.Contains("forecast") || lower.Contains("alert") ||
                lower.Contains("panel") || lower.Contains("screen") ||
                lower.Contains("hud") || lower.EndsWith("view"))
            {
                return false;
            }

            return !requireInventorySemanticName ||
                   lower.Contains("inventory") ||
                   lower.Contains("warehouse") ||
                   lower.Contains("stock");
        }

        private static bool RowsRepresentCanonicalState(
            Dictionary<string, IngredientRecord> canonicalRows,
            Dictionary<string, IngredientRecord> rows,
            out int overlap,
            out int exact)
        {
            overlap = 0;
            exact = 0;

            foreach (KeyValuePair<string, IngredientRecord> pair in rows)
            {
                IngredientRecord canonical;
                if (!canonicalRows.TryGetValue(pair.Key, out canonical))
                {
                    continue;
                }

                overlap++;
                IngredientRecord candidate = pair.Value;
                if (Math.Abs(canonical.Total - candidate.Total) <= 0.0001 &&
                    Math.Abs(canonical.Available - candidate.Available) <= 0.0001 &&
                    Math.Abs(canonical.Reserved - candidate.Reserved) <= 0.0001)
                {
                    exact++;
                }
            }

            int minimumOverlap =
                Math.Min(3, Math.Min(canonicalRows.Count, rows.Count));
            if (minimumOverlap <= 0 || overlap < minimumOverlap)
            {
                return false;
            }

            return exact * 5 >= overlap * 4;
        }

        private static int Score22DSourceType(
            Type type,
            int overlap,
            int exact,
            bool comesFromFunctionalTestContract)
        {
            string lower = (type.FullName ?? type.Name).ToLowerInvariant();
            int score = overlap * 100 + exact * 200;

            if (comesFromFunctionalTestContract) score += 10000;
            if (lower.Contains("warehouse")) score += 80;
            if (lower.Contains("application")) score += 60;
            if (lower.Contains("administration")) score += 50;
            if (lower.Contains("read")) score += 40;
            if (lower.Contains("query")) score += 40;
            if (lower.Contains("summary")) score += 30;
            if (lower.Contains("overview")) score += 30;
            if (lower.Contains("facade")) score += 30;

            // El servicio canónico nunca debe ganar como "fachada".
            if (type.Name.IndexOf(
                    "InventoryService",
                    StringComparison.OrdinalIgnoreCase) >= 0 &&
                !lower.Contains("warehouse") &&
                !lower.Contains("administration") &&
                !lower.Contains("read") &&
                !lower.Contains("query") &&
                !lower.Contains("overview"))
            {
                score -= 5000;
            }

            return score;
        }

        public static UnityEngine.Object FindSceneObject(Type type)
        {
            List<UnityEngine.Object> candidates = FindSceneObjects(type);
            return candidates.Count > 0 ? candidates[0] : null;
        }

        private static List<UnityEngine.Object> FindSceneObjects(Type type)
        {
            List<UnityEngine.Object> result = new List<UnityEngine.Object>();
            if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return result;
            }

            UnityEngine.Object[] candidates = Resources.FindObjectsOfTypeAll(type);
            for (int i = 0; i < candidates.Length; i++)
            {
                UnityEngine.Object candidate = candidates[i];
                if (candidate == null || candidate is EditorWindow)
                {
                    continue;
                }

                Component component = candidate as Component;
                if (component != null)
                {
                    if (component.gameObject.scene.IsValid() &&
                        component.gameObject.scene.isLoaded)
                    {
                        result.Add(candidate);
                    }
                    continue;
                }

                // Una fachada Application también puede ser ScriptableObject.
                // Solo se admite porque el tipo procede del contrato 39/0 y
                // después debe demostrar igualdad numérica con el canónico.
                result.Add(candidate);
            }

            return result;
        }

        public static MethodInfo FindZeroArgumentMethod(Type type, params string[] preferredNames)
        {
            if (type == null)
            {
                return null;
            }

            MethodInfo[] methods = type.GetMethods(InstanceFlags);
            for (int n = 0; n < preferredNames.Length; n++)
            {
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.GetParameters().Length == 0 &&
                        string.Equals(method.Name, preferredNames[n], StringComparison.Ordinal))
                    {
                        return method;
                    }
                }
            }
            return null;
        }

        public static bool TryReadLikelyRunningBoolean(object target, out bool value)
        {
            value = false;
            if (target == null)
            {
                return false;
            }

            Type type = target.GetType();
            FieldInfo[] fields = type.GetFields(InstanceFlags);
            string[] preferred =
            {
                "isRunning", "running", "testRunning", "isTestRunning",
                "testInProgress", "isTestInProgress"
            };

            for (int p = 0; p < preferred.Length; p++)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field.FieldType == typeof(bool) &&
                        string.Equals(field.Name, preferred[p], StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            value = (bool)field.GetValue(target);
                            return true;
                        }
                        catch { }
                    }
                }
            }
            return false;
        }

        public static Dictionary<string, IngredientRecord> ExtractIngredientRows(
            object root,
            bool allowSafeQueries)
        {
            Dictionary<string, IngredientRecord> best =
                new Dictionary<string, IngredientRecord>(StringComparer.Ordinal);
            HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            int budget = 5000;

            TraverseForIngredientRows(root, 0, 5, visited, ref budget, best);

            if (allowSafeQueries)
            {
                InvokeSafeReadQueries(root, best);
            }

            return best;
        }

        private static void InvokeSafeReadQueries(
            object root,
            Dictionary<string, IngredientRecord> best)
        {
            if (root == null)
            {
                return;
            }

            MethodInfo[] methods = root.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public
            );
            int invoked = 0;

            for (int i = 0; i < methods.Length && invoked < 24; i++)
            {
                MethodInfo method = methods[i];
                if (!IsSafeReadMethod(method))
                {
                    continue;
                }

                object[] args;
                if (!TryBuildSafeReadArguments(method, out args))
                {
                    continue;
                }

                try
                {
                    object returned = method.Invoke(root, args);
                    invoked++;

                    TraverseQueryResult(returned, best);

                    ParameterInfo[] parameters = method.GetParameters();
                    for (int p = 0; p < parameters.Length; p++)
                    {
                        // Los out/ref y buffers mutables son parte del resultado
                        // de muchos contratos Application de lectura.
                        if (parameters[p].IsOut ||
                            parameters[p].ParameterType.IsByRef ||
                            IsMutableReadBuffer(parameters[p].ParameterType))
                        {
                            TraverseQueryResult(args[p], best);
                        }
                    }
                }
                catch
                {
                    // Una sobrecarga opcional no legible no invalida la prueba.
                    // Si ninguna consulta produce filas, el preflight fallará.
                }
            }
        }

        private static bool IsSafeReadMethod(MethodInfo method)
        {
            if (method == null || method.IsGenericMethodDefinition || method.IsStatic)
            {
                return false;
            }

            Type declaringType = method.DeclaringType;
            if (declaringType == typeof(object) ||
                declaringType == typeof(UnityEngine.Object) ||
                declaringType == typeof(Component) ||
                declaringType == typeof(Behaviour) ||
                declaringType == typeof(MonoBehaviour))
            {
                return false;
            }

            string declaringNamespace =
                declaringType != null ? declaringType.Namespace : string.Empty;
            if (!string.IsNullOrEmpty(declaringNamespace) &&
                declaringNamespace.StartsWith(
                    "UnityEngine",
                    StringComparison.Ordinal))
            {
                return false;
            }

            string name = method.Name;
            string lower = name.ToLowerInvariant();

            bool readVerb =
                lower.StartsWith("get") ||
                lower.StartsWith("read") ||
                lower.StartsWith("query") ||
                lower.StartsWith("copy") ||
                lower.StartsWith("tryget") ||
                lower.StartsWith("tryread") ||
                lower.StartsWith("tryquery") ||
                lower.StartsWith("trycopy") ||
                lower.StartsWith("buildread") ||
                lower.StartsWith("buildoverview") ||
                lower.StartsWith("buildinventory") ||
                lower.StartsWith("createoverview");

            if (!readVerb)
            {
                return false;
            }

            // Blindaje frente a comandos aunque empiecen con Get/TryGet por azar.
            string[] forbidden =
            {
                "set", "adjust", "receive", "apply", "modify", "change",
                "consume", "reserve", "release", "expire", "delete", "save",
                "load", "restore", "rebuild", "refresh", "initialize"
            };
            for (int i = 0; i < forbidden.Length; i++)
            {
                if (lower.Contains(forbidden[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryBuildSafeReadArguments(
            MethodInfo method,
            out object[] args)
        {
            ParameterInfo[] parameters = method.GetParameters();
            args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                Type declaredType = parameter.ParameterType;
                Type type = declaredType.IsByRef
                    ? declaredType.GetElementType()
                    : declaredType;

                if (parameter.IsOptional)
                {
                    object defaultValue = parameter.DefaultValue;
                    if (defaultValue != DBNull.Value && defaultValue != Missing.Value)
                    {
                        args[i] = defaultValue;
                        continue;
                    }
                }

                if (parameter.IsOut)
                {
                    args[i] = CreateSafeDefaultValue(type, parameter.Name);
                    continue;
                }

                object value;
                if (!TryCreateSafeInputValue(type, parameter.Name, out value))
                {
                    return false;
                }
                args[i] = value;
            }

            return true;
        }

        private static bool TryCreateSafeInputValue(
            Type type,
            string parameterName,
            out object value)
        {
            value = null;
            if (type == null)
            {
                return false;
            }

            if (type == typeof(string))
            {
                value = string.Empty;
                return true;
            }
            if (type == typeof(bool))
            {
                value = false;
                return true;
            }
            if (type.IsEnum)
            {
                value = SelectSafeEnumValue(type, parameterName);
                return true;
            }
            if (type.IsPrimitive || type == typeof(decimal))
            {
                value = Activator.CreateInstance(type);
                return true;
            }
            if (type == typeof(DateTime))
            {
                value = DateTime.Now;
                return true;
            }

            object buffer = CreateMutableCollectionFor(type);
            if (buffer != null)
            {
                value = buffer;
                return true;
            }

            if (type.IsValueType)
            {
                value = Activator.CreateInstance(type);
                return true;
            }

            string lowerTypeName = (type.FullName ?? type.Name).ToLowerInvariant();
            bool looksLikeReadOptions =
                lowerTypeName.Contains("query") ||
                lowerTypeName.Contains("filter") ||
                lowerTypeName.Contains("criteria") ||
                lowerTypeName.Contains("options") ||
                lowerTypeName.Contains("readrequest") ||
                lowerTypeName.Contains("readmodelrequest");

            if (looksLikeReadOptions && !typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                try
                {
                    object options = Activator.CreateInstance(type);
                    InitializeSafeReadOptions(options);
                    value = options;
                    return true;
                }
                catch
                {
                    // Si el objeto de consulta requiere dependencias no inventamos valores.
                }
            }

            // No inventamos UnityEngine.Object ni servicios de dominio como entrada.
            return false;
        }

        private static void InitializeSafeReadOptions(object options)
        {
            if (options == null)
            {
                return;
            }

            Type type = options.GetType();
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance | BindingFlags.Public
            );
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsInitOnly || !field.FieldType.IsEnum)
                {
                    continue;
                }
                try
                {
                    field.SetValue(
                        options,
                        SelectSafeEnumValue(field.FieldType, field.Name)
                    );
                }
                catch { }
            }

            PropertyInfo[] properties = type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public
            );
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanWrite || property.GetIndexParameters().Length != 0 ||
                    !property.PropertyType.IsEnum)
                {
                    continue;
                }
                try
                {
                    property.SetValue(
                        options,
                        SelectSafeEnumValue(property.PropertyType, property.Name),
                        null
                    );
                }
                catch { }
            }
        }

        private static object CreateSafeDefaultValue(Type type, string parameterName)
        {
            if (type == null)
            {
                return null;
            }

            object collection = CreateMutableCollectionFor(type);
            if (collection != null)
            {
                return collection;
            }

            if (type.IsEnum)
            {
                return SelectSafeEnumValue(type, parameterName);
            }
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        private static object SelectSafeEnumValue(Type enumType, string parameterName)
        {
            string[] names = Enum.GetNames(enumType);
            string normalizedParameter = Normalize(parameterName ?? string.Empty);
            string[] preferred;

            if (normalizedParameter.Contains("filter"))
            {
                preferred = new[] { "All", "Todos", "None", "Default" };
            }
            else if (normalizedParameter.Contains("sort") ||
                     normalizedParameter.Contains("order"))
            {
                preferred = new[]
                {
                    "NameAscending", "IngredientAscending", "Name", "Ingredient",
                    "Ascending", "Default", "None"
                };
            }
            else
            {
                preferred = new[] { "All", "Default", "None" };
            }

            for (int p = 0; p < preferred.Length; p++)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.Equals(
                            names[i],
                            preferred[p],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return Enum.Parse(enumType, names[i]);
                    }
                }
            }

            Array values = Enum.GetValues(enumType);
            return values.Length > 0
                ? values.GetValue(0)
                : Activator.CreateInstance(enumType);
        }

        private static object CreateMutableCollectionFor(Type type)
        {
            if (type == null || type == typeof(string))
            {
                return null;
            }

            if (!type.IsInterface && !type.IsAbstract &&
                typeof(IList).IsAssignableFrom(type))
            {
                try
                {
                    return Activator.CreateInstance(type);
                }
                catch { }
            }

            if (type.IsGenericType)
            {
                Type generic = type.GetGenericTypeDefinition();
                if (generic == typeof(List<>) || generic == typeof(IList<>) ||
                    generic == typeof(ICollection<>) ||
                    generic == typeof(IEnumerable<>) ||
                    generic.FullName == "System.Collections.Generic.IReadOnlyList`1" ||
                    generic.FullName == "System.Collections.Generic.IReadOnlyCollection`1")
                {
                    Type element = type.GetGenericArguments()[0];
                    Type listType = typeof(List<>).MakeGenericType(element);
                    try
                    {
                        return Activator.CreateInstance(listType);
                    }
                    catch { }
                }
            }

            return null;
        }

        private static bool IsMutableReadBuffer(Type type)
        {
            if (type == null)
            {
                return false;
            }
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }
            if (type == null || type == typeof(string))
            {
                return false;
            }
            if (typeof(IList).IsAssignableFrom(type))
            {
                return true;
            }
            if (!type.IsGenericType)
            {
                return false;
            }
            Type generic = type.GetGenericTypeDefinition();
            return generic == typeof(List<>) ||
                   generic == typeof(IList<>) ||
                   generic == typeof(ICollection<>);
        }

        private static void TraverseQueryResult(
            object value,
            Dictionary<string, IngredientRecord> best)
        {
            if (value == null)
            {
                return;
            }
            HashSet<object> visited =
                new HashSet<object>(ReferenceEqualityComparer.Instance);
            int budget = 3500;
            TraverseForIngredientRows(value, 0, 6, visited, ref budget, best);
        }

        private static void TraverseForIngredientRows(
            object value,
            int depth,
            int maxDepth,
            HashSet<object> visited,
            ref int budget,
            Dictionary<string, IngredientRecord> best)
        {
            if (value == null || depth > maxDepth || budget-- <= 0)
            {
                return;
            }

            Type type = value.GetType();
            if (IsTerminal(type))
            {
                return;
            }

            if (!type.IsValueType)
            {
                if (!visited.Add(value))
                {
                    return;
                }
            }

            IngredientRecord record;
            if (TryBuildIngredientRecord(value, out record))
            {
                IngredientRecord current;
                if (!best.TryGetValue(record.IngredientId, out current) || record.Score > current.Score)
                {
                    best[record.IngredientId] = record;
                }
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count++ >= 512)
                    {
                        break;
                    }
                    TraverseForIngredientRows(item, depth + 1, maxDepth, visited, ref budget, best);
                }
                return;
            }

            // No atravesamos referencias a otros UnityEngine.Object desde un componente:
            // evita recorrer toda la escena. En el objeto raíz Application sí inspeccionamos
            // sus CAMPOS administrados para alcanzar DTO/colecciones internas, pero nunca
            // evaluamos propiedades públicas heredadas de Unity (transform, matrices, etc.).
            // A2 demostró que PropertyInfo.GetValue sobre esas propiedades podía disparar
            // miles de asserts ValidTRS() generados por la propia herramienta de diagnóstico.
            bool isUnityObject = value is UnityEngine.Object;
            if (depth > 0 && isUnityObject)
            {
                return;
            }

            FieldInfo[] fields = type.GetFields(InstanceFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic)
                {
                    continue;
                }
                try
                {
                    object child = field.GetValue(value);
                    TraverseForIngredientRows(child, depth + 1, maxDepth, visited, ref budget, best);
                }
                catch { }
            }

            // SEGURIDAD A3: nunca invoques getters arbitrarios de UnityEngine.Object.
            // Los getters nativos pueden tener efectos internos y, en concreto, provocar
            // ValidTRS() aunque el Transform de la escena sea perfectamente válido.
            if (isUnityObject)
            {
                return;
            }

            PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo prop = props[i];
                if (!prop.CanRead || prop.GetIndexParameters().Length != 0)
                {
                    continue;
                }
                try
                {
                    object child = prop.GetValue(value, null);
                    TraverseForIngredientRows(child, depth + 1, maxDepth, visited, ref budget, best);
                }
                catch { }
            }
        }

        private static bool TryBuildIngredientRecord(object value, out IngredientRecord record)
        {
            record = null;
            if (value == null)
            {
                return false;
            }

            Type type = value.GetType();
            string typeName = type.Name;
            string lowerType = typeName.ToLowerInvariant();

            if (lowerType.Contains("lot") || lowerType.Contains("movement") ||
                lowerType.Contains("reservation") || lowerType.Contains("recipe") ||
                lowerType.Contains("dish") || lowerType.Contains("policy"))
            {
                return false;
            }

            string ingredientId;
            if (!TryReadIngredientId(value, out ingredientId) ||
                string.IsNullOrWhiteSpace(ingredientId))
            {
                return false;
            }

            double total, available, reserved;
            bool hasTotal = TryReadDouble(value, TotalAliases, out total);
            bool hasAvailable = TryReadDouble(value, AvailableAliases, out available);
            bool hasReserved = TryReadDouble(value, ReservedAliases, out reserved);

            int known = (hasTotal ? 1 : 0) + (hasAvailable ? 1 : 0) + (hasReserved ? 1 : 0);
            if (known < 2)
            {
                return false;
            }

            if (!hasTotal) total = available + reserved;
            if (!hasAvailable) available = total - reserved;
            if (!hasReserved) reserved = total - available;

            if (double.IsNaN(total) || double.IsInfinity(total) ||
                double.IsNaN(available) || double.IsInfinity(available) ||
                double.IsNaN(reserved) || double.IsInfinity(reserved))
            {
                return false;
            }

            int score = known * 10;
            if (lowerType.Contains("ingredient")) score += 3;
            if (lowerType.Contains("summary") || lowerType.Contains("read") ||
                lowerType.Contains("view") || lowerType.Contains("snapshot") ||
                lowerType.Contains("state")) score += 4;

            record = new IngredientRecord
            {
                IngredientId = ingredientId.Trim(),
                Total = total,
                Available = available,
                Reserved = reserved,
                Score = score,
                SourceType = typeName
            };
            return true;
        }

        public static List<ReservationRecord> ExtractActiveReservations(object inventoryRoot)
        {
            List<ReservationRecord> result = new List<ReservationRecord>();
            HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int budget = 5000;
            TraverseForReservations(
                inventoryRoot,
                "inventory",
                0,
                6,
                visited,
                ids,
                ref budget,
                result
            );
            return result;
        }

        private static void TraverseForReservations(
            object value,
            string path,
            int depth,
            int maxDepth,
            HashSet<object> visited,
            HashSet<string> ids,
            ref int budget,
            List<ReservationRecord> result)
        {
            if (value == null || depth > maxDepth || budget-- <= 0)
            {
                return;
            }

            Type type = value.GetType();
            if (IsTerminal(type))
            {
                return;
            }

            if (!type.IsValueType && !visited.Add(value))
            {
                return;
            }

            string typeName = type.Name;
            if (typeName.IndexOf("Reservation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string reservationId;
                if (TryReadString(value, ReservationIdAliases, out reservationId) &&
                    !string.IsNullOrWhiteSpace(reservationId))
                {
                    string state;
                    bool hasState = TryReadString(value, StateAliases, out state);
                    bool explicitActive = hasState &&
                        state.IndexOf("Active", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool pathActive = path.IndexOf("active", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool explicitTerminal = hasState &&
                        (state.IndexOf("Consumed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         state.IndexOf("Cancelled", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         state.IndexOf("Released", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         state.IndexOf("Completed", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!explicitTerminal && (explicitActive || pathActive))
                    {
                        ReservationRecord reservation =
                            BuildReservationRecord(value, reservationId.Trim());
                        if (reservation.ByIngredient.Count > 0 && ids.Add(reservation.ReservationId))
                        {
                            result.Add(reservation);
                        }
                    }
                }
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count++ >= 512) break;
                    TraverseForReservations(
                        item, path + "[]", depth + 1, maxDepth,
                        visited, ids, ref budget, result
                    );
                }
                return;
            }

            bool isUnityObject = value is UnityEngine.Object;
            if (depth > 0 && isUnityObject)
            {
                return;
            }

            FieldInfo[] fields = type.GetFields(InstanceFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic) continue;
                try
                {
                    object child = field.GetValue(value);
                    TraverseForReservations(
                        child, path + "." + field.Name, depth + 1, maxDepth,
                        visited, ids, ref budget, result
                    );
                }
                catch { }
            }

            // SEGURIDAD A3: los servicios/Component raíz se recorren por campos propios,
            // no mediante getters públicos de UnityEngine.Object.
            if (isUnityObject)
            {
                return;
            }

            PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo prop = props[i];
                if (!prop.CanRead || prop.GetIndexParameters().Length != 0) continue;
                try
                {
                    object child = prop.GetValue(value, null);
                    TraverseForReservations(
                        child, path + "." + prop.Name, depth + 1, maxDepth,
                        visited, ids, ref budget, result
                    );
                }
                catch { }
            }
        }

        private static ReservationRecord BuildReservationRecord(object reservationObject, string id)
        {
            ReservationRecord result = new ReservationRecord { ReservationId = id };
            HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            int budget = 1200;
            TraverseReservationLines(reservationObject, 0, 5, visited, ref budget, result);
            return result;
        }

        private static void TraverseReservationLines(
            object value,
            int depth,
            int maxDepth,
            HashSet<object> visited,
            ref int budget,
            ReservationRecord result)
        {
            if (value == null || depth > maxDepth || budget-- <= 0)
            {
                return;
            }

            Type type = value.GetType();
            if (IsTerminal(type))
            {
                return;
            }

            if (!type.IsValueType && !visited.Add(value))
            {
                return;
            }

            string typeName = type.Name.ToLowerInvariant();
            if (!typeName.Contains("lot") && !typeName.Contains("allocation"))
            {
                string ingredientId;
                double quantity;
                if (TryReadIngredientId(value, out ingredientId) &&
                    TryReadDouble(value, LineQuantityAliases, out quantity) &&
                    !string.IsNullOrWhiteSpace(ingredientId) && quantity > Tolerance)
                {
                    double current;
                    result.ByIngredient.TryGetValue(ingredientId.Trim(), out current);
                    result.ByIngredient[ingredientId.Trim()] = current + quantity;
                }
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count++ >= 256) break;
                    TraverseReservationLines(item, depth + 1, maxDepth, visited, ref budget, result);
                }
                return;
            }

            bool isUnityObject = value is UnityEngine.Object;
            if (depth > 0 && isUnityObject)
            {
                return;
            }

            FieldInfo[] fields = type.GetFields(InstanceFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].IsStatic) continue;
                try
                {
                    TraverseReservationLines(
                        fields[i].GetValue(value), depth + 1, maxDepth,
                        visited, ref budget, result
                    );
                }
                catch { }
            }

            // SEGURIDAD A3: no evalúa getters públicos heredados de UnityEngine.Object.
            if (isUnityObject)
            {
                return;
            }

            PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < props.Length; i++)
            {
                if (!props[i].CanRead || props[i].GetIndexParameters().Length != 0) continue;
                try
                {
                    TraverseReservationLines(
                        props[i].GetValue(value, null), depth + 1, maxDepth,
                        visited, ref budget, result
                    );
                }
                catch { }
            }
        }

        private static bool TryReadIngredientId(object target, out string ingredientId)
        {
            ingredientId = string.Empty;
            if (target == null)
            {
                return false;
            }

            if (TryReadString(target, IngredientIdAliases, out ingredientId) &&
                !string.IsNullOrWhiteSpace(ingredientId))
            {
                return true;
            }

            // 2.2D puede exponer el ingrediente como Definition/Ingredient en el DTO
            // en lugar de duplicar IngredientId. Resolvemos únicamente referencias
            // semánticas explícitas para no confundir MovementId/ReservationId con
            // la identidad del ingrediente.
            string[] nestedAliases =
            {
                "ingredient", "ingredientdefinition", "definition",
                "ingredientdata", "item", "ingredientitem"
            };

            object nested;
            if (TryReadMember(target, nestedAliases, out nested) && nested != null)
            {
                if (nested is string)
                {
                    ingredientId = ((string)nested).Trim();
                    return !string.IsNullOrWhiteSpace(ingredientId);
                }

                if (TryReadString(nested, IngredientIdAliases, out ingredientId) &&
                    !string.IsNullOrWhiteSpace(ingredientId))
                {
                    return true;
                }

                string[] genericId = { "id" };
                if (TryReadString(nested, genericId, out ingredientId) &&
                    !string.IsNullOrWhiteSpace(ingredientId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadString(object target, string[] aliases, out string value)
        {
            value = string.Empty;
            object raw;
            if (!TryReadMember(target, aliases, out raw) || raw == null)
            {
                return false;
            }

            if (raw is string)
            {
                value = (string)raw;
                return true;
            }

            Type type = raw.GetType();
            if (type.IsEnum || raw is IFormattable)
            {
                value = raw.ToString();
                return true;
            }

            return false;
        }

        private static bool TryReadDouble(object target, string[] aliases, out double value)
        {
            value = 0.0;
            object raw;
            if (!TryReadMember(target, aliases, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                if (raw is decimal) value = (double)(decimal)raw;
                else value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadMember(object target, string[] aliases, out object value)
        {
            value = null;
            if (target == null)
            {
                return false;
            }

            Type type = target.GetType();
            FieldInfo[] fields = type.GetFields(InstanceFlags);
            for (int i = 0; i < fields.Length; i++)
            {
                string normalized = Normalize(fields[i].Name);
                if (!aliases.Contains(normalized)) continue;
                try
                {
                    value = fields[i].GetValue(target);
                    return true;
                }
                catch { }
            }

            PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < props.Length; i++)
            {
                if (!props[i].CanRead || props[i].GetIndexParameters().Length != 0) continue;
                string normalized = Normalize(props[i].Name);
                if (!aliases.Contains(normalized)) continue;
                try
                {
                    value = props[i].GetValue(target, null);
                    return true;
                }
                catch { }
            }

            return false;
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static bool IsTerminal(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                   type == typeof(decimal) || type == typeof(DateTime) ||
                   type == typeof(TimeSpan) || type == typeof(Guid);
        }

        private static Type[] GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).ToArray();
            }
            catch
            {
                return new Type[0];
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance =
                new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
#endif
