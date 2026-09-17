using System;
using System.Collections.Generic;
using System.IO;
using BistroBuilder.CameraSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderTableSelectionPlayTest
{
    private const string Key = "BB.TableSelection.Test";
    private static int stage;
    private static double next;
    private static string failure;
    private static BistroBuilderUiShell shell;
    private static BistroBuilderTableSelectionController selection;
    private static RestaurantTable table;
    private static BistroBuilderProfessionalCameraController cameraController;

    static BistroBuilderTableSelectionPlayTest()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            failure = null;
            next = EditorApplication.timeSinceStartup + 3.0;
            Application.runInBackground = true;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + ".Pass", false) ? 0 : 1);
        }
    }

    private static void Log(string text, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Assert) return;
        if (!string.IsNullOrEmpty(stack) &&
            (stack.Contains("RestaurantPlacementValidationService.cs") || stack.Contains("UnityEditor.Search")))
            return;
        failure = text;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 0.8;
        try
        {
            Check(failure == null, failure);
            switch (stage++)
            {
                case 0: PrepareAndSelect(); break;
                case 1: VerifyNormalAndCapture(); break;
                case 2: VerifyAttentionAndCapture(); break;
                case 3: VerifyCriticalAndCapture(); break;
                case 4: VerifyClearAndFinish(); break;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void PrepareAndSelect()
    {
        UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
        shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();
        selection = UnityEngine.Object.FindFirstObjectByType<BistroBuilderTableSelectionController>();
        table = UnityEngine.Object.FindFirstObjectByType<RestaurantTable>();
        cameraController = UnityEngine.Object.FindFirstObjectByType<BistroBuilderProfessionalCameraController>();
        Check(shell != null && selection != null && table != null, "Missing HUD, table selection or table");
        Check(selection.ValidateConfiguration(out string error), error);
        Check(cameraController != null, "Professional camera missing");
        shell.EnsureShell();
        table.SetState(TableState.Free);
        float distance = cameraController.TargetState.Distance;
        Check(selection.TrySelectForTest(table, true), "Table could not be selected");
        Check(Mathf.Abs(cameraController.TargetState.Distance - distance) < 0.001f,
            "Table selection changed camera zoom");
    }

    private static BistroBuilderUiSceneSelectionVisual Visual()
    {
        GameObject host = GameObject.Find("BB_UIUX_TableSelectionVisual");
        return host != null ? host.GetComponent<BistroBuilderUiSceneSelectionVisual>() : null;
    }

    private static void VerifyNormalAndCapture()
    {
        Check(selection.SelectedTable == table, "Selected table was lost");
        Check(shell.CurrentContextTitle == "Mesa " + table.TableId, "Context title not bound to table");
        Check(shell.CurrentContextBody.Contains("Libre"), "Free table context missing");
        Check(Visual() != null && Visual().State == BistroBuilderUiSceneSelectionState.Selected,
            "Normal selected visual missing");
        Capture("MesaSeleccionada_Normal.png");
        table.SetState(TableState.WaitingForWaiter);
    }

    private static void VerifyAttentionAndCapture()
    {
        Check(shell.CurrentContextBody.Contains("Esperando camarero"), "Attention context not refreshed");
        Check(Visual().State == BistroBuilderUiSceneSelectionState.Attention,
            "Attention visual not refreshed");
        Capture("MesaSeleccionada_Atencion.png");
        table.SetState(TableState.Dirty);
    }

    private static void VerifyCriticalAndCapture()
    {
        Check(shell.CurrentContextBody.Contains("Pendiente de limpieza"), "Critical context not refreshed");
        Check(Visual().State == BistroBuilderUiSceneSelectionState.Critical,
            "Critical visual not refreshed");
        Capture("MesaSeleccionada_Critica.png");
    }

    private static void VerifyClearAndFinish()
    {
        selection.ClearSelection(true);
        Check(selection.SelectedTable == null, "Selection did not clear");
        Check(shell.CurrentContextTitle == "Contexto", "Generic context was not restored");
        Check(Visual() != null && Visual().State == BistroBuilderUiSceneSelectionState.Hidden,
            "Selection outline remains visible after clear");
        Finish(true, "selection / live state / no zoom / context / clear / screenshots");
    }

    private static void Capture(string filename)
    {
        Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        Check(camera != null, "Camera missing for screenshot");
        var target = new RenderTexture(1920, 1080, 24);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var saved = new List<(Canvas canvas, RenderMode mode, Camera camera, float distance)>();
        foreach (Canvas canvas in canvases)
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            saved.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 2f;
        }

        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        Directory.CreateDirectory("docs/Images");
        File.WriteAllBytes("docs/Images/" + filename, image.EncodeToPNG());

        RenderTexture.active = oldActive;
        camera.targetTexture = oldTarget;
        foreach (var item in saved)
        {
            item.canvas.renderMode = item.mode;
            item.canvas.worldCamera = item.camera;
            item.canvas.planeDistance = item.distance;
        }
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(target);
    }

    private static void Finish(bool pass, string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        string result = (pass ? "PASS " : "FAIL ") + message;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/TableSelectionTest.txt", result);
        Debug.Log("BB_TABLE_SELECTION_" + result);
        SessionState.SetBool(Key + ".Pass", pass);
        EditorApplication.isPlaying = false;
    }
}
