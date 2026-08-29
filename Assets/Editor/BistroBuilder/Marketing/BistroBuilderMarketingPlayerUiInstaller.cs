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
/// Instalador transaccional e idempotente de la UI jugable de Marketing.
/// Solo crea Presentation y cablea autoridades ya existentes.
/// </summary>
public static class BistroBuilderMarketingPlayerUiInstaller
{
    private const string UiRootName = "BistroBuilderMarketingUI";

    [MenuItem("Tools/Bistro Builder/Marketing/UI jugable - Instalar + validar", false, 7262)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
            Debug.LogError(report);
        else
            Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Marketing", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(BistroBuilderMarketing7APaths.MainScene, OpenSceneMode.Single);
        if (!TryInstall(out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar la UI de Marketing.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar la UI.";
            return false;
        }

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] backup = File.ReadAllBytes(absoluteScene);
        Undo.SetCurrentGroupName("Instalar UI Marketing");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            GameObject gameSystems = FindUniqueNamedObject(scene, "GameSystems");
            if (gameSystems == null)
                throw new InvalidOperationException("No existe un GameSystems canónico único.");

            BistroBuilderMarketingPlayerFacade facade =
                EnsureUnique<BistroBuilderMarketingPlayerFacade>(scene, gameSystems);
            Assign(facade, "marketingService", RequireUnique<BistroBuilderMarketingService>(scene));
            Assign(facade, "guestRelationsService", RequireUnique<BistroBuilderGuestRelationsService>(scene));
            Assign(facade, "generalGameStateService", RequireUnique<BistroBuilderGeneralGameStateService>(scene));
            Assign(facade, "menuService", RequireUnique<BistroBuilderRestaurantMenuService>(scene));
            Assign(facade, "menuPortfolioService", RequireUnique<BistroBuilderMenuPortfolioService>(scene));
            Assign(facade, "dishCatalogService", RequireUnique<BistroBuilderDishCatalogService>(scene));

            GameObject previous = FindDirectRoot(scene, UiRootName);
            if (previous != null) Undo.DestroyObjectImmediate(previous);

            GameObject ui = NewUi(UiRootName, null);
            SceneManager.MoveGameObjectToScene(ui, scene);
            Stretch(ui.GetComponent<RectTransform>());
            Canvas canvas = Add<Canvas>(ui);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            CanvasScaler scaler = Add<CanvasScaler>(ui);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Add<GraphicRaycaster>(ui);

            BistroBuilderMarketingPlayerScreen screen = Add<BistroBuilderMarketingPlayerScreen>(ui);
            GameObject templates = NewUi("Templates", ui.transform);
            BistroBuilderMarketingPlayerRowView rowTemplate = BuildRowTemplate(templates.transform);
            templates.SetActive(false);

            GameObject root = NewUi("MarketingModal", ui.transform);
            Stretch(root.GetComponent<RectTransform>());
            Add<Image>(root).color = new Color(0.03f, 0.035f, 0.032f, 1f);
            CanvasGroup canvasGroup = Add<CanvasGroup>(root);

            CreateText(root.transform, "Title", "MARKETING", 30f,
                0.025f, 0.935f, 0.20f, 0.985f, FontStyles.Bold);
            TMP_Text summary = CreateText(root.transform, "Summary", string.Empty, 17f,
                0.22f, 0.945f, 0.67f, 0.98f);
            TMP_Text reputation = CreateText(root.transform, "Reputation", string.Empty, 15f,
                0.025f, 0.895f, 0.36f, 0.93f);
            TMP_Text recurring = CreateText(root.transform, "Recurring", string.Empty, 15f,
                0.37f, 0.895f, 0.67f, 0.93f);
            TMP_Text feedback = CreateText(root.transform, "Feedback", string.Empty, 14f,
                0.50f, 0.895f, 0.84f, 0.93f);
            Button close = CreateButton(root.transform, "Close", "Cerrar",
                0.86f, 0.935f, 0.975f, 0.98f, SurfaceDanger);

            Button catalogTab = CreateButton(root.transform, "CatalogTab", "Campañas",
                0.025f, 0.835f, 0.145f, 0.88f, SurfaceRaised);
            Button activeTab = CreateButton(root.transform, "ActiveTab", "Activas",
                0.155f, 0.835f, 0.275f, 0.88f, SurfaceRaised);
            TMP_InputField search = CreateInputField(root.transform, "Search", "Buscar campaña...",
                0.29f, 0.835f, 0.56f, 0.88f);
            Button filterAll = CreateButton(root.transform, "FilterAll", "Todas",
                0.025f, 0.775f, 0.125f, 0.815f, SurfaceRaised);
            Button filterLocal = CreateButton(root.transform, "FilterLocal", "Local",
                0.132f, 0.775f, 0.232f, 0.815f, SurfaceRaised);
            Button filterPromotions = CreateButton(root.transform, "FilterPromotions", "Promos",
                0.239f, 0.775f, 0.339f, 0.815f, SurfaceRaised);
            Button filterDigital = CreateButton(root.transform, "FilterDigital", "Digital",
                0.346f, 0.775f, 0.446f, 0.815f, SurfaceRaised);
            Button filterPress = CreateButton(root.transform, "FilterPress", "Prensa",
                0.453f, 0.775f, 0.553f, 0.815f, SurfaceRaised);
            Button filterEvents = CreateButton(root.transform, "FilterEvents", "Eventos",
                0.560f, 0.775f, 0.660f, 0.815f, SurfaceRaised);
            Button filterLoyalty = CreateButton(root.transform, "FilterLoyalty", "Fidelización",
                0.667f, 0.775f, 0.785f, 0.815f, SurfaceRaised);
            Button filterMenu = CreateButton(root.transform, "FilterMenu", "Carta / platos",
                0.792f, 0.775f, 0.975f, 0.815f, SurfaceRaised);

            GameObject listPanel = CreatePanel(root.transform, "CampaignListPanel",
                0.025f, 0.07f, 0.405f, 0.75f, SurfacePanel);
            RectTransform listContent = CreateScrollContent(
                listPanel.transform, "CampaignList", 0.02f, 0.065f, 0.98f, 0.98f);
            TMP_Text emptyState = CreateText(listPanel.transform, "EmptyState",
                "No hay campañas.", 16f, 0.08f, 0.42f, 0.92f, 0.58f);
            emptyState.alignment = TextAlignmentOptions.Center;
            GameObject detailPanel = CreatePanel(root.transform, "CampaignDetailPanel",
                0.425f, 0.07f, 0.975f, 0.75f, SurfacePanel);
            TMP_Text detailName = CreateText(detailPanel.transform, "DetailName", string.Empty,
                27f, 0.04f, 0.875f, 0.96f, 0.97f, FontStyles.Bold);
            TMP_Text detailFamily = CreateText(detailPanel.transform, "DetailFamily", string.Empty,
                16f, 0.04f, 0.82f, 0.96f, 0.875f);
            TMP_Text detailMeta = CreateText(detailPanel.transform, "DetailMeta", string.Empty,
                18f, 0.04f, 0.75f, 0.96f, 0.82f);
            TMP_Text detailDescription = CreateText(detailPanel.transform, "DetailDescription",
                string.Empty, 16f, 0.04f, 0.54f, 0.96f, 0.74f);
            detailDescription.textWrappingMode = TextWrappingModes.Normal;
            detailDescription.alignment = TextAlignmentOptions.TopLeft;
            TMP_Text detailEffects = CreateText(detailPanel.transform, "DetailEffects",
                string.Empty, 17f, 0.04f, 0.31f, 0.96f, 0.53f);
            detailEffects.textWrappingMode = TextWrappingModes.Normal;
            detailEffects.alignment = TextAlignmentOptions.TopLeft;
            TMP_Text detailRequirements = CreateText(detailPanel.transform, "DetailRequirements",
                string.Empty, 15f, 0.04f, 0.23f, 0.96f, 0.30f);
            detailRequirements.textWrappingMode = TextWrappingModes.Normal;

            GameObject targetPanel = CreatePanel(detailPanel.transform, "TargetPanel",
                0.04f, 0.12f, 0.64f, 0.22f, SurfaceInset);
            CreateText(targetPanel.transform, "TargetLabel", "Objetivo", 13f,
                0.03f, 0.58f, 0.22f, 0.94f, FontStyles.Bold);
            TMP_Text targetValue = CreateText(targetPanel.transform, "TargetValue", string.Empty,
                15f, 0.22f, 0.12f, 0.76f, 0.88f);
            Button targetPrevious = CreateButton(targetPanel.transform, "TargetPrevious", "‹",
                0.77f, 0.14f, 0.87f, 0.86f, SurfaceRaised);
            Button targetNext = CreateButton(targetPanel.transform, "TargetNext", "›",
                0.88f, 0.14f, 0.98f, 0.86f, SurfaceRaised);
            Button primaryAction = CreateButton(detailPanel.transform, "PrimaryAction",
                "Iniciar campaña", 0.67f, 0.12f, 0.96f, 0.22f, SurfaceAccent);
            TMP_Text primaryActionText = primaryAction.GetComponentInChildren<TMP_Text>(true);

            Assign(screen, "facade", facade);
            Assign(screen, "panelRoot", root);
            Assign(screen, "canvasGroup", canvasGroup);
            Assign(screen, "closeButton", close);
            Assign(screen, "catalogTabButton", catalogTab);
            Assign(screen, "activeTabButton", activeTab);
            Assign(screen, "searchInput", search);
            Assign(screen, "allFilterButton", filterAll);
            Assign(screen, "localFilterButton", filterLocal);
            Assign(screen, "promotionsFilterButton", filterPromotions);
            Assign(screen, "digitalFilterButton", filterDigital);
            Assign(screen, "pressFilterButton", filterPress);
            Assign(screen, "eventsFilterButton", filterEvents);
            Assign(screen, "loyaltyFilterButton", filterLoyalty);
            Assign(screen, "menuFilterButton", filterMenu);
            Assign(screen, "listContent", listContent);
            Assign(screen, "rowPrefab", rowTemplate);
            Assign(screen, "emptyStateText", emptyState);
            Assign(screen, "summaryText", summary);
            Assign(screen, "reputationText", reputation);
            Assign(screen, "recurringText", recurring);
            Assign(screen, "feedbackText", feedback);
            Assign(screen, "detailNameText", detailName);
            Assign(screen, "detailFamilyText", detailFamily);
            Assign(screen, "detailDescriptionText", detailDescription);
            Assign(screen, "detailEffectsText", detailEffects);
            Assign(screen, "detailMetaText", detailMeta);
            Assign(screen, "detailRequirementsText", detailRequirements);
            Assign(screen, "targetPanel", targetPanel);
            Assign(screen, "targetValueText", targetValue);
            Assign(screen, "targetPreviousButton", targetPrevious);
            Assign(screen, "targetNextButton", targetNext);
            Assign(screen, "primaryActionButton", primaryAction);
            Assign(screen, "primaryActionButtonText", primaryActionText);

            Button launcher = CreateButton(ui.transform, "OpenMarketingButton", "Marketing",
                0.770f, 0.925f, 0.880f, 0.975f, SurfaceAccent);
            UnityEventTools.AddPersistentListener(launcher.onClick, screen.Show);
            launcher.transform.SetAsFirstSibling();
            root.SetActive(false);

            if (!facade.ValidateConfiguration(out string facadeError))
                throw new InvalidOperationException(facadeError);
            if (!screen.ValidateConfiguration(out string screenError))
                throw new InvalidOperationException(screenError);
            EditorUtility.SetDirty(facade);
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la UI de Marketing.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderMarketingPlayerUiValidationResult validation =
                BistroBuilderMarketingPlayerUiValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderMarketingPlayerUiSelfTest.Run(
                out int passed,
                out int failed,
                out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(
                    "La UI de Marketing no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "UI jugable de Marketing instalada correctamente.\n" +
                     validation.BuildReport() + "\nAutotest: " + passed +
                     " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllBytes(absoluteScene, backup);
            AssetDatabase.ImportAsset(scene.path, ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            report = "La instalación de UI falló y la escena fue restaurada. " +
                     exception.Message;
            return false;
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static readonly Color SurfacePanel =
        new Color(0.065f, 0.075f, 0.068f, 0.99f);
    private static readonly Color SurfaceInset =
        new Color(0.085f, 0.095f, 0.087f, 1f);
    private static readonly Color SurfaceRaised =
        new Color(0.14f, 0.17f, 0.15f, 1f);
    private static readonly Color SurfaceAccent =
        new Color(0.25f, 0.36f, 0.25f, 1f);
    private static readonly Color SurfaceDanger =
        new Color(0.30f, 0.15f, 0.14f, 1f);

    private static BistroBuilderMarketingPlayerRowView BuildRowTemplate(Transform parent)
    {
        GameObject row = NewUi("MarketingRowTemplate", parent);
        LayoutElement layout = Add<LayoutElement>(row);
        layout.minHeight = 94f;
        layout.preferredHeight = 94f;
        layout.flexibleWidth = 1f;
        Image background = Add<Image>(row);
        background.color = new Color(0.09f, 0.105f, 0.10f, 0.98f);
        Button button = Add<Button>(row);
        button.targetGraphic = background;
        BistroBuilderMarketingPlayerRowView view = Add<BistroBuilderMarketingPlayerRowView>(row);
        Assign(view, "selectButton", button);
        Assign(view, "backgroundImage", background);
        TMP_Text name = CreateText(row.transform, "Name", string.Empty, 17f,
            0.025f, 0.56f, 0.67f, 0.95f, FontStyles.Bold);
        TMP_Text family = CreateText(row.transform, "Family", string.Empty, 12.5f,
            0.69f, 0.57f, 0.975f, 0.94f);
        family.alignment = TextAlignmentOptions.MidlineRight;
        TMP_Text meta = CreateText(row.transform, "Meta", string.Empty, 13f,
            0.025f, 0.08f, 0.58f, 0.53f);
        TMP_Text status = CreateText(row.transform, "Status", string.Empty, 12.5f,
            0.60f, 0.08f, 0.975f, 0.53f);
        status.alignment = TextAlignmentOptions.MidlineRight;
        Assign(view, "nameText", name);
        Assign(view, "familyText", family);
        Assign(view, "metaText", meta);
        Assign(view, "statusText", status);
        return view;
    }

    private static RectTransform CreateScrollContent(
        Transform parent,
        string name,
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        GameObject scroll = NewUi(name, parent);
        Anchor(scroll.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        ScrollRect scrollRect = Add<ScrollRect>(scroll);
        scrollRect.horizontal = false;
        GameObject viewport = NewUi("Viewport", scroll.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        Image viewportImage = Add<Image>(viewport);
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        Add<RectMask2D>(viewport);

        GameObject content = NewUi("Content", viewport.transform);
        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        VerticalLayoutGroup vertical = Add<VerticalLayoutGroup>(content);
        vertical.spacing = 8f;
        vertical.padding = new RectOffset(5, 5, 5, 5);
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        ContentSizeFitter fitter = Add<ContentSizeFitter>(content);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = rect;
        return rect;
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        float minX,
        float minY,
        float maxX,
        float maxY,
        Color color)
    {
        GameObject panel = NewUi(name, parent);
        Anchor(panel.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Add<Image>(panel).color = color;
        return panel;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        float minX,
        float minY,
        float maxX,
        float maxY,
        Color color)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Image image = Add<Image>(go);
        image.color = color;
        Button button = Add<Button>(go);
        button.targetGraphic = image;
        TMP_Text text = CreateText(go.transform, "Label", label, 15f,
            0.02f, 0.04f, 0.98f, 0.96f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_InputField CreateInputField(
        Transform parent,
        string name,
        string placeholder,
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Image image = Add<Image>(go);
        image.color = SurfaceInset;
        TMP_InputField input = Add<TMP_InputField>(go);

        TMP_Text text = CreateText(go.transform, "Text", string.Empty, 15f,
            0.035f, 0.08f, 0.965f, 0.92f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        TMP_Text hint = CreateText(go.transform, "Placeholder", placeholder, 15f,
            0.035f, 0.08f, 0.965f, 0.92f);
        hint.fontStyle = FontStyles.Italic;
        hint.color = new Color(0.62f, 0.65f, 0.61f, 1f);
        input.textComponent = text;
        input.placeholder = hint;
        input.targetGraphic = image;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        float minX,
        float minY,
        float maxX,
        float maxY,
        FontStyles fontStyle = FontStyles.Normal)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        TextMeshProUGUI text = Add<TextMeshProUGUI>(go);
        text.text = value;
        text.fontSize = size;
        text.fontStyle = fontStyle;
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

    private static void Stretch(RectTransform rect) =>
        Anchor(rect, 0f, 0f, 1f, 1f);

    private static void Anchor(
        RectTransform rect,
        float minX,
        float minY,
        float maxX,
        float maxY)
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

    private static T EnsureUnique<T>(Scene scene, GameObject owner)
        where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length > 1)
            throw new InvalidOperationException(
                "Hay varias instancias de " + typeof(T).Name + ".");
        if (values.Length == 1)
        {
            if (values[0].gameObject != owner)
                throw new InvalidOperationException(
                    typeof(T).Name + " está fuera de GameSystems.");
            return values[0];
        }
        return Undo.AddComponent<T>(owner);
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length != 1)
            throw new InvalidOperationException(
                "La UI de Marketing necesita una instancia de " +
                typeof(T).Name + "; hay " + values.Length + ".");
        return values[0];
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var result = new List<T>();
        for (int index = 0; index < all.Length; index++)
            if (all[index] != null && all[index].gameObject.scene == scene)
                result.Add(all[index]);
        return result.ToArray();
    }

    private static GameObject FindUniqueNamedObject(Scene scene, string name)
    {
        GameObject found = null;
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform != null && string.Equals(
                    transform.name, name, StringComparison.Ordinal))
            {
                found = transform.gameObject;
                count++;
            }
        }
        return count == 1 ? found : null;
    }

    private static GameObject FindDirectRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root != null && string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
        return null;
    }

    private static void Assign(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null)
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + fieldName + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
