using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BistroBuilderEditBlock18QueenTest
{
    private const string ArmedKey = "BB.Edit18.Queen.Armed";
    private const string CliKey = "BB.Edit18.Queen.Cli";
    private const string ReportPath = "EditBlock18QueenTestReport.txt";
    private const double StartupTimeout = 40d;

    private enum Phase
    {
        Idle,
        WaitingStartup,
        SavingRollback,
        SavingCheckpoint,
        LoadingCheckpoint,
        WaitingCheckpoint,
        LoadingRollback,
        WaitingRollback,
        DeletingCheckpoint,
        DeletingRollback
    }

    private static Phase phase;
    private static double deadline;
    private static int settleFrames;
    private static int capturedErrors;
    private static int rollbackSlot = -1;
    private static int checkpointSlot = -1;

    private static BistroBuilderSaveGameService save;
    private static BistroBuilderEditDocumentRuntimeService document;
    private static BistroBuilderEditRuntimeCoordinator coordinator;
    private static BistroBuilderEditPlayerFacade facade;
    private static BistroBuilderArchitectureRuntimeMaterializer materializer;
    private static BistroBuilderSpatialInteractionService spatial;
    private static BistroBuilderNavigationService navigation;
    private static BistroBuilderFinanceService finance;
    private static RestaurantServiceStateService serviceState;

    private static BistroBuilderEditDocument baselineDocument;
    private static BistroBuilderFinanceSnapshot baselineFinance;
    private static int baselineSubjects;
    private static int baselineObstacles;
    private static string checkpointFingerprint = string.Empty;
    private static long checkpointRevision;
    private static BistroBuilderFinanceSnapshot checkpointFinance;
    private static int checkpointSubjects;
    private static int checkpointObstacles;

    static BistroBuilderEditBlock18QueenTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayMode;
        EditorApplication.playModeStateChanged += HandlePlayMode;
    }

    [MenuItem("Bistro Builder/18 Modo Edicion/Run 18M Queen Test")]
    private static void RunFromMenu()
    {
        Begin(false);
    }

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderEditBlock18Installer.ScenePath,
            OpenSceneMode.Single);
        Begin(true);
    }

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlaying || SessionState.GetBool(ArmedKey, false))
        {
            if (commandLine) EditorApplication.Exit(1);
            return;
        }
        ResetStaticState();
        SessionState.SetBool(ArmedKey, true);
        SessionState.SetBool(CliKey, commandLine);
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            phase = Phase.WaitingStartup;
            deadline = EditorApplication.timeSinceStartup + StartupTimeout;
            settleFrames = 8;
            Application.logMessageReceived += HandleLog;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            CleanupSubscriptions();
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode &&
            SessionState.GetBool(ArmedKey, false))
        {
            bool cli = SessionState.GetBool(CliKey, false);
            SessionState.SetBool(ArmedKey, false);
            SessionState.SetBool(CliKey, false);
            if (cli)
            {
                string report = File.Exists(Path.GetFullPath(ReportPath))
                    ? File.ReadAllText(Path.GetFullPath(ReportPath))
                    : string.Empty;
                EditorApplication.Exit(report.Contains("[PASS]") ? 0 : 1);
            }
        }
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying ||
            !SessionState.GetBool(ArmedKey, false)) return;
        if (settleFrames > 0)
        {
            settleFrames--;
            return;
        }

        switch (phase)
        {
            case Phase.WaitingStartup:
                TryBeginWhenReady();
                break;
            case Phase.WaitingCheckpoint:
                ValidateCheckpointAndService();
                break;
            case Phase.WaitingRollback:
                ValidateRollbackAndDelete();
                break;
        }
    }

    private static void TryBeginWhenReady()
    {
        ResolveDependencies();
        if (!DependenciesReady())
        {
            if (EditorApplication.timeSinceStartup >= deadline)
                Complete(false, "No se inicializaron todas las autoridades 18M.");
            return;
        }

        if (!serviceState.IsClosed)
        {
            Complete(false, "La Queen Test 18M necesita comenzar con el restaurante Closed.");
            return;
        }
        if (!BistroBuilderEditBlock18SceneValidator.ValidateScene(
                SceneManager.GetActiveScene()) ||
            BistroBuilderEditBlock18SceneValidator.LastFailed != 0 ||
            BistroBuilderEditBlock18SceneValidator.LastPending != 0)
        {
            Complete(false, "La integraciÃ³n estructural 18M no estÃ¡ limpia en Play Mode.");
            return;
        }

        save.RefreshExtensions();
        if (!FindTwoFreeSlots(out rollbackSlot, out checkpointSlot))
        {
            Complete(false, "No hay dos slots temporales libres para la Queen Test 18M.");
            return;
        }

        baselineDocument = document.GetCommittedSnapshot();
        baselineFinance = finance.CreateSnapshot();
        baselineSubjects = spatial.SubjectCount;
        baselineObstacles = navigation.StaticObstacleCount;
        if (baselineFinance == null)
        {
            Complete(false, "No se pudo capturar el baseline financiero 18M.");
            return;
        }

        save.OperationCompleted -= HandleSaveOperation;
        save.OperationCompleted += HandleSaveOperation;
        phase = Phase.SavingRollback;
        if (!save.TrySaveSlot(rollbackSlot, "BB18 QUEEN ROLLBACK", out string error))
            Complete(false, "No se pudo guardar rollback 18M: " + error);
    }
    private static void HandleSaveOperation(BistroBuilderSaveOperationResult result)
    {
        if (result == null) return;
        if (!result.Succeeded)
        {
            Complete(false, "SaveGame 18M fallÃ³: " + result.Message);
            return;
        }

        if (phase == Phase.SavingRollback && result.SlotIndex == rollbackSlot)
        {
            if (!RunPrimaryRenovation(out string error))
            {
                Complete(false, "Reforma primaria 18M: " + error);
                return;
            }
            phase = Phase.SavingCheckpoint;
            if (!save.TrySaveSlot(checkpointSlot, "BB18 QUEEN CHECKPOINT", out error))
                Complete(false, "No se pudo guardar checkpoint 18M: " + error);
            return;
        }

        if (phase == Phase.SavingCheckpoint && result.SlotIndex == checkpointSlot)
        {
            CaptureCheckpoint();
            if (!RunMutationAfterCheckpoint(out string error))
            {
                Complete(false, "MutaciÃ³n posterior al checkpoint: " + error);
                return;
            }
            phase = Phase.LoadingCheckpoint;
            if (!save.TryLoadSlot(checkpointSlot, out error))
                Complete(false, "No se pudo cargar checkpoint 18M: " + error);
            return;
        }
        if (phase == Phase.LoadingCheckpoint && result.SlotIndex == checkpointSlot)
        {
            phase = Phase.WaitingCheckpoint;
            settleFrames = 6;
            return;
        }

        if (phase == Phase.LoadingRollback && result.SlotIndex == rollbackSlot)
        {
            phase = Phase.WaitingRollback;
            settleFrames = 6;
            return;
        }

        if (phase == Phase.DeletingCheckpoint && result.SlotIndex == checkpointSlot)
        {
            phase = Phase.DeletingRollback;
            if (!save.TryDeleteSlot(rollbackSlot, out string error))
                Complete(false, "No se pudo borrar rollback temporal 18M: " + error);
            return;
        }

        if (phase == Phase.DeletingRollback && result.SlotIndex == rollbackSlot)
        {
            Complete(true,
                "commit econÃ³mico, BBSIS, Navigation, Save/Load, servicio y rollback verificados.");
        }
    }

    private static bool RunPrimaryRenovation(out string error)
    {
        error = string.Empty;
        long baselineCash = baselineFinance.currentBalanceCents;
        int baselineTransactions = baselineFinance.transactions != null
            ? baselineFinance.transactions.Count : 0;
        if (!facade.BeginEdit())
        {
            error = facade.CreateSnapshot().statusMessage;
            return false;
        }
        var walls = new[]
        {
            Wall(new Vector2(30f, 30f), new Vector2(36f, 30f)),
            Wall(new Vector2(36f, 30f), new Vector2(36f, 34f)),
            Wall(new Vector2(36f, 34f), new Vector2(30f, 34f)),
            Wall(new Vector2(30f, 34f), new Vector2(30f, 30f))
        };
        for (int i = 0; i < walls.Length; i++)
        {
            if (!coordinator.TryExecute(
                    new BistroBuilderCreateWallCommand(walls[i]), out _, out error))
                return false;
        }

        var door = new BistroBuilderOpeningRecord
        {
            openingId = BistroBuilderEditId.NewId(),
            hostWallId = walls[0].wallId,
            axisPosition01 = 0.5f,
            width = 1.0f,
            height = 2.1f,
            openingType = "door"
        };
        if (!coordinator.TryExecute(
                new BistroBuilderCreateOpeningCommand(door), out _, out error))
            return false;
        if (!facade.Review())
        {
            error = facade.CreateSnapshot().statusMessage;
            return false;
        }
        if (!facade.Commit())
        {
            error = facade.CreateSnapshot().statusMessage;
            return false;
        }
        BistroBuilderEditDocument committed = document.GetCommittedSnapshot();
        int expectedWalls = baselineDocument.walls.Count + 4;
        int expectedOpenings = baselineDocument.openings.Count + 1;
        if (committed.revision != baselineDocument.revision + 1 ||
            committed.walls.Count != expectedWalls ||
            committed.openings.Count != expectedOpenings)
        {
            error = "El commit arquitectÃ³nico no publicÃ³ exactamente N+1 con 4 paredes y 1 puerta.";
            return false;
        }

        const long expectedDebit = 42000L;
        if (finance.CurrentBalanceCents != baselineCash - expectedDebit ||
            finance.TransactionCount != baselineTransactions + 5)
        {
            error = "Finance no publicÃ³ exactamente las 5 lÃ­neas de reforma esperadas.";
            return false;
        }
        if (materializer.LastDocument == null ||
            materializer.LastDocument.ComputeFingerprint() != committed.ComputeFingerprint())
        {
            error = "La materializaciÃ³n no refleja el documento comprometido.";
            return false;
        }
        if (spatial.SubjectCount < baselineSubjects + 5 ||
            navigation.StaticObstacleCount < baselineObstacles + 5)
        {
            error = "BBSIS/Navigation no proyectaron los obstÃ¡culos derivados de las paredes y puerta.";
            return false;
        }
        return true;
    }

    private static void CaptureCheckpoint()
    {
        BistroBuilderEditDocument current = document.GetCommittedSnapshot();
        checkpointFingerprint = current.ComputeFingerprint();
        checkpointRevision = current.revision;
        checkpointFinance = finance.CreateSnapshot();
        checkpointSubjects = spatial.SubjectCount;
        checkpointObstacles = navigation.StaticObstacleCount;
    }

    private static bool RunMutationAfterCheckpoint(out string error)
    {
        error = string.Empty;
        if (!facade.BeginEdit())
        {
            error = facade.CreateSnapshot().statusMessage;
            return false;
        }
        BistroBuilderWallRecord extra = Wall(
            new Vector2(40f, 40f), new Vector2(42f, 40f));
        if (!coordinator.TryExecute(
                new BistroBuilderCreateWallCommand(extra), out _, out error))
            return false;
        if (!facade.Review() || !facade.Commit())
        {
            error = facade.CreateSnapshot().statusMessage;
            return false;
        }
        BistroBuilderEditDocument mutated = document.GetCommittedSnapshot();
        if (mutated.revision != checkpointRevision + 1 ||
            mutated.ComputeFingerprint() == checkpointFingerprint)
        {
            error = "La mutaciÃ³n posterior al checkpoint no produjo una revisiÃ³n distinta.";
            return false;
        }
        if (finance.CurrentBalanceCents != checkpointFinance.currentBalanceCents - 3000L)
        {
            error = "La mutaciÃ³n posterior al checkpoint no produjo el dÃ©bito esperado.";
            return false;
        }
        return true;
    }
    private static void ValidateCheckpointAndService()
    {
        BistroBuilderEditDocument current = document.GetCommittedSnapshot();
        BistroBuilderFinanceSnapshot currentFinance = finance.CreateSnapshot();
        if (current.revision != checkpointRevision ||
            current.ComputeFingerprint() != checkpointFingerprint ||
            JsonUtility.ToJson(currentFinance) != JsonUtility.ToJson(checkpointFinance))
        {
            Complete(false, "Save/Load no restaurÃ³ exactamente el checkpoint arquitectÃ³nico/financiero.");
            return;
        }
        if (materializer.LastDocument == null ||
            materializer.LastDocument.ComputeFingerprint() != checkpointFingerprint ||
            spatial.SubjectCount != checkpointSubjects ||
            navigation.StaticObstacleCount != checkpointObstacles)
        {
            Complete(false, "BBSIS/Navigation/materializaciÃ³n no quedaron exactos tras Load.");
            return;
        }
        if (!serviceState.TryOpenService())
        {
            Complete(false, "No se pudo abrir servicio real sobre la arquitectura cargada.");
            return;
        }
        if (coordinator.TryBeginSession(out _))
        {
            Complete(false, "Servicio abierto no bloqueÃ³ ediciÃ³n como exige la autoridad Gameplay.");
            return;
        }
        // 368EF permite Save/Load durante servicio con persistencia autoritativa completa.
        if (!save.TryLoadSlot(rollbackSlot, out _))
        {
            Complete(false, "Servicio activo no permitiÃ³ carga segura pese a disponer de persistencia autoritativa 368EF.");
            return;
        }
        {
            Complete(false, "No se pudo cerrar servicio tras la comprobaciÃ³n 18M.");
            return;
        }
        phase = Phase.LoadingRollback;
        if (!save.TryLoadSlot(rollbackSlot, out string error))
            Complete(false, "No se pudo cargar rollback 18M: " + error);
    }
    private static void ValidateRollbackAndDelete()
    {
        BistroBuilderEditDocument current = document.GetCommittedSnapshot();
        BistroBuilderFinanceSnapshot currentFinance = finance.CreateSnapshot();
        if (current.ComputeFingerprint() != baselineDocument.ComputeFingerprint() ||
            current.revision != baselineDocument.revision ||
            JsonUtility.ToJson(currentFinance) != JsonUtility.ToJson(baselineFinance))
        {
            Complete(false, "Rollback Save/Load no restaurÃ³ el baseline exacto.");
            return;
        }
        if (coordinator.HasSession || !serviceState.IsClosed)
        {
            Complete(false, "Rollback dejÃ³ una sesiÃ³n de ediciÃ³n o servicio activo.");
            return;
        }
        if (materializer.LastDocument == null ||
            materializer.LastDocument.ComputeFingerprint() != baselineDocument.ComputeFingerprint() ||
            spatial.SubjectCount != baselineSubjects ||
            navigation.StaticObstacleCount != baselineObstacles)
        {
            Complete(false, "Rollback no restaurÃ³ la proyecciÃ³n espacial/circulaciÃ³n exacta.");
            return;
        }
        if (capturedErrors != 0)
        {
            Complete(false, "Se capturaron " + capturedErrors +
                " Error/Exception/Assert durante la Queen Test.");
            return;
        }
        phase = Phase.DeletingCheckpoint;
        if (!save.TryDeleteSlot(checkpointSlot, out string error))
            Complete(false, "No se pudo borrar checkpoint temporal 18M: " + error);
    }

    private static BistroBuilderWallRecord Wall(Vector2 start, Vector2 end)
    {
        return new BistroBuilderWallRecord
        {
            wallId = BistroBuilderEditId.NewId(),
            buildPlaneId = "default",
            axisStart = start,
            axisEnd = end,
            height = 2.8f,
            thickness = 0.12f,
            wallDefinitionId = "wall.default"
        };
    }

    private static bool FindTwoFreeSlots(out int first, out int second)
    {
        first = -1;
        second = -1;
        for (int slot = 970; slot <= 979; slot++)
        {
            if (save.SlotExists(slot)) continue;
            if (first < 0) first = slot;
            else
            {
                second = slot;
                return true;
            }
        }
        return false;
    }

    private static void ResolveDependencies()
    {
        save = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
        document = UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>();
        coordinator = UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
        facade = UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditPlayerFacade>();
        materializer = UnityEngine.Object.FindFirstObjectByType<BistroBuilderArchitectureRuntimeMaterializer>();
        spatial = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        navigation = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        finance = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFinanceService>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();
    }
    private static bool DependenciesReady()
    {
        return save != null && !save.IsBusy &&
            document != null && coordinator != null && facade != null &&
            materializer != null && spatial != null && navigation != null &&
            finance != null && finance.IsInitialized && serviceState != null;
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            capturedErrors++;
    }

    private static void Complete(bool success, string message)
    {
        CleanupSubscriptions();
        string report =
            "=== BISTRO BUILDER â€” BLOCK 18M QUEEN TEST ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n" +
            "Core architecture + Finance + BBSIS + Navigation + SaveGame + service-state integration.\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static void CleanupSubscriptions()
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= HandleLog;
        if (save != null) save.OperationCompleted -= HandleSaveOperation;
    }

    private static void ResetStaticState()
    {
        phase = Phase.Idle;
        deadline = 0d;
        settleFrames = 0;
        capturedErrors = 0;
        rollbackSlot = -1;
        checkpointSlot = -1;
        save = null;
        document = null;
        coordinator = null;
        facade = null;
        materializer = null;
        spatial = null;
        navigation = null;
        finance = null;
        serviceState = null;
        baselineDocument = null;
        baselineFinance = null;
        baselineSubjects = 0;
        baselineObstacles = 0;
        checkpointFingerprint = string.Empty;
        checkpointRevision = 0L;
        checkpointFinance = null;
        checkpointSubjects = 0;
        checkpointObstacles = 0;
    }
}
