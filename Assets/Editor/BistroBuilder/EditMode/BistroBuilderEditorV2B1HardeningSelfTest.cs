using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderEditorV2B1HardeningSelfTest
{
    private const string ReportName = "EditorV2_B1_Hardening_Report.txt";
    private static readonly List<string> Lines = new List<string>();
    private static int pass;
    private static int fail;

    [MenuItem("Tools/Bistro Builder/Edit Mode/Editor V2/B1 - Hardening Self Test", false, 18100)]
    public static void RunFromMenu() => Run(false);

    public static void RunFromCommandLine()
    {
        try
        {
            Run(true);
            EditorApplication.Exit(fail == 0 ? 0 : 1);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    private static void Run(bool cli)
    {
        pass = 0;
        fail = 0;
        Lines.Clear();

        TestFinanceFinalizeFailureCompensatesWorld();
        TestPostPublishJournalFailureDoesNotStrandCommit();
        TestPrePublishJournalFailureHasNoWorldSideEffects();
        TestRuntimeRollbackIsExactAndIdempotenceSafe();
        TestLoadMaterializesExactlyOnce();

        string report =
            "EDITOR V2 - B1 HARDENING SELF TEST\n" +
            string.Join("\n", Lines) +
            $"\nResultado: {pass} OK / {fail} fallos.";

        string root = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(Path.Combine(root, ReportName), report);
        Debug.Log(report);

        if (fail > 0)
            throw new InvalidOperationException(report);
    }

    private static void TestFinanceFinalizeFailureCompensatesWorld()
    {
        var baseline = new BistroBuilderEditDocument { revision = 100 };
        string baselineFingerprint = baseline.ComputeFingerprint();
        var session = DirtySession(baseline, out BistroBuilderWallRecord wall);
        var store = new BistroBuilderInMemoryEditDocumentStore(baseline);
        var journals = new BistroBuilderInMemoryCommitJournalStore();
        var economy = new ControlledEconomy { failFinalize = true };
        var coordinator = new BistroBuilderEditCommitCoordinator(store, journals, economy);

        bool ok = coordinator.TryCommit(
            session,
            out _,
            out BistroBuilderEditCommitJournal journal,
            out _,
            out string error);

        BistroBuilderEditDocument after = store.GetCommittedSnapshot();

        Check(!ok && !string.IsNullOrWhiteSpace(error),
            "B1 commit informa fallo cuando Finanzas rechaza Finalize");
        Check(after.revision == baseline.revision &&
              after.FindWall(wall.wallId) == null &&
              after.ComputeFingerprint() == baselineFingerprint,
            "B1 rollback compensatorio restaura exactamente la Baseline");
        Check(economy.prepareCount == 1 &&
              economy.finalizeCount == 1 &&
              economy.abortCount == 1,
            "B1 autorización económica queda abortada tras fallo de Finalize");
        Check(session.State == BistroBuilderEditSessionState.ActiveDirty,
            "B1 sesión vuelve a edición tras rollback compensatorio");
        Check(journal != null &&
              journal.state == BistroBuilderEditCommitJournalState.Aborted &&
              journals.TryRead(journal.operationId, out BistroBuilderEditCommitJournal persisted) &&
              persisted.state == BistroBuilderEditCommitJournalState.Aborted,
            "B1 journal termina Aborted después de compensar publicación");
    }

    private static void TestPostPublishJournalFailureDoesNotStrandCommit()
    {
        var baseline = new BistroBuilderEditDocument { revision = 200 };
        var session = DirtySession(baseline, out BistroBuilderWallRecord wall);
        var store = new BistroBuilderInMemoryEditDocumentStore(baseline);
        var journals = new ControlledJournalStore { failWriteOrdinal = 3 };
        var economy = new ControlledEconomy();
        var coordinator = new BistroBuilderEditCommitCoordinator(store, journals, economy);

        bool ok = coordinator.TryCommit(
            session,
            out BistroBuilderEditDocument committed,
            out BistroBuilderEditCommitJournal journal,
            out _,
            out string error);

        Check(ok && string.IsNullOrEmpty(error) &&
              committed != null &&
              committed.revision == 201 &&
              committed.FindWall(wall.wallId) != null,
            "B1 fallo de telemetría Published no deja commit a medias");
        Check(economy.finalizeCount == 1 &&
              economy.abortCount == 0 &&
              session.State == BistroBuilderEditSessionState.Committed,
            "B1 documento, Finanzas y sesión finalizan aunque falle journal post-publicación");
        Check(journal != null &&
              journal.state == BistroBuilderEditCommitJournalState.Finalized,
            "B1 resultado del commit conserva estado Finalized");
    }

    private static void TestPrePublishJournalFailureHasNoWorldSideEffects()
    {
        var baseline = new BistroBuilderEditDocument { revision = 300 };
        string fingerprint = baseline.ComputeFingerprint();
        var session = DirtySession(baseline, out _);
        var store = new BistroBuilderInMemoryEditDocumentStore(baseline);
        var journals = new ControlledJournalStore { failWriteOrdinal = 2 };
        var economy = new ControlledEconomy();
        var coordinator = new BistroBuilderEditCommitCoordinator(store, journals, economy);

        bool ok = coordinator.TryCommit(session, out _, out _, out _, out _);
        BistroBuilderEditDocument after = store.GetCommittedSnapshot();

        Check(!ok &&
              after.revision == 300 &&
              after.ComputeFingerprint() == fingerprint &&
              economy.finalizeCount == 0 &&
              economy.abortCount == 1,
            "B1 fallo journal pre-publicación no toca mundo ni cobra");
    }

    private static void TestRuntimeRollbackIsExactAndIdempotenceSafe()
    {
        var host = new GameObject("EditorV2_B1_RuntimeRollback");
        try
        {
            var runtime = host.AddComponent<BistroBuilderEditDocumentRuntimeService>();
            var baseline = new BistroBuilderEditDocument { revision = 400 };
            var candidate = baseline.DeepClone();
            candidate.revision = 401;
            candidate.walls.Add(Wall(new Vector2(0f, 0f), new Vector2(3f, 0f)));

            Check(runtime.ReplaceCommittedForLoad(baseline, out _),
                "B1 runtime acepta baseline de prueba");

            int publications = 0;
            runtime.DocumentPublished += _ => publications++;

            Check(runtime.TryPublish(400, candidate, "b1-runtime-op", out _),
                "B1 runtime publica candidato de prueba");
            Check(runtime.TryRollbackPublished(
                    "b1-runtime-op",
                    401,
                    baseline,
                    out _) &&
                  runtime.Revision == 400 &&
                  runtime.GetCommittedSnapshot().ComputeFingerprint() == baseline.ComputeFingerprint(),
                "B1 runtime rollback restaura revisión y fingerprint exactos");
            Check(publications == 2,
                "B1 publish y rollback notifican una proyección cada uno");
            Check(!runtime.TryRollbackPublished(
                    "b1-runtime-op",
                    401,
                    baseline,
                    out _),
                "B1 rollback no puede repetirse sobre operación ya compensada");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void TestLoadMaterializesExactlyOnce()
    {
        var host = new GameObject("EditorV2_B1_LoadProjection");
        try
        {
            var runtime = host.AddComponent<BistroBuilderEditDocumentRuntimeService>();
            var materializer = host.AddComponent<BistroBuilderArchitectureRuntimeMaterializer>();
            host.AddComponent<BistroBuilderEditDocumentMaterializationBridge>();
            var provider = host.AddComponent<BistroBuilderEditDocumentSaveSectionProvider>();

            int before = materializer.RebuildInvocationCount;
            var loaded = new BistroBuilderEditDocument { revision = 500 };
            loaded.walls.AddRange(RectangleWalls(0f, 0f, 4f, 3f));

            var context = new BistroBuilderSaveLoadContext(1, false, 64);
            System.Collections.IEnumerator routine = provider.ApplyState(loaded, context);
            while (routine.MoveNext()) { }

            Check(!context.HasFailed && runtime.Revision == 500,
                "B1 Save/Load aplica documento arquitectónico válido");
            Check(materializer.RebuildInvocationCount == before + 1,
                "B1 Load materializa arquitectura exactamente una vez");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static BistroBuilderEditSession DirtySession(
        BistroBuilderEditDocument baseline,
        out BistroBuilderWallRecord wall)
    {
        var session = new BistroBuilderEditSession(baseline);
        wall = Wall(new Vector2(0f, 0f), new Vector2(4f, 0f));
        if (!session.TryExecute(
                new BistroBuilderCreateWallCommand(wall),
                out _,
                out string error))
            throw new InvalidOperationException("No se pudo preparar sesión B1: " + error);
        return session;
    }

    private static BistroBuilderWallRecord Wall(Vector2 a, Vector2 b)
    {
        return new BistroBuilderWallRecord
        {
            wallId = BistroBuilderEditId.NewId(),
            buildPlaneId = "default",
            axisStart = a,
            axisEnd = b,
            height = 2.8f,
            thickness = 0.12f,
            wallDefinitionId = "wall.default"
        };
    }

    private static List<BistroBuilderWallRecord> RectangleWalls(
        float x,
        float y,
        float width,
        float height)
    {
        Vector2 a = new Vector2(x, y);
        Vector2 b = new Vector2(x + width, y);
        Vector2 c = new Vector2(x + width, y + height);
        Vector2 d = new Vector2(x, y + height);
        return new List<BistroBuilderWallRecord>
        {
            Wall(a, b),
            Wall(b, c),
            Wall(c, d),
            Wall(d, a)
        };
    }

    private sealed class ControlledEconomy : IBistroBuilderEditEconomicGateway
    {
        public int prepareCount;
        public int finalizeCount;
        public int abortCount;
        public bool failPrepare;
        public bool failFinalize;
        public bool failAbort;

        public bool TryPrepareAuthorization(
            BistroBuilderEditEconomicProposal proposal,
            out BistroBuilderEditEconomicAuthorization authorization,
            out string error)
        {
            prepareCount++;
            if (failPrepare)
            {
                authorization = default;
                error = "B1 injected prepare failure.";
                return false;
            }

            authorization = new BistroBuilderEditEconomicAuthorization(
                "b1-auth-" + prepareCount,
                proposal.draftRevision,
                1000);
            error = string.Empty;
            return true;
        }

        public bool TryFinalizeAuthorization(
            BistroBuilderEditEconomicAuthorization authorization,
            string commitOperationId,
            out string error)
        {
            finalizeCount++;
            if (failFinalize)
            {
                error = "B1 injected finalize failure.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryAbortAuthorization(
            BistroBuilderEditEconomicAuthorization authorization,
            out string error)
        {
            abortCount++;
            if (failAbort)
            {
                error = "B1 injected abort failure.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    private sealed class ControlledJournalStore : IBistroBuilderEditCommitJournalStore
    {
        private readonly Dictionary<string, BistroBuilderEditCommitJournal> journals =
            new Dictionary<string, BistroBuilderEditCommitJournal>(StringComparer.Ordinal);
        private int writes;

        public int failWriteOrdinal;

        public bool TryWrite(BistroBuilderEditCommitJournal journal, out string error)
        {
            writes++;
            if (writes == failWriteOrdinal)
            {
                error = "B1 injected journal failure #" + writes;
                return false;
            }

            error = string.Empty;
            journals[journal.operationId] = Clone(journal);
            return true;
        }

        public bool TryRead(string operationId, out BistroBuilderEditCommitJournal journal)
        {
            journal = null;
            if (!journals.TryGetValue(operationId ?? string.Empty, out var stored))
                return false;
            journal = Clone(stored);
            return true;
        }

        private static BistroBuilderEditCommitJournal Clone(
            BistroBuilderEditCommitJournal source)
        {
            return new BistroBuilderEditCommitJournal
            {
                operationId = source.operationId,
                sessionId = source.sessionId,
                baselineRevision = source.baselineRevision,
                draftRevision = source.draftRevision,
                draftFingerprint = source.draftFingerprint,
                economicAuthorizationId = source.economicAuthorizationId,
                state = source.state
            };
        }
    }

    private static void Check(bool condition, string label)
    {
        if (condition)
        {
            pass++;
            Lines.Add("OK - " + label);
        }
        else
        {
            fail++;
            Lines.Add("FAIL - " + label);
        }
    }
}
