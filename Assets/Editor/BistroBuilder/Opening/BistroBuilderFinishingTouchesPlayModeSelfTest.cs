using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderFinishingTouchesPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.FinishingTouches.Stage";
    private const string SuccessKey = "BB.FinishingTouches.Success";
    private const int DiagnosticSlot = 98;

    private static BistroBuilderNewGameOpeningService opening;
    private static BistroBuilderSaveGameService save;
    private static double started;

    static BistroBuilderFinishingTouchesPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    public static void RunFromCommandLine() => Begin(true);

    [MenuItem("Tools/Bistro Builder/Opening/Finishing Touches - PlayMode", false, 16005)]
    private static void RunFromMenu() => Begin(false);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Ya existe una prueba PlayMode en curso.");

        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;

        if (change == PlayModeStateChange.EnteredPlayMode)
            SessionState.SetString(StageKey, stage.EndsWith("cli", StringComparison.Ordinal)
                ? "setup_cli" : "setup_menu");
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            if (cli) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage) || stage.StartsWith("exit_", StringComparison.Ordinal))
            return;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);

        try
        {
            if (stage.StartsWith("setup_", StringComparison.Ordinal))
            {
                opening = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningService>();
                save = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
                if (opening == null || save == null)
                    throw new InvalidOperationException("No están disponibles los servicios de Nueva partida.");

                SetPrivate(opening, "defaultSaveSlot", DiagnosticSlot);
                if (save.SlotExists(DiagnosticSlot))
                    save.TryDeleteSlot(DiagnosticSlot, out _);

                started = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "wait_clean_cli" : "wait_clean_menu");
                return;
            }

            if (stage.StartsWith("wait_clean_", StringComparison.Ordinal))
            {
                if (save.IsBusy)
                {
                    Timeout(10d, "limpieza del slot");
                    return;
                }

                if (!opening.TryCreateNewGame(
                        "Finishing Touches Test",
                        BistroBuilderStartingPremisesProfile.FinishingTouches,
                        out string error))
                    throw new InvalidOperationException("TryCreateNewGame falló: " + error);

                ValidateRuntimeResult();

                ScreenCapture.CaptureScreenshot(
                    Path.Combine(Application.dataPath, "../Logs/FinishingTouches_1920.png"));

                started = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "wait_save_cli" : "wait_save_menu");
                return;
            }

            if (stage.StartsWith("wait_save_", StringComparison.Ordinal))
            {
                if (save.IsBusy)
                {
                    Timeout(15d, "guardado inicial");
                    return;
                }

                if (!save.SlotExists(DiagnosticSlot))
                    throw new InvalidOperationException("No se creó el checkpoint inicial.");

                if (!opening.TryValidateInitialDesign(out string validationError))
                    throw new InvalidOperationException(
                        "El preset generado no supera la validación inicial: " + validationError);

                if (!save.TryDeleteSlot(DiagnosticSlot, out string cleanupError))
                    throw new InvalidOperationException(
                        "No pudo limpiarse el slot de diagnóstico: " + cleanupError);

                started = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "cleanup_cli" : "cleanup_menu");
                return;
            }

            if (stage.StartsWith("cleanup_", StringComparison.Ordinal))
            {
                if (save.IsBusy)
                {
                    Timeout(10d, "limpieza final");
                    return;
                }

                Finish(true,
                    "Últimos retoques crea arquitectura real, zonas cocina/comedor, " +
                    "mobiliario BBPLFS operativo y elimina la geometría técnica.", cli);
            }
        }
        catch (Exception e)
        {
            Finish(false, Unwrap(e).Message, cli);
        }
    }

    private static void ValidateRuntimeResult()
    {
        if (opening.Phase != BistroBuilderNewGamePhase.InitialSetup ||
            !opening.IsInitialEditModeActive ||
            opening.PremisesProfile != BistroBuilderStartingPremisesProfile.FinishingTouches)
            throw new InvalidOperationException(
                "El perfil no quedó en Diseño inicial / Modo Edición.");

        BistroBuilderEditDocumentRuntimeService documentService =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>();
        if (documentService == null)
            throw new InvalidOperationException("Falta el documento de arquitectura.");

        BistroBuilderEditDocument document = documentService.GetCommittedSnapshot();
        if (document.walls.Count < 5 || document.openings.Count < 2)
            throw new InvalidOperationException(
                "La arquitectura del preset está incompleta.");

        if (!HasZone(document, "zone.dining") || !HasZone(document, "zone.kitchen"))
            throw new InvalidOperationException(
                "Faltan las zonas funcionales de comedor/cocina.");

        RestaurantTableRegistry tables =
            UnityEngine.Object.FindFirstObjectByType<RestaurantTableRegistry>();
        RestaurantSeatRegistry seats =
            UnityEngine.Object.FindFirstObjectByType<RestaurantSeatRegistry>();
        if (tables == null || tables.RegisteredTableCount < 1)
            throw new InvalidOperationException("BBPLFS no materializó mesas.");
        if (seats == null || seats.RegisteredSeatCount < 4)
            throw new InvalidOperationException("BBPLFS no materializó suficientes sillas.");

        RestaurantPlaceableRegistry placeables =
            UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>();
        if (placeables == null || placeables.RegisteredPlaceableCount < 5)
            throw new InvalidOperationException(
                "El preset no contiene mobiliario canónico suficiente.");

        if (ActiveNamed("Kitchen_Test") || ActiveNamed("PlacementObstacle_Test"))
            throw new InvalidOperationException(
                "Sigue activa geometría técnica de prototipo.");

        GameObject duplicateOverlay = GameObject.Find("InitialDesignActions");
        if (duplicateOverlay != null && duplicateOverlay.activeInHierarchy)
            throw new InvalidOperationException(
                "Sigue visible el overlay duplicado de Diseño inicial.");
    }

    private static bool HasZone(BistroBuilderEditDocument document, string id)
    {
        for (int i = 0; i < document.zones.Count; i++)
            if (document.zones[i] != null &&
                string.Equals(document.zones[i].zoneDefinitionId, id, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static bool ActiveNamed(string name)
    {
        GameObject[] all = UnityEngine.Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null &&
                all[i].activeInHierarchy &&
                string.Equals(all[i].name, name, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static void Timeout(double seconds, string operation)
    {
        if (EditorApplication.timeSinceStartup - started > seconds)
            throw new TimeoutException("Timeout durante " + operation + ".");
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(
            field,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) throw new MissingFieldException(target.GetType().Name, field);
        info.SetValue(target, value);
    }

    private static Exception Unwrap(Exception e) =>
        e is TargetInvocationException tie && tie.InnerException != null
            ? tie.InnerException
            : e;

    private static void Finish(bool ok, string message, bool cli)
    {
        string report = "BB_FINISHING_TOUCHES_" + (ok ? "PASS" : "FAIL") + " | " + message;
        if (ok) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, ok);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
