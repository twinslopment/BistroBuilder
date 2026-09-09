using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 5F — Preflight read-only del Queen Test de Horarios.
/// </summary>
public sealed class BistroBuilderStaff5FQueenPreflightWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Entra en Play Mode para ejecutar el preflight 5F.";
    private MessageType reportType = MessageType.Info;

    [MenuItem("Tools/Bistro Builder/Personal/5F - Queen Test preflight", false, 3291)]
    private static void Open() => GetWindow<BistroBuilderStaff5FQueenPreflightWindow>("Queen Horarios 5F");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("5F — PREFLIGHT QUEEN TEST HORARIOS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Comprueba autoridades, persistencia, UI, servicio y agentes reales sin modificar la partida.",
            MessageType.Info);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("EJECUTAR PREFLIGHT 5F", GUILayout.Height(36f))) RunPreflight();
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, reportType);
        EditorGUILayout.EndScrollView();
    }

    private void RunPreflight()
    {
        var lines = new List<string>();
        int passed = 0;
        int failed = 0;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Finish(lines, 0, 1, "No hay escena activa cargada.");
            return;
        }

        BistroBuilderSaveGameService save = Unique<BistroBuilderSaveGameService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffService staff = Unique<BistroBuilderStaffService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffRecruitmentService recruitment = Unique<BistroBuilderStaffRecruitmentService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffScheduleService schedule = Unique<BistroBuilderStaffScheduleService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffScheduleSessionBridge bridge = Unique<BistroBuilderStaffScheduleSessionBridge>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffSessionService session = Unique<BistroBuilderStaffSessionService>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffScheduleSaveSectionProvider provider =
            Unique<BistroBuilderStaffScheduleSaveSectionProvider>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffSchedulePlayerFacade facade =
            Unique<BistroBuilderStaffSchedulePlayerFacade>(scene, lines, ref passed, ref failed);
        BistroBuilderStaffSchedulePlayerScreen screen =
            Unique<BistroBuilderStaffSchedulePlayerScreen>(scene, lines, ref passed, ref failed);
        RestaurantServiceStateService serviceState =
            Unique<RestaurantServiceStateService>(scene, lines, ref passed, ref failed);
        BistroBuilderCanonicalOrderIntegrationService orderIntegration =
            Unique<BistroBuilderCanonicalOrderIntegrationService>(scene, lines, ref passed, ref failed);
        Unique<WaiterTaskCoordinator>(scene, lines, ref passed, ref failed);

        if (save != null)
        {
            save.RefreshExtensions();
            Check(save.ValidateConfiguration(out string saveError),
                "SaveGame válido. " + saveError, lines, ref passed, ref failed);
            Check(
                save.HasProvider(BistroBuilderStaffStateSaveSectionProvider.StableSectionId) &&
                save.HasProvider(BistroBuilderStaffRecruitmentSaveSectionProvider.StableSectionId) &&
                save.HasProvider(BistroBuilderStaffSessionSaveSectionProvider.StableSectionId) &&
                save.HasProvider(BistroBuilderStaffScheduleSaveSectionProvider.StableSectionId),
                "SaveGame registra Staff/Recruitment/Session/Schedule.",
                lines, ref passed, ref failed);
        }
        if (staff != null)
            Check(staff.ValidateConfiguration(out string error), "Staff válido. " + error,
                lines, ref passed, ref failed);
        if (recruitment != null)
        {
            bool recruitmentReady =
                recruitment.ValidateConfiguration(out string error) &&
                recruitment.EnsureMarketReady(out error);
            Check(recruitmentReady,
                recruitmentReady ? "Recruitment válido y mercado disponible." :
                    "Recruitment no disponible: " + error,
                lines, ref passed, ref failed);
        }
        if (schedule != null)
        {
            Check(schedule.ValidateConfiguration(out string error), "Schedule válido. " + error,
                lines, ref passed, ref failed);
            Check(schedule.ScheduleProfile != null && schedule.ScheduleProfile.PlanningHorizonDays >= 1,
                "Horizonte de planificación disponible.", lines, ref passed, ref failed);
        }
        if (bridge != null)
            Check(bridge.ValidateConfiguration(out string error), "Bridge 5C válido. " + error,
                lines, ref passed, ref failed);
        if (session != null)
            Check(session.ValidateConfiguration(out string error), "Session 4D válida. " + error,
                lines, ref passed, ref failed);
        if (provider != null)
            Check(provider.ValidateConfiguration(out string error), "Provider 5D válido. " + error,
                lines, ref passed, ref failed);
        if (facade != null)
            Check(facade.ValidateConfiguration(out string error), "Facade 5E válida. " + error,
                lines, ref passed, ref failed);
        if (screen != null)
            Check(screen.ValidateConfiguration(out string error), "Screen 5E válida. " + error,
                lines, ref passed, ref failed);

        if (serviceState != null && session != null)
            Check(serviceState.IsClosed && !session.HasActiveSession,
                "La prueba comienza en Closed y sin sesión 4D activa.",
                lines, ref passed, ref failed);

        if (orderIntegration != null)
            Check(orderIntegration.ValidateConfiguration(out string orderError) &&
                  orderIntegration.CurrentMealService != BistroBuilderMealServiceAvailability.None,
                "Servicio gastronómico concreto disponible. " + orderError,
                lines, ref passed, ref failed);

        int waiterCount = Object.FindObjectsByType<Waiter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        Check(waiterCount > 0, "Existen Waiter reales: " + waiterCount + ".",
            lines, ref passed, ref failed);

        if (staff != null)
        {
            var employees = new List<BistroBuilderEmployeeRecord>();
            staff.CopyEmployees(employees, false);
            int eligible = 0;
            foreach (BistroBuilderEmployeeRecord employee in employees)
            {
                if (employee == null ||
                    employee.employmentStatus != BistroBuilderEmploymentStatus.Active ||
                    employee.availability != BistroBuilderEmployeeAvailability.Available ||
                    !staff.TryGetRoleDefinition(employee.roleId, out BistroBuilderStaffRoleDefinition role) ||
                    role == null ||
                    role.operationalAdapterId != BistroBuilderStaffOperationalAdapterIds.WaiterAgent)
                    continue;
                eligible++;
            }
            int recruitableWaiters = 0;
            if (eligible == 0 && recruitment != null)
            {
                var candidates = new List<BistroBuilderStaffCandidateRecord>();
                recruitment.CopyCandidates(candidates);
                foreach (BistroBuilderStaffCandidateRecord candidate in candidates)
                {
                    if (candidate == null ||
                        !staff.TryGetRoleDefinition(candidate.roleId, out BistroBuilderStaffRoleDefinition role) ||
                        role == null ||
                        role.operationalAdapterId != BistroBuilderStaffOperationalAdapterIds.WaiterAgent)
                        continue;
                    recruitableWaiters++;
                }
            }

            Check(eligible > 0 || recruitableWaiters > 0,
                eligible > 0
                    ? "Hay camareros Employee disponibles para planificar: " + eligible + "."
                    : "No hay Employee disponible; Queen puede contratar de forma reversible " +
                      recruitableWaiters + " candidato(s) camarero.",
                lines, ref passed, ref failed);
        }

        Finish(lines, passed, failed, null);
    }

    private static T Unique<T>(Scene scene, List<string> lines, ref int passed, ref int failed)
        where T : Component
    {
        T[] all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var matches = new List<T>();
        foreach (T value in all) if (value != null && value.gameObject.scene == scene) matches.Add(value);
        bool ok = matches.Count == 1;
        Check(ok,
            ok ? "Existe una única autoridad " + typeof(T).Name + "." :
                 typeof(T).Name + " debe existir una vez; hay " + matches.Count + ".",
            lines, ref passed, ref failed);
        return ok ? matches[0] : null;
    }

    private static void Check(bool condition, string text, List<string> lines,
        ref int passed, ref int failed)
    {
        if (condition) { passed++; lines.Add("[OK] " + text); }
        else { failed++; lines.Add("[ERROR] " + text); }
    }

    private void Finish(List<string> lines, int passed, int failed, string immediate)
    {
        if (!string.IsNullOrWhiteSpace(immediate)) lines.Add("[ERROR] " + immediate);
        report = "5F — PREFLIGHT QUEEN HORARIOS\n" + string.Join("\n", lines) +
                 "\n\nResultado: " + passed + " OK / " + failed + " errores.";
        reportType = failed == 0 ? MessageType.Info : MessageType.Error;
        Repaint();
        if (failed == 0) Debug.Log(report); else Debug.LogError(report);
    }
}
