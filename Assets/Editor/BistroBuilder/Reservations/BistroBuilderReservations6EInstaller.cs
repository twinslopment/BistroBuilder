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
/// 6E — Instalador idempotente de la agenda jugable de Reservas.
/// Solo crea Presentation y la conecta con 6A–6D canónicos.
/// </summary>
public static class BistroBuilderReservations6EInstaller
{
    private const string MainScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    public const string UiRootName = "BistroBuilderReservationsUI";

    [MenuItem("Tools/Bistro Builder/Reservations/6E - Instalar UI + validar", false, 650)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
            Debug.LogError(report);
        else
            Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — 6E", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            throw new InvalidOperationException(report);
        }
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "6E no puede instalarse durante Play Mode.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar 6E.";
            return false;
        }

        string scenePath = scene.path;
        byte[] backup = File.ReadAllBytes(Path.GetFullPath(scenePath));
        try
        {
            BistroBuilderReservationService reservations =
                RequireUnique<BistroBuilderReservationService>(scene);
            BistroBuilderReservationAvailabilityService availability =
                RequireUnique<BistroBuilderReservationAvailabilityService>(scene);
            BistroBuilderGeneralGameStateService gameState =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            RequireUnique<BistroBuilderReservationsSaveSectionProvider>(scene);

            GameObject previous = FindRoot(scene, UiRootName);
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous);

            GameObject ui = NewUi(UiRootName, null);
            SceneManager.MoveGameObjectToScene(ui, scene);
            Stretch(ui.GetComponent<RectTransform>());
            Canvas canvas = ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 46;
            CanvasScaler scaler = ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            ui.AddComponent<GraphicRaycaster>();

            BistroBuilderReservationPlayerFacade facade =
                ui.AddComponent<BistroBuilderReservationPlayerFacade>();
            Assign(facade, "reservationService", reservations);
            Assign(facade, "availabilityService", availability);
            Assign(facade, "gameStateService", gameState);

            BistroBuilderReservationPlayerScreen screen =
                ui.AddComponent<BistroBuilderReservationPlayerScreen>();
            Assign(screen, "facade", facade);

            GameObject templates = NewUi("Templates", ui.transform);
            BistroBuilderReservationRowView rowTemplate =
                BuildRowTemplate(templates.transform);
            templates.SetActive(false);

            GameObject panel = CreatePanel(
                ui.transform,
                "ReservationsPanel",
                0f, 0f, 1f, 1f,
                new Color(0.032f, 0.037f, 0.043f, 1f));

            TMP_Text title = CreateText(
                panel.transform, "Title", "RESERVAS",
                31f, 0.025f, 0.93f, 0.28f, 0.985f);
            title.fontStyle = FontStyles.Bold;
            TMP_Text dayHeader = CreateText(
                panel.transform, "DayHeader", string.Empty,
                20f, 0.30f, 0.93f, 0.52f, 0.985f);
            TMP_Text summary = CreateText(
                panel.transform, "Summary", string.Empty,
                16f, 0.025f, 0.875f, 0.60f, 0.92f);
            TMP_Text feedback = CreateText(
                panel.transform, "Feedback", string.Empty,
                15f, 0.61f, 0.875f, 0.975f, 0.92f);
            Button close = CreateButton(
                panel.transform, "Close", "Cerrar",
                0.865f, 0.935f, 0.975f, 0.98f, false);
            Button previousDay = CreateButton(
                panel.transform, "PreviousDay", "Día anterior",
                0.025f, 0.81f, 0.145f, 0.86f, false);
            Button nextDay = CreateButton(
                panel.transform, "NextDay", "Día siguiente",
                0.155f, 0.81f, 0.275f, 0.86f, false);
            Button newReservation = CreateButton(
                panel.transform, "NewReservation", "Nueva reserva",
                0.445f, 0.81f, 0.61f, 0.86f, true);

            CreateColumnHeader(panel.transform, "TimeHeader", "HORA", 0.035f, 0.115f);
            CreateColumnHeader(panel.transform, "GuestHeader", "CLIENTE", 0.12f, 0.34f);
            CreateColumnHeader(panel.transform, "PartyHeader", "GRUPO", 0.345f, 0.43f);
            CreateColumnHeader(panel.transform, "TableHeader", "MESA", 0.435f, 0.515f);
            CreateColumnHeader(panel.transform, "StatusHeader", "ESTADO", 0.52f, 0.61f);

            RectTransform agenda = CreateScroll(
                panel.transform,
                "AgendaScroll",
                0.025f, 0.07f, 0.62f, 0.77f);
            TMP_Text empty = CreateText(
                panel.transform, "EmptyState",
                "No hay reservas para este día.",
                20f, 0.10f, 0.35f, 0.55f, 0.52f);
            empty.alignment = TextAlignmentOptions.Center;
            empty.color = new Color(0.70f, 0.72f, 0.70f, 1f);

            GameObject form = CreatePanel(
                panel.transform,
                "ReservationForm",
                0.64f, 0.07f, 0.975f, 0.86f,
                new Color(0.055f, 0.065f, 0.075f, 0.98f));
            TMP_Text formMode = CreateText(
                form.transform, "FormMode", "NUEVA RESERVA",
                20f, 0.06f, 0.91f, 0.94f, 0.975f);
            formMode.fontStyle = FontStyles.Bold;
            formMode.color = new Color(0.78f, 0.72f, 0.56f, 1f);

            CreateFieldLabel(form.transform, "GuestLabel", "Nombre", 0.83f, 0.88f);
            TMP_InputField guest = CreateInput(
                form.transform, "GuestInput", "Nombre de la reserva",
                0.06f, 0.75f, 0.94f, 0.825f, false, 80);

            CreateFieldLabel(form.transform, "PartyLabel", "Personas", 0.67f, 0.72f);
            Button partyMinus = CreateButton(
                form.transform, "PartyMinus", "-",
                0.06f, 0.59f, 0.18f, 0.66f, false);
            TMP_Text partyValue = CreateValueBox(
                form.transform, "PartyValue", "2 personas",
                0.20f, 0.59f, 0.80f, 0.66f);
            Button partyPlus = CreateButton(
                form.transform, "PartyPlus", "+",
                0.82f, 0.59f, 0.94f, 0.66f, false);

            CreateFieldLabel(form.transform, "TimeLabel", "Hora", 0.51f, 0.56f);
            Button timeMinus = CreateButton(
                form.transform, "TimeMinus", "-30 min",
                0.06f, 0.43f, 0.27f, 0.50f, false);
            TMP_Text timeValue = CreateValueBox(
                form.transform, "TimeValue", "13:00",
                0.29f, 0.43f, 0.71f, 0.50f);
            Button timePlus = CreateButton(
                form.transform, "TimePlus", "+30 min",
                0.73f, 0.43f, 0.94f, 0.50f, false);

            CreateFieldLabel(form.transform, "DurationLabel", "Duración", 0.35f, 0.40f);
            Button durationMinus = CreateButton(
                form.transform, "DurationMinus", "-30 min",
                0.06f, 0.27f, 0.27f, 0.34f, false);
            TMP_Text durationValue = CreateValueBox(
                form.transform, "DurationValue", "90 min",
                0.29f, 0.27f, 0.71f, 0.34f);
            Button durationPlus = CreateButton(
                form.transform, "DurationPlus", "+30 min",
                0.73f, 0.27f, 0.94f, 0.34f, false);

            TMP_Text tableValue = CreateValueBox(
                form.transform, "TableValue", "Mesa automática al guardar",
                0.06f, 0.19f, 0.94f, 0.25f);
            tableValue.color = new Color(0.72f, 0.82f, 0.72f, 1f);

            TMP_InputField notes = CreateInput(
                form.transform, "NotesInput", "Notas opcionales",
                0.06f, 0.08f, 0.94f, 0.18f, true, 280);
            Button save = CreateButton(
                form.transform, "SaveReservation", "Crear reserva",
                0.06f, 0.01f, 0.57f, 0.07f, true);
            Button cancel = CreateButton(
                form.transform, "CancelReservation", "Cancelar reserva",
                0.59f, 0.01f, 0.94f, 0.07f, false);

            Assign(screen, "panelRoot", panel);
            Assign(screen, "agendaContent", agenda);
            Assign(screen, "rowPrefab", rowTemplate);
            Assign(screen, "closeButton", close);
            Assign(screen, "previousDayButton", previousDay);
            Assign(screen, "nextDayButton", nextDay);
            Assign(screen, "newReservationButton", newReservation);
            Assign(screen, "saveReservationButton", save);
            Assign(screen, "cancelReservationButton", cancel);
            Assign(screen, "partyMinusButton", partyMinus);
            Assign(screen, "partyPlusButton", partyPlus);
            Assign(screen, "timeMinusButton", timeMinus);
            Assign(screen, "timePlusButton", timePlus);
            Assign(screen, "durationMinusButton", durationMinus);
            Assign(screen, "durationPlusButton", durationPlus);
            Assign(screen, "guestInput", guest);
            Assign(screen, "notesInput", notes);
            Assign(screen, "titleText", title);
            Assign(screen, "dayHeaderText", dayHeader);
            Assign(screen, "summaryText", summary);
            Assign(screen, "feedbackText", feedback);
            Assign(screen, "emptyStateText", empty);
            Assign(screen, "formModeText", formMode);
            Assign(screen, "partyValueText", partyValue);
            Assign(screen, "timeValueText", timeValue);
            Assign(screen, "durationValueText", durationValue);
            Assign(screen, "tableValueText", tableValue);

            Button launcher = CreateButton(
                ui.transform, "OpenReservationsButton", "Reservas",
                0.655f, 0.925f, 0.765f, 0.975f, true);
            UnityEventTools.AddPersistentListener(launcher.onClick, screen.Show);
            launcher.transform.SetAsFirstSibling();
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
                throw new InvalidOperationException("Unity no pudo guardar la UI 6E.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderReservations6EValidation validation =
                BistroBuilderReservations6EValidator.ValidateCurrentScene();
            if (validation.Errors > 0)
                throw new InvalidOperationException(validation.BuildReport());

            report = "6E instalado correctamente.\n" + validation.BuildReport();
            return true;
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(Path.GetFullPath(scenePath), backup);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            report = "6E falló y la escena fue restaurada. " + exception.Message;
            Debug.LogException(exception);
            return false;
        }
    }

    private static BistroBuilderReservationRowView BuildRowTemplate(Transform parent)
    {
        GameObject row = NewUi("ReservationRowTemplate", parent);
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.minHeight = 72f;
        layout.preferredHeight = 72f;
        layout.flexibleWidth = 1f;
        Image image = row.AddComponent<Image>();
        image.color = new Color(0.085f, 0.10f, 0.115f, 0.98f);
        Button button = row.AddComponent<Button>();
        button.targetGraphic = image;
        BistroBuilderReservationRowView view =
            row.AddComponent<BistroBuilderReservationRowView>();

        TMP_Text time = RowText(row.transform, "Time", 0.02f, 0.14f);
        TMP_Text guest = RowText(row.transform, "Guest", 0.15f, 0.50f);
        TMP_Text party = RowText(row.transform, "Party", 0.51f, 0.63f);
        TMP_Text table = RowText(row.transform, "Table", 0.64f, 0.78f);
        TMP_Text status = RowText(row.transform, "Status", 0.79f, 0.98f);
        time.alignment = TextAlignmentOptions.Midline;
        party.alignment = TextAlignmentOptions.Midline;
        table.alignment = TextAlignmentOptions.Midline;
        status.alignment = TextAlignmentOptions.Midline;

        Assign(view, "selectButton", button);
        Assign(view, "background", image);
        Assign(view, "timeText", time);
        Assign(view, "guestText", guest);
        Assign(view, "partyText", party);
        Assign(view, "tableText", table);
        Assign(view, "statusText", status);
        return view;
    }

    private static RectTransform CreateScroll(
        Transform parent,
        string name,
        float minX, float minY, float maxX, float maxY)
    {
        GameObject scroll = CreatePanel(
            parent, name, minX, minY, maxX, maxY,
            new Color(0.028f, 0.034f, 0.041f, 0.96f));
        ScrollRect scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        GameObject viewport = NewUi("Viewport", scroll.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();

        GameObject content = NewUi("Content", viewport.transform);
        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        VerticalLayoutGroup vertical = content.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(8, 8, 8, 8);
        vertical.spacing = 7f;
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childScaleWidth = false;
        vertical.childScaleHeight = false;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = rect;
        return rect;
    }

    private static TMP_InputField CreateInput(
        Transform parent,
        string name,
        string placeholderText,
        float minX, float minY, float maxX, float maxY,
        bool multiline,
        int characterLimit)
    {
        GameObject root = CreatePanel(
            parent, name, minX, minY, maxX, maxY,
            new Color(0.10f, 0.115f, 0.13f, 1f));
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        input.characterLimit = characterLimit;
        input.lineType = multiline
            ? TMP_InputField.LineType.MultiLineNewline
            : TMP_InputField.LineType.SingleLine;

        GameObject area = NewUi("Text Area", root.transform);
        RectTransform areaRect = area.GetComponent<RectTransform>();
        Anchor(areaRect, 0.03f, 0.08f, 0.97f, 0.92f);
        area.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = CreateText(
            area.transform, "Placeholder", placeholderText,
            15f, 0f, 0f, 1f, 1f) as TextMeshProUGUI;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0.48f, 0.50f, 0.50f, 1f);
        TextMeshProUGUI text = CreateText(
            area.transform, "Text", string.Empty,
            16f, 0f, 0f, 1f, 1f) as TextMeshProUGUI;
        text.color = new Color(0.94f, 0.94f, 0.91f, 1f);
        placeholder.textWrappingMode = multiline
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;
        text.textWrappingMode = multiline
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;
        input.textViewport = areaRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private static TMP_Text CreateValueBox(
        Transform parent,
        string name,
        string value,
        float minX, float minY, float maxX, float maxY)
    {
        GameObject box = CreatePanel(
            parent, name + "Box", minX, minY, maxX, maxY,
            new Color(0.085f, 0.10f, 0.115f, 1f));
        TMP_Text text = CreateText(
            box.transform, name, value, 16f,
            0.04f, 0f, 0.96f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static TMP_Text CreateFieldLabel(
        Transform parent,
        string name,
        string text,
        float minY,
        float maxY)
    {
        TMP_Text label = CreateText(
            parent, name, text, 13f,
            0.06f, minY, 0.94f, maxY);
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.70f, 0.72f, 0.70f, 1f);
        return label;
    }

    private static TMP_Text CreateColumnHeader(
        Transform parent,
        string name,
        string text,
        float minX,
        float maxX)
    {
        TMP_Text label = CreateText(
            parent, name, text, 12f,
            minX, 0.775f, maxX, 0.805f);
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.78f, 0.72f, 0.56f, 1f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private static TMP_Text RowText(
        Transform parent,
        string name,
        float minX,
        float maxX)
    {
        TMP_Text text = CreateText(
            parent, name, string.Empty, 15f,
            minX, 0f, maxX, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        float minX, float minY, float maxX, float maxY,
        bool accent)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Image image = go.AddComponent<Image>();
        image.color = accent
            ? new Color(0.20f, 0.31f, 0.24f, 1f)
            : new Color(0.14f, 0.17f, 0.19f, 1f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText(
            go.transform, "Label", label, 15f,
            0.03f, 0f, 0.97f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        if (accent)
        {
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.88f, 0.93f, 0.82f, 1f);
        }
        return button;
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        float minX, float minY, float maxX, float maxY,
        Color color)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        float minX, float minY, float maxX, float maxY)
    {
        GameObject go = NewUi(name, parent);
        Anchor(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = new Color(0.92f, 0.92f, 0.90f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static void Anchor(
        RectTransform rect,
        float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect) =>
        Anchor(rect, 0f, 0f, 1f, 1f);

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
        return null;
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindScene<T>(scene);
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Se esperaba exactamente un " + typeof(T).Name +
                " y hay " + matches.Length + ".");
        return matches[0];
    }

    internal static T[] FindScene<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<T>(true));
        return list.ToArray();
    }

    private static void Assign(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                target.GetType().Name + " no expone " + propertyName + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

public sealed class BistroBuilderReservations6EValidation
{
    public int Correct;
    public int Warnings;
    public int Errors;
    public readonly List<string> Lines = new List<string>();

    public string BuildReport() =>
        "6E — Validación UI Reservas\n" +
        string.Join("\n", Lines) + "\nResultado: " + Correct +
        " OK / " + Warnings + " avisos / " + Errors + " errores.";
}

public static class BistroBuilderReservations6EValidator
{
    [MenuItem("Tools/Bistro Builder/Reservations/6E - Validar UI", false, 651)]
    private static void RunFromMenu()
    {
        BistroBuilderReservations6EValidation result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static BistroBuilderReservations6EValidation ValidateCurrentScene()
    {
        var result = new BistroBuilderReservations6EValidation();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Fail(result, "No hay escena activa válida.");
            return result;
        }

        GameObject root = FindRoot(scene, BistroBuilderReservations6EInstaller.UiRootName);
        Check(result, root != null, "Existe BistroBuilderReservationsUI.");
        if (root == null) return result;

        BistroBuilderReservationPlayerFacade[] facades =
            BistroBuilderReservations6EInstaller.FindScene<BistroBuilderReservationPlayerFacade>(scene);
        BistroBuilderReservationPlayerScreen[] screens =
            BistroBuilderReservations6EInstaller.FindScene<BistroBuilderReservationPlayerScreen>(scene);
        Check(result, facades.Length == 1, "Existe una única ReservationPlayerFacade.");
        Check(result, screens.Length == 1, "Existe una única ReservationPlayerScreen.");
        if (facades.Length == 1)
        {
            string error = string.Empty;
            Check(result, facades[0].ValidateConfiguration(out error),
                string.IsNullOrWhiteSpace(error) ? "Facade 6E configurada." : error);
        }
        if (screens.Length == 1)
        {
            string error = string.Empty;
            Check(result, screens[0].ValidateConfiguration(out error),
                string.IsNullOrWhiteSpace(error) ? "Screen 6E configurada." : error);
        }

        Canvas canvas = root.GetComponent<Canvas>();
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        Check(result, canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay,
            "Canvas 6E usa ScreenSpaceOverlay.");
        Check(result,
            scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
            scaler.referenceResolution == new Vector2(1920f, 1080f),
            "CanvasScaler 6E usa referencia 1920x1080.");
        Check(result, root.GetComponentsInChildren<RectMask2D>(true).Length >= 3,
            "Scroll e inputs usan RectMask2D.");
        Check(result, root.GetComponentsInChildren<Mask>(true).Length == 0,
            "6E no utiliza Mask clásico transparente.");

        Transform launcher = root.transform.Find("OpenReservationsButton");
        Check(result, launcher != null && launcher.GetComponent<Button>() != null,
            "Existe launcher jugable Reservas.");
        Transform templates = root.transform.Find("Templates");
        Check(result, templates != null && !templates.gameObject.activeSelf,
            "Templates 6E permanecen inactivos.");
        Check(result,
            root.GetComponentsInChildren<BistroBuilderReservationRowView>(true).Length == 1,
            "Existe una única plantilla de fila de agenda.");

        BistroBuilderReservations6DValidationResult baseGate =
            BistroBuilderReservations6DValidator.ValidateCurrentScene();
        Check(result, baseGate.Errors == 0,
            baseGate.Errors == 0
                ? "Persistencia 6D permanece válida."
                : "Regresión 6D: " + baseGate.BuildReport());
        return result;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
        return null;
    }

    private static void Check(
        BistroBuilderReservations6EValidation result,
        bool condition,
        string message)
    {
        if (condition) Pass(result, message);
        else Fail(result, message);
    }

    private static void Pass(
        BistroBuilderReservations6EValidation result,
        string message)
    {
        result.Correct++;
        result.Lines.Add("[OK] " + message);
    }

    private static void Fail(
        BistroBuilderReservations6EValidation result,
        string message)
    {
        result.Errors++;
        result.Lines.Add("[ERROR] " + message);
    }
}
