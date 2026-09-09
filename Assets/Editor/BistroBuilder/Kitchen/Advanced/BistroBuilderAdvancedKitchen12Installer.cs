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

/// <summary>Instalador transaccional e idempotente del Bloque 12.</summary>
public static class BistroBuilderAdvancedKitchen12Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string UiRootName = "BistroBuilderAdvancedKitchenUI";
    private const string RoleCatalogPath =
        "Assets/Resources/BistroBuilder/Staff/StaffRoleCatalog.asset";
    private const string RecruitmentPath =
        "Assets/Resources/BistroBuilder/Staff/StaffRecruitmentProfile.asset";

    [MenuItem("Tools/Bistro Builder/Kitchen/12 - Instalar + validar", false, 12000)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report)) Debug.LogError(report);
        else Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Cocina 12", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        if (!BistroBuilderSceneLockGuard.TryEnsureWritable(
                ScenePath, 5, 150, out string lockError))
            throw new InvalidOperationException(
                "BB Scene Lock Guard bloqueó 12: " + lockError);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!TryInstall(out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 12.";
            return false;
        }

        string absoluteScene = Path.GetFullPath(ScenePath);
        byte[] backup = File.ReadAllBytes(absoluteScene);
        try
        {
            EnsureCookRoleAssets();
            BistroBuilderKitchenStationCatalog catalog =
                BistroBuilderAdvancedKitchen12Seed.EnsureCatalog(out string seedError);
            if (catalog == null) throw new InvalidOperationException(seedError);

            GameObject host = FindUniqueNamed(scene, "GameSystems");
            if (host == null)
                throw new InvalidOperationException("No existe un GameSystems canónico único.");

            KitchenSystem kitchen = RequireUnique<KitchenSystem>(scene);
            BistroBuilderOrderLineExecutionService execution =
                RequireUnique<BistroBuilderOrderLineExecutionService>(scene);
            BistroBuilderCanonicalOrderService canonical =
                RequireUnique<BistroBuilderCanonicalOrderService>(scene);
            BistroBuilderStaffService staff = RequireUnique<BistroBuilderStaffService>(scene);
            BistroBuilderCustomerExperienceTrackingService experience =
                RequireUnique<BistroBuilderCustomerExperienceTrackingService>(scene);
            BistroBuilderAdvancedKitchenService advanced =
                EnsureUniqueOnHost<BistroBuilderAdvancedKitchenService>(scene, host);
            BistroBuilderAdvancedKitchenPlayerFacade facade =
                EnsureUniqueOnHost<BistroBuilderAdvancedKitchenPlayerFacade>(scene, host);

            Assign(advanced, "kitchenSystem", kitchen);
            Assign(advanced, "lineExecutionService", execution);
            Assign(advanced, "canonicalOrderService", canonical);
            Assign(advanced, "stationCatalog", catalog);
            Assign(advanced, "staffService", staff);
            Assign(kitchen, "advancedKitchenService", advanced);
            Assign(experience, "advancedKitchenService", advanced);
            Assign(facade, "kitchenService", advanced);

            if (!advanced.TryRebuildStationConfiguration(out string rebuildError))
                throw new InvalidOperationException(rebuildError);
            if (!advanced.ValidateConfiguration(out string advancedError))
                throw new InvalidOperationException(advancedError);
            if (!facade.ValidateConfiguration(out string facadeError))
                throw new InvalidOperationException(facadeError);

            BuildUi(scene, facade);
            EditorUtility.SetDirty(advanced);
            EditorUtility.SetDirty(facade);
            EditorUtility.SetDirty(kitchen);
            EditorUtility.SetDirty(experience);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la instalación del Bloque 12.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderAdvancedKitchen12ValidationResult validation =
                BistroBuilderAdvancedKitchen12Validator.ValidateCurrentScene();
            bool selfOk = BistroBuilderAdvancedKitchen12SelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(
                    "Bloque 12 no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "Bloque 12 instalado correctamente.\n" +
                validation.BuildReport() + "\nAutotest: " +
                passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try
            {
                File.WriteAllBytes(absoluteScene, backup);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception rollbackError) { Debug.LogException(rollbackError); }
            report = "La instalación 12 falló y fue restaurada. " + exception.Message;
            return false;
        }
    }

    private static void EnsureCookRoleAssets()
    {
        BistroBuilderStaffRoleCatalog roles =
            AssetDatabase.LoadAssetAtPath<BistroBuilderStaffRoleCatalog>(RoleCatalogPath);
        BistroBuilderStaffRecruitmentProfile recruitment =
            AssetDatabase.LoadAssetAtPath<BistroBuilderStaffRecruitmentProfile>(RecruitmentPath);
        if (roles == null || recruitment == null)
            throw new InvalidOperationException("No se encontraron los assets canónicos de Personal.");

        SerializedObject roleSo = new SerializedObject(roles);
        SerializedProperty roleList = roleSo.FindProperty("roles");
        bool hasCook = false;
        for (int i = 0; i < roleList.arraySize; i++)
        {
            SerializedProperty element = roleList.GetArrayElementAtIndex(i);
            hasCook |= string.Equals(
                element.FindPropertyRelative("roleId").stringValue,
                "cook", StringComparison.Ordinal);
        }
        if (!hasCook)
        {
            int index = roleList.arraySize;
            roleList.InsertArrayElementAtIndex(index);
            SerializedProperty cook = roleList.GetArrayElementAtIndex(index);
            cook.FindPropertyRelative("roleId").stringValue = "cook";
            cook.FindPropertyRelative("displayName").stringValue = "Cocinero/a";
            cook.FindPropertyRelative("active").boolValue = true;
            cook.FindPropertyRelative("operationalAdapterId").stringValue =
                BistroBuilderStaffOperationalAdapterIds.CookAgent;
            roleSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(roles);
        }

        SerializedObject recruitmentSo = new SerializedObject(recruitment);
        SerializedProperty enabledRoles = recruitmentSo.FindProperty("enabledRoleIds");
        bool recruitmentHasCook = false;
        for (int i = 0; i < enabledRoles.arraySize; i++)
            recruitmentHasCook |= string.Equals(
                enabledRoles.GetArrayElementAtIndex(i).stringValue,
                "cook", StringComparison.Ordinal);
        if (!recruitmentHasCook)
        {
            int index = enabledRoles.arraySize;
            enabledRoles.InsertArrayElementAtIndex(index);
            enabledRoles.GetArrayElementAtIndex(index).stringValue = "cook";
            recruitmentSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(recruitment);
        }
        AssetDatabase.SaveAssets();
        string roleError = string.Empty;
        string recruitmentError = string.Empty;
        bool rolesValid = roles.TryValidate(out roleError);
        bool recruitmentValid = rolesValid &&
            recruitment.TryValidate(roles, out recruitmentError);
        if (!rolesValid || !recruitmentValid)
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(roleError) ? recruitmentError : roleError);
    }

    private static void BuildUi(
        Scene scene,
        BistroBuilderAdvancedKitchenPlayerFacade facade)
    {
        GameObject previous = FindDirectRoot(scene, UiRootName);
        if (previous != null) Undo.DestroyObjectImmediate(previous);
        GameObject ui = NewUi(UiRootName, null);
        SceneManager.MoveGameObjectToScene(ui, scene);
        Stretch(ui.GetComponent<RectTransform>());
        Canvas canvas = Add<Canvas>(ui);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 49;
        CanvasScaler scaler = Add<CanvasScaler>(ui);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Add<GraphicRaycaster>(ui);
        BistroBuilderAdvancedKitchenPlayerScreen screen =
            Add<BistroBuilderAdvancedKitchenPlayerScreen>(ui);

        GameObject root = NewUi("AdvancedKitchenModal", ui.transform);
        Stretch(root.GetComponent<RectTransform>());
        Add<Image>(root).color = new Color(0.025f, 0.03f, 0.028f, 0.995f);
        CreateText(root.transform, "Title", "COCINA", 31f,
            .03f, .92f, .35f, .98f, FontStyles.Bold);
        TMP_Text summary = CreateText(root.transform, "Summary", "", 17f,
            .25f, .91f, .82f, .985f);
        summary.textWrappingMode = TextWrappingModes.Normal;
        Button close = CreateButton(root.transform, "Close", "Cerrar",
            .86f, .925f, .975f, .98f, Danger);

        Button normal = CreateButton(root.transform, "Normal", "Entrada normal",
            .03f, .84f, .18f, .90f, Accent);
        Button reduced = CreateButton(root.transform, "Reduced", "Reducir ritmo",
            .19f, .84f, .34f, .90f, Warn);
        Button paused = CreateButton(root.transform, "Paused", "Pausar entradas",
            .35f, .84f, .50f, .90f, Danger);

        GameObject stationPanel = CreatePanel(
            root.transform, "Stations", .03f, .16f, .55f, .81f, Panel);
        TMP_Text stations = CreateText(stationPanel.transform, "StationList", "", 16f,
            .035f, .14f, .965f, .965f);
        stations.alignment = TextAlignmentOptions.TopLeft;
        stations.textWrappingMode = TextWrappingModes.Normal;
        Button previousStation = CreateButton(stationPanel.transform,
            "PreviousStation", "< Estación", .035f, .025f, .47f, .105f, Inset);
        Button nextStation = CreateButton(stationPanel.transform,
            "NextStation", "Estación >", .53f, .025f, .965f, .105f, Inset);

        GameObject detailPanel = CreatePanel(
            root.transform, "TaskDetail", .57f, .16f, .975f, .81f, Panel);
        TMP_Text detail = CreateText(detailPanel.transform, "TaskText", "", 18f,
            .055f, .30f, .945f, .95f);
        detail.alignment = TextAlignmentOptions.TopLeft;
        detail.textWrappingMode = TextWrappingModes.Normal;
        Button previousTask = CreateButton(detailPanel.transform,
            "PreviousTask", "< Preparación", .055f, .20f, .47f, .28f, Inset);
        Button nextTask = CreateButton(detailPanel.transform,
            "NextTask", "Preparación >", .53f, .20f, .945f, .28f, Inset);
        Button prioritize = CreateButton(detailPanel.transform,
            "Prioritize", "Priorizar preparación", .25f, .095f, .75f, .175f, Accent);
        TMP_Text feedback = CreateText(detailPanel.transform, "Feedback", "", 14f,
            .055f, .02f, .945f, .085f);
        feedback.textWrappingMode = TextWrappingModes.Normal;

        Assign(screen, "facade", facade);
        Assign(screen, "panelRoot", root);
        Assign(screen, "closeButton", close);
        Assign(screen, "normalButton", normal);
        Assign(screen, "reducedButton", reduced);
        Assign(screen, "pausedButton", paused);
        Assign(screen, "previousStationButton", previousStation);
        Assign(screen, "nextStationButton", nextStation);
        Assign(screen, "previousTaskButton", previousTask);
        Assign(screen, "nextTaskButton", nextTask);
        Assign(screen, "prioritizeButton", prioritize);
        Assign(screen, "summaryText", summary);
        Assign(screen, "stationListText", stations);
        Assign(screen, "taskDetailText", detail);
        Assign(screen, "feedbackText", feedback);

        Button launcher = CreateButton(ui.transform, "OpenAdvancedKitchenButton",
            "Cocina", .76f, .925f, .855f, .975f, Accent);
        UnityEventTools.AddPersistentListener(launcher.onClick, screen.Show);
        launcher.transform.SetAsFirstSibling();
        root.SetActive(false);
        if (!screen.ValidateConfiguration(out string screenError))
            throw new InvalidOperationException(screenError);
        EditorUtility.SetDirty(screen);
        EditorUtility.SetDirty(ui);
    }

    private static readonly Color Panel = new Color(.06f, .07f, .065f, .99f);
    private static readonly Color Inset = new Color(.09f, .105f, .095f, 1f);
    private static readonly Color Accent = new Color(.28f, .40f, .27f, 1f);
    private static readonly Color Danger = new Color(.30f, .15f, .14f, 1f);
    private static readonly Color Warn = new Color(.42f, .31f, .13f, 1f);

    private static GameObject CreatePanel(
        Transform parent, string name,
        float a, float b, float c, float d, Color color)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), a, b, c, d);
        Add<Image>(go).color = color;
        return go;
    }

    private static Button CreateButton(
        Transform parent, string name, string label,
        float a, float b, float c, float d, Color color)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), a, b, c, d);
        Image image = Add<Image>(go);
        image.color = color;
        Button button = Add<Button>(go);
        button.targetGraphic = image;
        TMP_Text text = CreateText(go.transform, "Label", label, 15f,
            .02f, .04f, .98f, .96f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_Text CreateText(
        Transform parent, string name, string value, float size,
        float a, float b, float c, float d,
        FontStyles style = FontStyles.Normal)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), a, b, c, d);
        TextMeshProUGUI text = Add<TextMeshProUGUI>(go);
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = new Color(.92f, .93f, .90f, 1f);
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

    private static void Stretch(RectTransform rect) => Anchor(rect, 0, 0, 1, 1);

    private static void Anchor(
        RectTransform rect, float a, float b, float c, float d)
    {
        rect.anchorMin = new Vector2(a, b);
        rect.anchorMax = new Vector2(c, d);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T Add<T>(GameObject owner) where T : Component
    {
        T current = owner.GetComponent<T>();
        return current != null ? current : Undo.AddComponent<T>(owner);
    }

    private static T EnsureUniqueOnHost<T>(Scene scene, GameObject host)
        where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length > 1)
            throw new InvalidOperationException("Duplicado: " + typeof(T).Name);
        T value = values.Length == 1 ? values[0] : Undo.AddComponent<T>(host);
        if (value.gameObject != host)
            throw new InvalidOperationException(typeof(T).Name + " debe vivir en GameSystems.");
        return value;
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length != 1)
            throw new InvalidOperationException(
                "Se esperaba 1 " + typeof(T).Name + "; hay " + values.Length + ".");
        return values[0];
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) list.Add(found[i]);
        }
        return list.ToArray();
    }

    private static GameObject FindUniqueNamed(Scene scene, string name)
    {
        GameObject found = null;
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                if (tr != null && tr.name == name) { found = tr.gameObject; count++; }
        return count == 1 ? found : null;
    }

    private static GameObject FindDirectRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root != null && root.name == name) return root;
        return null;
    }

    private static void Assign(
        UnityEngine.Object owner,
        string fieldName,
        UnityEngine.Object value)
    {
        var field = owner.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(owner.GetType().Name, fieldName);
        field.SetValue(owner, value);
        EditorUtility.SetDirty(owner);
    }

    private static bool TrySaveSceneWithRetry(Scene scene)
    {
        for (int i = 0; i < 4; i++)
        {
            if (EditorSceneManager.SaveScene(scene)) return true;
            Thread.Sleep(150);
        }
        return false;
    }
}
