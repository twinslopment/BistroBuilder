using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BistroBuilderProgression9EInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string UiRootName = "BistroBuilderProgressionUI";

    [MenuItem("Tools/Bistro Builder/Progression/9E - Instalar + validar", false, 9040)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report)) Debug.LogError(report);
        else Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Progresión 9E", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!TryInstall(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar 9E.";
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 9E.";
            return false;
        }
        if (!BistroBuilderProgression9ESelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 9E falló: " + prePassed +
                " OK / " + preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] backup = File.ReadAllBytes(absoluteScene);
        try
        {
            GameObject host = FindUniqueNamedObject(scene, "GameSystems");
            if (host == null)
                throw new InvalidOperationException("No existe un GameSystems canónico único.");
            BistroBuilderUpgradeService upgrades = RequireUnique<BistroBuilderUpgradeService>(scene);
            BistroBuilderProgressionMilestoneService milestones =
                RequireUnique<BistroBuilderProgressionMilestoneService>(scene);
            BistroBuilderGeneralGameStateService general =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            BistroBuilderReputationService reputation =
                RequireUnique<BistroBuilderReputationService>(scene);
            BistroBuilderFinanceService finance = RequireUnique<BistroBuilderFinanceService>(scene);
            BistroBuilderProgressionPlayerFacade facade =
                EnsureUniqueOnHost<BistroBuilderProgressionPlayerFacade>(scene, host);

            Assign(facade, "upgradeService", upgrades);
            Assign(facade, "milestoneService", milestones);
            Assign(facade, "generalGameStateService", general);
            Assign(facade, "reputationService", reputation);
            Assign(facade, "financeService", finance);

            if (!facade.ValidateConfiguration(out string facadeError))
                throw new InvalidOperationException(facadeError);

            BuildUi(scene, facade);
            EditorUtility.SetDirty(facade);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación 9E.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderProgression9EValidationResult validation =
                BistroBuilderProgression9EValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderProgression9ESelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException("9E no superó gates: " +
                    validation.Errors + " errores / " + failed + " fallos.");

            report = "9E — UI jugable instalada correctamente.\n" +
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
            report = "La instalación 9E falló y fue restaurada. " + exception.Message;
            return false;
        }
    }

    private static void BuildUi(Scene scene, BistroBuilderProgressionPlayerFacade facade)
    {
        GameObject previous = FindDirectRoot(scene, UiRootName);
        if (previous != null) Undo.DestroyObjectImmediate(previous);

        GameObject ui = NewUi(UiRootName, null);
        SceneManager.MoveGameObjectToScene(ui, scene);
        Stretch(ui.GetComponent<RectTransform>());
        Canvas canvas = Add<Canvas>(ui);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 47;
        CanvasScaler scaler = Add<CanvasScaler>(ui);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Add<GraphicRaycaster>(ui);

        BistroBuilderProgressionPlayerScreen screen =
            Add<BistroBuilderProgressionPlayerScreen>(ui);
        GameObject root = NewUi("ProgressionModal", ui.transform);
        Stretch(root.GetComponent<RectTransform>());
        Add<Image>(root).color = new Color(0.025f, 0.03f, 0.028f, 0.995f);
        CanvasGroup group = Add<CanvasGroup>(root);

        CreateText(root.transform, "Title", "MEJORAS Y PROGRESIÓN", 31f,
            0.025f, 0.925f, 0.36f, 0.98f, FontStyles.Bold);
        TMP_Text summary = CreateText(root.transform, "Summary", string.Empty, 16f,
            0.37f, 0.94f, 0.82f, 0.98f);
        Button close = CreateButton(root.transform, "Close", "Cerrar",
            0.865f, 0.93f, 0.975f, 0.98f, SurfaceDanger);

        GameObject milestonePanel = CreatePanel(root.transform, "MilestonePanel",
            0.025f, 0.835f, 0.975f, 0.915f, SurfacePanel);
        TMP_Text milestone = CreateText(milestonePanel.transform, "Milestone", string.Empty, 16f,
            0.025f, 0.08f, 0.975f, 0.92f, FontStyles.Bold);
        milestone.textWrappingMode = TextWrappingModes.Normal;

        float yMin = 0.775f, yMax = 0.825f;
        Button all = CreateButton(root.transform, "All", "Todas", 0.025f, yMin, 0.145f, yMax, SurfaceAccent);
        Button dining = CreateButton(root.transform, "Dining", "Sala", 0.15f, yMin, 0.27f, yMax, SurfaceInset);
        Button kitchen = CreateButton(root.transform, "Kitchen", "Cocina", 0.275f, yMin, 0.395f, yMax, SurfaceInset);
        Button terrace = CreateButton(root.transform, "Terrace", "Terraza", 0.40f, yMin, 0.52f, yMax, SurfaceInset);
        Button bar = CreateButton(root.transform, "Bar", "Barra", 0.525f, yMin, 0.645f, yMax, SurfaceInset);
        Button infrastructure = CreateButton(root.transform, "Infrastructure", "Infraestructura",
            0.65f, yMin, 0.80f, yMax, SurfaceInset);
        Button ambience = CreateButton(root.transform, "Ambience", "Ambiente",
            0.805f, yMin, 0.975f, yMax, SurfaceInset);

        GameObject listPanel = CreatePanel(root.transform, "UpgradeList",
            0.025f, 0.18f, 0.485f, 0.755f, SurfacePanel);
        TMP_Text list = CreateText(listPanel.transform, "List", string.Empty, 15f,
            0.04f, 0.13f, 0.96f, 0.95f);
        list.alignment = TextAlignmentOptions.TopLeft;
        list.textWrappingMode = TextWrappingModes.Normal;
        Button previousButton = CreateButton(listPanel.transform, "Previous", "◀ Anterior",
            0.04f, 0.03f, 0.47f, 0.105f, SurfaceInset);
        Button nextButton = CreateButton(listPanel.transform, "Next", "Siguiente ▶",
            0.53f, 0.03f, 0.96f, 0.105f, SurfaceInset);

        GameObject detailPanel = CreatePanel(root.transform, "UpgradeDetail",
            0.505f, 0.18f, 0.975f, 0.755f, SurfacePanel);
        TMP_Text detailName = CreateText(detailPanel.transform, "Name", string.Empty, 25f,
            0.045f, 0.84f, 0.955f, 0.96f, FontStyles.Bold);
        TMP_Text detailDescription = CreateText(detailPanel.transform, "Description", string.Empty, 16f,
            0.045f, 0.66f, 0.955f, 0.82f);
        detailDescription.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text detailMeta = CreateText(detailPanel.transform, "Meta", string.Empty, 15f,
            0.045f, 0.56f, 0.955f, 0.65f);
        TMP_Text detailEffects = CreateText(detailPanel.transform, "Effects", string.Empty, 17f,
            0.045f, 0.37f, 0.955f, 0.54f, FontStyles.Bold);
        detailEffects.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text detailRequirements = CreateText(detailPanel.transform, "Requirements", string.Empty, 15f,
            0.045f, 0.19f, 0.955f, 0.35f);
        detailRequirements.textWrappingMode = TextWrappingModes.Normal;
        Button buy = CreateButton(detailPanel.transform, "Buy", "Comprar",
            0.55f, 0.045f, 0.955f, 0.15f, SurfaceAccent);
        TMP_Text buyText = buy.GetComponentInChildren<TMP_Text>(true);

        TMP_Text feedback = CreateText(root.transform, "Feedback", string.Empty, 14f,
            0.025f, 0.105f, 0.975f, 0.16f);
        feedback.textWrappingMode = TextWrappingModes.Normal;

        Assign(screen, "facade", facade);
        Assign(screen, "panelRoot", root);
        Assign(screen, "canvasGroup", group);
        Assign(screen, "closeButton", close);
        Assign(screen, "allButton", all);
        Assign(screen, "diningButton", dining);
        Assign(screen, "kitchenButton", kitchen);
        Assign(screen, "terraceButton", terrace);
        Assign(screen, "barButton", bar);
        Assign(screen, "infrastructureButton", infrastructure);
        Assign(screen, "ambienceButton", ambience);
        Assign(screen, "previousButton", previousButton);
        Assign(screen, "nextButton", nextButton);
        Assign(screen, "buyButton", buy);
        Assign(screen, "buyButtonText", buyText);
        Assign(screen, "summaryText", summary);
        Assign(screen, "milestoneText", milestone);
        Assign(screen, "listText", list);
        Assign(screen, "detailNameText", detailName);
        Assign(screen, "detailDescriptionText", detailDescription);
        Assign(screen, "detailMetaText", detailMeta);
        Assign(screen, "detailEffectsText", detailEffects);
        Assign(screen, "detailRequirementsText", detailRequirements);
        Assign(screen, "feedbackText", feedback);

        Button launcher = CreateButton(ui.transform, "OpenProgressionButton", "Mejoras",
            0.765f, 0.925f, 0.875f, 0.975f, SurfaceAccent);
        UnityEventTools.AddPersistentListener(launcher.onClick, screen.Show);
        launcher.transform.SetAsFirstSibling();
        root.SetActive(false);

        if (!screen.ValidateConfiguration(out string screenError))
            throw new InvalidOperationException(screenError);
        EditorUtility.SetDirty(screen);
        EditorUtility.SetDirty(ui);
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
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
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
            if (root != null && string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
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

    private static bool TrySaveSceneWithRetry(Scene scene)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (EditorSceneManager.SaveScene(scene)) return true;
            Thread.Sleep(250 + attempt * 150);
        }
        return false;
    }
}
