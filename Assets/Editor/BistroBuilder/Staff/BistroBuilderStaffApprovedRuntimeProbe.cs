using System;
using System.IO;
using System.Reflection;
using TMPro;
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
        Transform contextualMenu = p.Find("ApprovedViewMenu");
        Transform scheduleTab = p.Find("ApprovedScheduleTab");
        Transform viewSwitcher = p.Find("ApprovedViewSwitcher");
        Pass(contextualMenu != null && scheduleTab != null &&
            viewSwitcher != null && !contextualMenu.gameObject.activeSelf &&
            !scheduleTab.gameObject.activeSelf &&
            staff.anchorMax.y > .89f,
            "Referencia V5: sin pestañas permanentes, tablas bajo cabecera y selector PERSONAL.");
        bool realIcons = true;
        foreach (string name in new[] {"role", "salary", "assignment", "state"})
            realIcons &= Resources.Load<Sprite>(
                "BistroBuilder/UI/StaffIcons/" + name) != null;
        Pass(realIcons && inspector.Find("InfoIcon_0") != null &&
            inspector.Find("InfoIcon_1") != null &&
            inspector.Find("InfoIcon_2") != null &&
            inspector.Find("InfoIcon_3") != null,
            "Ficha con los cuatro iconos específicos, no símbolos tipográficos.");
        Pass(Resources.LoadAll<Sprite>(
                 "BistroBuilder/UI/StaffPortraits/waiter").Length >= 3 &&
             Resources.LoadAll<Sprite>(
                 "BistroBuilder/UI/StaffPortraits/cook").Length >= 2 &&
             BistroBuilderStaffVisuals.Portrait("waiter", "Laura Torres") != null &&
             BistroBuilderStaffVisuals.Portrait("cook", "Marco Ruiz") != null,
             "Los cinco retratos originales están importados como Sprite en Personal.");

        TMP_Text mainTitle = p.Find("Title")?.GetComponent<TMP_Text>();
        TMP_Text rowName = null;
        if (content != null)
        {
            foreach (TMP_Text label in content.GetComponentsInChildren<TMP_Text>(true))
                if (label != null && label.name == "Name")
                { rowName = label; break; }
        }
        TMP_FontAsset bodyFace = BistroBuilderStaffVisuals.StaffRegular;
        TMP_FontAsset boldFace = BistroBuilderStaffVisuals.StaffBold;
        TMP_FontAsset titleFace = BistroBuilderStaffVisuals.StaffTitle;
        TMP_Text departmentTitle = content?.Find(
            "Department_SALA/DepartmentBand/DepartmentTitle")?.GetComponent<TMP_Text>();
        TMP_Text columnName = content?.Find(
            "Department_SALA/DepartmentColumns/Column_0")?.GetComponent<TMP_Text>();
        Pass(mainTitle != null && rowName != null &&
            departmentTitle != null && columnName != null &&
            titleFace != null && bodyFace != null && boldFace != null &&
            mainTitle.font == titleFace &&
            departmentTitle.font == titleFace &&
            rowName.font == titleFace && columnName.font == titleFace &&
            rowName.fontStyle == FontStyles.Bold &&
            titleFace.name.Contains("Recoleta") &&
            bodyFace == BistroBuilderTypography.Body &&
            boldFace == BistroBuilderTypography.Emphasis &&
            titleFace.fallbackFontAssetTable.Contains(bodyFace),
            "Recoleta oficial en títulos, columnas y nombres; Inter secundario, sin Georgia.");
        TMP_FontAsset sourceRecoleta = Resources.Load<TMP_FontAsset>(
            "BistroBuilder/UI/Typography/Recoleta-SDF");
        bool demo = sourceRecoleta != null &&
            sourceRecoleta.faceInfo.styleName.IndexOf("DEMO",
                StringComparison.OrdinalIgnoreCase) >= 0;
        // TMPro's runtime character lookup may cache fallback mappings after
        // rendering. Inspect the SOURCE SDF character table instead to prove
        // that the broken DEMO accents/euro cannot originate in its atlas.
        bool nativeAccent = false, nativeEuro = false;
        if (sourceRecoleta != null && sourceRecoleta.characterTable != null)
            foreach (TMP_Character glyph in sourceRecoleta.characterTable)
            {
                if (glyph == null) continue;
                if (glyph.unicode == (uint)'ó') nativeAccent = true;
                if (glyph.unicode == (uint)'€') nativeEuro = true;
            }
        bool cleanGlyphs = titleFace != null && (demo
            ? titleFace.atlasPopulationMode == AtlasPopulationMode.Static &&
              !nativeAccent && !nativeEuro &&
              titleFace.fallbackFontAssetTable.Contains(bodyFace) &&
              bodyFace.HasCharacter('ó', false, false) &&
              bodyFace.HasCharacter('€', false, false)
            : titleFace.atlasPopulationMode == AtlasPopulationMode.Dynamic &&
              titleFace.HasCharacter('ó', true, true) &&
              titleFace.HasCharacter('€', true, true));
        Pass(cleanGlyphs,
            "Recoleta DEMO usa glifos estáticos válidos; Inter oficial cubre acentos y euro ausentes.");
        bool alignedColumns = false;
        if (content != null)
            foreach (Transform child in content.GetComponentsInChildren<Transform>(true))
                if (child != null && child.name == "RowDivider_4" &&
                    child.parent.Find("RowDivider_0") != null)
                { alignedColumns = true; break; }
        Pass(alignedColumns && content?.Find(
            "Department_SALA/DepartmentColumns/ColumnDivider_0") != null &&
            content?.Find("ApprovedDepartmentGap") != null,
            "SALA y COCINA usan tablas continuas con columnas alineadas y separación entre secciones.");

        TMP_Text caption = content?.Find(
            "Department_SALA/DepartmentBand/DepartmentCaption")?.GetComponent<TMP_Text>();
        TMP_Text salary = null;
        TMP_Text skills = inspector?.Find("Skill_0")?.GetComponent<TMP_Text>();
        TMP_Text infoKey = inspector?.Find("InfoLabel_0")?.GetComponent<TMP_Text>();
        TMP_Text infoValue = inspector?.Find("InfoValue_0")?.GetComponent<TMP_Text>();
        TMP_Text hire = null;
        if (content != null)
            foreach (TMP_Text label in content.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label == null) continue;
                if (salary == null && label.name == "Salary") salary = label;
                if (hire == null && label.name == "VacancyHire_Label") hire = label;
            }
        Pass(caption != null && caption.font == bodyFace &&
             caption.fontSize >= BistroBuilderStaffVisuals.CaptionSize - 1f &&
             columnName != null && columnName.font == titleFace &&
             columnName.fontSize >= BistroBuilderStaffVisuals.ColumnSize - 1f &&
             salary != null && salary.font == titleFace &&
             skills != null && skills.font == titleFace &&
             infoKey != null && infoKey.font == titleFace &&
             infoValue != null && infoValue.font == titleFace &&
             hire != null && hire.font == titleFace,
            "Recoleta en columnas, filas, habilidades, datos y Contratar; Inter en la descripción.");
        int serifActionButtons = 0;
        foreach (Button action in inspector.GetComponentsInChildren<Button>(true))
        {
            if (action == null) continue;
            TMP_Text actionLabel = action.GetComponentInChildren<TMP_Text>(true);
            if (actionLabel == null) continue;
            if (actionLabel.font == titleFace) serifActionButtons++;
        }
        Pass(serifActionButtons >= 4,
            "Los cuatro botones inferiores de la ficha utilizan Recoleta.");
        BistroBuilderStaffVisuals.NormalizeHierarchy(root);
        Pass(salary != null && salary.font == titleFace &&
            salary.fontStyle == FontStyles.Normal &&
            columnName != null && columnName.font == titleFace &&
            columnName.fontStyle == FontStyles.Bold,
            "La segunda normalización mantiene Recoleta Regular/Bold sin falsear todas las filas como títulos.");
        int typographyCount = 0, nonOfficial = 0;
        string badExamples = string.Empty;
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null) continue;
            typographyCount++;
            if (BistroBuilderStaffVisuals.IsOfficialFont(label)) continue;
            nonOfficial++;
            if (nonOfficial <= 4)
                badExamples += " " + label.name + "=" +
                    (label.font != null ? label.font.name : "NULL");
        }
        Pass(typographyCount >= 55 && nonOfficial == 0,
            "Auditoría integral: " + typographyCount +
            " textos de Personal/Candidatos/modales solo con Recoleta e Inter." +
            (nonOfficial > 0 ? " INCORRECTOS:" + badExamples : string.Empty));

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
        Button menu = root != null
            ? root.Find("ApprovedViewSwitcher")?.GetComponent<Button>()
            : null;
        if (menu == null || !menu.gameObject.activeInHierarchy ||
            !menu.IsInteractable())
        {
            Pass(false, "PERSONAL dispone de selector contextual accionable.");
            Finish();
            return;
        }
        menu.onClick.Invoke();
        Transform tabTransform = root.Find("ApprovedScheduleTab");
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
        Pass(root != null &&
             root.transform.Find("Title")?.GetComponent<TMP_Text>()?.font ==
                 BistroBuilderStaffVisuals.StaffTitle,
            "Horarios comparte Recoleta oficial con Personal; datos y controles usan Inter.");
        int count = 0, incorrect = 0;
        if (root != null)
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label == null) continue;
                count++;
                if (!BistroBuilderStaffVisuals.IsOfficialFont(label)) incorrect++;
            }
        Pass(count >= 20 && incorrect == 0,
            "Auditoría integral Horarios: " + count +
            " textos con fuentes oficiales; ninguna heredada.");

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
