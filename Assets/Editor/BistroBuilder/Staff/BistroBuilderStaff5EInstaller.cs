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
/// Instalador idempotente 5E de la UI jugable de Horarios.
/// Solo crea Presentation y la conecta con las autoridades 5A–5D existentes.
/// </summary>
public static class BistroBuilderStaff5EInstaller
{
    private const string UiRootName = "BistroBuilderStaffScheduleUI";

    [MenuItem("Tools/Bistro Builder/Personal/5E - Instalar UI horarios + validar", false, 3279)]
    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Bistro Builder — 5E",
                "Sal de Play Mode antes de instalar la UI de horarios.", "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            EditorUtility.DisplayDialog("Bistro Builder — 5E",
                "Abre y guarda la escena principal antes de instalar.", "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);
        Undo.SetCurrentGroupName("Instalar UI Horarios 5E");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            if (!BistroBuilderStaff5EStaticSelfTest.Run(
                    out int staticPassed,
                    out int staticFailed,
                    out string staticReport))
            {
                Debug.LogError(staticReport);
                throw new InvalidOperationException(
                    "Gate estático 5E: " + staticFailed + " fallos / " +
                    staticPassed + " correctos.");
            }
            Debug.Log(staticReport);

            BistroBuilderStaffScheduleService schedule =
                RequireUnique<BistroBuilderStaffScheduleService>(scene);
            BistroBuilderStaffService staff = RequireUnique<BistroBuilderStaffService>(scene);
            RequireUnique<BistroBuilderStaffScheduleSaveSectionProvider>(scene);

            GameObject previous = FindRoot(scene, UiRootName);
            if (previous != null) Undo.DestroyObjectImmediate(previous);

            GameObject ui = NewUi(UiRootName, null);
            SceneManager.MoveGameObjectToScene(ui, scene);
            Stretch(ui.GetComponent<RectTransform>());
            Canvas canvas = Undo.AddComponent<Canvas>(ui);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(ui);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            Undo.AddComponent<GraphicRaycaster>(ui);

            BistroBuilderStaffSchedulePlayerFacade facade =
                Undo.AddComponent<BistroBuilderStaffSchedulePlayerFacade>(ui);
            Assign(facade, "scheduleService", schedule);
            Assign(facade, "staffService", staff);

            BistroBuilderStaffSchedulePlayerScreen screen =
                Undo.AddComponent<BistroBuilderStaffSchedulePlayerScreen>(ui);
            Assign(screen, "facade", facade);

            GameObject templates = NewUi("Templates", ui.transform);
            BistroBuilderStaffScheduleEmployeeRowView rowTemplate =
                BuildRowTemplate(templates.transform);
            templates.SetActive(false);

            GameObject panel = CreatePanel(ui.transform, "SchedulePanel",
                0.09f, 0.08f, 0.91f, 0.92f,
                new Color(0.045f, 0.052f, 0.06f, 0.99f));
            TMP_Text title = CreateText(panel.transform, "Title", "HORARIOS Y TURNOS",
                30f, 0.025f, 0.925f, 0.42f, 0.985f);
            TMP_Text header = CreateText(panel.transform, "Header", string.Empty,
                19f, 0.025f, 0.855f, 0.56f, 0.915f);
            TMP_Text coverage = CreateText(panel.transform, "Coverage", string.Empty,
                17f, 0.025f, 0.795f, 0.975f, 0.85f);
            TMP_Text feedback = CreateText(panel.transform, "Feedback", string.Empty,
                15f, 0.025f, 0.025f, 0.975f, 0.075f);

            Button close = CreateButton(panel.transform, "Close", "Cerrar",
                0.86f, 0.935f, 0.975f, 0.98f);
            Button previousDay = CreateButton(panel.transform, "PreviousDay", "◀ Día",
                0.025f, 0.72f, 0.14f, 0.77f);
            Button nextDay = CreateButton(panel.transform, "NextDay", "Día ▶",
                0.15f, 0.72f, 0.265f, 0.77f);
            Button lunch = CreateButton(panel.transform, "Lunch", "Comida",
                0.30f, 0.72f, 0.415f, 0.77f);
            Button dinner = CreateButton(panel.transform, "Dinner", "Cena",
                0.425f, 0.72f, 0.54f, 0.77f);
            Button autoFill = CreateButton(panel.transform, "AutoFill", "Cobertura mínima",
                0.59f, 0.72f, 0.76f, 0.77f);
            Button copyPrevious = CreateButton(panel.transform, "CopyPrevious", "Copiar día anterior",
                0.77f, 0.72f, 0.975f, 0.77f);

