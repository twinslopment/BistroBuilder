using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedCustomers10EPlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Customers.10E.Play.Stage";
    private const string SuccessKey = "BB.Customers.10E.Play.Success";
    private const string ReportPath = "Customers10EPlayModeReport.txt";

    static BistroBuilderAdvancedCustomers10EPlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Customers/10E - PlayMode real", false, 10043)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 10E ya está ejecutándose.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, commandLine ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stage)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "run_cli" : "run_menu");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool success = SessionState.GetBool(SuccessKey, false);
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            SessionState.EraseString(StageKey);
            if (cli) EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("run_", StringComparison.Ordinal) || Time.frameCount < 4)
            return;
        bool commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
        SessionState.SetString(StageKey, commandLine ? "running_cli" : "running_menu");
        RunScenario(commandLine);
    }

    private static void RunScenario(bool commandLine)
    {
        var advocacy = Find<BistroBuilderAdvancedCustomerAdvocacyService>();
        var history = Find<BistroBuilderAdvancedCustomerHistoryService>();
        var guestRelations = Find<BistroBuilderGuestRelationsService>();
        var marketing = Find<BistroBuilderMarketingDemandIntegrationService>();
        var general = Find<BistroBuilderGeneralGameStateService>();
        if (advocacy == null || history == null || guestRelations == null ||
            marketing == null || general == null)
        {
            Finish(false, "10E: faltan autoridades runtime.", commandLine);
            return;
        }

        BistroBuilderAdvancedCustomerHistorySnapshot originalHistory = history.CreateSnapshot();
        BistroBuilderGuestRelationsSnapshot originalGuests = guestRelations.CreateSnapshot();
        int originalDay = general.DayIndex;
        int originalYear = general.CalendarYear;
        int originalMonth = general.CalendarMonth;
        int originalCalendarDay = general.CalendarDay;
        bool success = false;
        string message = string.Empty;
        try
        {
            if (!general.TrySetCalendar(10, 1, 1, 10))
                throw new InvalidOperationException("No pudo preparar el día diagnóstico.");

            BistroBuilderAdvancedCustomerHistorySnapshot historyFixture =
                BuildHistoryFixture();
            BistroBuilderGuestRelationsSnapshot guestFixture = BuildGuestFixture();
            if (!history.TryRestoreSnapshot(historyFixture, out string historyError))
                throw new InvalidOperationException(historyError);
            if (!guestRelations.TryRestoreSnapshot(guestFixture, out string guestError))
                throw new InvalidOperationException(guestError);
            string advocacyError = string.Empty;
            string marketingError = string.Empty;
            if (!advocacy.ValidateConfiguration(out advocacyError) ||
                !marketing.ValidateConfiguration(out marketingError))
                throw new InvalidOperationException(advocacyError + " " + marketingError);

            int strongPriority = advocacy.GetReturnPriority("cohort.strong");
            int weakPriority = advocacy.GetReturnPriority("cohort.weak");
            if (strongPriority <= weakPriority)
                throw new InvalidOperationException("La cohorte fuerte no obtiene mayor prioridad.");
            FieldInfo eligibleField = typeof(BistroBuilderMarketingDemandIntegrationService)
                .GetField("eligibleCohorts", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo selectMethod = typeof(BistroBuilderMarketingDemandIntegrationService)
                .GetMethod("FindNextEligibleCohort", BindingFlags.Instance | BindingFlags.NonPublic);
            if (eligibleField == null || selectMethod == null)
                throw new InvalidOperationException("10E: no pudo inspeccionar la selección de retornos.");

            var eligible = eligibleField.GetValue(marketing) as
                List<BistroBuilderGuestVisitCohortRecord>;
            if (eligible == null)
                throw new InvalidOperationException("10E: buffer de cohortes no disponible.");
            guestRelations.CopyEligibleCohorts(general.DayIndex, eligible);
            if (eligible.Count < 2 || eligible[0].cohortId != "cohort.weak")
                throw new InvalidOperationException(
                    "10E: el fixture no demuestra inversión frente al orden por recencia.");

            var selected = selectMethod.Invoke(marketing,
                new object[] { BistroBuilderMarketingCustomerSegment.Any }) as
                BistroBuilderGuestVisitCohortRecord;
            if (selected == null || selected.cohortId != "cohort.strong")
                throw new InvalidOperationException(
                    "Marketing no priorizó la cohorte con mejor fidelidad/satisfacción.");

            success = true;
            message = "PASS — Marketing mantiene la cantidad de retornos, pero 10E " +
                "prioriza en runtime la cohorte con mejor intención de volver y recomendación.";
        }
        catch (Exception exception)
        {
            success = false;
            message = exception.Message;
        }
        finally
        {
            string restoreErrors = string.Empty;
            if (!history.TryRestoreSnapshot(originalHistory, out string historyRestore))
                restoreErrors += " Historial: " + historyRestore;
            if (!guestRelations.TryRestoreSnapshot(originalGuests, out string guestRestore))
                restoreErrors += " GuestRelations: " + guestRestore;
            if (!general.TrySetCalendar(
                    originalDay, originalYear, originalMonth, originalCalendarDay))
                restoreErrors += " Calendario no restaurado.";
            marketing.ValidateConfiguration(out _);
            if (!string.IsNullOrWhiteSpace(restoreErrors))
            {
                success = false;
                message += restoreErrors;
            }
        }

        Finish(success, message, commandLine);
    }

    private static BistroBuilderAdvancedCustomerHistorySnapshot BuildHistoryFixture()
    {
        var snapshot = BistroBuilderAdvancedCustomerHistoryEngine.CreateEmptySnapshot();
        snapshot.customers.Add(BuildHistory("cohort.weak", 9, 2, 8000, 5200,
            BistroBuilderCustomerLoyaltyTier.Returning));
        snapshot.customers.Add(BuildHistory("cohort.strong", 4, 6, 42000, 8500,
            BistroBuilderCustomerLoyaltyTier.Vip));
        snapshot.revision = 2;
        return snapshot;
    }

    private static BistroBuilderAdvancedCustomerHistoryRecord BuildHistory(
        string cohortId, int lastDay, int visits, long spend, int satisfaction,
        BistroBuilderCustomerLoyaltyTier tier)
    {
        var record = new BistroBuilderAdvancedCustomerHistoryRecord
        {
            cohortId = cohortId,
            segmentId = "general",
            firstVisitDay = 1,
            lastVisitDay = lastDay,
            visitCount = visits,
            lifetimeSpendCents = spend,
            satisfactionTotalBasisPoints = (long)satisfaction * visits,
            loyaltyTier = tier
        };
        record.recentVisits.Add(new BistroBuilderAdvancedCustomerVisitHistoryRecord
        {
            experienceId = "exp." + cohortId,
            dayIndex = lastDay,
            paidAmountCents = spend / Math.Max(1, visits),
            satisfactionBasisPoints = satisfaction,
            waitingBasisPoints = Math.Max(0, satisfaction - 500),
            foodQualityBasisPoints = Math.Min(10000, satisfaction + 300),
            valueBasisPoints = satisfaction
        });
        return record;
    }
    private static BistroBuilderGuestRelationsSnapshot BuildGuestFixture()
    {
        var snapshot = BistroBuilderGuestRelationsEngine.CreateEmptySnapshot();
        snapshot.cohorts.Add(new BistroBuilderGuestVisitCohortRecord
        {
            cohortId = "cohort.weak",
            segmentId = "general",
            partySize = 2,
            visitCount = 2,
            lastVisitDay = 9
        });
        snapshot.cohorts.Add(new BistroBuilderGuestVisitCohortRecord
        {
            cohortId = "cohort.strong",
            segmentId = "general",
            partySize = 2,
            visitCount = 6,
            lastVisitDay = 4
        });
        snapshot.nextCohortSequence = 3;
        snapshot.revision = 2;
        return snapshot;
    }

    private static void Finish(bool success, string message, bool commandLine)
    {
        string report = "=== BISTRO BUILDER — 10E / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static T Find<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}
