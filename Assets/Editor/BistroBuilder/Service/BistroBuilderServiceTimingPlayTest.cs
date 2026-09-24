using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BistroBuilderServiceTimingPlayTest
{
    private const string Key = "BB.ServiceTiming.PlayTest";
    private static int stage;
    private static double next;
    private static string failure;
    private static BistroBuilderUiShell shell;
    private static BistroBuilderTableSelectionController selection;
    private static BistroBuilderTableContextActionService actions;
    private static BistroBuilderCustomerExperienceTrackingService experience;
    private static TableAssignmentSystem assignments;
    private static WaiterTaskCoordinator coordinator;
    private static RestaurantTable table;
    private static CustomerGroup group;
    private static int baselineOverallSatisfaction;

    static BistroBuilderServiceTimingPlayTest()
    {
        EditorApplication.playModeStateChanged += State;
    }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".Pass", false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
        EditorApplication.isPlaying = true;
    }

    private static void State(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            failure = null;
            next = EditorApplication.timeSinceStartup + 3.0;
            Application.runInBackground = true;
            Application.logMessageReceived += Log;
            EditorApplication.update += Tick;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + ".Pass", false) ? 0 : 1);
        }
    }

    private static void Log(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Assert)
            return;
        if (!string.IsNullOrEmpty(stack) && stack.Contains("UnityEditor.Search"))
            return;
        failure = message;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next)
            return;
        next = EditorApplication.timeSinceStartup + 0.8;

        try
        {
            Check(failure == null, failure);
            switch (stage++)
            {
                case 0: Prepare(); break;
                case 1: SetBillWait(30f); break;
                case 2: VerifyNormal(); SetBillWait(120f); break;
                case 3: VerifyAttention(); SetBillWait(210f); break;
                case 4: VerifyDelayAndCapture(); break;
                case 5: ClickAccelerate(); break;
                case 6: VerifyPrioritizedAndCapture(); break;
                case 7: ClickExplainDelay(); break;
                case 8: VerifyExplainedAndCapture(); break;
                case 9:
                    Finish(
                        true,
                        "normal / attention / delay / two-actions / urgent / explain / mitigation / no-repeat"
                    );
                    break;
            }
        }
        catch (Exception error)
        {
            Finish(false, error.ToString());
        }
    }

    private static void Prepare()
    {
        UnityEngine.Object.FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()?.Hide();
        shell = UnityEngine.Object.FindFirstObjectByType<BistroBuilderUiShell>();
        selection = UnityEngine.Object.FindFirstObjectByType<BistroBuilderTableSelectionController>();
        actions = UnityEngine.Object.FindFirstObjectByType<BistroBuilderTableContextActionService>();
        experience = UnityEngine.Object.FindFirstObjectByType<BistroBuilderCustomerExperienceTrackingService>();
        assignments = UnityEngine.Object.FindFirstObjectByType<TableAssignmentSystem>();
        coordinator = UnityEngine.Object.FindFirstObjectByType<WaiterTaskCoordinator>();

        Check(shell != null && selection != null && actions != null && experience != null &&
              assignments != null && coordinator != null, "Faltan dependencias de la vertical de cuenta.");

        foreach (Waiter waiter in UnityEngine.Object.FindObjectsByType<Waiter>(FindObjectsSortMode.None))
            coordinator.UnregisterWaiter(waiter);

        foreach (RestaurantTable candidate in
                 UnityEngine.Object.FindObjectsByType<RestaurantTable>(FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.AssignedCustomerGroup == null)
            {
                table = candidate;
                break;
            }
        }
        Check(table != null, "No hay una mesa libre para la prueba.");

        var groupGo = new GameObject("BB_ServiceTiming_PlayTest_Group");
        group = groupGo.AddComponent<CustomerGroup>();
        Check(group.Initialize(990101, Math.Max(1, Math.Min(2, table.Capacity))), "No se pudo inicializar grupo.");
        Check(assignments.RegisterCustomerGroup(group), "No se pudo registrar grupo.");
        Check(group.AssignTable(table), "No se pudo asignar mesa al grupo.");
        group.SetState(CustomerGroupState.WaitingForBill);
        table.SetState(TableState.WaitingForBill);

        shell.EnsureShell();
        Check(selection.TrySelectForTest(table, true), "No se pudo seleccionar la mesa.");
    }

    private static void VerifyNormal()
    {
        Check(actions.TryGetBillSnapshot(table, out var snapshot), "Falta snapshot normal.");
        Check(snapshot.TimingState == BistroBuilderServiceTimingState.Normal, "30s debe ser Normal.");
        Button button = ActionButton();
        Check(!button.gameObject.activeSelf, "Agilizar no debe aparecer en Normal.");
        Check(!ExplainButton().gameObject.activeSelf, "Explicar demora no debe aparecer en Normal.");
        Check(ContextText().Contains("tiempo normal"), "El panel no explica el estado Normal.");
    }

    private static void VerifyAttention()
    {
        Check(actions.TryGetBillSnapshot(table, out var snapshot), "Falta snapshot Atención.");
        Check(snapshot.TimingState == BistroBuilderServiceTimingState.Attention, "120s debe ser Atención.");
        Button button = ActionButton();
        Check(button.gameObject.activeSelf && button.interactable, "Agilizar debe aparecer en Atención.");
        Check(Label(button) == "AGILIZAR CUENTA", "Etiqueta incorrecta en Atención.");
        Check(!ExplainButton().gameObject.activeSelf, "Explicar demora no debe aparecer todavía en Atención.");
        CheckNoBottomOverlap(button.transform as RectTransform);
    }

    private static void VerifyDelayAndCapture()
    {
        Check(actions.TryGetBillSnapshot(table, out var snapshot), "Falta snapshot Demora.");
        Check(snapshot.TimingState == BistroBuilderServiceTimingState.Delay, "210s debe ser Demora.");
        Button accelerate = ActionButton();
        Button explain = ExplainButton();
        Check(accelerate.gameObject.activeSelf, "Agilizar debe seguir visible en Demora.");
        Check(explain.gameObject.activeSelf && explain.interactable,
            "Explicar demora debe aparecer desde Demora.");
        Check(Label(explain) == "EXPLICAR DEMORA", "Etiqueta incorrecta de Explicar demora.");
        CheckNoBottomOverlap(accelerate.transform as RectTransform);
        CheckNoBottomOverlap(explain.transform as RectTransform);
        Check(!WorldRect(accelerate.transform as RectTransform)
            .Overlaps(WorldRect(explain.transform as RectTransform)),
            "Las acciones contextuales no deben solaparse entre sí.");
        Check(ContextText().Contains("Demora") || ContextText().Contains("demora"),
            "El panel no muestra Demora.");
        baselineOverallSatisfaction = CurrentOverallSatisfaction();
        Capture("ServiceTiming_Delay.png");
    }

    private static void ClickAccelerate()
    {
        ActionButton().onClick.Invoke();
    }

    private static void VerifyPrioritizedAndCapture()
    {
        Check(actions.TryGetBillSnapshot(table, out var snapshot), "Falta snapshot priorizado.");
        Check(snapshot.IsAlreadyAccelerated, "La cuenta no quedó priorizada.");
        Check(snapshot.TaskPriority == WaiterTaskPriority.Urgent, "La tarea no quedó Urgent.");
        Check(!snapshot.CanAccelerate, "No debe poder agilizarse dos veces.");
        Check(!ActionButton().gameObject.activeSelf, "Agilizar debe desaparecer tras priorizar.");
        Button explain = ExplainButton();
        Check(explain.gameObject.activeSelf && explain.interactable,
            "Explicar demora debe seguir disponible tras priorizar.");
        CheckNoBottomOverlap(explain.transform as RectTransform);
        Check(ContextText().Contains("priorizada"), "Falta feedback de cuenta priorizada.");
        Check(!actions.TryAccelerateBill(table, out _), "La repetición de Agilizar debe rechazarse.");
        Capture("ServiceTiming_Prioritized.png");
    }

    private static void ClickExplainDelay()
    {
        ExplainButton().onClick.Invoke();
    }

    private static void VerifyExplainedAndCapture()
    {
        Check(actions.TryGetBillSnapshot(table, out var snapshot), "Falta snapshot tras explicar.");
        Check(snapshot.IsDelayExplained, "La demora no quedó marcada como explicada.");
        Check(!snapshot.CanExplainDelay, "Explicar demora debe ser de una sola aplicación.");
        Check(!ExplainButton().gameObject.activeSelf,
            "Explicar demora debe desaparecer después de aplicarse.");
        Check(!ActionButton().gameObject.activeSelf,
            "Agilizar ya priorizado debe permanecer oculto.");
        Check(ContextText().Contains("Demora explicada") ||
              ContextText().Contains("demora ha sido explicada"),
            "Falta feedback de demora explicada.");

        Check(experience.TryGetRuntimeVisit(group.GroupId, out var visit) &&
              visit.billDelayExplanationMitigationBasisPoints == 1500,
            "La visita no conserva la mitigación provisional del 15%.");

        int after = CurrentOverallSatisfaction();
        Check(after > baselineOverallSatisfaction,
            "Explicar demora debe mitigar realmente el impacto en satisfacción.");
        Check(!actions.TryExplainBillDelay(table, out _),
            "La repetición de Explicar demora debe rechazarse.");
        Capture("ServiceTiming_Explained.png");
    }

    private static void SetBillWait(float seconds)
    {
        Check(experience.TryGetRuntimeVisit(group.GroupId, out _),
            "Experience Tracking todavía no registró la visita.");

        FieldInfo visitsField = typeof(BistroBuilderCustomerExperienceTrackingService)
            .GetField("visitsByGroup", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(visitsField != null, "No se encontró visitsByGroup.");
        var visits = visitsField.GetValue(experience) as IDictionary;
        Check(visits != null && visits.Contains(group.GroupId), "No existe visita interna para el grupo.");
        var visit = visits[group.GroupId] as BistroBuilderReputationVisitRuntimeRecord;
        Check(visit != null, "La visita interna es nula.");
        visit.billWaitSeconds = seconds;
    }

    private static Button ActionButton()
    {
        return FindButton(BistroBuilderUiShell.ServiceActionName);
    }

    private static Button ExplainButton()
    {
        return FindButton(BistroBuilderUiShell.SecondaryContextActionName);
    }

    private static Button FindButton(string buttonName)
    {
        foreach (Button candidate in UnityEngine.Object.FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.name == buttonName)
                return candidate;
        }
        throw new Exception("No existe el botón contextual " + buttonName + ".");
    }

    private static string Label(Button button)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        return text != null ? text.text : string.Empty;
    }

    private static string ContextText()
    {
        GameObject panel = GameObject.Find(BistroBuilderUiShell.ContextPanelName);
        TMP_Text body = panel != null ? panel.transform.Find("Body")?.GetComponent<TMP_Text>() : null;
        return body != null ? body.text : string.Empty;
    }

    private static int CurrentOverallSatisfaction()
    {
        Check(experience.TryGetRuntimeVisit(group.GroupId, out var visit) && visit != null,
            "No se pudo leer la visita para satisfacción.");
        Check(BistroBuilderCustomerExperienceEvaluator.TryEvaluate(
                visit,
                1,
                out BistroBuilderCustomerExperienceRecord score,
                out string error
            ),
            "No se pudo evaluar satisfacción: " + error);
        return score.overallSatisfactionBasisPoints;
    }

    private static void CheckNoBottomOverlap(RectTransform action)
    {
        Check(action != null, "La acción contextual no tiene RectTransform.");

        RectTransform dateTime = null;
        RectTransform timeDock = null;
        foreach (RectTransform candidate in UnityEngine.Object.FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate == null) continue;
            if (candidate.name == "BottomDateTime") dateTime = candidate;
            else if (candidate.name == "BB_368B_TimeControlsDock") timeDock = candidate;
        }

        Check(dateTime != null && timeDock != null, "Faltan fecha/hora o controles de velocidad.");
        Check(!WorldRect(action).Overlaps(WorldRect(dateTime)),
            "La acción contextual se solapa con fecha/hora.");
        Check(!WorldRect(action).Overlaps(WorldRect(timeDock)),
            "La acción contextual se solapa con controles de velocidad.");
    }

    private static Rect WorldRect(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private static void Capture(string filename)
    {
        Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        Check(camera != null, "No hay cámara para captura.");

        var target = new RenderTexture(1920, 1080, 24);
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var saved = new List<(Canvas canvas, RenderMode mode, Camera worldCamera, float distance)>();

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
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/" + filename, image.EncodeToPNG());

        RenderTexture.active = oldActive;
        camera.targetTexture = oldTarget;
        foreach (var item in saved)
        {
            item.canvas.renderMode = item.mode;
            item.canvas.worldCamera = item.worldCamera;
            item.canvas.planeDistance = item.distance;
        }
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(target);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Finish(bool pass, string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        string result = (pass ? "PASS " : "FAIL ") + message;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ServiceTimingPlayTest.txt", result);
        Debug.Log("BB_SERVICE_TIMING_PLAYTEST_" + result);
        SessionState.SetBool(Key + ".Pass", pass);
        EditorApplication.isPlaying = false;
    }
}
