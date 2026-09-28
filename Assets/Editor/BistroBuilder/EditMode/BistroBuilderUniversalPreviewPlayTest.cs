using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

/// <summary>
/// Prueba de integración real del BB Universal Preview System sobre
/// Assets/Scenes/Prototype_Restaurant.unity.
/// No llama al renderer directamente: mobiliario pasa por el controlador
/// de edición real y construcción pasa por Mouse/InputSystem + Update real.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderUniversalPreviewPlayTest
{
    private const string Armed = "BB.UniversalPreview.PlayTest";
    private const string Batch = Armed + ".Batch";
    private const string Pass = Armed + ".Pass";
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    private static RestaurantEditInteractionController edit;
    private static RestaurantPlacementTransactionService transaction;
    private static RestaurantPlacementValidationService validation;
    private static RestaurantPlaceableRegistry registry;
    private static BistroBuilderUniversalPreviewService preview;
    private static BistroBuilderUniversalPreviewRenderer previewRenderer;
    private static BistroBuilderFurniturePreviewProxyRenderer furnitureProxy;
    private static BistroBuilderConstructionAuthoringRuntimeTool construction;
    private static BistroBuilderEditRuntimeCoordinator coordinator;

    private static RestaurantPlaceableObject target;
    private static RestaurantPlaceableObject blocker;
    private static RestaurantAreaMember targetMember;
    private static RestaurantAreaMember blockerMember;
    private static readonly List<RestaurantAreaMember> linkedBuffer =
        new List<RestaurantAreaMember>(16);

    private static MeshRenderer[] targetRenderers = Array.Empty<MeshRenderer>();
    private static int baselineEnabledRenderers;
    private static Vector3 originalPosition;
    private static Quaternion originalRotation;
    private static int baselineWalls;

    private static Camera testCamera;
    private static Mouse diagnosticMouse;
    private static int stage;
    private static int lastFrame;
    private static double next;
    private static double deadline;
    private static string failure;

    static BistroBuilderUniversalPreviewPlayTest()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Bistro Builder/Validation/Universal Preview/Run", false, 52050)]
    public static void RunFromMenu()
    {
        SessionState.SetBool(Batch, false);
        ArmAndRun();
    }

    public static void RunBatch()
    {
        SessionState.SetBool(Batch, true);
        ArmAndRun();
    }

    private static void ArmAndRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("BB Universal Preview: ya hay una sesión Play activa.");
            return;
        }

        SessionState.SetBool(Armed, true);
        SessionState.SetBool(Pass, false);
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Armed, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            lastFrame = -1;
            failure = null;
            deadline = EditorApplication.timeSinceStartup + 180d;
            next = EditorApplication.timeSinceStartup + 3d;
            EditorApplication.update += Tick;
            Application.logMessageReceived += HandleLog;
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.update -= Tick;
        Application.logMessageReceived -= HandleLog;

        bool passed = SessionState.GetBool(Pass, false);
        bool batch = SessionState.GetBool(Batch, false);
        SessionState.SetBool(Armed, false);

        if (batch)
        {
            EditorApplication.Exit(passed ? 0 : 1);
            return;
        }

        EditorUtility.DisplayDialog(
            "BB Universal Preview",
            passed
                ? "PASS\n\nLa preview universal ha superado la prueba de integración."
                : "FAIL\n\nRevisa Logs/UniversalPreviewPlayTest.txt",
            "Aceptar");
    }

    private static void HandleLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Assert)
            failure = message + "\n" + stackTrace;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < next ||
            Time.frameCount == lastFrame)
            return;

        next = EditorApplication.timeSinceStartup + 0.25d;
        lastFrame = Time.frameCount;

        try
        {
            if (failure != null)
                throw new Exception(failure);

            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Universal Preview test timed out.");

            switch (stage)
            {
                case 0:
                    ResolveRuntime();
                    if (!Ready())
                        return;

                    UnityEngine.Object
                        .FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()
                        ?.Hide();

                    Check(edit.TryEnterEditMode(), "No se pudo entrar en modo edición.");
                    SelectFurniturePair();

                    originalPosition = targetMember.transform.position;
                    originalRotation = targetMember.transform.rotation;
                    targetRenderers = target.GetComponentsInChildren<MeshRenderer>(true);
                    baselineEnabledRenderers = CountEnabledTargetRenderers();

                    ConfigureFurnitureCamera();
                    Check(edit.TrySelectPlaceable(target), "No se pudo seleccionar el artículo de prueba.");
                    Check(!edit.HasActivePlacement, "Seleccionar abrió una colocación: debe ser solo selección.");
                    break;

                case 1:
                    Check(CountEnabledTargetRenderers() == baselineEnabledRenderers,
                        "Seleccionar alteró la visibilidad del mobiliario.");
                    Check(edit.TryBeginMoveSelected(), "No se pudo iniciar Move sobre la selección.");
                    break;

                case 2:
                    Check(edit.HasActivePlacement && transaction.HasActiveTransaction,
                        "Move no abrió la transacción real.");
                    Check(furnitureProxy != null, "No existe BistroBuilderFurniturePreviewProxyRenderer.");
                    AssertFurniturePreview(validExpected: true);
                    if (baselineEnabledRenderers > 0)
                        Check(CountEnabledTargetRenderers() < baselineEnabledRenderers,
                            "El proxy no tomó el control visual al comenzar a mover.");
                    Capture("UniversalPreview_FurnitureValid.png");

                    ForceFurnitureCandidate(
                        blockerMember.transform.position,
                        blockerMember.transform.rotation);
                    break;

                case 3:
                    Check(edit.LastValidationResult.IsValid == false,
                        "La superposición deliberada no fue inválida.");
                    Check(preview.Current.Validity == BistroBuilderPreviewValidity.Invalid,
                        "La preview universal no recibió validez inválida.");
                    Check(preview.Current.ConflictObject != null,
                        "No se localizó el objeto causante del conflicto.");
                    Check(preview.Current.ConflictSegments.Count >= 4,
                        "El conflicto no generó representación localizada.");
                    Capture("UniversalPreview_FurnitureInvalid.png");

                    Check(edit.CancelActivePlacement(), "No se pudo cancelar Move.");
                    break;

                case 4:
                    Check(!transaction.HasActiveTransaction, "La transacción sigue abierta tras cancelar.");
                    Check(Vector3.Distance(targetMember.transform.position, originalPosition) < 0.0001f,
                        "Cancelar no restauró la posición original.");
                    Check(Quaternion.Angle(targetMember.transform.rotation, originalRotation) < 0.01f,
                        "Cancelar no restauró la rotación original.");
                    Check(CountEnabledTargetRenderers() == baselineEnabledRenderers,
                        "Cancelar no restauró los renderizadores originales.");
                    Check(!preview.Current.IsVisible ||
                          preview.Current.OwnerId != BistroBuilderUniversalPreviewService.FurnitureOwner,
                        "La preview de mobiliario quedó activa después de cancelar.");

                    PrepareConstructionTest();
                    break;

                case 5:
                    Check(coordinator.HasSession,
                        "Construction no inicializó Draft Session después de su Update real.");
                    baselineWalls = coordinator.Session.Draft.walls.Count;
                    Pointer(30f, 30f, true);
                    break;

                case 6:
                    Pointer(36f, 34f, true);
                    break;

                case 7:
                    Check(construction.HasActiveGesture,
                        "La herramienta real de construcción no mantiene gesto activo.");
                    Check(coordinator.Session.Draft.walls.Count == baselineWalls,
                        "La preview modificó el borrador antes de confirmar.");
                    Check(preview.Current.IsVisible,
                        "La construcción no publicó preview universal.");
                    Check(preview.Current.OwnerId ==
                          BistroBuilderUniversalPreviewService.ConstructionOwner,
                        "La autoridad visual no pertenece a Construction.");
                    Check(preview.Current.Domain == BistroBuilderPreviewDomain.Room,
                        "La preview no identifica la operación como Room.");
                    Check(preview.Current.CandidateSegments.Count >= 8,
                        "La habitación no publicó sus cuatro lados.");
                    Check(preview.Current.Volumes.Count >= 4,
                        "La habitación no publicó volúmenes provisionales.");
                    Capture("UniversalPreview_Construction.png");
                    Pointer(36f, 34f, false);
                    break;

                case 8:
                    Check(coordinator.Session.Draft.walls.Count == baselineWalls + 4,
                        "Soltar la preview no materializó las cuatro paredes esperadas.");
                    Check(construction.TryUndo(out string undoError),
                        "No se pudo deshacer la habitación de prueba: " + undoError);
                    Check(coordinator.Session.Draft.walls.Count == baselineWalls,
                        "Undo no devolvió la construcción al estado inicial.");
                    construction.SetMode(BistroBuilderConstructionRuntimeMode.Furniture);
                    edit.TryExitEditMode(true);
                    Finish(true,
                        "selección aislada / proxy al mover / preview válida / conflicto localizado / " +
                        "cancelación exacta / construcción unificada / volumen provisional / undo");
                    return;
            }

            stage++;
        }
        catch (Exception error)
        {
            Finish(false, "Stage " + stage + ": " + error);
        }
    }

    private static void ResolveRuntime()
    {
        edit = UnityEngine.Object.FindFirstObjectByType<RestaurantEditInteractionController>();
        transaction = UnityEngine.Object.FindFirstObjectByType<RestaurantPlacementTransactionService>();
        validation = UnityEngine.Object.FindFirstObjectByType<RestaurantPlacementValidationService>();
        registry = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>();
        preview = BistroBuilderUniversalPreviewService.GetOrCreate();
        previewRenderer = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUniversalPreviewRenderer>();
        furnitureProxy = UnityEngine.Object.FindFirstObjectByType<BistroBuilderFurniturePreviewProxyRenderer>();
        construction = UnityEngine.Object.FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        coordinator = UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
    }

    private static bool Ready()
    {
        return edit != null &&
               transaction != null &&
               validation != null &&
               registry != null &&
               registry.RegisteredPlaceableCount >= 2 &&
               preview != null &&
               previewRenderer != null &&
               furnitureProxy != null &&
               construction != null &&
               coordinator != null &&
               BistroBuilderConstructionPlayerPanel.Instance != null &&
               BistroBuilderConstructionPlayerPanel.Instance.IsReady;
    }

    private static void SelectFurniturePair()
    {
        target = null;
        blocker = null;
        targetMember = null;
        blockerMember = null;

        foreach (RestaurantPlaceableObject candidate in registry.RegisteredPlaceables)
        {
            if (!IsUsablePlaceable(candidate, out RestaurantAreaMember member))
                continue;

            target = candidate;
            targetMember = member;
            break;
        }

        Check(target != null, "No se encontró mobiliario editable con footprint.");

        linkedBuffer.Clear();
        edit.PlacementLinkedGroupService?.CopyLinkedMembers(targetMember, linkedBuffer);

        foreach (RestaurantPlaceableObject candidate in registry.RegisteredPlaceables)
        {
            if (candidate == target ||
                !IsUsablePlaceable(candidate, out RestaurantAreaMember member) ||
                linkedBuffer.Contains(member))
                continue;

            blocker = candidate;
            blockerMember = member;
            break;
        }

        Check(blocker != null,
            "No se encontró un segundo colocable independiente para provocar conflicto.");
    }

    private static bool IsUsablePlaceable(
        RestaurantPlaceableObject placeable,
        out RestaurantAreaMember member)
    {
        member = null;
        if (placeable == null ||
            !placeable.TryGetComponent(out RestaurantEditableObject editable) ||
            !editable.EditingEnabled ||
            !editable.HasValidDefinition ||
            !placeable.TryGetComponent(out member) ||
            !placeable.TryGetComponent(out RestaurantPlacementFootprint footprint) ||
            !footprint.BlocksOtherPlacements)
            return false;

        if (placeable.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
            return false;

        return validation.ValidateCurrentPlacement(member).IsValid;
    }

    private static void ConfigureFurnitureCamera()
    {
        EnsureCamera();

        testCamera.orthographic = true;
        testCamera.orthographicSize = 5f;
        testCamera.transform.position =
            originalPosition + new Vector3(0f, 12f, 0f);
        testCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        SetPrivateField(edit, "interactionCamera", testCamera);
    }

    private static void PrepareConstructionTest()
    {
        EnsureCamera();

        testCamera.orthographic = true;
        testCamera.orthographicSize = 12f;
        testCamera.transform.position = new Vector3(33f, 30f, 32f);
        testCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        SetPrivateField(construction, "interactionCamera", testCamera);

        if (diagnosticMouse == null)
            diagnosticMouse = InputSystem.AddDevice<Mouse>();

        InputSystem.settings.backgroundBehavior =
            InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

        construction.SetRoomZone("zone.dining");
        construction.SetMode(BistroBuilderConstructionRuntimeMode.Room);

        // La sesión se crea en el siguiente Update real de la herramienta.
        // El siguiente stage la comprueba antes de enviar el primer clic.
    }

    private static void AssertFurniturePreview(bool validExpected)
    {
        BistroBuilderUniversalPreviewState state = preview.Current;

        Check(state.IsVisible, "La preview de mobiliario no está visible.");
        Check(state.OwnerId == BistroBuilderUniversalPreviewService.FurnitureOwner,
            "La preview activa no pertenece a Furniture.");
        Check(state.Domain == BistroBuilderPreviewDomain.Furniture,
            "La preview activa no está tipada como Furniture.");
        Check(state.HasCandidatePose, "Furniture no publicó Candidate Pose.");
        Check(state.CandidateSegments.Count >= 8,
            "Furniture no publicó una huella completa.");
        Check(state.GhostSegments.Count >= 8,
            "Mover un existente no publicó ghost de posición original.");

        if (validExpected)
            Check(state.Validity == BistroBuilderPreviewValidity.Valid,
                "La posición original no se representa como válida.");
    }

    private static void ForceFurnitureCandidate(Vector3 position, Quaternion rotation)
    {
        SetPrivateField(edit, "candidatePosition", position);
        SetPrivateField(edit, "candidateRotation", rotation);
        SetPrivateField(edit, "hasCandidatePose", true);
        SetPrivateField(edit, "hasPublishedPreviewPose", false);

        MethodInfo publish = typeof(RestaurantEditInteractionController)
            .GetMethod("PublishPreviewIfChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

        Check(publish != null, "No se encontró PublishPreviewIfChanged.");
        publish.Invoke(edit, null);
    }

    private static void Pointer(float x, float z, bool down)
    {
        Vector2 screen =
            testCamera.WorldToScreenPoint(new Vector3(x, 0f, z));

        MouseState state = new MouseState { position = screen };
        if (down)
            state = state.WithButton(MouseButton.Left);

        InputSystem.QueueStateEvent(diagnosticMouse, state);
    }

    private static int CountEnabledTargetRenderers()
    {
        int count = 0;
        for (int i = 0; i < targetRenderers.Length; i++)
            if (targetRenderers[i] != null && targetRenderers[i].enabled)
                count++;
        return count;
    }

    private static void EnsureCamera()
    {
        if (testCamera != null)
            return;

        GameObject go = new GameObject("BB_UniversalPreviewTestCamera");
        testCamera = go.AddComponent<Camera>();
        testCamera.depth = 100f;
        testCamera.clearFlags = CameraClearFlags.SolidColor;
        testCamera.backgroundColor = new Color(0.09f, 0.12f, 0.11f);
    }

    private static void Capture(string fileName)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
            testCamera == null)
            return;

        Directory.CreateDirectory("Logs");

        RenderTexture previous = RenderTexture.active;
        RenderTexture targetTexture =
            new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);

        testCamera.targetTexture = targetTexture;
        testCamera.Render();
        RenderTexture.active = targetTexture;

        Texture2D image =
            new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
        image.Apply();

        File.WriteAllBytes(
            Path.Combine("Logs", fileName),
            image.EncodeToPNG());

        RenderTexture.active = previous;
        testCamera.targetTexture = null;
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(targetTexture);
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Check(field != null,
            "No se encontró el campo privado " + fieldName + ".");
        field.SetValue(instance, value);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= HandleLog;

        if (diagnosticMouse != null)
        {
            InputSystem.RemoveDevice(diagnosticMouse);
            diagnosticMouse = null;
        }

        if (transaction != null && transaction.HasActiveTransaction)
            edit?.CancelActivePlacement();

        string report = (passed ? "PASS " : "FAIL ") + message;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/UniversalPreviewPlayTest.txt", report);
        Debug.Log("BB_UNIVERSAL_PREVIEW_" + report);

        SessionState.SetBool(Pass, passed);
        EditorApplication.isPlaying = false;
    }
}
