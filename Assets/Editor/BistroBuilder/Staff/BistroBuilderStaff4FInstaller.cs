using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instalador acumulativo 4F de la UI jugable de Personal.
///
/// Crea una jerarquía uGUI/TMP dedicada y una StaffPlayerFacade sobre el
/// GameSystems canónico. Presentation no recibe autoridad sobre empleados,
/// candidatos, XP, servicio, dinero ni Save: todas sus dependencias se cablean
/// contra los servicios 4A–4E ya existentes.
///
/// El instalador es idempotente. Si cualquier gate falla, restaura la escena
/// original desde copia binaria y no deja una instalación parcial.
/// </summary>
public static class BistroBuilderStaff4FInstaller
{
    private const string UiControllerName = "BistroBuilderStaffUI";
    private const string PanelRootName = "StaffPanelRoot";

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
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Personal",
                "Abre y guarda la escena principal antes de instalar 4F.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Personal",
                "Guarda la escena antes de instalar 4F.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScenePath);

        Undo.SetCurrentGroupName("Instalar 4F UI de Personal");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            // 4F se apoya en 4E y no debe ocultar un gate previo roto.
            BistroBuilderStaff4EValidatorV2.Result validation4E =
                BistroBuilderStaff4EValidatorV2.ValidateCurrentScene();
            if (validation4E.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    "4E todavía no supera su validación estructural. " +
                    "Corrige esos errores antes de instalar Presentation.");
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

            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems en la escena.");
            }

            BistroBuilderStaffService staff =
                RequireUnique<BistroBuilderStaffService>(scene);
            BistroBuilderStaffRecruitmentService recruitment =
                RequireUnique<BistroBuilderStaffRecruitmentService>(scene);
            BistroBuilderStaffDevelopmentService development =
                RequireUnique<BistroBuilderStaffDevelopmentService>(scene);
            BistroBuilderStaffSessionService session =
                RequireUnique<BistroBuilderStaffSessionService>(scene);

            BistroBuilderStaffPlayerFacade facade =
                EnsureUniqueComponent<BistroBuilderStaffPlayerFacade>(
                    scene,
                    gameSystems);
            AssignObject(facade, "staffService", staff);
            AssignObject(facade, "recruitmentService", recruitment);
            AssignObject(facade, "developmentService", development);
            AssignObject(facade, "sessionService", session);

            GameObject controller = FindDirectRoot(scene, UiControllerName);
            if (controller == null)
            {
                controller = new GameObject(UiControllerName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(
                    controller,
                    "Crear UI de Personal 4F");
                SceneManager.MoveGameObjectToScene(controller, scene);
            }

            RectTransform controllerRect = controller.GetComponent<RectTransform>();
            StretchFull(controllerRect);

            Canvas canvas = EnsureComponent<Canvas>(controller);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = EnsureComponent<CanvasScaler>(controller);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureComponent<GraphicRaycaster>(controller);

            BistroBuilderStaffPlayerScreen screen =
                EnsureComponent<BistroBuilderStaffPlayerScreen>(controller);

            // La UI se reconstruye de forma determinista para que una segunda
            // ejecución no duplique nodos ni conserve wiring obsoleto.
            Transform existingPanel = controller.transform.Find(PanelRootName);
            if (existingPanel != null)
            {
                Undo.DestroyObjectImmediate(existingPanel.gameObject);
            }
            Transform existingLauncher = controller.transform.Find("OpenStaffButton");
            if (existingLauncher != null)
            {
                Undo.DestroyObjectImmediate(existingLauncher.gameObject);
            }
            Transform existingTemplates = controller.transform.Find("Templates");
            if (existingTemplates != null)
            {
                Undo.DestroyObjectImmediate(existingTemplates.gameObject);
            }

            GameObject templates = CreateUiObject(
                "Templates",
                controller.transform,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero);
            templates.SetActive(false);

            BistroBuilderStaffPlayerEmployeeRowView employeeTemplate =
                BuildEmployeeRowTemplate(templates.transform);
            BistroBuilderStaffPlayerCandidateRowView candidateTemplate =
                BuildCandidateRowTemplate(templates.transform);

            GameObject panelRoot = BuildPanel(
                controller.transform,
                facade,
                screen,
                employeeTemplate,
                candidateTemplate);

            Button launcher = BuildLauncher(controller.transform);
            UnityEventTools.RemovePersistentListeners(launcher.onClick);
            UnityEventTools.AddPersistentListener(launcher.onClick, screen.Show);

            // La pantalla comienza cerrada, pero el componente controlador
            // permanece activo y el botón launcher puede abrirla en Play Mode.
            panelRoot.SetActive(false);

            EnsureEventSystemWarning(scene);

            if (!facade.ValidateConfiguration(out string facadeError))
            {
                throw new InvalidOperationException(
                    "StaffPlayerFacade inválida tras instalación: " + facadeError);
            }
            if (!screen.ValidateConfiguration(out string screenError))
            {
                throw new InvalidOperationException(
                    "StaffPlayerScreen inválida tras instalación: " + screenError);
            }

            EditorUtility.SetDirty(facade);
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 4F.");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderStaff4FValidationResult validation4F =
                BistroBuilderStaff4FValidator.ValidateCurrentScene();
            bool finalStaticOk = BistroBuilderStaff4FStaticSelfTest.Run(
                out int finalPassed,
                out int finalFailed,
                out string finalReport);

            Debug.Log(validation4F.BuildReport());
            Debug.Log(finalReport);

            if (validation4F.ErrorCount > 0 || !finalStaticOk)
            {
                throw new InvalidOperationException(
                    "Los gates 4F no fueron limpios. Validación: " +
                    validation4F.ErrorCount + " errores; autotest: " +
                    finalFailed + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Personal",
                "UI 4F instalada correctamente.\n\n" +
                "Validación: " + validation4F.CorrectCount + " OK / " +
                validation4F.WarningCount + " avisos / 0 errores\n" +
                "Autotest estático: " + finalPassed + " OK / 0 fallos\n\n" +
                "Pendiente: prueba visual y funcional real en Play Mode.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllBytes(absoluteScenePath, sceneBackup);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Personal",
                "La instalación 4F falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static GameObject BuildPanel(
        Transform parent,
        BistroBuilderStaffPlayerFacade facade,
        BistroBuilderStaffPlayerScreen screen,
        BistroBuilderStaffPlayerEmployeeRowView employeeTemplate,
        BistroBuilderStaffPlayerCandidateRowView candidateTemplate)
    {
        GameObject root = CreateUiObject(
            PanelRootName,
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        Image background = EnsureComponent<Image>(root);
        background.color = new Color(0.045f, 0.052f, 0.06f, 0.985f);
        CanvasGroup canvasGroup = EnsureComponent<CanvasGroup>(root);

        GameObject header = CreateUiObject(
            "Header",
            root.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(24f, -76f),
            new Vector2(-24f, -18f));
        TMP_Text title = AddText(
            header.transform,
            "Title",
            "PERSONAL",
            30f,
            TextAlignmentOptions.Left);
        SetAnchors(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.35f, 1f),
            Vector2.zero, Vector2.zero);
        TMP_Text summary = AddText(
            header.transform,
            "Summary",
            string.Empty,
            18f,
            TextAlignmentOptions.Center);
        SetAnchors(summary.rectTransform, new Vector2(0.32f, 0f), new Vector2(0.82f, 1f),
            Vector2.zero, Vector2.zero);
        Button close = AddButton(
            header.transform,
            "CloseButton",
            "Cerrar",
            new Vector2(0.86f, 0.1f),
            new Vector2(1f, 0.9f));

        GameObject tabs = CreateUiObject(
            "Tabs",
            root.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(24f, -126f),
            new Vector2(-24f, -82f));
        Button staffTab = AddButton(
            tabs.transform,
            "StaffTab",
            "Plantilla",
            new Vector2(0f, 0f),
            new Vector2(0.18f, 1f));
        Button candidateTab = AddButton(
            tabs.transform,
            "CandidatesTab",
            "Candidatos",
            new Vector2(0.19f, 0f),
            new Vector2(0.37f, 1f));
        TMP_Text feedback = AddText(
            tabs.transform,
            "Feedback",
            string.Empty,
            16f,
            TextAlignmentOptions.Right);
        SetAnchors(feedback.rectTransform,
            new Vector2(0.40f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        GameObject staffPanel = CreateUiObject(
            "StaffPanel",
            root.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(24f, 24f),
            new Vector2(-24f, -136f));
        RectTransform employeeContent = BuildScrollArea(
            staffPanel.transform,
            "EmployeeList",
            new Vector2(0f, 0f),
            new Vector2(0.40f, 1f),
            out _);
        GameObject employeeDetail = BuildDetailPanel(
            staffPanel.transform,
            "EmployeeDetail",
            new Vector2(0.42f, 0f),
            new Vector2(1f, 1f));

        TMP_Text employeeName = AddDetailText(employeeDetail.transform, "Name", 0.88f, 1f, 28f);
        TMP_Text employeeRole = AddDetailText(employeeDetail.transform, "Role", 0.80f, 0.88f, 19f);
        TMP_Text employeeContract = AddDetailText(employeeDetail.transform, "Contract", 0.70f, 0.80f, 17f);
        TMP_Text employeeProgress = AddDetailText(employeeDetail.transform, "Progress", 0.60f, 0.70f, 17f);
        TMP_Text employeeSkills = AddDetailText(employeeDetail.transform, "Skills", 0.43f, 0.60f, 17f);
        TMP_Text employeePerformance = AddDetailText(employeeDetail.transform, "Performance", 0.25f, 0.43f, 17f);
        TMP_Text employeeSession = AddDetailText(employeeDetail.transform, "Session", 0.16f, 0.25f, 17f);
        Button availability = AddButton(
            employeeDetail.transform,
            "AvailabilityButton",
            "Disponibilidad",
            new Vector2(0f, 0.03f),
            new Vector2(0.46f, 0.13f));
        TMP_Text availabilityText = availability.GetComponentInChildren<TMP_Text>(true);
        Button dismiss = AddButton(
            employeeDetail.transform,
            "DismissButton",
            "Despedir",
            new Vector2(0.54f, 0.03f),
            new Vector2(1f, 0.13f));

        GameObject candidatePanel = CreateUiObject(
            "CandidatesPanel",
            root.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(24f, 24f),
            new Vector2(-24f, -136f));
        RectTransform candidateContent = BuildScrollArea(
            candidatePanel.transform,
            "CandidateList",
            new Vector2(0f, 0f),
            new Vector2(0.40f, 1f),
            out _);
        GameObject candidateDetail = BuildDetailPanel(
            candidatePanel.transform,
            "CandidateDetail",
            new Vector2(0.42f, 0f),
            new Vector2(1f, 1f));
        TMP_Text candidateName = AddDetailText(candidateDetail.transform, "Name", 0.86f, 1f, 28f);
        TMP_Text candidateRole = AddDetailText(candidateDetail.transform, "Role", 0.76f, 0.86f, 19f);
        TMP_Text candidateProfile = AddDetailText(candidateDetail.transform, "Profile", 0.62f, 0.76f, 17f);
        TMP_Text candidateSkills = AddDetailText(candidateDetail.transform, "Skills", 0.40f, 0.62f, 17f);
        TMP_Text candidateSalary = AddDetailText(candidateDetail.transform, "Salary", 0.25f, 0.40f, 20f);
        Button hire = AddButton(
            candidateDetail.transform,
            "HireButton",
            "Contratar",
            new Vector2(0f, 0.03f),
            new Vector2(0.46f, 0.13f));
        Button refresh = AddButton(
            candidateDetail.transform,
            "RefreshButton",
            "Refrescar mercado",
            new Vector2(0.54f, 0.03f),
            new Vector2(1f, 0.13f));

        GameObject confirmation = CreateUiObject(
            "Confirmation",
            root.transform,
            new Vector2(0.28f, 0.34f),
            new Vector2(0.72f, 0.66f),
            Vector2.zero,
            Vector2.zero);
        Image confirmBackground = EnsureComponent<Image>(confirmation);
        confirmBackground.color = new Color(0.09f, 0.10f, 0.11f, 1f);
        TMP_Text confirmationText = AddText(
            confirmation.transform,
            "Message",
            string.Empty,
            20f,
            TextAlignmentOptions.Center);
        SetAnchors(confirmationText.rectTransform,
            new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.90f), Vector2.zero, Vector2.zero);
        Button confirmAccept = AddButton(
            confirmation.transform,
            "Accept",
            "Confirmar",
            new Vector2(0.08f, 0.10f),
            new Vector2(0.46f, 0.30f));
        Button confirmCancel = AddButton(
            confirmation.transform,
            "Cancel",
            "Cancelar",
            new Vector2(0.54f, 0.10f),
            new Vector2(0.92f, 0.30f));

        AssignObject(screen, "facade", facade);
        AssignObject(screen, "panelRoot", root);
        AssignObject(screen, "canvasGroup", canvasGroup);
        AssignObject(screen, "closeButton", close);
        AssignObject(screen, "staffTabButton", staffTab);
        AssignObject(screen, "candidatesTabButton", candidateTab);
        AssignObject(screen, "headerSummaryText", summary);
        AssignObject(screen, "feedbackText", feedback);
        AssignObject(screen, "staffPanel", staffPanel);
        AssignObject(screen, "employeeListContent", employeeContent);
        AssignObject(screen, "employeeRowPrefab", employeeTemplate);
        AssignObject(screen, "employeeNameText", employeeName);
        AssignObject(screen, "employeeRoleText", employeeRole);
        AssignObject(screen, "employeeContractText", employeeContract);
        AssignObject(screen, "employeeProgressText", employeeProgress);
        AssignObject(screen, "employeeSkillsText", employeeSkills);
        AssignObject(screen, "employeePerformanceText", employeePerformance);
        AssignObject(screen, "employeeSessionText", employeeSession);
        AssignObject(screen, "toggleAvailabilityButton", availability);
        AssignObject(screen, "toggleAvailabilityButtonText", availabilityText);
        AssignObject(screen, "dismissButton", dismiss);
        AssignObject(screen, "candidatesPanel", candidatePanel);
        AssignObject(screen, "candidateListContent", candidateContent);
        AssignObject(screen, "candidateRowPrefab", candidateTemplate);
        AssignObject(screen, "candidateNameText", candidateName);
        AssignObject(screen, "candidateRoleText", candidateRole);
        AssignObject(screen, "candidateProfileText", candidateProfile);
        AssignObject(screen, "candidateSkillsText", candidateSkills);
        AssignObject(screen, "candidateSalaryText", candidateSalary);
        AssignObject(screen, "hireButton", hire);
        AssignObject(screen, "refreshCandidatesButton", refresh);
        AssignObject(screen, "confirmationPanel", confirmation);
        AssignObject(screen, "confirmationText", confirmationText);
        AssignObject(screen, "confirmationAcceptButton", confirmAccept);
        AssignObject(screen, "confirmationCancelButton", confirmCancel);

        return root;
    }

    private static Button BuildLauncher(Transform parent)
    {
        GameObject go = CreateUiObject(
            "OpenStaffButton",
            parent,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-222f, -70f),
            new Vector2(-24f, -22f));
        Image image = EnsureComponent<Image>(go);
        image.color = new Color(0.13f, 0.16f, 0.18f, 0.96f);
        Button button = EnsureComponent<Button>(go);
        button.targetGraphic = image;
        TMP_Text text = AddText(
            go.transform,
            "Label",
            "Personal",
            20f,
            TextAlignmentOptions.Center);
        StretchFull(text.rectTransform);
        return button;
    }

    private static BistroBuilderStaffPlayerEmployeeRowView BuildEmployeeRowTemplate(
        Transform parent)
    {
        GameObject row = CreateUiObject(
            "EmployeeRowTemplate",
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        LayoutElement layout = EnsureComponent<LayoutElement>(row);
        layout.preferredHeight = 76f;
        Image image = EnsureComponent<Image>(row);
        image.color = new Color(0.09f, 0.105f, 0.12f, 0.98f);
        Button button = EnsureComponent<Button>(row);
        button.targetGraphic = image;
        BistroBuilderStaffPlayerEmployeeRowView view =
            EnsureComponent<BistroBuilderStaffPlayerEmployeeRowView>(row);

        TMP_Text name = AddRowText(row.transform, "Name", 0.02f, 0.28f, 18f);
        TMP_Text role = AddRowText(row.transform, "Role", 0.29f, 0.47f, 15f);
        TMP_Text status = AddRowText(row.transform, "Status", 0.48f, 0.65f, 15f);
        TMP_Text level = AddRowText(row.transform, "Level", 0.66f, 0.79f, 15f);
        TMP_Text salary = AddRowText(row.transform, "Salary", 0.80f, 0.98f, 15f);

        AssignObject(view, "selectButton", button);
        AssignObject(view, "nameText", name);
        AssignObject(view, "roleText", role);
        AssignObject(view, "statusText", status);
        AssignObject(view, "levelText", level);
        AssignObject(view, "salaryText", salary);
        row.SetActive(false);
        return view;
    }

    private static BistroBuilderStaffPlayerCandidateRowView BuildCandidateRowTemplate(
        Transform parent)
    {
        GameObject row = CreateUiObject(
            "CandidateRowTemplate",
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        LayoutElement layout = EnsureComponent<LayoutElement>(row);
        layout.preferredHeight = 76f;
        Image image = EnsureComponent<Image>(row);
        image.color = new Color(0.09f, 0.105f, 0.12f, 0.98f);
        Button button = EnsureComponent<Button>(row);
        button.targetGraphic = image;
        BistroBuilderStaffPlayerCandidateRowView view =
            EnsureComponent<BistroBuilderStaffPlayerCandidateRowView>(row);

        TMP_Text name = AddRowText(row.transform, "Name", 0.02f, 0.32f, 18f);
        TMP_Text role = AddRowText(row.transform, "Role", 0.33f, 0.52f, 15f);
        TMP_Text profile = AddRowText(row.transform, "Profile", 0.53f, 0.71f, 15f);
        TMP_Text salary = AddRowText(row.transform, "Salary", 0.72f, 0.98f, 15f);

        AssignObject(view, "selectButton", button);
        AssignObject(view, "nameText", name);
        AssignObject(view, "roleText", role);
        AssignObject(view, "profileText", profile);
        AssignObject(view, "salaryText", salary);
        row.SetActive(false);
        return view;
    }

    private static RectTransform BuildScrollArea(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        out ScrollRect scrollRect)
    {
        GameObject scroll = CreateUiObject(
            name,
            parent,
            anchorMin,
            anchorMax,
            Vector2.zero,
            Vector2.zero);
        Image background = EnsureComponent<Image>(scroll);
        background.color = new Color(0.065f, 0.075f, 0.085f, 0.98f);
        scrollRect = EnsureComponent<ScrollRect>(scroll);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        GameObject viewport = CreateUiObject(
            "Viewport",
            scroll.transform,
            Vector2.zero,
            Vector2.one,
            new Vector2(8f, 8f),
            new Vector2(-8f, -8f));
        Image viewportImage = EnsureComponent<Image>(viewport);
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        EnsureComponent<RectMask2D>(viewport);

        GameObject content = CreateUiObject(
            "Content",
            viewport.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            Vector2.zero,
            Vector2.zero);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.pivot = new Vector2(0.5f, 1f);
        VerticalLayoutGroup layout = EnsureComponent<VerticalLayoutGroup>(content);
        layout.spacing = 6f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = EnsureComponent<ContentSizeFitter>(content);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        return contentRect;
    }

    private static GameObject BuildDetailPanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject panel = CreateUiObject(
            name,
            parent,
            anchorMin,
            anchorMax,
            Vector2.zero,
            Vector2.zero);
        Image image = EnsureComponent<Image>(panel);
        image.color = new Color(0.07f, 0.08f, 0.09f, 0.98f);
        return panel;
    }

    private static TMP_Text AddDetailText(
        Transform parent,
        string name,
        float minY,
        float maxY,
        float fontSize)
    {
        TMP_Text text = AddText(
            parent,
            name,
            string.Empty,
            fontSize,
            TextAlignmentOptions.TopLeft);
        SetAnchors(
            text.rectTransform,
            new Vector2(0.04f, minY),
            new Vector2(0.96f, maxY),
            Vector2.zero,
            Vector2.zero);
        return text;
    }

    private static TMP_Text AddRowText(
        Transform parent,
        string name,
        float minX,
        float maxX,
        float fontSize)
    {
        TMP_Text text = AddText(
            parent,
            name,
            string.Empty,
            fontSize,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(
            text.rectTransform,
            new Vector2(minX, 0f),
            new Vector2(maxX, 1f),
            new Vector2(2f, 2f),
            new Vector2(-2f, -2f));
        return text;
    }

    private static Button AddButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject go = CreateUiObject(
            name,
            parent,
            anchorMin,
            anchorMax,
            Vector2.zero,
            Vector2.zero);
        Image image = EnsureComponent<Image>(go);
        image.color = new Color(0.16f, 0.19f, 0.21f, 1f);
        Button button = EnsureComponent<Button>(go);
        button.targetGraphic = image;
        TMP_Text text = AddText(
            go.transform,
            "Label",
            label,
            17f,
            TextAlignmentOptions.Center);
        StretchFull(text.rectTransform);
        return button;
    }

    private static TMP_Text AddText(
        Transform parent,
        string name,
        string value,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateUiObject(
            name,
            parent,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        TextMeshProUGUI text = EnsureComponent<TextMeshProUGUI>(go);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.92f, 0.92f, 0.90f, 1f);
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetAnchors(rect, anchorMin, anchorMax, offsetMin, offsetMax);
        return go;
    }

    private static void SetAnchors(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void StretchFull(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(gameObject);
    }

    private static T EnsureUniqueComponent<T>(
        Scene scene,
        GameObject owner)
        where T : Component
    {
        T[] existing = FindSceneComponents<T>(scene);
        if (existing.Length > 1)
        {
            throw new InvalidOperationException(
                "La escena contiene varias instancias de " + typeof(T).Name + ".");
        }
        if (existing.Length == 1)
        {
            if (existing[0].gameObject != owner)
            {
                throw new InvalidOperationException(
                    typeof(T).Name + " existe fuera de su propietario canónico.");
            }
            return existing[0];
        }
        return Undo.AddComponent<T>(owner);
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] values = FindSceneComponents<T>(scene);
        if (values.Length != 1)
        {
            throw new InvalidOperationException(
                "4F necesita exactamente una instancia de " +
                typeof(T).Name + "; hay " + values.Length + ".");
        }
        return values[0];
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
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

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        GameObject found = null;
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform[] transforms =
                roots[index].GetComponentsInChildren<Transform>(true);
            for (int child = 0; child < transforms.Length; child++)
            {
                if (transforms[child] != null &&
                    string.Equals(
                        transforms[child].name,
                        "GameSystems",
                        StringComparison.Ordinal))
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

    private static void AssignObject(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + propertyName +
                " en " + target.GetType().Name + ".");
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureEventSystemWarning(Scene scene)
    {
        EventSystem[] eventSystems = FindSceneComponents<EventSystem>(scene);
        if (eventSystems.Length == 0)
        {
            Debug.LogWarning(
                "4F: no existe EventSystem en la escena. La jerarquía de " +
                "Personal está instalada, pero los botones necesitarán el " +
                "EventSystem canónico del proyecto para recibir input.");
        }
        else if (eventSystems.Length > 1)
        {
            Debug.LogWarning(
                "4F: la escena contiene varios EventSystem. Personal no los " +
                "modifica; conviene consolidarlos antes de la prueba visual.");
        }
    }
}