            RectTransform content = CreateScroll(panel.transform,
                "EmployeeScroll", 0.025f, 0.095f, 0.975f, 0.69f);

            Assign(screen, "panelRoot", panel);
            Assign(screen, "employeeContent", content);
            Assign(screen, "employeeRowPrefab", rowTemplate);
            Assign(screen, "closeButton", close);
            Assign(screen, "previousDayButton", previousDay);
            Assign(screen, "nextDayButton", nextDay);
            Assign(screen, "lunchButton", lunch);
            Assign(screen, "dinnerButton", dinner);
            Assign(screen, "autoFillButton", autoFill);
            Assign(screen, "copyPreviousButton", copyPrevious);
            Assign(screen, "headerText", header);
            Assign(screen, "coverageText", coverage);
            Assign(screen, "feedbackText", feedback);

            Button launcher = CreateButton(ui.transform, "OpenScheduleButton", "Horarios",
                0.77f, 0.925f, 0.875f, 0.975f);
            UnityEventTools.AddPersistentListener(launcher.onClick, screen.Show);
            panel.SetActive(false);

            if (!facade.ValidateConfiguration(out string facadeError))
                throw new InvalidOperationException(facadeError);
            if (!screen.ValidateConfiguration(out string screenError))
                throw new InvalidOperationException(screenError);

            EditorUtility.SetDirty(facade);
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity no pudo guardar la escena 5E.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderStaff5EValidationResult validation =
                BistroBuilderStaff5EValidator.ValidateCurrentScene();
            bool finalStatic = BistroBuilderStaff5EStaticSelfTest.Run(
                out int passed,
                out int failed,
                out string report);
            Debug.Log(validation.BuildReport());
            Debug.Log(report);
            if (validation.errors > 0 || !finalStatic)
                throw new InvalidOperationException(
                    "5E no superó gates: " + validation.errors +
                    " errores estructurales / " + failed + " fallos estáticos.");

