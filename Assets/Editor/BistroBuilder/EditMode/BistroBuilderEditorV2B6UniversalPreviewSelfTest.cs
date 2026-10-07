using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using BistroBuilder.ConstructionAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BistroBuilderEditorV2B6UniversalPreviewSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private static readonly List<string> Lines = new List<string>(160);
    private static int pass;
    private static int fail;

    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    [MenuItem("Bistro Builder/QA/Editor V2/B6 Universal Preview Self Test")]
    public static void RunFromMenu()
    {
        Run();
    }

    private static bool Run()
    {
        Lines.Clear();
        pass = 0;
        fail = 0;
        Lines.Add("EDITOR V2 - B6 UNIVERSAL PREVIEW SELF TEST");

        GameObject furnitureRoot = null;
        GameObject conflictRoot = null;

        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            InvokeBootstrap(typeof(BistroBuilderConstructionAuthoringRuntimeBootstrap));
            InvokeBootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));
            InvokeStaticPrivate(
                typeof(BistroBuilderUniversalPreviewService),
                "EnsureRuntimeInstallation");

            var editMode = Find<RestaurantEditModeService>();
            var preview = Find<BistroBuilderUniversalPreviewService>();
            var previewRenderer = Find<BistroBuilderUniversalPreviewRenderer>();
            var proxy = Find<BistroBuilderFurniturePreviewProxyRenderer>();
            var feedback = Find<RestaurantEditPlacementVisualFeedback>();
            var tool = Find<BistroBuilderConstructionAuthoringRuntimeTool>();
            var architecture = Find<BistroBuilderEditRuntimeCoordinator>();
            var document = Find<BistroBuilderEditDocumentRuntimeService>();
            var renovation = Find<BistroBuilderEditorV2RenovationSession>();

            bool dependencies =
                editMode != null &&
                preview != null &&
                previewRenderer != null &&
                proxy != null &&
                tool != null &&
                architecture != null &&
                document != null;

            Check(
                dependencies,
                "B6 dispone de Universal Preview, renderer, proxy de mobiliario y Construction reales");
            if (!dependencies)
                return Finish();

            InvokeLifecycle(previewRenderer, "Awake");
            InvokeLifecycle(previewRenderer, "OnEnable");
            InvokeLifecycle(proxy, "Awake");

            Check(
                Count<BistroBuilderUniversalPreviewService>() == 1,
                "B6 mantiene una única autoridad Universal Preview");
            Check(
                Count<BistroBuilderUniversalPreviewRenderer>() == 1,
                "B6 mantiene un único renderer universal");
            Check(
                Count<BistroBuilderFurniturePreviewProxyRenderer>() == 1,
                "B6 mantiene un único proxy visual de mobiliario");

            bool entered =
                editMode.IsEditModeActive ||
                editMode.TryEnterEditMode(out _, out _);
            Check(entered, "B6 entra en modo edición real");
            if (!entered)
                return Finish();

            if (!architecture.HasSession)
            {
                bool sessionStarted =
                    architecture.TryBeginSession(out string sessionError);
                Check(
                    sessionStarted,
                    "B6 dispone de Draft arquitectónico real" +
                    Suffix(sessionError));
                if (!sessionStarted)
                    return Finish();
            }
            else
            {
                Check(true, "B6 reutiliza la sesión arquitectónica transaccional de B5");
            }

            string committedBaseline =
                document.GetCommittedSnapshot().ComputeFingerprint();
            string draftBaseline =
                architecture.Session.Draft.ComputeFingerprint();
            long draftRevisionBaseline =
                architecture.Session.DraftRevision;
            int undoBaseline =
                architecture.Session.UndoCount;
            bool renovationDirtyBeforePreview =
                renovation != null && renovation.HasPendingChanges;

            // ---------------------------------------------------------
            // 1. Construction real: habitación provisional.
            // ---------------------------------------------------------
            bool roomPreviewed =
                tool.TryPreviewRoomAtPlanPoints(
                    new Vector2(120f, 120f),
                    new Vector2(124f, 123f),
                    "zone.dining",
                    out string roomPreviewError);

            Check(
                roomPreviewed,
                "B6 previsualiza una habitación con el adaptador Construction real" +
                Suffix(roomPreviewError));
            Check(
                preview.Current.IsVisible &&
                preview.Current.OwnerId == BistroBuilderUniversalPreviewService.ConstructionOwner &&
                preview.Current.Domain == BistroBuilderPreviewDomain.Room &&
                preview.Current.Validity == BistroBuilderPreviewValidity.Valid &&
                preview.Current.CandidateSegments.Count == 8 &&
                preview.Current.Volumes.Count == 4,
                "B6 habitación usa lenguaje universal: contorno + 4 volúmenes provisionales");
            AddExample("ROOM", preview.Current);

            Check(
                architecture.Session.DraftRevision == draftRevisionBaseline &&
                architecture.Session.UndoCount == undoBaseline &&
                string.Equals(
                    architecture.Session.Draft.ComputeFingerprint(),
                    draftBaseline,
                    StringComparison.Ordinal),
                "B6 preview de habitación no muta Draft ni historial");
            Check(
                renovation == null ||
                renovation.HasPendingChanges == renovationDirtyBeforePreview,
                "B6 preview puro no ensucia la reforma transaccional B5");

            tool.CancelCurrentGesture();
            CheckHidden(preview, "B6 cancelar habitación retira todo el preview sin residuo");
            Check(
                string.Equals(
                    architecture.Session.Draft.ComputeFingerprint(),
                    draftBaseline,
                    StringComparison.Ordinal),
                "B6 cancelar habitación conserva exactamente el Draft inicial");

            // ---------------------------------------------------------
            // 2. Module adapter real.
            // ---------------------------------------------------------
            tool.ConfigureModule(2f, 0f);
            tool.SetMode(BistroBuilderConstructionRuntimeMode.WallModule);
            InvokePrivate(
                tool,
                "RenderModule",
                new object[] { new Vector2(135f, 135f) });

            Check(
                preview.Current.IsVisible &&
                preview.Current.Domain == BistroBuilderPreviewDomain.Module &&
                preview.Current.CandidateSegments.Count == 2 &&
                preview.Current.Volumes.Count == 1,
                "B6 módulo estructural publica segmento y volumen en Universal Preview");
            AddExample("MODULE", preview.Current);

            Check(
                LegacyConstructionPreviewIsSuppressed(tool),
                "B6 no crea preview/snap paralelo legacy cuando Universal Preview está disponible");

            tool.SetMode(BistroBuilderConstructionRuntimeMode.Furniture);
            CheckHidden(preview, "B6 al abandonar módulo limpia su preview universal");

            // ---------------------------------------------------------
            // 3. Furniture common visual language.
            // ---------------------------------------------------------
            furnitureRoot = new GameObject("__B6_FurnitureCandidate");
            furnitureRoot.transform.position = new Vector3(20f, 0f, 20f);
            var member = furnitureRoot.AddComponent<RestaurantAreaMember>();
            var footprint = furnitureRoot.AddComponent<RestaurantPlacementFootprint>();

            conflictRoot = new GameObject("__B6_FurnitureConflict");
            conflictRoot.transform.position = new Vector3(20.6f, 0f, 20f);
            var conflictFootprint =
                conflictRoot.AddComponent<RestaurantPlacementFootprint>();

            var validValidation =
                new RestaurantPlacementValidationResult(
                    RestaurantPlacementValidationStatus.Valid,
                    member,
                    footprint,
                    null,
                    null,
                    null,
                    RestaurantPlacementConflictType.None);

            var snapped =
                new RestaurantPlacementSnapResult(
                    true,
                    furnitureRoot.transform.position,
                    furnitureRoot.transform.rotation,
                    default,
                    conflictRoot,
                    RestaurantPlacementSnapHintState.Available,
                    0.05f);

            preview.PublishFurniture(
                member,
                furnitureRoot.transform.position - Vector3.right,
                furnitureRoot.transform.rotation,
                validValidation,
                snapped,
                true);

            Check(
                preview.Current.IsVisible &&
                preview.Current.OwnerId == BistroBuilderUniversalPreviewService.FurnitureOwner &&
                preview.Current.Domain == BistroBuilderPreviewDomain.Furniture &&
                preview.Current.Validity == BistroBuilderPreviewValidity.Valid &&
                preview.Current.Phase == BistroBuilderPreviewPhase.Snapped &&
                preview.Current.CandidateSegments.Count == 8 &&
                preview.Current.GhostSegments.Count == 8 &&
                preview.Current.HasSnapPoint,
                "B6 mobiliario válido comparte huella, ghost y snap universal");
            AddExample("FURNITURE_VALID", preview.Current);

            var invalidValidation =
                new RestaurantPlacementValidationResult(
                    RestaurantPlacementValidationStatus.PhysicalOverlap,
                    member,
                    footprint,
                    null,
                    null,
                    conflictFootprint,
                    RestaurantPlacementConflictType.PhysicalOverlap);

            preview.PublishFurniture(
                member,
                furnitureRoot.transform.position - Vector3.right,
                furnitureRoot.transform.rotation,
                invalidValidation,
                RestaurantPlacementSnapResult.Unsnapped(
                    furnitureRoot.transform.position,
                    furnitureRoot.transform.rotation),
                true);

            Check(
                preview.Current.Validity == BistroBuilderPreviewValidity.Invalid &&
                ReferenceEquals(preview.Current.ConflictObject, conflictFootprint) &&
                preview.Current.ConflictSegments.Count == 8 &&
                preview.Current.CandidateSegments.Count == 8,
                "B6 conflicto de mobiliario se localiza en la huella conflictiva");
            AddExample("FURNITURE_CONFLICT", preview.Current);

            bool legacyTintDisabled =
                feedback == null ||
                !ReadPrivateField<bool>(
                    feedback,
                    "useLegacyFullObjectTint",
                    true);
            Check(
                legacyTintDisabled,
                "B6 no tinta por defecto el objeto completo verde/rojo");

            preview.ClearOwner(BistroBuilderUniversalPreviewService.FurnitureOwner);
            CheckHidden(preview, "B6 limpiar mobiliario deja estado universal vacío");

            // ---------------------------------------------------------
            // 4. Furniture proxy: lift / settle / cancel.
            // ---------------------------------------------------------
            GameObject meshChild =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshChild.name = "__B6_FurnitureMesh";
            meshChild.transform.SetParent(furnitureRoot.transform, false);
            MeshRenderer sourceRenderer =
                meshChild.GetComponent<MeshRenderer>();

            InvokePrivate(proxy, "BeginProxy", new object[] { member });
            int proxyEntryCount =
                ReadPrivateListCount(proxy, "entries");
            Check(
                proxyEntryCount > 0 && !sourceRenderer.enabled,
                "B6 proxy toma el mesh real y separa la presentación de la pose lógica");

            float liftHeight =
                ReadPrivateField<float>(proxy, "liftHeight", 0f);
            float liftDuration =
                ReadPrivateField<float>(proxy, "liftDuration", 0f);
            float settleDuration =
                ReadPrivateField<float>(proxy, "settleDuration", 0f);
            Check(
                liftHeight > 0f &&
                liftDuration > 0f &&
                settleDuration > 0f,
                "B6 elevación y asentamiento de mobiliario están configurados y acotados");

            InvokePrivate(
                proxy,
                "HandleCommitted",
                new object[] { member, validValidation });
            Check(
                ReadPrivateField<bool>(proxy, "settling", false) &&
                !sourceRenderer.enabled,
                "B6 confirmación inicia asentamiento sin saltar el objeto fuente");

            InvokePrivate(
                proxy,
                "HandleCancelled",
                new object[] { member });
            Check(
                sourceRenderer.enabled &&
                ReadPrivateListCount(proxy, "entries") == 0,
                "B6 cancelación del proxy restaura renderer exacto y libera estado temporal");

            // ---------------------------------------------------------
            // 5. Build a draft-only room and exercise Surface adapter.
            // ---------------------------------------------------------
            BistroBuilderRoomProjection room =
                CreateDraftOnlyRoom(
                    architecture,
                    new Vector2(200f, 200f),
                    4f,
                    out string roomSetupError);

            Check(
                room != null,
                "B6 prepara habitación de prueba solo en Draft para validar superficies" +
                Suffix(roomSetupError));

            if (room != null)
            {
                bool selectedRoom =
                    tool.TrySelectArchitecture(
                        EntityKind.Room,
                        room.room.roomId,
                        out string selectRoomError);
                Check(
                    selectedRoom,
                    "B6 selecciona habitación mediante autoridad Construction" +
                    Suffix(selectRoomError));

                long beforeSurfaceRevision =
                    architecture.Session.DraftRevision;
                int beforeSurfaceUndo =
                    architecture.Session.UndoCount;
                string beforeSurfaceFingerprint =
                    architecture.Session.Draft.ComputeFingerprint();

                bool surfacePreviewed =
                    tool.TryRefreshSelectedRoomFloorFinishPreview(
                        out string surfaceError);
                Check(
                    surfacePreviewed &&
                    preview.Current.Domain == BistroBuilderPreviewDomain.Surface &&
                    preview.Current.Validity == BistroBuilderPreviewValidity.Valid &&
                    preview.Current.CandidateSegments.Count == room.boundary.Count * 2 &&
                    preview.Current.Volumes.Count == 1,
                    "B6 superficie seleccionada publica contorno y cobertura universal" +
                    Suffix(surfaceError));
                AddExample("SURFACE", preview.Current);

                Check(
                    architecture.Session.DraftRevision == beforeSurfaceRevision &&
                    architecture.Session.UndoCount == beforeSurfaceUndo &&
                    string.Equals(
                        architecture.Session.Draft.ComputeFingerprint(),
                        beforeSurfaceFingerprint,
                        StringComparison.Ordinal),
                    "B6 preview de superficie no ejecuta comando ni modifica Draft");

                tool.ClearSurfacePreview();
                CheckHidden(preview, "B6 cerrar superficie retira su preview sin residuo");
            }

            // ---------------------------------------------------------
            // 6. Opening adapter real using one test-room wall.
            // ---------------------------------------------------------
            BistroBuilderWallRecord hostWall =
                FindWall(
                    architecture.Session.Draft,
                    "b6_preview_room_wall_0");
            if (hostWall != null)
            {
                Vector2 midpoint =
                    (hostWall.axisStart + hostWall.axisEnd) * 0.5f;
                tool.SetMode(BistroBuilderConstructionRuntimeMode.Door);
                InvokePrivate(
                    tool,
                    "RenderOpeningHover",
                    new object[] { midpoint });

                Check(
                    preview.Current.IsVisible &&
                    preview.Current.Domain == BistroBuilderPreviewDomain.Opening &&
                    preview.Current.CandidateSegments.Count == 2 &&
                    preview.Current.HasSnapPoint,
                    "B6 puerta/ventana usa el mismo preview y snap universal");
                AddExample("OPENING", preview.Current);

                tool.SetMode(BistroBuilderConstructionRuntimeMode.Furniture);
                CheckHidden(preview, "B6 abandonar hueco limpia preview universal");
            }
            else
            {
                Check(false, "B6 no encontró la pared de prueba para preview de hueco");
            }

            // ---------------------------------------------------------
            // 7. Renderer pooling + representative performance.
            // ---------------------------------------------------------
            var candidates = new List<Vector3>(8)
            {
                new Vector3(0f, .05f, 0f), new Vector3(2f, .05f, 0f),
                new Vector3(2f, .05f, 0f), new Vector3(2f, .05f, 2f),
                new Vector3(2f, .05f, 2f), new Vector3(0f, .05f, 2f),
                new Vector3(0f, .05f, 2f), new Vector3(0f, .05f, 0f)
            };
            var ghosts = new List<Vector3>(4)
            {
                new Vector3(-1f, .04f, 0f), new Vector3(-1f, .04f, 2f),
                new Vector3(-1f, .04f, 2f), new Vector3(-2f, .04f, 2f)
            };
            var volumes = new List<BistroBuilderPreviewBox>(2)
            {
                new BistroBuilderPreviewBox(
                    new Vector3(1f, 1.25f, 0f),
                    Quaternion.identity,
                    new Vector3(2f, 2.5f, .12f)),
                new BistroBuilderPreviewBox(
                    new Vector3(2f, 1.25f, 1f),
                    Quaternion.Euler(0f, 90f, 0f),
                    new Vector3(2f, 2.5f, .12f))
            };

            preview.PublishConstruction(
                BistroBuilderPreviewDomain.Wall,
                BistroBuilderPreviewValidity.Valid,
                BistroBuilderPreviewPhase.Snapped,
                candidates,
                ghosts,
                volumes,
                true,
                new Vector3(2f, .07f, 0f),
                "B6 performance warmup");

            Transform visualRoot =
                ReadPrivateField<Transform>(
                    previewRenderer,
                    "visualRoot",
                    null);
            int warmChildCount =
                visualRoot != null ? visualRoot.childCount : -1;

            const int iterations = 10000;
            long allocatedBefore =
                GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                preview.PublishConstruction(
                    BistroBuilderPreviewDomain.Wall,
                    BistroBuilderPreviewValidity.Valid,
                    BistroBuilderPreviewPhase.Snapped,
                    candidates,
                    ghosts,
                    volumes,
                    true,
                    new Vector3(2f, .07f, 0f),
                    "B6 performance");
            }
            stopwatch.Stop();
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            int finalChildCount =
                visualRoot != null ? visualRoot.childCount : -1;

            Check(
                warmChildCount >= 0 &&
                finalChildCount == warmChildCount,
                "B6 renderer reutiliza pools: 10.000 updates no crean nuevos objetos visuales");
            Check(
                stopwatch.ElapsedMilliseconds < 3000,
                "B6 rendimiento: 10.000 publicaciones < 3 s (" +
                stopwatch.ElapsedMilliseconds + " ms)");
            Check(
                allocated < 8L * 1024L * 1024L,
                "B6 asignaciones acotadas en 10.000 publicaciones (" +
                allocated + " bytes)");

            Lines.Add(
                "METRIC - PREVIEW_10000_MS=" +
                stopwatch.ElapsedMilliseconds);
            Lines.Add(
                "METRIC - PREVIEW_10000_ALLOCATED_BYTES=" +
                allocated);
            Lines.Add(
                "METRIC - PREVIEW_POOL_CHILDREN=" +
                warmChildCount);

            preview.ClearOwner(
                BistroBuilderUniversalPreviewService.ConstructionOwner);
            CheckHidden(preview, "B6 limpiar tras stress test deja renderer sin preview activo");

            // ---------------------------------------------------------
            // 8. Final non-destructive gate.
            // ---------------------------------------------------------
            if (renovation != null && renovation.IsActive)
            {
                bool discarded =
                    renovation.TryDiscardChanges(out string discardError);
                Check(
                    discarded,
                    "B6 limpia su fixture Draft mediante la transacción B5" +
                    Suffix(discardError));
            }
            else if (architecture.HasSession)
            {
                architecture.CancelSession();
                Check(true, "B6 cancela su fixture Draft al cerrar la prueba");
            }

            Check(
                string.Equals(
                    document.GetCommittedSnapshot().ComputeFingerprint(),
                    committedBaseline,
                    StringComparison.Ordinal),
                "B6 ningún preview ni fixture de prueba altera el documento canónico");

            CheckHidden(
                preview,
                "B6 finaliza sin preview, ghost, snap, conflicto ni volumen residual");
        }
        catch (Exception ex)
        {
            fail++;
            Lines.Add("EXCEPTION - " + ex);
        }
        finally
        {
            if (furnitureRoot != null)
                Object.DestroyImmediate(furnitureRoot);
            if (conflictRoot != null)
                Object.DestroyImmediate(conflictRoot);
        }

        return Finish();
    }

    private static BistroBuilderRoomProjection CreateDraftOnlyRoom(
        BistroBuilderEditRuntimeCoordinator architecture,
        Vector2 origin,
        float side,
        out string error)
    {
        error = string.Empty;
        Vector2[] points =
        {
            origin,
            origin + new Vector2(side, 0f),
            origin + new Vector2(side, side),
            origin + new Vector2(0f, side)
        };

        for (int i = 0; i < 4; i++)
        {
            var wall = new BistroBuilderWallRecord
            {
                wallId =
                    new BistroBuilderEditId(
                        "b6_preview_room_wall_" + i),
                buildPlaneId = "default",
                axisStart = points[i],
                axisEnd = points[(i + 1) % 4],
                baseElevation = 0f,
                height = 2.5f,
                thickness = 0.12f,
                wallDefinitionId = "wall.default"
            };

            if (!architecture.TryExecute(
                    new BistroBuilderCreateWallCommand(wall),
                    out _,
                    out error))
            {
                return null;
            }
        }

        BistroBuilderRoomProjection best = null;
        float bestDistance = float.PositiveInfinity;
        Vector2 expectedCenter =
            origin + Vector2.one * side * 0.5f;

        foreach (BistroBuilderRoomProjection room in
                 architecture.Session.RoomProjections)
        {
            if (room == null || room.boundary.Count < 3)
                continue;

            Vector2 center = Vector2.zero;
            for (int i = 0; i < room.boundary.Count; i++)
                center += room.boundary[i];
            center /= room.boundary.Count;

            float distance =
                Vector2.Distance(center, expectedCenter);
            if (distance < bestDistance)
            {
                best = room;
                bestDistance = distance;
            }
        }

        if (best == null || bestDistance > side)
        {
            error = "La topología no detectó la habitación de prueba.";
            return null;
        }

        return best;
    }

    private static BistroBuilderWallRecord FindWall(
        BistroBuilderEditDocument document,
        string id)
    {
        if (document == null)
            return null;

        for (int i = 0; i < document.walls.Count; i++)
        {
            BistroBuilderWallRecord wall = document.walls[i];
            if (wall != null &&
                string.Equals(
                    wall.wallId.Value,
                    id,
                    StringComparison.Ordinal))
            {
                return wall;
            }
        }

        return null;
    }

    private static bool LegacyConstructionPreviewIsSuppressed(
        BistroBuilderConstructionAuthoringRuntimeTool tool)
    {
        LineRenderer[] previewLines =
            ReadPrivateField<LineRenderer[]>(
                tool,
                "previewLines",
                Array.Empty<LineRenderer>());
        LineRenderer snapMarker =
            ReadPrivateField<LineRenderer>(
                tool,
                "snapMarker",
                null);
        LineRenderer snapPulse =
            ReadPrivateField<LineRenderer>(
                tool,
                "snapPulseLine",
                null);

        bool linesSuppressed = true;
        for (int i = 0; i < previewLines.Length; i++)
        {
            if (previewLines[i] != null &&
                previewLines[i].enabled)
            {
                linesSuppressed = false;
                break;
            }
        }

        return linesSuppressed &&
               snapMarker == null &&
               snapPulse == null;
    }

    private static void CheckHidden(
        BistroBuilderUniversalPreviewService preview,
        string label)
    {
        BistroBuilderUniversalPreviewState state = preview.Current;
        Check(
            !state.IsVisible &&
            state.OwnerId.Length == 0 &&
            state.Domain == BistroBuilderPreviewDomain.None &&
            state.Phase == BistroBuilderPreviewPhase.Hidden &&
            state.CandidateSegments.Count == 0 &&
            state.GhostSegments.Count == 0 &&
            state.ConflictSegments.Count == 0 &&
            state.Volumes.Count == 0 &&
            !state.HasSnapPoint &&
            state.ConflictObject == null,
            label);
    }

    private static void AddExample(
        string name,
        BistroBuilderUniversalPreviewState state)
    {
        Lines.Add(
            "EXAMPLE - " + name +
            " owner=" + state.OwnerId +
            " domain=" + state.Domain +
            " validity=" + state.Validity +
            " phase=" + state.Phase +
            " candidateEdges=" + (state.CandidateSegments.Count / 2) +
            " ghostEdges=" + (state.GhostSegments.Count / 2) +
            " conflictEdges=" + (state.ConflictSegments.Count / 2) +
            " volumes=" + state.Volumes.Count +
            " snap=" + state.HasSnapPoint);
    }

    private static int ReadPrivateListCount(
        object target,
        string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            return -1;
        var collection = field.GetValue(target) as ICollection;
        return collection != null ? collection.Count : -1;
    }

    private static T ReadPrivateField<T>(
        object target,
        string fieldName,
        T fallback)
    {
        if (target == null)
            return fallback;
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            return fallback;
        object value = field.GetValue(target);
        return value is T typed ? typed : fallback;
    }

    private static object InvokePrivate(
        object target,
        string methodName,
        object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(
                target.GetType().FullName,
                methodName);
        return method.Invoke(target, arguments);
    }

    private static void InvokeStaticPrivate(
        Type type,
        string methodName)
    {
        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(
                type.FullName,
                methodName);
        method.Invoke(null, null);
    }

    private static void InvokeLifecycle(
        object target,
        string methodName)
    {
        if (target == null)
            return;
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public);
        method?.Invoke(target, null);
    }

    private static void InvokeBootstrap(Type type)
    {
        MethodInfo method = type.GetMethod(
            "Install",
            BindingFlags.Static |
            BindingFlags.NonPublic |
            BindingFlags.Public);
        if (method == null)
            throw new MissingMethodException(
                type.FullName,
                "Install");
        method.Invoke(null, null);
    }

    private static int Count<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length;
    }

    private static T Find<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(
            FindObjectsInactive.Include);
    }

    private static T nullSafe<T>() where T : Object
    {
        return null;
    }

    private static string Suffix(string error)
    {
        return string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : ": " + error;
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

    private static bool Finish()
    {
        Lines.Add(
            "Resultado: " + pass +
            " OK / " + fail +
            " fallos.");

        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "EditorV2_B6_UniversalPreview_Report.txt");
        File.WriteAllLines(
            path,
            Lines,
            Encoding.UTF8);

        UnityEngine.Debug.Log(
            string.Join(
                Environment.NewLine,
                Lines));

        return fail == 0;
    }
}
