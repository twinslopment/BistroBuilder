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
/// Instalador acumulativo y transaccional de Reputación 8A-8G.
/// </summary>
public static class BistroBuilderReputationBlock8Installer
{
    private const string UiRootName = "BistroBuilderReputationUI";

    [MenuItem("Tools/Bistro Builder/Reputation/8G - Instalar bloque completo", false, 8193)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report)) Debug.LogError(report);
        else Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Reputación", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(BistroBuilderMarketing7APaths.MainScene, OpenSceneMode.Single);
        if (!TryInstall(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar Reputación.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar Reputación.";
            return false;
        }

        if (!BistroBuilderReputationBlock8SelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo del Bloque 8 falló: " + prePassed +
                     " OK / " + preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] backup = File.ReadAllBytes(absoluteScene);
        Undo.SetCurrentGroupName("Instalar Reputación 8A-8G");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            GameObject host = FindUniqueNamedObject(scene, "GameSystems");
            if (host == null)
                throw new InvalidOperationException("No existe un GameSystems canónico único.");

            BistroBuilderSaveGameService save = RequireUnique<BistroBuilderSaveGameService>(scene);
            BistroBuilderReputationService reputation = RequireUnique<BistroBuilderReputationService>(scene);
            BistroBuilderReputationSaveSectionProvider stateProvider =
                RequireUnique<BistroBuilderReputationSaveSectionProvider>(scene);
            BistroBuilderGuestRelationsService relations = RequireUnique<BistroBuilderGuestRelationsService>(scene);
            TableAssignmentSystem tables = RequireUnique<TableAssignmentSystem>(scene);
            OrderSystem orders = RequireUnique<OrderSystem>(scene);
            BistroBuilderCanonicalOrderService canonical =
                RequireUnique<BistroBuilderCanonicalOrderService>(scene);
            BistroBuilderFinanceService finance = RequireUnique<BistroBuilderFinanceService>(scene);
            BistroBuilderDishCatalogService dishes = RequireUnique<BistroBuilderDishCatalogService>(scene);
            BistroBuilderGeneralGameStateService general =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            BistroBuilderMarketingDemandIntegrationService demand =
                RequireUnique<BistroBuilderMarketingDemandIntegrationService>(scene);

            BistroBuilderCustomerExperienceTrackingService tracking =
                EnsureUniqueOnHost<BistroBuilderCustomerExperienceTrackingService>(scene, host);
            BistroBuilderReputationRuntimeSaveSectionProvider runtimeProvider =
                EnsureUniqueOnHost<BistroBuilderReputationRuntimeSaveSectionProvider>(scene, host);
            BistroBuilderReputationPlayerFacade facade =
                EnsureUniqueOnHost<BistroBuilderReputationPlayerFacade>(scene, host);

            Assign(tracking, "reputationService", reputation);
            Assign(tracking, "tableAssignmentSystem", tables);
            Assign(tracking, "orderSystem", orders);
            Assign(tracking, "canonicalOrderService", canonical);
            Assign(tracking, "financeService", finance);
            Assign(tracking, "dishCatalogService", dishes);
            Assign(tracking, "generalGameStateService", general);

            Assign(runtimeProvider, "saveGameService", save);
            Assign(runtimeProvider, "trackingService", tracking);

            Assign(facade, "reputationService", reputation);
            Assign(facade, "guestRelationsService", relations);
            Assign(facade, "trackingService", tracking);
            Assign(facade, "generalGameStateService", general);

            save.RefreshExtensions();
            if (!tracking.ValidateConfiguration(out string trackingError))
                throw new InvalidOperationException(trackingError);
            if (!runtimeProvider.ValidateConfiguration(out string runtimeError))
                throw new InvalidOperationException(runtimeError);
            if (!facade.ValidateConfiguration(out string facadeError))
                throw new InvalidOperationException(facadeError);
            if (!demand.ValidateConfiguration(out string demandError))
                throw new InvalidOperationException(demandError);
            if (!save.HasProvider(BistroBuilderReputationSaveSectionProvider.StableSectionId) ||
                !save.HasProvider(BistroBuilderReputationRuntimeSaveSectionProvider.StableSectionId))
                throw new InvalidOperationException("SaveGame no descubre las dos secciones de Reputación.");

            BuildUi(scene, facade);

            EditorUtility.SetDirty(tracking);
            EditorUtility.SetDirty(runtimeProvider);
            EditorUtility.SetDirty(facade);
            EditorUtility.SetDirty(save);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación del Bloque 8.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderReputationBlock8ValidationResult validation =
                BistroBuilderReputationBlock8Validator.ValidateCurrentScene();
            bool selfOk = BistroBuilderReputationBlock8SelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException("Bloque 8 no superó gates: " +
                    validation.Errors + " errores / " + failed + " fallos.");

            report = "Bloque 8 — Reputación instalado correctamente.\n" +
                     validation.BuildReport() + "\nAutotest: " + passed +
                     " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try
            {
                File.WriteAllBytes(absoluteScene, backup);
                AssetDatabase.ImportAsset(scene.path, ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackError) { Debug.LogException(rollbackError); }
            report = "La instalación del Bloque 8 falló y la escena fue restaurada. " +
                     exception.Message;
            return false;
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static void BuildUi(Scene scene, BistroBuilderReputationPlayerFacade facade)
    {
        GameObject previous = FindDirectRoot(scene, UiRootName);
        if (previous != null) Undo.DestroyObjectImmediate(previous);

        GameObject ui = NewUi(UiRootName, null);
        SceneManager.MoveGameObjectToScene(ui, scene);
        Stretch(ui.GetComponent<RectTransform>());
        Canvas canvas = Add<Canvas>(ui);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 46;
        CanvasScaler scaler = Add<CanvasScaler>(ui);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Add<GraphicRaycaster>(ui);

        BistroBuilderReputationPlayerScreen screen = Add<BistroBuilderReputationPlayerScreen>(ui);
        GameObject root = NewUi("ReputationModal", ui.transform);
        Stretch(root.GetComponent<RectTransform>());
        Add<Image>(root).color = new Color(0.025f, 0.03f, 0.028f, 0.995f);
        CanvasGroup group = Add<CanvasGroup>(root);

        CreateText(root.transform, "Title", "REPUTACIÓN", 31f,
            0.025f, 0.925f, 0.25f, 0.98f, FontStyles.Bold);
        TMP_Text summary = CreateText(root.transform, "Summary", string.Empty, 16f,
            0.25f, 0.94f, 0.75f, 0.98f);
        Button close = CreateButton(root.transform, "Close", "Cerrar",
            0.865f, 0.93f, 0.975f, 0.98f, SurfaceDanger);

        GameObject headline = CreatePanel(root.transform, "Headline",
            0.025f, 0.79f, 0.975f, 0.91f, SurfacePanel);
        TMP_Text global = CreateText(headline.transform, "Global", string.Empty, 28f,
            0.025f, 0.54f, 0.38f, 0.94f, FontStyles.Bold);
        TMP_Text satisfaction = CreateText(headline.transform, "Satisfaction", string.Empty, 16f,
            0.39f, 0.55f, 0.69f, 0.94f);
        TMP_Text demand = CreateText(headline.transform, "Demand", string.Empty, 15f,
            0.025f, 0.08f, 0.94f, 0.48f);

        GameObject aspects = CreatePanel(root.transform, "Aspects",
            0.025f, 0.59f, 0.975f, 0.765f, SurfacePanel);
        TMP_Text service = Aspect(aspects.transform, "Service", 0.015f, 0.195f);
        TMP_Text waiting = Aspect(aspects.transform, "Waiting", 0.205f, 0.385f);
        TMP_Text food = Aspect(aspects.transform, "Food", 0.395f, 0.575f);
        TMP_Text value = Aspect(aspects.transform, "Value", 0.585f, 0.765f);
        TMP_Text ambience = Aspect(aspects.transform, "Ambience", 0.775f, 0.985f);

        GameObject left = CreatePanel(root.transform, "ExperiencePanel",
            0.025f, 0.20f, 0.485f, 0.565f, SurfacePanel);
        TMP_Text experience = CreateText(left.transform, "Experience", string.Empty, 17f,
            0.04f, 0.68f, 0.96f, 0.95f, FontStyles.Bold);
        experience.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text habitual = CreateText(left.transform, "Habitual", string.Empty, 16f,
            0.04f, 0.38f, 0.96f, 0.66f);
        habitual.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text discovery = CreateText(left.transform, "Discovery", string.Empty, 15f,
            0.04f, 0.05f, 0.96f, 0.36f);
        discovery.textWrappingMode = TextWrappingModes.Normal;

        GameObject right = CreatePanel(root.transform, "ReviewsPanel",
            0.505f, 0.20f, 0.975f, 0.565f, SurfacePanel);
        TMP_Text reviews = CreateText(right.transform, "Reviews", string.Empty, 15f,
            0.045f, 0.05f, 0.955f, 0.95f, FontStyles.Normal);
        reviews.textWrappingMode = TextWrappingModes.Normal;
        reviews.alignment = TextAlignmentOptions.TopLeft;

        TMP_Text feedback = CreateText(root.transform, "Feedback", string.Empty, 14f,
            0.025f, 0.12f, 0.975f, 0.18f);
        feedback.textWrappingMode = TextWrappingModes.Normal;

        Assign(screen, "facade", facade);
        Assign(screen, "panelRoot", root);
        Assign(screen, "canvasGroup", group);
        Assign(screen, "closeButton", close);
        Assign(screen, "summaryText", summary);
        Assign(screen, "globalScoreText", global);
        Assign(screen, "satisfactionText", satisfaction);
        Assign(screen, "demandText", demand);
        Assign(screen, "serviceAspectText", service);
        Assign(screen, "waitingAspectText", waiting);
        Assign(screen, "foodAspectText", food);
        Assign(screen, "valueAspectText", value);
        Assign(screen, "ambienceAspectText", ambience);
        Assign(screen, "experienceText", experience);
        Assign(screen, "habitualText", habitual);
        Assign(screen, "discoveryText", discovery);
        Assign(screen, "reviewsText", reviews);
        Assign(screen, "feedbackText", feedback);

        Button launcher = CreateButton(ui.transform, "OpenReputationButton", "Reputación",
            0.885f, 0.925f, 0.985f, 0.975f, SurfaceAccent);
        UnityEventTools.AddPersistentListener(launcher.onClick, screen.Show);
        launcher.transform.SetAsFirstSibling();
        root.SetActive(false);

        if (!screen.ValidateConfiguration(out string screenError))
            throw new InvalidOperationException(screenError);
        EditorUtility.SetDirty(screen);
        EditorUtility.SetDirty(ui);
    }

    private static TMP_Text Aspect(Transform parent, string name, float minX, float maxX)
    {
        GameObject panel = CreatePanel(parent, name + "Card", minX, 0.10f, maxX, 0.90f, SurfaceInset);
        TMP_Text text = CreateText(panel.transform, name + "Text", string.Empty, 16f,
            0.04f, 0.05f, 0.96f, 0.95f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static readonly Color SurfacePanel = new Color(0.06f, 0.07f, 0.065f, 0.99f);
    private static readonly Color SurfaceInset = new Color(0.09f, 0.105f, 0.095f, 1f);
    private static readonly Color SurfaceAccent = new Color(0.28f, 0.40f, 0.27f, 1f);
    private static readonly Color SurfaceDanger = new Color(0.30f, 0.15f, 0.14f, 1f);

    private static GameObject CreatePanel(Transform parent, string name,
        float minX, float minY, float maxX, float maxY, Color color)
    {
        GameObject panel = NewUi(name, parent);
        Anchor(panel.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Add<Image>(panel).color = color;
        return panel;
    }

    private static Button CreateButton(Transform parent, string name, string label,
        float minX, float minY, float maxX, float maxY, Color color)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Image image = Add<Image>(go); image.color = color;
        Button button = Add<Button>(go); button.targetGraphic = image;
        TMP_Text text = CreateText(go.transform, "Label", label, 15f,
            0.02f, 0.04f, 0.98f, 0.96f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value,
        float size, float minX, float minY, float maxX, float maxY,
        FontStyles style = FontStyles.Normal)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        TextMeshProUGUI text = Add<TextMeshProUGUI>(go);
        text.text = value; text.fontSize = size; text.fontStyle = style;
        text.color = new Color(0.92f, 0.93f, 0.90f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect) => Anchor(rect, 0f, 0f, 1f, 1f);
    private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T Add<T>(GameObject owner) where T : Component
    {
        T current = owner.GetComponent<T>();
        return current != null ? current : Undo.AddComponent<T>(owner);
    }

    private static T EnsureUniqueOnHost<T>(Scene scene, GameObject host) where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length > 1)
            throw new InvalidOperationException("Hay varias instancias de " + typeof(T).Name + ".");
        T component = values.Length == 1 ? values[0] : Undo.AddComponent<T>(host);
        if (component.gameObject != host)
            throw new InvalidOperationException(typeof(T).Name + " debe vivir en GameSystems.");
        return component;
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length != 1)
            throw new InvalidOperationException("Se esperaba exactamente un " +
                typeof(T).Name + "; hay " + values.Length + ".");
        return values[0];
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++) if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }

    private static GameObject FindUniqueNamedObject(Scene scene, string name)
    {
        GameObject found = null; int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (transform != null && string.Equals(transform.name, name, StringComparison.Ordinal))
            { found = transform.gameObject; count++; }
        return count == 1 ? found : null;
    }

    private static GameObject FindDirectRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root != null && string.Equals(root.name, name, StringComparison.Ordinal)) return root;
        return null;
    }

    private static void Assign(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + " no contiene " + fieldName + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