            EditorUtility.DisplayDialog("Bistro Builder — 5E",
                "UI de Horarios instalada: " + validation.correct + " OK / " +
                validation.warnings + " avisos / 0 errores; " + passed +
                " gates estáticos OK.\n\nPendiente prueba visual/funcional en Play Mode.", "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("Bistro Builder — 5E",
                "La instalación falló y la escena fue restaurada.\n\n" +
                exception.Message, "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static BistroBuilderStaffScheduleEmployeeRowView BuildRowTemplate(Transform parent)
    {
        GameObject row = NewUi("ScheduleEmployeeRowTemplate", parent);
        LayoutElement layout = Undo.AddComponent<LayoutElement>(row);
        layout.preferredHeight = 70f;
        Image image = Undo.AddComponent<Image>(row);
        image.color = new Color(0.09f, 0.105f, 0.12f, 0.98f);
        Button button = Undo.AddComponent<Button>(row);
        button.targetGraphic = image;
        BistroBuilderStaffScheduleEmployeeRowView view =
            Undo.AddComponent<BistroBuilderStaffScheduleEmployeeRowView>(row);

        TMP_Text name = RowText(row.transform, "Name", 0.02f, 0.34f);
        TMP_Text availability = RowText(row.transform, "Availability", 0.35f, 0.52f);
        TMP_Text salary = RowText(row.transform, "Salary", 0.53f, 0.76f);
        TMP_Text scheduled = RowText(row.transform, "Scheduled", 0.77f, 0.98f);
        Assign(view, "toggleButton", button);
        Assign(view, "nameText", name);
        Assign(view, "availabilityText", availability);
        Assign(view, "salaryText", salary);
        Assign(view, "scheduledText", scheduled);
        return view;
    }

    private static RectTransform CreateScroll(
        Transform parent, string name, float minX, float minY, float maxX, float maxY)
    {
        GameObject scroll = CreatePanel(parent, name, minX, minY, maxX, maxY,
            new Color(0.035f, 0.04f, 0.05f, 0.95f));
        ScrollRect scrollRect = Undo.AddComponent<ScrollRect>(scroll);
        scrollRect.horizontal = false;

        GameObject viewport = NewUi("Viewport", scroll.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        Undo.AddComponent<RectMask2D>(viewport);

        GameObject content = NewUi("Content", viewport.transform);
        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = Undo.AddComponent<VerticalLayoutGroup>(content);
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = Undo.AddComponent<ContentSizeFitter>(content);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = rect;
        return rect;
    }

    private static TMP_Text RowText(Transform parent, string name, float minX, float maxX)
    {
        return CreateText(parent, name, string.Empty, 16f, minX, 0f, maxX, 1f);
    }

    private static GameObject CreatePanel(
        Transform parent, string name, float minX, float minY, float maxX, float maxY,
        Color color)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Image image = Undo.AddComponent<Image>(go);
        image.color = color;
        return go;
    }

    private static Button CreateButton(
        Transform parent, string name, string label,
        float minX, float minY, float maxX, float maxY)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Image image = Undo.AddComponent<Image>(go);
        image.color = new Color(0.12f, 0.14f, 0.16f, 1f);
        Button button = Undo.AddComponent<Button>(go);
        button.targetGraphic = image;
        TMP_Text text = CreateText(go.transform, "Label", label, 16f, 0f, 0f, 1f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_Text CreateText(
        Transform parent, string name, string text, float size,
        float minX, float minY, float maxX, float maxY)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        TextMeshProUGUI label = Undo.AddComponent<TextMeshProUGUI>(go);
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        return label;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Crear UI Horarios 5E");
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static void Anchor(RectTransform rect,
        float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect) => Anchor(rect, 0f, 0f, 1f, 1f);

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (string.Equals(root.name, name, StringComparison.Ordinal)) return root;
        return null;
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindScene<T>(scene);
        if (matches.Length != 1)
            throw new InvalidOperationException("Se esperaba un " + typeof(T).Name +
                " y hay " + matches.Length + ".");
        return matches[0];
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<T>();
        foreach (T value in all)
            if (value != null && value.gameObject.scene == scene) list.Add(value);
        return list.ToArray();
    }

    private static void Assign(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            throw new InvalidOperationException("No existe " + name + " en " + target.GetType().Name + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

public sealed class BistroBuilderStaff5EValidationResult
{
    public int correct;
    public int warnings;
    public int errors;
    public readonly List<string> lines = new List<string>();
    public string BuildReport() => "5E — Validación UI Horarios\n" +
        string.Join("\n", lines) + "\nResultado: " + correct + " OK / " +
        warnings + " avisos / " + errors + " errores";
}

public static class BistroBuilderStaff5EValidator
{
    [MenuItem("Tools/Bistro Builder/Personal/5E - Validar UI horarios", false, 3280)]
    private static void RunMenu()
    {
        BistroBuilderStaff5EValidationResult result = ValidateCurrentScene();
        if (result.errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderStaff5EValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderStaff5EValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Fail(result, "No hay escena activa.");
            return result;
        }

        BistroBuilderStaffSchedulePlayerFacade[] facades = Find<BistroBuilderStaffSchedulePlayerFacade>(scene);
        BistroBuilderStaffSchedulePlayerScreen[] screens = Find<BistroBuilderStaffSchedulePlayerScreen>(scene);
        if (facades.Length == 1) Pass(result, "Existe una única SchedulePlayerFacade.");
        else Fail(result, "SchedulePlayerFacade debe existir una vez; hay " + facades.Length + ".");
        if (screens.Length == 1) Pass(result, "Existe una única SchedulePlayerScreen.");
        else Fail(result, "SchedulePlayerScreen debe existir una vez; hay " + screens.Length + ".");

        if (facades.Length == 1)
        {
            if (facades[0].ValidateConfiguration(out string error)) Pass(result, "Facade configurada.");
            else Fail(result, error);
        }
        if (screens.Length == 1)
        {
            if (screens[0].ValidateConfiguration(out string error)) Pass(result, "Screen configurada.");
            else Fail(result, error);
        }

        GameObject root = null;
        foreach (GameObject item in scene.GetRootGameObjects())
            if (item.name == UiRootNameForValidation) root = item;
        if (root != null && root.GetComponent<Canvas>() != null)
            Pass(result, "Jerarquía UI 5E canónica presente.");
        else
            Fail(result, "Falta BistroBuilderStaffScheduleUI con Canvas.");

        return result;
    }

    private const string UiRootNameForValidation = "BistroBuilderStaffScheduleUI";
    private static T[] Find<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<T>();
        foreach (T value in all)
            if (value != null && value.gameObject.scene == scene) list.Add(value);
        return list.ToArray();
    }
    private static void Pass(BistroBuilderStaff5EValidationResult result, string text)
    { result.correct++; result.lines.Add("[OK] " + text); }
    private static void Fail(BistroBuilderStaff5EValidationResult result, string text)
    { result.errors++; result.lines.Add("[ERROR] " + text); }
}
