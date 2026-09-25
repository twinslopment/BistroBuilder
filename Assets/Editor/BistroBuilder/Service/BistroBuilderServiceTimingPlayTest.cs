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
    private static int takeOrderBaselineSatisfaction;
    private static int takeOrderAfterExplainSatisfaction;
    private static int foodBaselineSatisfaction;
    private static int foodAfterExplainSatisfaction;
    private static int baselineOverallSatisfaction;
    private static int afterExplainSatisfaction;
    private static int afterApologySatisfaction;
    private static int explicitIncidentBaselineSatisfaction;

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

                case 1: SetTakeOrderWait(15f); break;
                case 2: VerifyGenericNormal(BistroBuilderServiceTimingPhase.TakeOrder); SetTakeOrderWait(25f); break;
                case 3: VerifyGenericAttention(BistroBuilderServiceTimingPhase.TakeOrder); SetTakeOrderWait(40f); break;
                case 4: VerifyGenericDelayAndCapture(BistroBuilderServiceTimingPhase.TakeOrder, "ServiceTiming_TakeOrder_Delay.png"); SetTakeOrderWait(55f); break;
                case 5: VerifyGenericIncidentAndCapture(BistroBuilderServiceTimingPhase.TakeOrder, "ServiceTiming_TakeOrder_Incident.png"); break;
                case 6: ClickExplainDelay(); break;
                case 7: VerifyGenericExplained(BistroBuilderServiceTimingPhase.TakeOrder); break;
                case 8: ClickApology(); break;
                case 9: VerifyGenericApology(BistroBuilderServiceTimingPhase.TakeOrder); BeginFoodPhase(); SetFoodWait(20f, 21f); break;

                case 10: VerifyGenericNormal(BistroBuilderServiceTimingPhase.FoodDelivery); SetFoodWait(20f, 25f); break;
                case 11: VerifyGenericAttention(BistroBuilderServiceTimingPhase.FoodDelivery); SetFoodWait(20f, 33f); break;
                case 12: VerifyGenericDelayAndCapture(BistroBuilderServiceTimingPhase.FoodDelivery, "ServiceTiming_Food_Delay.png"); SetFoodWait(20f, 45f); break;
                case 13: VerifyGenericIncidentAndCapture(BistroBuilderServiceTimingPhase.FoodDelivery, "ServiceTiming_Food_Incident.png"); break;
                case 14: ClickExplainDelay(); break;
                case 15: VerifyGenericExplained(BistroBuilderServiceTimingPhase.FoodDelivery); break;
                case 16: ClickApology(); break;
                case 17: VerifyGenericApology(BistroBuilderServiceTimingPhase.FoodDelivery); BeginBillPhase(); SetBillWait(30f); break;

                case 18: VerifyNormal(); SetBillWait(120f); break;
                case 19: VerifyAttention(); SetBillWait(210f); break;
                case 20: VerifyDelayAndCapture(); SetBillWait(300f); break;
                case 21: VerifyIncidentAndCapture(); break;
                case 22: ClickAccelerate(); break;
                case 23: VerifyPrioritizedAndCapture(); break;
                case 24: ClickExplainDelay(); break;
                case 25: VerifyExplainedAndCapture(); break;
                case 26: ClickApology(); break;
                case 27: VerifyApologyAndCapture(); InjectExplicitIncident(); break;
                case 28: VerifyExplicitIncidentAndCapture(); break;
                case 29: ClickApology(); break;
                case 30: VerifyExplicitIncidentRecoveredAndCapture(); break;
                case 31:
                    Finish(
                        true,
                        "take-order + food dynamic + bill / normal-attention-delay-incident / explain-apology / urgent / persistence-ready / no-repeat / UI-no-overlap"
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
        Time.timeScale = 0f;
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
        group.SetState(CustomerGroupState.WaitingForWaiter);
        table.SetState(TableState.WaitingForWaiter);

        shell.EnsureShell();
        Check(selection.TrySelectForTest(table, true), "No se pudo seleccionar la mesa.");
    }

    private static void SetTakeOrderWait(float seconds)
    {
        GetInternalVisit().waiterWaitSeconds = seconds;
        RefreshShellForTest();
    }

    private static void BeginFoodPhase()
    {
        group.SetState(CustomerGroupState.WaitingForFood);
        table.SetState(TableState.WaitingForFood);
        RefreshShellForTest();
    }

    private static void SetFoodWait(
        float expectedSeconds,
        float elapsedSeconds)
    {
        BistroBuilderReputationVisitRuntimeRecord visit =
            GetInternalVisit();
        visit.expectedFoodSeconds = expectedSeconds;
        visit.foodWaitSeconds = elapsedSeconds;
        RefreshShellForTest();
    }

    private static void BeginBillPhase()
    {
        group.SetState(CustomerGroupState.WaitingForBill);
        table.SetState(TableState.WaitingForBill);
        RefreshShellForTest();
    }

    private static void VerifyGenericNormal(
        BistroBuilderServiceTimingPhase phase)
    {
        Check(
            actions.TryGetActiveWaitSnapshot(table, out var snapshot) &&
            snapshot.Phase == phase &&
            snapshot.TimingState ==
                BistroBuilderServiceTimingState.Normal,
            phase + " debe estar en Normal."
        );
        Check(
            !ActionButton().gameObject.activeSelf,
            "Agilizar cuenta no debe aparecer fuera de BillDelivery."
        );
        Check(
            !ExplainButton().gameObject.activeSelf,
            "Explicar demora no debe aparecer en Normal."
        );
        Check(
            !ApologyButton().gameObject.activeSelf,
            "Disculpa no debe aparecer en Normal."
        );
    }

    private static void VerifyGenericAttention(
        BistroBuilderServiceTimingPhase phase)
    {
        Check(
            actions.TryGetActiveWaitSnapshot(table, out var snapshot) &&
            snapshot.Phase == phase &&
            snapshot.TimingState ==
                BistroBuilderServiceTimingState.Attention,
            phase + " debe estar en Atención."
        );
        Check(
            !ActionButton().gameObject.activeSelf,
            "Agilizar cuenta no debe aparecer fuera de BillDelivery."
        );
        Check(
            !ExplainButton().gameObject.activeSelf,
            "Explicar demora no debe aparecer en Atención."
        );
        Check(
            !ApologyButton().gameObject.activeSelf,
            "Disculpa no debe aparecer en Atención."
        );
        Check(
            ContextText().Contains("Atención") ||
            ContextText().Contains("atención"),
            "El panel no muestra Atención."
        );
    }

    private static void VerifyGenericDelayAndCapture(
        BistroBuilderServiceTimingPhase phase,
        string captureName)
    {
        Check(
            actions.TryGetActiveWaitSnapshot(table, out var snapshot) &&
            snapshot.Phase == phase &&
            snapshot.TimingState == BistroBuilderServiceTimingState.Delay,
            phase + " debe estar en Demora."
        );

        Button explain = ExplainButton();
        Check(
            !ActionButton().gameObject.activeSelf,
            "Agilizar cuenta no debe aparecer fuera de BillDelivery."
        );
        Check(
            explain.gameObject.activeSelf && explain.interactable,
            "Explicar demora debe aparecer desde Demora."
        );
        Check(
            Label(explain) == "EXPLICAR DEMORA",
            "Etiqueta incorrecta de Explicar demora."
        );
        Check(
            !ApologyButton().gameObject.activeSelf,
            "Disculpa no debe aparecer todavía en Demora."
        );
        CheckNoBottomOverlap(explain.transform as RectTransform);

        int satisfaction = CurrentOverallSatisfaction();
        if (phase == BistroBuilderServiceTimingPhase.TakeOrder)
            takeOrderBaselineSatisfaction = satisfaction;
        else
            foodBaselineSatisfaction = satisfaction;

        Capture(captureName);
    }

    private static void VerifyGenericIncidentAndCapture(
        BistroBuilderServiceTimingPhase phase,
        string captureName)
    {
        Check(
            actions.TryGetActiveWaitSnapshot(table, out var snapshot) &&
            snapshot.Phase == phase &&
            snapshot.TimingState ==
                BistroBuilderServiceTimingState.Incident &&
            snapshot.HasTimingIncident,
            phase + " debe estar en Incidencia."
        );
        Check(
            actions.TryGetApologySnapshot(table, out var apology) &&
            apology.HasTimingIncident &&
            apology.TimingPhase == phase &&
            apology.CanApologize,
            phase + " debe habilitar Disculpa."
        );

        Button explain = ExplainButton();
        Button apologize = ApologyButton();
        Check(
            !ActionButton().gameObject.activeSelf,
            "Agilizar cuenta no debe aparecer fuera de BillDelivery."
        );
        Check(
            explain.gameObject.activeSelf && explain.interactable,
            "Explicar demora debe seguir disponible en Incidencia."
        );
        Check(
            apologize.gameObject.activeSelf && apologize.interactable,
            "Disculpa debe aparecer en Incidencia."
        );
        CheckNoBottomOverlap(explain.transform as RectTransform);
        CheckNoBottomOverlap(apologize.transform as RectTransform);
        CheckNoPairwiseOverlap(explain, apologize);

        int satisfaction = CurrentOverallSatisfaction();
        if (phase == BistroBuilderServiceTimingPhase.TakeOrder)
            takeOrderBaselineSatisfaction = satisfaction;
        else
            foodBaselineSatisfaction = satisfaction;

        Capture(captureName);
    }

    private static void VerifyGenericExplained(
        BistroBuilderServiceTimingPhase phase)
    {
        Check(
            actions.TryGetActiveWaitSnapshot(table, out var snapshot) &&
            snapshot.Phase == phase &&
            snapshot.IsDelayExplained &&
            !snapshot.CanExplainDelay,
            phase + " no conservó Explicar demora."
        );
        Check(
            !ExplainButton().gameObject.activeSelf,
            "Explicar demora debe desaparecer tras aplicarse."
        );
        Check(
            ApologyButton().gameObject.activeSelf,
            "Disculpa debe seguir disponible tras explicar una Incidencia."
        );

        BistroBuilderReputationVisitRuntimeRecord visit =
            GetInternalVisit();
        int mitigation =
            phase == BistroBuilderServiceTimingPhase.TakeOrder
                ? visit.waiterDelayExplanationMitigationBasisPoints
                : visit.foodDelayExplanationMitigationBasisPoints;
        Check(
            mitigation == 1500,
            phase + " no conservó la mitigación del 15%."
        );
        Check(
            !actions.TryExplainDelay(table, out _),
            phase + " permitió repetir Explicar demora."
        );

        int after = CurrentOverallSatisfaction();
        if (phase == BistroBuilderServiceTimingPhase.TakeOrder)
        {
            takeOrderAfterExplainSatisfaction = after;
            Check(
                after > takeOrderBaselineSatisfaction,
                "Explicar demora TakeOrder no mejoró satisfacción."
            );
        }
        else
        {
            foodAfterExplainSatisfaction = after;
            Check(
                after > foodBaselineSatisfaction,
                "Explicar demora FoodDelivery no mejoró satisfacción."
            );
        }
    }

    private static void VerifyGenericApology(
        BistroBuilderServiceTimingPhase phase)
    {
        Check(
            actions.TryGetActiveWaitSnapshot(table, out var snapshot) &&
            snapshot.Phase == phase &&
            snapshot.IsTimingIncidentApologized,
            phase + " no conservó Disculpa."
        );
        Check(
            actions.TryGetApologySnapshot(table, out var apology) &&
            apology.TimingIncidentAlreadyApologized &&
            !apology.CanApologize,
            phase + " dejó Disculpa repetible."
        );
        Check(
            !ApologyButton().gameObject.activeSelf,
            "Disculpa debe desaparecer tras aplicarse."
        );

        BistroBuilderReputationVisitRuntimeRecord visit =
            GetInternalVisit();
        int mitigation =
            phase == BistroBuilderServiceTimingPhase.TakeOrder
                ? visit.waiterIncidentApologyMitigationBasisPoints
                : visit.foodIncidentApologyMitigationBasisPoints;
        Check(
            mitigation == 2500,
            phase + " no conservó la recuperación del 25%."
        );
        Check(
            !actions.TryApologize(table, out _),
            phase + " permitió repetir Disculpa."
        );

        int after = CurrentOverallSatisfaction();
        int afterExplain =
            phase == BistroBuilderServiceTimingPhase.TakeOrder
                ? takeOrderAfterExplainSatisfaction
                : foodAfterExplainSatisfaction;
        Check(
            after > afterExplain,
            "Disculpa " + phase +
            " no mejoró satisfacción tras Explicar demora."
        );
    }

    private static void VerifyNormal()
    {
        Check(actions.TryGetBillSnapshot(table, out var snapshot), "Falta snapshot normal.");
        Check(snapshot.TimingState == BistroBuilderServiceTimingState.Normal, "30s debe ser Normal.");
        Button button = ActionButton();
        Check(!button.gameObject.activeSelf, "Agilizar no debe aparecer en Normal.");
        Check(!ExplainButton().gameObject.activeSelf, "Explicar demora no debe aparecer en Normal.");
        Check(!ApologyButton().gameObject.activeSelf, "Disculpa no debe aparecer en Normal.");
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
        Check(!ApologyButton().gameObject.activeSelf, "Disculpa no debe aparecer en Atención.");
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
        Check(!ApologyButton().gameObject.activeSelf,
            "Disculpa no debe aparecer todavía en Demora.");
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

    private static void VerifyIncidentAndCapture()
    {
        Check(actions.TryGetBillSnapshot(table, out var bill),
            "Falta snapshot Incidencia.");
        Check(bill.TimingState == BistroBuilderServiceTimingState.Incident,
            "300s debe ser Incidencia.");
        Check(actions.TryGetApologySnapshot(table, out var apology),
            "Falta snapshot de Disculpa.");
        Check(apology.CanApologize && apology.HasBillTimingIncident,
            "Incidencia de cuenta debe habilitar Disculpa.");

        Button accelerate = ActionButton();
        Button explain = ExplainButton();
        Button apologize = ApologyButton();

        Check(accelerate.gameObject.activeSelf && accelerate.interactable,
            "Agilizar debe estar disponible en Incidencia.");
        Check(explain.gameObject.activeSelf && explain.interactable,
            "Explicar demora debe estar disponible en Incidencia.");
        Check(apologize.gameObject.activeSelf && apologize.interactable,
            "Disculpa debe estar disponible en Incidencia.");
        Check(Label(apologize) == "DISCULPA",
            "Etiqueta incorrecta de Disculpa.");

        CheckNoBottomOverlap(accelerate.transform as RectTransform);
        CheckNoBottomOverlap(explain.transform as RectTransform);
        CheckNoBottomOverlap(apologize.transform as RectTransform);
        CheckNoPairwiseOverlap(accelerate, explain, apologize);

        Check(ContextText().Contains("Incidencia") &&
              ContextText().Contains("Disculpa"),
            "El panel no informa de la incidencia y la recuperación.");

        baselineOverallSatisfaction = CurrentOverallSatisfaction();
        Capture("ServiceTiming_Incident.png");
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
        Button apologize = ApologyButton();
        Check(explain.gameObject.activeSelf && explain.interactable,
            "Explicar demora debe seguir disponible tras priorizar.");
        Check(apologize.gameObject.activeSelf && apologize.interactable,
            "Disculpa debe seguir disponible tras priorizar.");
        CheckNoBottomOverlap(explain.transform as RectTransform);
        CheckNoBottomOverlap(apologize.transform as RectTransform);
        CheckNoPairwiseOverlap(explain, apologize);
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
        Check(ApologyButton().gameObject.activeSelf,
            "Disculpa debe seguir disponible tras Explicar demora.");
        Check(ContextText().Contains("Demora explicada") ||
              ContextText().Contains("demora ha sido explicada"),
            "Falta feedback de demora explicada.");

        Check(experience.TryGetRuntimeVisit(group.GroupId, out var visit) &&
              visit.billDelayExplanationMitigationBasisPoints == 1500,
            "La visita no conserva la mitigación provisional del 15%.");

        afterExplainSatisfaction = CurrentOverallSatisfaction();
        Check(afterExplainSatisfaction > baselineOverallSatisfaction,
            "Explicar demora debe mitigar realmente el impacto en satisfacción.");
        Check(!actions.TryExplainBillDelay(table, out _),
            "La repetición de Explicar demora debe rechazarse.");
        Capture("ServiceTiming_Explained.png");
    }

    private static void ClickApology()
    {
        ApologyButton().onClick.Invoke();
    }

    private static void VerifyApologyAndCapture()
    {
        Check(actions.TryGetApologySnapshot(table, out var apology),
            "Falta snapshot tras Disculpa.");
        Check(apology.BillIncidentAlreadyApologized,
            "La incidencia temporal no quedó marcada como disculpada.");
        Check(!apology.CanApologize,
            "La misma incidencia temporal no debe aceptar otra Disculpa.");
        Check(!ApologyButton().gameObject.activeSelf,
            "Disculpa debe desaparecer tras aplicarse.");

        Check(experience.TryGetRuntimeVisit(group.GroupId, out var visit) &&
              visit.billIncidentApologyMitigationBasisPoints == 2500,
            "La visita no conserva la recuperación provisional del 25%.");

        afterApologySatisfaction = CurrentOverallSatisfaction();
        Check(afterApologySatisfaction > afterExplainSatisfaction,
            "Disculpa debe recuperar parte adicional del impacto en satisfacción.");
        Check(ContextText().Contains("disculpa realizada"),
            "Falta feedback de Disculpa realizada.");
        Check(!actions.TryApologize(table, out _),
            "La repetición de Disculpa para la misma incidencia debe rechazarse.");

        Capture("ServiceTiming_Apology.png");
    }

    private static void InjectExplicitIncident()
    {
        BistroBuilderReputationVisitRuntimeRecord visit =
            GetInternalVisit();
        visit.recoverableServiceIncidentCount = 1;
        visit.apologizedServiceIncidentCount = 0;
        visit.serviceIncidentPenaltyBasisPoints = 1000;
        visit.serviceIncidentApologyRecoveryBasisPoints = 0;
        RefreshShellForTest();
    }

    private static void VerifyExplicitIncidentAndCapture()
    {
        Check(actions.TryGetApologySnapshot(table, out var apology),
            "Falta snapshot para la incidencia explícita.");
        Check(apology.BillIncidentAlreadyApologized,
            "La disculpa temporal anterior debe conservarse.");
        Check(apology.HasExplicitServiceIncident &&
              apology.PendingExplicitIncidentCount == 1 &&
              apology.CanApologize,
            "Una incidencia explícita nueva debe reabrir Disculpa.");

        Button apologize = ApologyButton();
        Check(apologize.gameObject.activeSelf && apologize.interactable,
            "Disculpa debe reaparecer por una incidencia explícita.");
        Check(ContextText().Contains("fallo de servicio") ||
              ContextText().Contains("Incidencia de servicio"),
            "El panel no informa del fallo explícito.");

        explicitIncidentBaselineSatisfaction =
            CurrentOverallSatisfaction();
        Check(explicitIncidentBaselineSatisfaction <
              afterApologySatisfaction,
            "La incidencia explícita debe tener impacto real antes de la recuperación.");

        Capture("ServiceTiming_ExplicitIncident.png");
    }

    private static void VerifyExplicitIncidentRecoveredAndCapture()
    {
        Check(experience.TryGetRuntimeVisit(group.GroupId, out var visit) &&
              visit.recoverableServiceIncidentCount == 1 &&
              visit.apologizedServiceIncidentCount == 1 &&
              visit.serviceIncidentPenaltyBasisPoints == 1000 &&
              visit.serviceIncidentApologyRecoveryBasisPoints == 500,
            "La recuperación de la incidencia explícita no quedó persistida.");

        Check(actions.TryGetApologySnapshot(table, out var apology) &&
              !apology.CanApologize &&
              apology.PendingExplicitIncidentCount == 0,
            "No debe quedar otra Disculpa pendiente tras cubrir la incidencia.");
        Check(!ApologyButton().gameObject.activeSelf,
            "Disculpa debe desaparecer tras cubrir la incidencia explícita.");

        int recovered = CurrentOverallSatisfaction();
        Check(recovered > explicitIncidentBaselineSatisfaction,
            "Disculpa debe recuperar parte del daño de la incidencia explícita.");
        Check(ContextText().Contains("disculpa realizada"),
            "Falta feedback de recuperación tras la incidencia explícita.");
        Check(!actions.TryApologize(table, out _),
            "No debe poder repetirse Disculpa sin una incidencia nueva.");

        Capture("ServiceTiming_ExplicitIncidentRecovered.png");
    }

    private static void SetBillWait(float seconds)
    {
        Check(experience.TryGetRuntimeVisit(group.GroupId, out _),
            "Experience Tracking todavía no registró la visita.");
        GetInternalVisit().billWaitSeconds = seconds;
        RefreshShellForTest();
    }

    private static void RefreshShellForTest()
    {
        Check(shell != null, "El HUD no está disponible para refrescar la prueba.");
        MethodInfo refresh =
            typeof(BistroBuilderUiShell).GetMethod(
                "RefreshReadModels",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        Check(refresh != null, "No se pudo resolver RefreshReadModels.");
        refresh.Invoke(shell, null);
        Canvas.ForceUpdateCanvases();
    }

    private static BistroBuilderReputationVisitRuntimeRecord GetInternalVisit()
    {
        FieldInfo visitsField =
            typeof(BistroBuilderCustomerExperienceTrackingService)
                .GetField(
                    "visitsByGroup",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
        Check(visitsField != null, "No se encontró visitsByGroup.");
        var visits = visitsField.GetValue(experience) as IDictionary;
        Check(visits != null && visits.Contains(group.GroupId),
            "No existe visita interna para el grupo.");
        var visit =
            visits[group.GroupId] as BistroBuilderReputationVisitRuntimeRecord;
        Check(visit != null, "La visita interna es nula.");
        return visit;
    }

    private static Button ActionButton()
    {
        return FindButton(BistroBuilderUiShell.ServiceActionName);
    }

    private static Button ExplainButton()
    {
        return FindButton(BistroBuilderUiShell.SecondaryContextActionName);
    }

    private static Button ApologyButton()
    {
        return FindButton(BistroBuilderUiShell.TertiaryContextActionName);
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

    private static void CheckNoPairwiseOverlap(params Button[] buttons)
    {
        for (int first = 0; first < buttons.Length; first++)
        {
            RectTransform a = buttons[first] != null
                ? buttons[first].transform as RectTransform
                : null;
            Check(a != null, "Una acción contextual no tiene RectTransform.");

            for (int second = first + 1; second < buttons.Length; second++)
            {
                RectTransform b = buttons[second] != null
                    ? buttons[second].transform as RectTransform
                    : null;
                Check(b != null,
                    "Una acción contextual no tiene RectTransform.");
                Check(!WorldRect(a).Overlaps(WorldRect(b)),
                    "Las acciones contextuales no deben solaparse entre sí.");
            }
        }
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
