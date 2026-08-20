using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instalador idempotente de 4F. Crea solo Presentation: una fachada en el
/// GameSystems canónico y una jerarquía uGUI/TMP independiente. No crea ni
/// sustituye autoridades de Personal, Waiter, Finanzas o Save.
/// </summary>
public static class BistroBuilderStaff4FInstaller
{
    private const string UiRootName = "BistroBuilderStaffUI";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4F - Instalar UI + validar",
        false,
        3250)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Personal",
                "Sal de Play Mode antes de instalar 4F.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Personal",
                "Abre la escena principal, guárdala y vuelve a ejecutar 4F.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);
        Undo.SetCurrentGroupName("Instalar 4F Personal");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            BistroBuilderStaff4EValidatorV2.Result validation4E =
                BistroBuilderStaff4EValidatorV2.ValidateCurrentScene();
            if (validation4E.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    "4E tiene errores estructurales; 4F no modificará la escena.");
            }

            bool staticOk = BistroBuilderStaff4FStaticSelfTest.Run(
                out int staticPassed,
                out int staticFailed,
                out string staticReport);
            Debug.Log(staticReport);
            if (!staticOk)
            {
                throw new InvalidOperationException(
                    "El gate estático 4F falló: " + staticFailed +
                    " fallos / " + staticPassed + " correctos.");
            }

            GameObject gameSystems = FindUniqueNamedObject(scene, "GameSystems");
            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");
            }

            BistroBuilderStaffPlayerFacade facade =
                EnsureUnique<BistroBuilderStaffPlayerFacade>(scene, gameSystems);
            Assign(facade, "staffService", RequireUnique<BistroBuilderStaffService>(scene));
            Assign(
                facade,
                "recruitmentService",
                RequireUnique<BistroBuilderStaffRecruitmentService>(scene));
            Assign(
                facade,
                "developmentService",
                RequireUnique<BistroBuilderStaffDevelopmentService>(scene));
            Assign(
                facade,
                "sessionService",
                RequireUnique<BistroBuilderStaffSessionService>(scene));

            GameObject oldUi = FindDirectRoot(scene, UiRootName);
            if (oldUi != null)
            {
                Undo.DestroyObjectImmediate(oldUi);
            }

            GameObject ui = NewUi(UiRootName, null);
            SceneManager.MoveGameObjectToScene(ui, scene);
            Canvas canvas = Add<Canvas>(ui);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = Add<CanvasScaler>(ui);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            Add<GraphicRaycaster>(ui);
            Stretch(ui.GetComponent<RectTransform>());

            BistroBuilderStaffPlayerScreen screen =
                Add<BistroBuilderStaffPlayerScreen>(ui);

            GameObject templates = NewUi("Templates", ui.transform);
            BistroBuilderStaffPlayerEmployeeRowView employeeTemplate =
                BuildEmployeeTemplate(templates.transform);
            BistroBuilderStaffPlayerCandidateRowView candidateTemplate =
                BuildCandidateTemplate(templates.transform);
            templates.SetActive(false);

            GameObject panel = BuildScreen(
                ui.transform,
                facade,
                screen,
                employeeTemplate,
                candidateTemplate);
            panel.SetActive(false);

            Button launcher = Button(
                ui.transform,
                "OpenStaffButton",
                "Personal",
                new Vector2(0.885f, 0.925f),
                new Vector2(0.988f, 0.975f));
            UnityEventTools.AddPersistentListener(launcher.onClick, screen.Show);

            if (!facade.ValidateConfiguration(out string facadeError))
            {
                throw new InvalidOperationException(facadeError);
            }
            if (!screen.ValidateConfiguration(out string screenError))
            {
                throw new InvalidOperationException(screenError);
            }

            EditorUtility.SetDirty(facade);
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 4F.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderStaff4FValidationResult validation =
                BistroBuilderStaff4FValidator.ValidateCurrentScene();
            bool finalStatic = BistroBuilderStaff4FStaticSelfTest.Run(
                out int passed,
                out int failed,
                out string finalReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(finalReport);

            if (validation.ErrorCount > 0 || !finalStatic)
            {
                throw new InvalidOperationException(
                    "4F no superó sus gates finales: " + validation.ErrorCount +
                    " errores estructurales y " + failed + " fallos estáticos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Personal",
                "4F instalado.\n\n" +
                validation.CorrectCount + " validaciones correctas / " +
                validation.WarningCount + " avisos / 0 errores\n" +
                passed + " autotests estáticos correctos / 0 fallos\n\n" +
                "Pendiente: prueba real en Play Mode.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Personal",
                "La instalación falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static GameObject BuildScreen(
        Transform parent,
        BistroBuilderStaffPlayerFacade facade,
        BistroBuilderStaffPlayerScreen screen,
        BistroBuilderStaffPlayerEmployeeRowView employeeTemplate,
        BistroBuilderStaffPlayerCandidateRowView candidateTemplate)
    {
        GameObject root = NewUi("StaffPanelRoot", parent);
        Stretch(root.GetComponent<RectTransform>());
        Add<Image>(root).color = new Color(0.045f, 0.052f, 0.06f, 0.985f);
        CanvasGroup group = Add<CanvasGroup>(root);

        TMP_Text title = Text(root.transform, "Title", "PERSONAL", 30f);
        Anchor(title.rectTransform, 0.025f, 0.925f, 0.25f, 0.985f);
        TMP_Text summary = Text(root.transform, "Summary", string.Empty, 17f);
        Anchor(summary.rectTransform, 0.27f, 0.925f, 0.75f, 0.985f);
        Button close = Button(root.transform, "Close", "Cerrar",
            new Vector2(0.86f, 0.93f), new Vector2(0.975f, 0.98f));
        Button staffTab = Button(root.transform, "StaffTab", "Plantilla",
            new Vector2(0.025f, 0.87f), new Vector2(0.17f, 0.915f));
        Button candidateTab = Button(root.transform, "CandidateTab", "Candidatos",
            new Vector2(0.18f, 0.87f), new Vector2(0.325f, 0.915f));
        TMP_Text feedback = Text(root.transform, "Feedback", string.Empty, 15f);
        Anchor(feedback.rectTransform, 0.35f, 0.87f, 0.975f, 0.915f);

        GameObject staffPanel = NewUi("StaffPanel", root.transform);
        Anchor(staffPanel.GetComponent<RectTransform>(), 0.025f, 0.04f, 0.975f, 0.85f);
        RectTransform employeeContent = Scroll(
            staffPanel.transform, "Employees", 0f, 0f, 0.39f, 1f);
        GameObject employeeDetail = Panel(
            staffPanel.transform, "EmployeeDetail", 0.41f, 0f, 1f, 1f);
        TMP_Text employeeName = Detail(employeeDetail.transform, "Name", 0.88f, 1f, 28f);
        TMP_Text employeeRole = Detail(employeeDetail.transform, "Role", 0.80f, 0.88f, 19f);
        TMP_Text employeeContract = Detail(employeeDetail.transform, "Contract", 0.69f, 0.80f, 17f);
        TMP_Text employeeProgress = Detail(employeeDetail.transform, "Progress", 0.58f, 0.69f, 17f);
        TMP_Text employeeSkills = Detail(employeeDetail.transform, "Skills", 0.40f, 0.58f, 17f);
        TMP_Text employeePerformance = Detail(employeeDetail.transform, "Performance", 0.22f, 0.40f, 17f);
        TMP_Text employeeSession = Detail(employeeDetail.transform, "Session", 0.14f, 0.22f, 16f);
        Button availability = Button(employeeDetail.transform, "Availability", "Disponibilidad",
            new Vector2(0f, 0.02f), new Vector2(0.47f, 0.11f));
        TMP_Text availabilityText = availability.GetComponentInChildren<TMP_Text>(true);
        Button dismiss = Button(employeeDetail.transform, "Dismiss", "Despedir",
            new Vector2(0.53f, 0.02f), new Vector2(1f, 0.11f));

        GameObject candidatesPanel = NewUi("CandidatesPanel", root.transform);
        Anchor(candidatesPanel.GetComponent<RectTransform>(), 0.025f, 0.04f, 0.975f, 0.85f);
        RectTransform candidateContent = Scroll(
            candidatesPanel.transform, "Candidates", 0f, 0f, 0.39f, 1f);
        GameObject candidateDetail = Panel(
            candidatesPanel.transform, "CandidateDetail", 0.41f, 0f, 1f, 1f);
        TMP_Text candidateName = Detail(candidateDetail.transform, "Name", 0.86f, 1f, 28f);
        TMP_Text candidateRole = Detail(candidateDetail.transform, "Role", 0.76f, 0.86f, 19f);
        TMP_Text candidateProfile = Detail(candidateDetail.transform, "Profile", 0.62f, 0.76f, 17f);
        TMP_Text candidateSkills = Detail(candidateDetail.transform, "Skills", 0.40f, 0.62f, 17f);
        TMP_Text candidateSalary = Detail(candidateDetail.transform, "Salary", 0.25f, 0.40f, 20f);
        Button hire = Button(candidateDetail.transform, "Hire", "Contratar",
            new Vector2(0f, 0.02f), new Vector2(0.47f, 0.11f));
        Button refresh = Button(candidateDetail.transform, "Refresh", "Refrescar mercado",
            new Vector2(0.53f, 0.02f), new Vector2(1f, 0.11f));

        GameObject confirmation = Panel(root.transform, "Confirmation", 0.30f, 0.35f, 0.70f, 0.65f);
        TMP_Text confirmationText = Text(confirmation.transform, "Message", string.Empty, 20f);
        Anchor(confirmationText.rectTransform, 0.08f, 0.38f, 0.92f, 0.90f);
        Button accept = Button(confirmation.transform, "Accept", "Confirmar",
            new Vector2(0.08f, 0.10f), new Vector2(0.46f, 0.30f));
        Button cancel = Button(confirmation.transform, "Cancel", "Cancelar",
            new Vector2(0.54f, 0.10f), new Vector2(0.92f, 0.30f));

        Assign(screen, "facade", facade);
        Assign(screen, "panelRoot", root);
        Assign(screen, "canvasGroup", group);
        Assign(screen, "closeButton", close);
        Assign(screen, "staffTabButton", staffTab);
        Assign(screen, "candidatesTabButton", candidateTab);
        Assign(screen, "headerSummaryText", summary);
        Assign(screen, "feedbackText", feedback);
        Assign(screen, "staffPanel", staffPanel);
        Assign(screen, "employeeListContent", employeeContent);
        Assign(screen, "employeeRowPrefab", employeeTemplate);
        Assign(screen, "employeeNameText", employeeName);
        Assign(screen, "employeeRoleText", employeeRole);
        Assign(screen, "employeeContractText", employeeContract);
        Assign(screen, "employeeProgressText", employeeProgress);
        Assign(screen, "employeeSkillsText", employeeSkills);
        Assign(screen, "employeePerformanceText", employeePerformance);
        Assign(screen, "employeeSessionText", employeeSession);
        Assign(screen, "toggleAvailabilityButton", availability);
        Assign(screen, "toggleAvailabilityButtonText", availabilityText);
        Assign(screen, "dismissButton", dismiss);
        Assign(screen, "candidatesPanel", candidatesPanel);
        Assign(screen, "candidateListContent", candidateContent);
        Assign(screen, "candidateRowPrefab", candidateTemplate);
        Assign(screen, "candidateNameText", candidateName);
        Assign(screen, "candidateRoleText", candidateRole);
        Assign(screen, "candidateProfileText", candidateProfile);
        Assign(screen, "candidateSkillsText", candidateSkills);
        Assign(screen, "candidateSalaryText", candidateSalary);
        Assign(screen, "hireButton", hire);
        Assign(screen, "refreshCandidatesButton", refresh);
        Assign(screen, "confirmationPanel", confirmation);
        Assign(screen, "confirmationText", confirmationText);
        Assign(screen, "confirmationAcceptButton", accept);
        Assign(screen, "confirmationCancelButton", cancel);
        return root;
    }

    private static BistroBuilderStaffPlayerEmployeeRowView BuildEmployeeTemplate(Transform parent)
    {
        GameObject row = RowBase("EmployeeRowTemplate", parent);
        BistroBuilderStaffPlayerEmployeeRowView view = Add<BistroBuilderStaffPlayerEmployeeRowView>(row);
        Button button = row.GetComponent<Button>();
        TMP_Text name = RowText(row.transform, "Name", 0.02f, 0.28f);
        TMP_Text role = RowText(row.transform, "Role", 0.29f, 0.47f);
        TMP_Text status = RowText(row.transform, "Status", 0.48f, 0.65f);
        TMP_Text level = RowText(row.transform, "Level", 0.66f, 0.79f);
        TMP_Text salary = RowText(row.transform, "Salary", 0.80f, 0.98f);
        Assign(view, "selectButton", button);
        Assign(view, "nameText", name);
        Assign(view, "roleText", role);
        Assign(view, "statusText", status);
        Assign(view, "levelText", level);
        Assign(view, "salaryText", salary);
        return view;
    }

    private static BistroBuilderStaffPlayerCandidateRowView BuildCandidateTemplate(Transform parent)
    {
        GameObject row = RowBase("CandidateRowTemplate", parent);
        BistroBuilderStaffPlayerCandidateRowView view = Add<BistroBuilderStaffPlayerCandidateRowView>(row);
        Button button = row.GetComponent<Button>();
        TMP_Text name = RowText(row.transform, "Name", 0.02f, 0.32f);
        TMP_Text role = RowText(row.transform, "Role", 0.33f, 0.52f);
        TMP_Text profile = RowText(row.transform, "Profile", 0.53f, 0.71f);
        TMP_Text salary = RowText(row.transform, "Salary", 0.72f, 0.98f);
        Assign(view, "selectButton", button);
        Assign(view, "nameText", name);
        Assign(view, "roleText", role);
        Assign(view, "profileText", profile);
        Assign(view, "salaryText", salary);
        return view;
    }

    private static GameObject RowBase(string name, Transform parent)
    {
        GameObject row = NewUi(name, parent);
        Add<LayoutElement>(row).preferredHeight = 76f;
        Image image = Add<Image>(row);
        image.color = new Color(0.09f, 0.105f, 0.12f, 0.98f);
        Button button = Add<Button>(row);
        button.targetGraphic = image;
        return row;
    }

    private static RectTransform Scroll(
        Transform parent, string name, float minX, float minY, float maxX, float maxY)
    {
        GameObject scroll = Panel(parent, name, minX, minY, maxX, maxY);
        ScrollRect scrollRect = Add<ScrollRect>(scroll);
        scrollRect.horizontal = false;
        GameObject viewport = NewUi("Viewport", scroll.transform);
        Anchor(viewport.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
        Add<Image>(viewport).color = new Color(1f, 1f, 1f, 0.001f);
        Add<RectMask2D>(viewport);
        GameObject content = NewUi("Content", viewport.transform);
        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = Add<VerticalLayoutGroup>(content);
        layout.spacing = 6f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childForceExpandHeight = false;
        Add<ContentSizeFitter>(content).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = rect;
        return rect;
    }

    private static GameObject Panel(
        Transform parent, string name, float minX, float minY, float maxX, float maxY)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Add<Image>(go).color = new Color(0.07f, 0.08f, 0.09f, 0.98f);
        return go;
    }

    private static TMP_Text Detail(Transform parent, string name, float minY, float maxY, float size)
    {
        TMP_Text text = Text(parent, name, string.Empty, size);
        Anchor(text.rectTransform, 0.04f, minY, 0.96f, maxY);
        return text;
    }

    private static TMP_Text RowText(Transform parent, string name, float minX, float maxX)
    {
        TMP_Text text = Text(parent, name, string.Empty, 15f);
        Anchor(text.rectTransform, minX, 0f, maxX, 1f);
        return text;
    }

    private static Button Button(
        Transform parent, string name, string label, Vector2 min, Vector2 max)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), min.x, min.y, max.x, max.y);
        Image image = Add<Image>(go);
        image.color = new Color(0.16f, 0.19f, 0.21f, 1f);
        Button button = Add<Button>(go);
        button.targetGraphic = image;
        TMP_Text text = Text(go.transform, "Label", label, 17f);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_Text Text(Transform parent, string name, string value, float size)
    {
        GameObject go = NewUi(name, parent);
        TextMeshProUGUI text = Add<TextMeshProUGUI>(go);
        text.text = value;
        text.fontSize = size;
        text.color = new Color(0.92f, 0.92f, 0.90f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }
        return go;
    }

    private static void Stretch(RectTransform rect) =>
        Anchor(rect, 0f, 0f, 1f, 1f);

    private static void Anchor(
        RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T Add<T>(GameObject owner) where T : Component
    {
        T existing = owner.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(owner);
    }

    private static T EnsureUnique<T>(Scene scene, GameObject owner) where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length > 1)
        {
            throw new InvalidOperationException("Hay varias instancias de " + typeof(T).Name + ".");
        }
        if (values.Length == 1)
        {
            if (values[0].gameObject != owner)
            {
                throw new InvalidOperationException(typeof(T).Name + " está fuera de GameSystems.");
            }
            return values[0];
        }
        return Undo.AddComponent<T>(owner);
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length != 1)
        {
            throw new InvalidOperationException(
                "4F necesita exactamente una instancia de " + typeof(T).Name +
                "; hay " + values.Length + ".");
        }
        return values[0];
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var result = new List<T>();
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
            {
                result.Add(all[index]);
            }
        }
        return result.ToArray();
    }

    private static GameObject FindUniqueNamedObject(Scene scene, string name)
    {
        GameObject found = null;
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
            for (int child = 0; child < transforms.Length; child++)
            {
                if (string.Equals(transforms[child].name, name, StringComparison.Ordinal))
                {
                    found = transforms[child].gameObject;
                    count++;
                }
            }
        }
        return count == 1 ? found : null;
    }

    private static GameObject FindDirectRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            if (string.Equals(roots[index].name, name, StringComparison.Ordinal))
            {
                return roots[index];
            }
        }
        return null;
    }

    private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene el campo serializado " + field + ".");
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
