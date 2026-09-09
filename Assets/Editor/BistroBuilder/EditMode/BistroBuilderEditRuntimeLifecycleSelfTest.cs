using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderEditRuntimeLifecycleSelfTest
{
    private static int passed;
    private static int failed;
    private static readonly List<string> lines = new List<string>();

    [MenuItem("Tools/Bistro Builder/Edit Mode/18 - Runtime Lifecycle Self Test", false, 18003)]
    public static void RunFromMenu()
    {
        passed = 0;
        failed = 0;
        lines.Clear();
        TestMissingCoordinator();
        TestCanonicalAvailabilityAuthority();
        TestFacadeReviewAndLoadIsolation();
        TestSameRevisionStaleBaseline();
        TestSaveGuard();
        string report = "BB EDIT MODE BLOCK 18 - RUNTIME LIFECYCLE SELF TEST\n" +
            string.Join("\n", lines) + $"\nResultado: {passed} OK / {failed} fallos.";
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "EditRuntimeLifecycleSelfTestReport.txt"), report);
        Debug.Log(report);
        if (failed != 0) throw new InvalidOperationException(report);
    }

    public static void RunFromCommandLine()
    {
        try { RunFromMenu(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void TestMissingCoordinator()
    {
        var host = new GameObject("BB18_FacadeMissingCoordinator_Test");
        host.SetActive(false);
        try
        {
            var facade = host.AddComponent<BistroBuilderEditPlayerFacade>();
            Check(!facade.BeginEdit(), "Facade rechaza BeginEdit sin coordinador");
            Check(!facade.Undo(), "Facade rechaza Undo sin coordinador");
            Check(!facade.Redo(), "Facade rechaza Redo sin coordinador");
            Check(!facade.Review(), "Facade rechaza Review sin coordinador");
            Check(!facade.Commit() && !string.IsNullOrWhiteSpace(facade.CreateSnapshot().statusMessage),
                "Facade rechaza Commit sin coordinador y conserva diagnóstico visible");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void TestCanonicalAvailabilityAuthority()
    {
        var host = new GameObject("BB18_ServiceAvailability_Test");
        host.SetActive(false);
        try
        {
            var document = host.AddComponent<BistroBuilderEditDocumentRuntimeService>();
            var service = host.AddComponent<RestaurantServiceStateService>();
            var legacy = host.AddComponent<RestaurantEditModeService>();
            var rule = host.AddComponent<RestaurantServiceEditModeAvailabilityRule>();
            SetReference(rule, "serviceStateService", service);
            SetReference(rule, "editModeService", legacy);
            var coordinator = host.AddComponent<BistroBuilderEditRuntimeCoordinator>();
            coordinator.ConfigureAvailabilityRuntime(legacy, rule);
            var economy = new TestEconomy();
            coordinator.TryBindEconomyGateway(economy, out _);

            service.TryBeginPreparation();
            rule.CanEnterEditMode(out string expectedRejection);
            Check(!coordinator.TryBeginSession(out string rejection) && rejection == expectedRejection,
                "Preparing bloquea nueva sesión con mensaje de autoridad Gameplay");
            service.TryOpenService();
            Check(!coordinator.TryBeginSession(out _), "Open bloquea nueva sesión");
            service.TryBeginClosing();
            Check(!coordinator.TryBeginSession(out _), "Closing bloquea nueva sesión");
            service.TryCompleteClosing();
            Check(coordinator.TryBeginSession(out _) && !legacy.IsEditModeActive,
                "Closed permite Draft sin alterar entrada del editor existente");
            coordinator.TryExecute(new BistroBuilderCreateWallCommand(Wall(0f)), out _, out _);
            coordinator.TryReview(out _);
            legacy.TryEnterEditMode(out _, out _);
            service.TryOpenService();
            Check(!coordinator.TryCommit(out _, out _, out _) && economy.prepareCount == 0 && document.Revision == 0,
                "Servicio iniciado después de Review impide publicación y autorización Finance");
            Check(coordinator.HasSession && coordinator.IsDirty && !coordinator.CanEditNow(out _),
                "Servicio conserva Draft y regla se evalúa aunque editor existente siga activo");
            service.TryBeginClosing();
            service.TryCompleteClosing();
            Check(coordinator.TryCommit(out _, out _, out _) && economy.prepareCount == 1 && document.Revision == 1,
                "Tras cerrar servicio se puede confirmar el mismo Draft una sola vez");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void TestFacadeReviewAndLoadIsolation()
    {
        var host = new GameObject("BB18_FacadeReview_Test");
        try
        {
            host.AddComponent<BistroBuilderEditDocumentRuntimeService>();
            var coordinator = host.AddComponent<BistroBuilderEditRuntimeCoordinator>();
            var facade = host.AddComponent<BistroBuilderEditPlayerFacade>();
            SetReference(facade, "coordinator", coordinator);
            coordinator.TryBindEconomyGateway(new TestEconomy(), out _);
            facade.BeginEdit();
            coordinator.TryExecute(new BistroBuilderCreateWallCommand(Wall(0f)), out _, out _);
            Check(facade.Review() && facade.CreateSnapshot().canCommit && facade.CreateSnapshot().isDirty,
                "Review mantiene habilitada confirmación de reforma pendiente");
            Check(facade.Commit() && !facade.CreateSnapshot().hasSession && !facade.CreateSnapshot().canCommit,
                "Commit finaliza sesión y deshabilita nueva confirmación");
            facade.BeginEdit();
            coordinator.TryExecute(new BistroBuilderCreateWallCommand(Wall(2f)), out _, out _);
            var oldSession = coordinator.Session;
            coordinator.DiscardSessionForLoad();
            Check(!coordinator.HasSession && coordinator.Session == null &&
                oldSession.State == BistroBuilderEditSessionState.Cancelled && !facade.CreateSnapshot().canUndo,
                "Load descarta workspace anterior sin permitir Undo sobre otra partida");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void TestSameRevisionStaleBaseline()
    {
        var host = new GameObject("BB18_StaleBaseline_Test");
        try
        {
            var document = host.AddComponent<BistroBuilderEditDocumentRuntimeService>();
            var coordinator = host.AddComponent<BistroBuilderEditRuntimeCoordinator>();
            var economy = new TestEconomy();
            coordinator.TryBindEconomyGateway(economy, out _);
            coordinator.TryBeginSession(out _);
            coordinator.TryExecute(new BistroBuilderCreateWallCommand(Wall(0f)), out _, out _);
            var loaded = new BistroBuilderEditDocument();
            var loadedWall = Wall(8f);
            loaded.walls.Add(loadedWall);
            document.ReplaceCommittedForLoad(loaded, out _);
            Check(!coordinator.TryCommit(out _, out _, out string error) && !string.IsNullOrWhiteSpace(error) &&
                economy.prepareCount == 0 && document.GetCommittedSnapshot().walls[0].wallId == loadedWall.wallId,
                "Load de contenido diferente con igual revisión bloquea Draft obsoleto antes de Finance");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void TestSaveGuard()
    {
        var host = new GameObject("BB18_EditSaveGuard_Test");
        host.SetActive(false);
        try
        {
            host.AddComponent<BistroBuilderEditDocumentRuntimeService>();
            var coordinator = host.AddComponent<BistroBuilderEditRuntimeCoordinator>();
            var guard = host.AddComponent<BistroBuilderEditSessionSaveGuard>();
            SetReference(guard, "coordinator", coordinator);
            Check(guard.ValidateConfiguration(out _) &&
                guard.CanSave(out _) && guard.CanLoad(out _),
                "Save/Load permitido sin workspace semántico activo");
            Check(coordinator.TryBeginSession(out _),
                "SaveGuard fixture abre sesión semántica");
            Check(!guard.CanSave(out string saveError) &&
                !guard.CanLoad(out string loadError) &&
                !string.IsNullOrWhiteSpace(saveError) &&
                !string.IsNullOrWhiteSpace(loadError),
                "Save/Load bloqueado mientras existe sesión semántica activa");
            coordinator.CancelSession();
            Check(guard.CanSave(out _) && guard.CanLoad(out _),
                "Save/Load vuelve a estar disponible tras cancelar la sesión");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static BistroBuilderWallRecord Wall(float z)
    {
        return new BistroBuilderWallRecord
        {
            wallId = BistroBuilderEditId.NewId(), wallDefinitionId = "test.lifecycle.wall", buildPlaneId = "default",
            axisStart = new Vector2(0f, z), axisEnd = new Vector2(3f, z), height = 2.8f, thickness = 0.12f
        };
    }

    private sealed class TestEconomy : IBistroBuilderEditEconomicGateway
    {
        public int prepareCount;
        public bool TryPrepareAuthorization(BistroBuilderEditEconomicProposal proposal,
            out BistroBuilderEditEconomicAuthorization authorization, out string error)
        {
            prepareCount++;
            authorization = new BistroBuilderEditEconomicAuthorization("lifecycle-test-" + prepareCount,
                proposal.draftRevision, 0);
            error = string.Empty;
            return true;
        }
        public bool TryFinalizeAuthorization(BistroBuilderEditEconomicAuthorization authorization,
            string operationId, out string error) { error = string.Empty; return true; }
        public bool TryAbortAuthorization(BistroBuilderEditEconomicAuthorization authorization,
            out string error) { error = string.Empty; return true; }
    }

    private static void Check(bool condition, string label)
    {
        if (condition) { passed++; lines.Add("OK - " + label); }
        else { failed++; lines.Add("FAIL - " + label); }
    }
}
