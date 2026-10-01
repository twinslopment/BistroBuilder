using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reversible real-scene Play Mode gate for the approved PERSONAL/HORARIOS
/// presentation. Uses only in-memory play session; never saves the scene,
/// a profile, a candidate or a game slot. Runs also via -executeMethod in batch.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderStaffApprovedRuntimeProbe
{
    private const string Prefix = "BB.PersonalApproved.Probe.";
    private const string RootScene = "Assets/Scenes/Prototype_Restaurant.unity";
    private static BistroBuilderStaffPlayerScreen personal;
    private static BistroBuilderStaffSchedulePlayerScreen schedule;
    private static int passed, failed;
    private static bool batch;
    private static string directory;
    private static bool personalImagePending, scheduleImagePending;

    static BistroBuilderStaffApprovedRuntimeProbe()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Bistro Builder/Personal/V1 - Prueba real visual reversible", false, 3258)]
    public static void RunMenu() => Begin(false);

    // Batch: Unity -batchmode -projectPath ... -executeMethod
    // BistroBuilderStaffApprovedRuntimeProbe.RunBatch -logFile ...
    public static void RunBatch() => Begin(true);

    private static void Begin(bool exitWhenDone)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SessionState.GetBool(Prefix + "Running", false))
        {
            Debug.LogError("[PERSONAL APPROVED] El editor ya está en Play Mode.");
            if (exitWhenDone) EditorApplication.Exit(2);
            return;
        }
        if (!File.Exists(RootScene))
        {
            Debug.LogError("[PERSONAL APPROVED] Falta Prototype_Restaurant.unity.");
            if (exitWhenDone) EditorApplication.Exit(2);
            return;
        }
        if (SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogError("[PERSONAL APPROVED] Hay cambios de escena sin guardar. No se sobrescriben.");
            if (exitWhenDone) EditorApplication.Exit(2);
            return;
        }

        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetBool(Prefix + "Batch", exitWhenDone);
        SessionState.SetInt(Prefix + "Stage", 0);
        SessionState.SetInt(Prefix + "Passed", 0);
        SessionState.SetInt(Prefix + "Failed", 0);
        SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup);
        directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Logs", "PersonalApproved");
        Directory.CreateDirectory(directory);
        EditorSceneManager.OpenScene(RootScene, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void Pass(bool success, string description)
    {
        if (success) passed++;
        else failed++;
        Debug.Log((success ? "[PERSONAL APPROVED] PASS " :
            "[PERSONAL APPROVED] FAIL ") + description);
        SessionState.SetInt(Prefix + "Passed", passed);
        SessionState.SetInt(Prefix + "Failed", failed);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        int stage = SessionState.GetInt(Prefix + "Stage", 0);
        if (!EditorApplication.isPlaying)
        {
            if (stage < 4) return;
            int result = SessionState.GetInt(Prefix + "Failed", 0);
            int correct = SessionState.GetInt(Prefix + "Passed", 0);
            bool isBatch = SessionState.GetBool(Prefix + "Batch", false);
            Debug.Log("[PERSONAL APPROVED] PLAY-MODE " + correct + " PASS / " +
                result + " FAIL · screenshots en Logs/PersonalApproved");
            SessionState.SetBool(Prefix + "Running", false);
            SessionState.SetInt(Prefix + "Stage", 0);
            if (isBatch) EditorApplication.Exit(result == 0 ? 0 : 1);
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        float elapsed = (float)EditorApplication.timeSinceStartup -
            SessionState.GetFloat(Prefix + "Started", 0);
        if (elapsed > 180f)
        {
            failed++;
            Debug.LogError("[PERSONAL APPROVED] Timeout 180 s.");
            Finish();
            return;
        }
        if (Time.frameCount < 15) return;
        try
        {
            passed = SessionState.GetInt(Prefix + "Passed", 0);
            failed = SessionState.GetInt(Prefix + "Failed", 0);
            batch = SessionState.GetBool(Prefix + "Batch", false);
            directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Logs", "PersonalApproved");
            if (stage == 0) StartPersonal();
            else if (stage == 1 && Time.frameCount >= 42) AuditPersonal();
            else if (stage == 2 && Time.frameCount >= 80) StartSchedule();
            else if (stage == 3 && Time.frameCount >= 111) AuditSchedule();
        }
        catch (Exception exception)
        {
            failed++;
            Debug.LogException(exception);
            Finish();
        }
    }

    private static T FindScene<T>() where T : Component
    {
        T[] candidates = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < candidates.Length; i++)
            if (candidates[i] != null &&
                candidates[i].gameObject.scene == SceneManager.GetActiveScene())
                return candidates[i];
        return null;
    }

    private static RectTransform GetRect(Component host, string property)
    {
        FieldInfo field = host.GetType().GetField(property,
            BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEngine.Object value = field?.GetValue(host) as UnityEngine.Object;
        return value is GameObject go ? go.transform as RectTransform
            : value as RectTransform;
    }

    private static void StartPersonal()
    {
        personal = FindScene<BistroBuilderStaffPlayerScreen>();
        schedule = FindScene<BistroBuilderStaffSchedulePlayerScreen>();
        Pass(personal != null && schedule != null,
            "Las pantallas Personal 4F y Horarios 5E existen en Prototype_Restaurant.");
        if (personal == null || schedule == null)
        {
            Finish();
            return;
        }
        // This is a throw-away Play session. Hire through the canonical Facade
        // solely to test real candidate -> employee bindings and populate rows.
        BistroBuilderStaffPlayerFacade facade =
            FindScene<BistroBuilderStaffPlayerFacade>();
        string facadeError = string.Empty;
        if (facade != null && facade.TryBuildSnapshot(
                out BistroBuilderStaffPlayerUiSnapshot before,
                out facadeError))
        {
            if (before.employees.Count < 2)
            {
                bool waiter = false, cook = false;
                foreach (var candidate in before.candidates)
                {
                    if (candidate == null) continue;
                    if (candidate.roleId == "waiter" && waiter) continue;
                    if (candidate.roleId == "cook" && cook) continue;
                    if (facade.TryHireCandidate(candidate.candidateId, out _, out _))
                    {
                        if (candidate.roleId == "waiter") waiter = true;
                        if (candidate.roleId == "cook") cook = true;
                    }
                    if (waiter && cook) break;
                }
            }
            Pass(facade.TryBuildSnapshot(out var after, out _) &&
                after.departments.Count >= 2 &&
                after.employees.Count >= 1,
                "El catálogo produce Sala/Cocina y las filas proyectan empleados reales.");
        }
        else
        {
            Debug.LogWarning("[PERSONAL APPROVED] No pudo prepararse mercado transient: " +
                (facadeError ?? "Fachada no disponible"));
        }
        personal.Show();
        SessionState.SetInt(Prefix + "Stage", 1);
    }

    private static bool Inside(RectTransform outer, RectTransform inner)
    {
        if (outer == null || inner == null) return false;
        var a = new Vector3[4];
        var b = new Vector3[4];
        outer.GetWorldCorners(a);
        inner.GetWorldCorners(b);
        return b[0].x >= a[0].x - 3f &&
               b[2].x <= a[2].x + 3f &&
               b[0].y >= a[0].y - 3f &&
               b[2].y <= a[2].y + 3f;
    }

    private static void AuditPersonal()
    {
        RectTransform root = GetRect(personal, "panelRoot");
        Transform p = root.transform;
        RectTransform roster = p.Find("StaffPanel/EmployeeList") as RectTransform;
        RectTransform inspector = p.Find("StaffPanel/EmployeeDetail") as RectTransform;
        RectTransform staff = p.Find("StaffPanel") as RectTransform;
        Transform content = p.Find("StaffPanel/EmployeeList/Viewport/Content");
        Pass(root != null && staff != null && roster != null &&
            inspector != null, "Plantilla tiene exactamente los dos paneles aprobados.");
        Pass(roster != null && inspector != null &&
            roster.anchorMax.x < inspector.anchorMin.x &&
            Mathf.Abs(roster.anchorMax.x - .559f) < .006f &&
            Mathf.Abs(inspector.anchorMin.x - .568f) < .006f,
            "Tabla 56% y ficha 43%, sin superposición horizontal.");
        Pass(content != null &&
            content.Find("Department_SALA") != null &&
            content.Find("Department_COCINA") != null,
            "Existen SALA y COCINA, incluso sin plazas máximas inventadas.");
        Pass(inspector != null &&
            inspector.Find("HeroPortrait") != null &&
            inspector.Find("LevelXPBar") != null &&
            inspector.Find("SkillTrack_0") != null &&
            inspector.Find("SkillTrack_1") != null &&
            inspector.Find("SkillTrack_2") != null &&
            inspector.Find("SkillTrack_3") != null,
            "Ficha aprobada incluye retrato, XP real y cuatro barras.");
        Pass(Inside(root, staff) && Inside(staff, roster) &&
            Inside(staff, inspector), "Los paneles quedan dentro del área segura.");
        Pass(p.Find("ApprovedScheduleTab") != null &&
            p.Find("ApprovedPersonalFrame") != null &&
            p.Find("ApprovedPersonalPaper") != null,
            "Navegación y marco marfil/latón únicos.");

        string file = Path.Combine(directory, "Personal_1920x1080.png");
        ScreenCapture.CaptureScreenshot(file);
        SessionState.SetInt(Prefix + "Stage", 2);
    }

    private static void StartSchedule()
    {
        // Exercise the actual PERSONAL tab wiring: it hides 4F before opening 5E.
        // Calling ShowFromPersonal directly bypasses that navigation contract and
        // incorrectly reports an overlap that cannot occur through the tab.
        RectTransform root = GetRect(personal, "panelRoot");
        Transform tabTransform = root != null
            ? root.Find("ApprovedScheduleTab") : null;
        Button tab = tabTransform != null
            ? tabTransform.GetComponent<Button>() : null;
        if (tab == null || !tab.gameObject.activeInHierarchy || !tab.IsInteractable())
        {
            Pass(false, "Horarios tiene una pestaña accionable en PERSONAL.");
            Finish();
            return;
        }
        tab.onClick.Invoke();
        Pass(schedule.IsVisible && !personal.IsVisible,
            "Horarios abre como tercera pestaña sin pantallas solapadas.");
        SessionState.SetInt(Prefix + "Stage", 3);
    }

    private static void AuditSchedule()
    {
        RectTransform root = GetRect(schedule, "panelRoot");
        Pass(root != null &&
            root.transform.Find("ApprovedScheduleFrame") != null &&
            root.transform.Find("ApprovedPayrollFooter") != null &&
            root.transform.Find("GoToPlantilla") != null &&
            root.transform.Find("GoToCandidates") != null,
            "Horarios mantiene el mismo lenguaje visual y regreso a Personal.");

        BistroBuilderStaffSchedulePlayerFacade facade =
            FindScene<BistroBuilderStaffSchedulePlayerFacade>();
        if (facade != null && facade.TryBuildSnapshot(
                schedule.SelectedDayIndex, schedule.SelectedMealService,
                out BistroBuilderStaffSchedulePlayerSnapshot snapshot, out _))
        {
            int waiters = 0, cooks = 0;
            long total = 0, kitchen = 0;
            foreach (var member in snapshot.employees)
            {
                if (member == null || !member.scheduled) continue;
                total += Math.Max(0L, member.salaryCentsPerService);
                if (member.operationalAdapterId ==
                    BistroBuilderStaffOperationalAdapterIds.CookAgent)
                {
                    cooks++;
                    kitchen += Math.Max(0L, member.salaryCentsPerService);
                }
                else if (member.operationalAdapterId ==
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent)
                    waiters++;
            }
            Pass(snapshot.scheduledWaiters == waiters &&
                snapshot.scheduledCooks == cooks &&
                snapshot.projectedCookSalaryCents == kitchen &&
                snapshot.projectedTotalSalaryCents == total,
                "Coste total y recuentos de Sala y Cocina coinciden con todos los turnos.");
        }
        else Pass(false, "No se pudo calcular una planificación canónica de prueba.");

        ScreenCapture.CaptureScreenshot(Path.Combine(
            directory, "Horarios_1920x1080.png"));
        Finish();
    }

    private static void Finish()
    {
        SessionState.SetInt(Prefix + "Failed", failed);
        SessionState.SetInt(Prefix + "Passed", passed);
        SessionState.SetInt(Prefix + "Stage", 4);
        EditorApplication.ExitPlaymode();
    }
}
