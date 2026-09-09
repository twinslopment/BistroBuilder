using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderEditBlock18CoreSelfTest
{
    private const string ReportName = "EditBlock18CoreSelfTestReport.txt";
    private static int pass;
    private static int fail;
    private static readonly List<string> lines = new List<string>();

    [MenuItem("Tools/Bistro Builder/Edit Mode/18 - Core Self Test", false, 18000)]
    public static void RunFromMenu() => Run(false);

    public static void RunFromCommandLine()
    {
        try { Run(true); EditorApplication.Exit(fail == 0 ? 0 : 1); }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    private static void Run(bool cli)
    {
        pass = 0; fail = 0; lines.Clear();
        TestStableIdAndIsolation();
        TestTopologyTJunction();
        TestTopologyXJunction();
        TestCollinearOverlap();
        TestTransactionalRoomsUndoRedo();
        TestOpeningValidationAndCascade();
        TestCommitAndCancel();
        TestJsonRoundTrip();
        TestUniversalCatalogAndFeedback();
        TestEconomicAtomicCommit();
        TestSurfaceAndZoneCommands();
        TestGeneratedGeometry();
        TestRuntimeDocumentPersistenceAndCapabilities();
        TestRuntimeCoordinatorAndEconomics();
        TestSpatialNavigationProjection();
        TestFinancePricingPolicy();
        TestExternalNavigationValidation();
        string report = "BB EDIT MODE BLOCK 18 - CORE SELF TEST\n" + string.Join("\n", lines) +
            $"\nResultado: {pass} OK / {fail} fallos.";
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName), report);
        Debug.Log(report);
        if (fail > 0) throw new InvalidOperationException(report);
    }

    private static void TestStableIdAndIsolation()
    {
        var baseline = new BistroBuilderEditDocument { revision = 7 };
        var session = new BistroBuilderEditSession(baseline);
        var wall = Wall(new Vector2(0,0), new Vector2(4,0));
        Check(session.TryExecute(new BistroBuilderCreateWallCommand(wall), out _, out _), "18A comando entra en Draft");
        Check(baseline.walls.Count == 0 && session.Draft.walls.Count == 1, "18A Baseline aislada del Draft");
        Check(session.Draft.walls[0].wallId == wall.wallId, "18D WallId estable en Draft");
    }

    private static void TestTopologyTJunction()
    {
        var a = Wall(new Vector2(0,0), new Vector2(4,0));
        var b = Wall(new Vector2(2,-2), new Vector2(2,0));
        var top = new BistroBuilderWallTopologyBuilder().Build(new [] { a, b });
        Check(!top.HasBlockingDiagnostics, "18D T-junction vÃ¡lida");
        Check(CountSpans(top, a.wallId) == 2 && CountSpans(top, b.wallId) == 1, "18D T-junction deriva spans sin partir WallId");
    }
    private static void TestTopologyXJunction()
    {
        var a = Wall(new Vector2(-2,0), new Vector2(2,0));
        var b = Wall(new Vector2(0,-2), new Vector2(0,2));
        var top = new BistroBuilderWallTopologyBuilder().Build(new [] { a, b });
        Check(!top.HasBlockingDiagnostics, "18D X-junction vÃ¡lida");
        Check(CountSpans(top, a.wallId) == 2 && CountSpans(top, b.wallId) == 2, "18D X-junction conserva ambos WallId");
    }

    private static void TestCollinearOverlap()
    {
        var a = Wall(new Vector2(0,0), new Vector2(4,0));
        var b = Wall(new Vector2(2,0), new Vector2(6,0));
        var top = new BistroBuilderWallTopologyBuilder().Build(new [] { a, b });
        Check(top.HasBlockingDiagnostics, "18D solape colineal bloqueante");
    }

    private static void TestTransactionalRoomsUndoRedo()
    {
        var session = new BistroBuilderEditSession(new BistroBuilderEditDocument());
        var walls = RectangleWalls(0, 0, 6, 3);
        for (int i = 0; i < walls.Count; i++)
            Check(session.TryExecute(new BistroBuilderCreateWallCommand(walls[i]), out _, out _), $"18A CreateWall {i + 1}/4");
        Check(session.RoomProjections.Count == 1, "18E recinto cerrado detecta una habitaciÃ³n");
        BistroBuilderEditId roomId = session.RoomProjections[0].room.roomId;
        Check(session.TryUndo(out _), "18L Undo pared");
        Check(session.RoomProjections.Count == 0, "18E Undo abre recinto coherentemente");
        Check(session.TryRedo(out _), "18L Redo pared");
        Check(session.RoomProjections.Count == 1 && session.RoomProjections[0].room.roomId == roomId,
            "18E Redo restaura el mismo RoomId");

        var divider = Wall(new Vector2(2,0), new Vector2(2,3));
        Check(session.TryExecute(new BistroBuilderCreateWallCommand(divider), out _, out _), "18D divisor interior creado");
        Check(session.RoomProjections.Count == 2, "18E split produce dos habitaciones");
        bool retained = false;
        for (int i = 0; i < session.RoomProjections.Count; i++) if (session.RoomProjections[i].room.roomId == roomId) retained = true;
        Check(retained, "18E split conserva un RoomId preexistente");
        var splitIds = new HashSet<string>();
        for (int i = 0; i < session.RoomProjections.Count; i++) splitIds.Add(session.RoomProjections[i].room.roomId.Value);
        Check(session.TryUndo(out _), "18L Undo split");
        Check(session.RoomProjections.Count == 1 && session.RoomProjections[0].room.roomId == roomId, "18E merge por Undo recupera habitaciÃ³n original");
        Check(session.TryRedo(out _), "18L Redo split");
        bool sameSplitIds = session.RoomProjections.Count == 2;
        for (int i = 0; i < session.RoomProjections.Count; i++) sameSplitIds &= splitIds.Contains(session.RoomProjections[i].room.roomId.Value);
        Check(sameSplitIds, "18E Redo split recupera exactamente los mismos RoomIds");
    }

    private static void TestOpeningValidationAndCascade()
    {
        var doc = new BistroBuilderEditDocument();
        var wall = Wall(new Vector2(0,0), new Vector2(5,0)); doc.walls.Add(wall);
        var session = new BistroBuilderEditSession(doc);
        var opening = new BistroBuilderOpeningRecord
        { openingId = BistroBuilderEditId.NewId(), hostWallId = wall.wallId, axisPosition01 = 0.5f, width = 1f, height = 2.1f };
        Check(session.TryExecute(new BistroBuilderCreateOpeningCommand(opening), out _, out _), "18F opening ligado a WallId");
        Check(session.TryReview(out var validDiagnostics), "18H opening vÃ¡lido supera validaciÃ³n intrÃ­nseca");
        var shortWall = wall.DeepClone(); shortWall.axisEnd = new Vector2(0.7f, 0);
        var update = new BistroBuilderUpdateWallCommand(shortWall);
        session = new BistroBuilderEditSession(doc);
        session.TryExecute(new BistroBuilderCreateOpeningCommand(opening), out _, out _);
        Check(session.TryExecute(update, out _, out _), "18D pared host modificable en Draft");
        Check(!session.TryReview(out var invalidDiagnostics) && ContainsCode(invalidDiagnostics, "ARCH_OPENING_OUTSIDE_HOST"),
            "18F opening fuera del host bloquea commit sin recolocaciÃ³n silenciosa");

        session = new BistroBuilderEditSession(doc);
        session.TryExecute(new BistroBuilderCreateOpeningCommand(opening), out _, out _);
        Check(session.TryExecute(new BistroBuilderDeleteWallCommand(wall.wallId), out _, out _), "18F DeleteWall compone cascade de openings");
        Check(session.Draft.FindWall(wall.wallId) == null && session.Draft.FindOpening(opening.openingId) == null,
            "18F host y opening desaparecen atÃ³micamente");
        Check(session.TryUndo(out _), "18L Undo cascade");
        Check(session.Draft.FindWall(wall.wallId) != null && session.Draft.FindOpening(opening.openingId) != null,
            "18F Undo restaura host/opening con mismos IDs");
    }

    private static void TestCommitAndCancel()
    {
        var baseline = new BistroBuilderEditDocument { revision = 12 };
        var session = new BistroBuilderEditSession(baseline);
        var wall = Wall(new Vector2(0,0), new Vector2(3,0));
        session.TryExecute(new BistroBuilderCreateWallCommand(wall), out _, out _);
        Check(session.TryCommit(out var committed, out _), "18A commit vÃ¡lido");
        Check(committed.revision == 13 && committed.FindWall(wall.wallId) != null, "18A commit publica N+1");
        Check(baseline.revision == 12 && baseline.walls.Count == 0, "18A commit no muta Baseline original");
        var cancelled = new BistroBuilderEditSession(baseline);
        cancelled.TryExecute(new BistroBuilderCreateWallCommand(wall), out _, out _);
        cancelled.Cancel();
        Check(cancelled.State == BistroBuilderEditSessionState.Cancelled && cancelled.Draft.walls.Count == 0,
            "18A Cancel restaura Draft a Baseline");
    }

    private static void TestJsonRoundTrip()
    {
        var doc = new BistroBuilderEditDocument { revision = 21 };
        var wall = Wall(new Vector2(1,2), new Vector2(4,2)); doc.walls.Add(wall);
        string json = JsonUtility.ToJson(doc);
        var copy = JsonUtility.FromJson<BistroBuilderEditDocument>(json);
        Check(copy != null && copy.revision == 21 && copy.walls.Count == 1 && copy.walls[0].wallId == wall.wallId,
            "18L JSON round-trip conserva Stable IDs");
        Check(doc.ComputeFingerprint() == copy.ComputeFingerprint(), "18L fingerprint canÃ³nico estable tras persistencia");
    }

    private static void TestUniversalCatalogAndFeedback()
    {
        var index = new BistroBuilderUniversalEditCatalogIndex();
        index.Rebuild(new []
        {
            new BistroBuilderEditCatalogDefinition { definitionId = "chair_a", categoryId = "furniture", semanticKind = "seat", familyId = "chair" },
            new BistroBuilderEditCatalogDefinition { definitionId = "wall_basic", categoryId = "construction", semanticKind = "wall", familyId = "wall" },
            new BistroBuilderEditCatalogDefinition { definitionId = "chair_a", categoryId = "duplicate" }
        });
        Check(index.Items.Count == 2, "18B catÃ¡logo universal elimina DefinitionId duplicados");
        var found = new List<BistroBuilderEditCatalogDefinition>();
        Check(index.Search("chair", "furniture", found) == 1 && found[0].definitionId == "chair_a",
            "18B bÃºsqueda semÃ¡ntica/categorÃ­a determinista");
        var hub = new BistroBuilderEditFeedbackEventHub(); int events = 0;
        hub.EventPublished += _ => events++;
        hub.Publish(new BistroBuilderEditFeedbackEvent(BistroBuilderEditFeedbackEventType.DraftChanged,
            BistroBuilderEditId.NewId(), "wall", BistroBuilderEditFeedbackStyle.DirectionalReveal,
            BistroBuilderEditDiagnosticSeverity.Info, new Bounds()));
        Check(events == 1, "18J feedback desacoplado publica eventos sin decidir construcciÃ³n");
        var journal = new BistroBuilderEditCommitJournal { operationId = "op", draftFingerprint = "abc", state = BistroBuilderEditCommitJournalState.Prepared };
        Check(journal.Matches("op", "abc"), "18I/18L CommitJournal identifica operaciÃ³n idempotente");
    }
    private static void TestEconomicAtomicCommit()
    {
        var baseline = new BistroBuilderEditDocument { revision = 30 };
        var session = new BistroBuilderEditSession(baseline);
        var wall = Wall(new Vector2(0,0), new Vector2(4,0));
        session.TryExecute(new BistroBuilderCreateWallCommand(wall), out _, out _);
        var store = new BistroBuilderInMemoryEditDocumentStore(baseline);
        var journals = new BistroBuilderInMemoryCommitJournalStore();
        var economy = new FakeEconomy();
        var coordinator = new BistroBuilderEditCommitCoordinator(store, journals, economy);
        bool ok = coordinator.TryCommit(session, out var committed, out var journal, out _, out _);
        Check(ok && committed.revision == 31 && committed.FindWall(wall.wallId) != null,
            "18I commit econÃ³mico publica documento N+1");
        Check(economy.prepareCount == 1 && economy.finalizeCount == 1 && economy.abortCount == 0,
            "18I autorizaciÃ³n econÃ³mica Prepare/Finalize Ãºnica");
        Check(journal != null && journal.state == BistroBuilderEditCommitJournalState.Finalized &&
            journals.TryRead(journal.operationId, out var persisted) && persisted.state == BistroBuilderEditCommitJournalState.Finalized,
            "18L CommitJournal persiste estado Finalized");

        var staleSession = new BistroBuilderEditSession(baseline);
        staleSession.TryExecute(new BistroBuilderCreateWallCommand(Wall(new Vector2(0,1), new Vector2(4,1))), out _, out _);
        var staleEconomy = new FakeEconomy();
        var staleCoordinator = new BistroBuilderEditCommitCoordinator(store, new BistroBuilderInMemoryCommitJournalStore(), staleEconomy);
        Check(!staleCoordinator.TryCommit(staleSession, out _, out _, out _, out _),
            "18A baseline stale bloquea publicaciÃ³n");
        Check(staleEconomy.abortCount == 1 && staleSession.State == BistroBuilderEditSessionState.ActiveDirty,
            "18I fallo de publicaciÃ³n aborta autorizaciÃ³n y devuelve Draft a ediciÃ³n");
    }

    private static void TestSurfaceAndZoneCommands()
    {
        var session = new BistroBuilderEditSession(new BistroBuilderEditDocument());
        var patch = new BistroBuilderSurfaceFinishPatchRecord
        {
            surfacePatchId = BistroBuilderEditId.NewId(), finishDefinitionId = "floor.wood",
            fallbackBoundary = new List<Vector2> { new Vector2(0,0), new Vector2(3,0), new Vector2(3,2), new Vector2(0,2) }
        };
        Check(session.TryExecute(new BistroBuilderApplySurfaceFinishCommand(patch), out _, out _),
            "18E acabado de superficie entra como comando semÃ¡ntico");
        string fingerprintWithSurface = session.Draft.ComputeFingerprint();
        Check(session.TryUndo(out _) && session.Draft.surfaces.Count == 0, "18E/18L Undo de superficie");
        Check(session.TryRedo(out _) && session.Draft.surfaces.Count == 1 && session.Draft.ComputeFingerprint() == fingerprintWithSurface,
            "18E/18L Redo de superficie recupera estado/fingerprint");

        var zone = new BistroBuilderFunctionalZoneRecord
        { zoneId = BistroBuilderEditId.NewId(), zoneDefinitionId = "zone.kitchen" };
        zone.explicitRegion.AddRange(new [] { new Vector2(0,0), new Vector2(2,0), new Vector2(2,2), new Vector2(0,2) });
        Check(session.TryExecute(new BistroBuilderSetFunctionalZoneCommand(zone), out _, out _),
            "18G zona funcional separada de Room creada por comando");
        string zoneFingerprint = session.Draft.ComputeFingerprint();
        Check(session.TryUndo(out _) && session.Draft.zones.Count == 0, "18G/18L Undo de zona funcional");
        Check(session.TryRedo(out _) && session.Draft.zones.Count == 1 && session.Draft.ComputeFingerprint() == zoneFingerprint,
            "18G/18L Redo de zona recupera estado/fingerprint");
    }
    private static void TestGeneratedGeometry()
    {
        var wall = Wall(new Vector2(0,0), new Vector2(4,0));
        var door = new BistroBuilderOpeningRecord
        {
            openingId = BistroBuilderEditId.NewId(), hostWallId = wall.wallId,
            axisPosition01 = 0.5f, width = 1f, height = 2.1f, bottomElevation = 0f
        };
        Mesh wallMesh = BistroBuilderWallGeometryBuilder.Build(wall, new [] { door });
        Check(wallMesh != null && wallMesh.vertexCount == 72 && wallMesh.triangles.Length == 108,
            "18D/18F mesh de pared recorta opening sin partir WallId");
        Check(Mathf.Abs(wallMesh.bounds.size.x - 4f) < 0.001f &&
            Mathf.Abs(wallMesh.bounds.size.y - 2.8f) < 0.001f,
            "18D geometría derivada conserva dimensiones canónicas");
        UnityEngine.Object.DestroyImmediate(wallMesh);

        var polygon = new List<Vector2>
        {
            new Vector2(0,0), new Vector2(3,0), new Vector2(3,1),
            new Vector2(1,1), new Vector2(1,3), new Vector2(0,3)
        };
        Mesh floor = BistroBuilderPlanarGeometryBuilder.BuildHorizontalPolygon(polygon);
        Check(floor.vertexCount == 6 && floor.triangles.Length == 12,
            "18E triangulación de habitación cóncava determinista");
        UnityEngine.Object.DestroyImmediate(floor);

        var doc = new BistroBuilderEditDocument();
        doc.walls.AddRange(RectangleWalls(0,0,5,3));
        var host = new GameObject("BB18_Materializer_Test");
        var materializer = host.AddComponent<BistroBuilderArchitectureRuntimeMaterializer>();
        var summary = materializer.Rebuild(doc);
        Check(summary.wallObjects == 4 && summary.roomFloorObjects == 1 && summary.generatedObjectCount == 5,
            "18D/18E materialización runtime crea paredes y suelo derivados del documento");
        UnityEngine.Object.DestroyImmediate(host);
    }
    private static void TestRuntimeDocumentPersistenceAndCapabilities()
    {
        var host = new GameObject("BB18_RuntimeDocument_Test");
        var runtime = host.AddComponent<BistroBuilderEditDocumentRuntimeService>();
        var baseline = new BistroBuilderEditDocument { revision = 50 };
        var wall = Wall(new Vector2(0,0), new Vector2(2,0));
        baseline.walls.Add(wall);
        Check(runtime.ReplaceCommittedForLoad(baseline, out _), "18L runtime document acepta baseline cargada");
        Check(runtime.CreateSession().BaselineRevision == 50, "18A sesión nace desde revisión comprometida real");
        Check(runtime.TryGetCapabilities(wall.wallId, out var caps) &&
            (caps & BistroBuilderEditCapability.EditEndpoints) != 0 &&
            (caps & BistroBuilderEditCapability.ChangeFinish) != 0,
            "18C capabilities arquitectónicas resueltas por Stable ID");

        int publishes = 0;
        runtime.DocumentPublished += _ => publishes++;
        var candidate = baseline.DeepClone(); candidate.revision = 51;
        Check(runtime.TryPublish(50, candidate, "op-runtime-1", out _), "18L runtime store publica N+1");
        Check(runtime.TryPublish(50, candidate, "op-runtime-1", out _) && publishes == 1,
            "18L publicación idempotente no duplica efectos");

        var providerHost = new GameObject("BB18_SaveProvider_Test");
        var provider = providerHost.AddComponent<BistroBuilderEditDocumentSaveSectionProvider>();
        var capture = new BistroBuilderSaveCaptureContext(1);
        var captureRoutine = provider.CaptureState(capture);
        while (captureRoutine.MoveNext()) { }
        Check(!capture.HasFailed && capture.State is BistroBuilderEditDocument captured && captured.revision == 51,
            "18L SaveGame captura documento arquitectónico comprometido");

        var loaded = candidate.DeepClone(); loaded.revision = 77;
        var load = new BistroBuilderSaveLoadContext(1, false, 64);
        var applyRoutine = provider.ApplyState(loaded, load);
        while (applyRoutine.MoveNext()) { }
        Check(!load.HasFailed && runtime.Revision == 77,
            "18L SaveGame restaura documento arquitectónico por provider real");

        UnityEngine.Object.DestroyImmediate(providerHost);
        UnityEngine.Object.DestroyImmediate(host);
    }
    private static void TestRuntimeCoordinatorAndEconomics()
    {
        var econSession = new BistroBuilderEditSession(new BistroBuilderEditDocument { revision = 10 });
        var patch = new BistroBuilderSurfaceFinishPatchRecord
        {
            surfacePatchId = BistroBuilderEditId.NewId(), finishDefinitionId = "finish.oak", surfaceRole = "floor"
        };
        patch.fallbackBoundary.AddRange(new [] { new Vector2(0,0), new Vector2(4,0), new Vector2(4,3), new Vector2(0,3) });
        Check(econSession.TryExecute(new BistroBuilderApplySurfaceFinishCommand(patch), out _, out _),
            "18E superficie económica entra en Draft");
        var zone = new BistroBuilderFunctionalZoneRecord { zoneId = BistroBuilderEditId.NewId(), zoneDefinitionId = "zone.dining" };
        zone.explicitRegion.AddRange(new [] { new Vector2(0,0), new Vector2(2,0), new Vector2(2,2), new Vector2(0,2) });
        Check(econSession.TryExecute(new BistroBuilderSetFunctionalZoneCommand(zone), out _, out _),
            "18G zona económica entra en Draft");
        var proposal = BistroBuilderEditEconomicProposalBuilder.Build(econSession);
        Check(proposal.lines.Count == 2 && Mathf.Abs(proposal.lines[0].area + proposal.lines[1].area - 16f) < 0.001f,
            "18I propuesta económica incluye superficies y zonas con área");

        var host = new GameObject("BB18_RuntimeCoordinator_Test");
        var runtime = host.AddComponent<BistroBuilderEditDocumentRuntimeService>();
        var coordinator = host.AddComponent<BistroBuilderEditRuntimeCoordinator>();
        Check(coordinator.TryBeginSession(out _), "18A coordinador runtime abre sesión real");
        Check(coordinator.TryExecute(new BistroBuilderCreateWallCommand(Wall(new Vector2(0,0), new Vector2(3,0))), out _, out _),
            "18K coordinador ejecuta comando semántico");
        Check(!coordinator.TryCommit(out _, out _, out string missingEconomy) && !string.IsNullOrWhiteSpace(missingEconomy),
            "18I coordinador bloquea commit económico sin Finanzas");
        var economy = new FakeEconomy();
        Check(coordinator.TryBindEconomyGateway(economy, out _) &&
            coordinator.TryCommit(out var committed, out _, out _) && committed.revision == 1,
            "18I/18L coordinador publica commit atómico con autoridad económica");
        Check(economy.prepareCount == 1 && economy.finalizeCount == 1 && runtime.Revision == 1,
            "18I coordinador finaliza economía una sola vez");
        UnityEngine.Object.DestroyImmediate(host);
    }
    private static void TestSpatialNavigationProjection()
    {
        var contract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
        contract.ConfigureForEditor(
            "architecture.wall.runtime",
            "generic",
            BistroBuilderAdaptiveSpatialProxyMode.Simple,
            new [] { "architecture.wall", "static.obstacle" });

        var wall = Wall(new Vector2(0,0), new Vector2(4,0));
        var door = new BistroBuilderOpeningRecord
        {
            openingId = BistroBuilderEditId.NewId(),
            hostWallId = wall.wallId,
            axisPosition01 = 0.5f,
            width = 1f,
            bottomElevation = 0f,
            height = 2.1f,
            openingType = "door"
        };
        var doc = new BistroBuilderEditDocument();
        doc.walls.Add(wall);
        doc.openings.Add(door);

        var host = new GameObject("BB18_SpatialProjection_Test");
        var materializer = host.AddComponent<BistroBuilderArchitectureRuntimeMaterializer>();
        materializer.ConfigureSpatialProjectionRuntime(contract, true, true);
        var summary = materializer.Rebuild(doc);
        Check(summary.obstacleSegments == 2,
            "18F puerta recorta obstáculo de circulación en dos segmentos");
        var footprints = host.GetComponentsInChildren<RestaurantPlacementFootprint>(true);
        var subjects = host.GetComponentsInChildren<BistroBuilderSpatialSubject>(true);
        Check(footprints.Length == 2,
            "17/18 Navigation recibe dos footprints estáticos fuera del hueco de puerta");
        Check(subjects.Length == 2 &&
            subjects[0].ValidateSubject(out _) && subjects[1].ValidateSubject(out _),
            "BBSIS recibe Spatial Subjects válidos derivados de pared semántica");

        var spatialHost = new GameObject("BB18_BBSIS_Test");
        var spatial = spatialHost.AddComponent<BistroBuilderSpatialInteractionService>();
        spatial.RebuildSubjects();
        bool wallBlocked = spatial.TryFindStaticGeometryConflict(
            BistroBuilderSpatialVolume.Circle(new Vector3(0.5f,0f,0f), 0.08f),
            string.Empty, string.Empty, out _);
        bool doorBlocked = spatial.TryFindStaticGeometryConflict(
            BistroBuilderSpatialVolume.Circle(new Vector3(2f,0f,0f), 0.08f),
            string.Empty, string.Empty, out _);
        Check(wallBlocked && !doorBlocked,
            "BBSIS bloquea pared y mantiene libre el hueco transitable de puerta");

        var navHost = new GameObject("BB18_Navigation_Test");
        var navigation = navHost.AddComponent<BistroBuilderNavigationService>();
        navigation.RebuildNavigationTopology();
        Check(navigation.StaticObstacleCount >= 2,
            "17/18 Navigation reconstruye topología con arquitectura publicada");

        UnityEngine.Object.DestroyImmediate(navHost);
        UnityEngine.Object.DestroyImmediate(spatialHost);
        UnityEngine.Object.DestroyImmediate(host);
        UnityEngine.Object.DestroyImmediate(contract);
    }

    private static void TestFinancePricingPolicy()
    {
        var proposal = new BistroBuilderEditEconomicProposal { draftRevision = 9 };
        proposal.lines.Add(new BistroBuilderEditEconomicLine
        {
            kind = BistroBuilderEditEconomicChangeKind.Added,
            entityId = BistroBuilderEditId.NewId(),
            definitionId = "wall.default",
            length = 4f
        });
        proposal.lines.Add(new BistroBuilderEditEconomicLine
        {
            kind = BistroBuilderEditEconomicChangeKind.Added,
            entityId = BistroBuilderEditId.NewId(),
            definitionId = "finish.tile",
            area = 10f
        });
        proposal.lines.Add(new BistroBuilderEditEconomicLine
        {
            kind = BistroBuilderEditEconomicChangeKind.Removed,
            entityId = BistroBuilderEditId.NewId(),
            definitionId = "door.basic",
            quantity = 1f
        });
        var rates = new List<BistroBuilderEditFinanceRateDefinition>
        {
            new BistroBuilderEditFinanceRateDefinition
            {
                definitionId = "wall.default",
                addedSignedCentsPerUnit = 1000
            },
            new BistroBuilderEditFinanceRateDefinition
            {
                definitionId = "finish.tile",
                addedSignedCentsPerUnit = 500
            },
            new BistroBuilderEditFinanceRateDefinition
            {
                definitionId = "door.basic",
                removedSignedCentsPerUnit = -2000
            }
        };
        var priced = new List<BistroBuilderPricedEditEconomicLine>();
        Check(BistroBuilderEditFinancePricingPolicy.TryPrice(
                proposal, rates, priced,
                out long credit, out long debit, out _),
            "18I Finanzas tarifa propuesta semántica sin mutar Draft");
        Check(debit == 9000 && credit == 2000 && priced.Count == 3,
            "18I Finanzas calcula débito/crédito determinista por longitud/área/unidad");

        rates.RemoveAt(rates.Count - 1);
        Check(!BistroBuilderEditFinancePricingPolicy.TryPrice(
                proposal, rates, priced,
                out _, out _, out string missingRate) &&
              !string.IsNullOrWhiteSpace(missingRate),
            "18I Finanzas bloquea definitionId sin tarifa en vez de construir gratis");
    }

    private static void TestExternalNavigationValidation()
    {
        var host = new GameObject("BB18_ExternalValidation_Test");
        var runtime = host.AddComponent<BistroBuilderEditDocumentRuntimeService>();
        var provider = host.AddComponent<BistroBuilderEditNavigationValidationProvider>();
        Check(provider.Register() && runtime.ValidationProviderCount == 1,
            "18H Navigation registra validación externa en la autoridad de sesión");

        var wall = Wall(new Vector2(0,0), new Vector2(4,0));
        var baseline = new BistroBuilderEditDocument { revision = 20 };
        baseline.walls.Add(wall);
        baseline.openings.Add(new BistroBuilderOpeningRecord
        {
            openingId = BistroBuilderEditId.NewId(),
            hostWallId = wall.wallId,
            axisPosition01 = 0.5f,
            width = 0.55f,
            bottomElevation = 0f,
            height = 2.1f,
            openingType = "door"
        });
        Check(runtime.ReplaceCommittedForLoad(baseline, out _),
            "18H baseline de validación externa cargada");

        BistroBuilderEditSession session = runtime.CreateSession();
        bool review = session.TryReview(out var diagnostics);
        Check(!review && ContainsCode(diagnostics, "NAV_PASSAGE_TOO_NARROW"),
            "18H Navigation bloquea Draft con paso físicamente insuficiente");
        Check(diagnostics.Exists(d => d.sourceSystem == "Navigation"),
            "18H diagnóstico conserva autoridad de origen Navigation");

        UnityEngine.Object.DestroyImmediate(host);
    }

    private sealed class FakeEconomy : IBistroBuilderEditEconomicGateway
    {
        public int prepareCount, finalizeCount, abortCount;
        public bool TryPrepareAuthorization(BistroBuilderEditEconomicProposal proposal,
            out BistroBuilderEditEconomicAuthorization authorization, out string error)
        { prepareCount++; authorization = new BistroBuilderEditEconomicAuthorization("auth-" + prepareCount, proposal.draftRevision, 1000); error = string.Empty; return true; }
        public bool TryFinalizeAuthorization(BistroBuilderEditEconomicAuthorization authorization, string commitOperationId, out string error)
        { finalizeCount++; error = string.Empty; return true; }
        public bool TryAbortAuthorization(BistroBuilderEditEconomicAuthorization authorization, out string error)
        { abortCount++; error = string.Empty; return true; }
    }
    private static BistroBuilderWallRecord Wall(Vector2 a, Vector2 b)
    {
        return new BistroBuilderWallRecord
        {
            wallId = BistroBuilderEditId.NewId(), buildPlaneId = "default",
            axisStart = a, axisEnd = b, height = 2.8f, thickness = 0.12f, wallDefinitionId = "wall.default"
        };
    }

    private static List<BistroBuilderWallRecord> RectangleWalls(float x, float y, float width, float height)
    {
        Vector2 a = new Vector2(x,y), b = new Vector2(x+width,y), c = new Vector2(x+width,y+height), d = new Vector2(x,y+height);
        return new List<BistroBuilderWallRecord> { Wall(a,b), Wall(b,c), Wall(c,d), Wall(d,a) };
    }
    private static int CountSpans(BistroBuilderWallTopologyProjection topology, BistroBuilderEditId wallId)
    {
        int count = 0; for (int i = 0; i < topology.spans.Count; i++) if (topology.spans[i].sourceWallId == wallId) count++; return count;
    }

    private static bool ContainsCode(List<BistroBuilderEditDiagnostic> diagnostics, string code)
    {
        for (int i = 0; i < diagnostics.Count; i++) if (diagnostics[i].code == code) return true; return false;
    }

    private static void Check(bool condition, string label)
    {
        if (condition) { pass++; lines.Add("OK - " + label); }
        else { fail++; lines.Add("FAIL - " + label); }
    }
}

