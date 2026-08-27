using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 4F — Instalador aditivo e idempotente de la formación jugable.
/// Solo modifica Presentation y deriva las opciones visibles del perfil 4C.
/// </summary>
public static class BistroBuilderStaff4FTrainingInstaller
{
    private const string DevelopmentProfilePath =
        "Assets/Resources/BistroBuilder/Staff/StaffDevelopmentProfile.asset";
    private const string ModalName = "TrainingModal";
    private const string OpenButtonName = "Training";

    [MenuItem(
        "Tools/Bistro Builder/Personal/4F - Instalar formación + validar",
        false,
        3252)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Formación",
                "Sal de Play Mode antes de instalar la UI de formación.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Formación",
                "Abre y guarda la escena principal antes de instalar.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);
        Undo.SetCurrentGroupName("Instalar formación 4F Personal");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            BistroBuilderStaffPlayerScreen screen =
                RequireUnique<BistroBuilderStaffPlayerScreen>(scene);
            BistroBuilderStaffPlayerFacade facade =
                RequireUnique<BistroBuilderStaffPlayerFacade>(scene);

            if (!screen.ValidateConfiguration(out string screenError))
            {
                throw new InvalidOperationException(screenError);
            }
            if (!facade.ValidateConfiguration(out string facadeError))
            {
                throw new InvalidOperationException(facadeError);
            }

            BistroBuilderStaffDevelopmentProfile profile =
                AssetDatabase.LoadAssetAtPath<BistroBuilderStaffDevelopmentProfile>(
                    DevelopmentProfilePath);
            string profileError = string.Empty;
            if (profile == null || !profile.TryValidate(out profileError))
            {
                throw new InvalidOperationException(
                    "Falta el perfil canónico 4C de desarrollo. " + profileError);
            }

            Transform employeeDetail = screen.transform.Find(
                "StaffPanelRoot/StaffPanel/EmployeeDetail");
            Transform panelRoot = screen.transform.Find("StaffPanelRoot");
            if (employeeDetail == null || panelRoot == null)
            {
                throw new InvalidOperationException(
                    "La jerarquía 4F base no contiene EmployeeDetail/StaffPanelRoot.");
            }

            DestroyChildIfPresent(employeeDetail, OpenButtonName);
            DestroyChildIfPresent(panelRoot, ModalName);

            RectTransform availability = FindRect(employeeDetail, "Availability");
            RectTransform dismiss = FindRect(employeeDetail, "Dismiss");
            SetAnchors(availability, 0.03f, 0.025f, 0.30f, 0.105f);
            SetAnchors(dismiss, 0.70f, 0.025f, 0.97f, 0.105f);

            Button openButton = CreateButton(
                employeeDetail, OpenButtonName, "Formación",
                0.365f, 0.025f, 0.635f, 0.105f);

            GameObject modal = CreatePanel(
                panelRoot, ModalName, 0.28f, 0.22f, 0.72f, 0.78f);
            CreateText(
                modal.transform, "Title", "FORMACIÓN", 26f,
                0.06f, 0.88f, 0.72f, 0.97f);
            TMP_Text employeeText = CreateText(
                modal.transform, "Employee", string.Empty, 15f,
                0.06f, 0.80f, 0.94f, 0.87f);
            TMP_Text feedback = CreateText(
                modal.transform, "Feedback", string.Empty, 14f,
                0.06f, 0.06f, 0.78f, 0.14f);
            Button close = CreateButton(
                modal.transform, "Close", "Cerrar",
                0.80f, 0.90f, 0.94f, 0.97f);

            int count = profile.Trainings.Count;
            var buttons = new List<Button>(count);
            var labels = new List<TMP_Text>(count);
            float top = 0.76f;
            float bottom = 0.17f;
            float gap = 0.012f;
            float height = (top - bottom - gap * Math.Max(0, count - 1)) /
                Math.Max(1, count);

            for (int index = 0; index < count; index++)
            {
                BistroBuilderStaffTrainingDefinition training = profile.Trainings[index];
                if (training == null)
                {
                    throw new InvalidOperationException(
                        "El perfil contiene una formación nula.");
                }

                float maxY = top - index * (height + gap);
                float minY = maxY - height;
                Button button = CreateButton(
                    modal.transform,
                    "Training_" + training.trainingId,
                    training.displayName,
                    0.06f, minY, 0.94f, maxY);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label == null)
                {
                    throw new InvalidOperationException(
                        "No pudo crearse el texto de una formación 4F.");
                }
                buttons.Add(button);
                labels.Add(label);
            }

            BistroBuilderStaffPlayerTrainingPanel trainingPanel =
                screen.GetComponent<BistroBuilderStaffPlayerTrainingPanel>();
            if (trainingPanel == null)
            {
                trainingPanel = Undo.AddComponent<
                    BistroBuilderStaffPlayerTrainingPanel>(screen.gameObject);
            }

            ConfigurePanel(
                trainingPanel,
                facade,
                screen,
                openButton,
                modal,
                close,
                employeeText,
                feedback,
                profile,
                buttons,
                labels);

            modal.SetActive(false);
            if (!trainingPanel.ValidateConfiguration(out string trainingError))
            {
                throw new InvalidOperationException(trainingError);
            }

            bool staticOk = BistroBuilderStaff4FTrainingStaticSelfTest.Run(
                out int passed,
                out int failed,
                out string report);
            Debug.Log(report);
            if (!staticOk)
            {
                throw new InvalidOperationException(
                    "Gate estático de formación 4F: " + failed +
                    " fallos / " + passed + " correctos.");
            }

            EditorUtility.SetDirty(trainingPanel);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena con la formación 4F.");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Formación",
                "Formación 4F instalada y gate estático correcto.\n\n" +
                "Pendiente validación visual/funcional real en Play Mode.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog(
                "Bistro Builder — 4F Formación",
                "La instalación falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static void ConfigurePanel(
        BistroBuilderStaffPlayerTrainingPanel panel,
        BistroBuilderStaffPlayerFacade facade,
        BistroBuilderStaffPlayerScreen screen,
        Button openButton,
        GameObject modal,
        Button close,
        TMP_Text employeeText,
        TMP_Text feedback,
        BistroBuilderStaffDevelopmentProfile profile,
        List<Button> buttons,
        List<TMP_Text> labels)
    {
        var serialized = new SerializedObject(panel);
        SetObject(serialized, "facade", facade);
        SetObject(serialized, "screen", screen);
        SetObject(serialized, "openButton", openButton);
        SetObject(serialized, "modalRoot", modal);
        SetObject(serialized, "closeButton", close);
        SetObject(serialized, "employeeText", employeeText);
        SetObject(serialized, "feedbackText", feedback);

        SerializedProperty bindings = serialized.FindProperty("bindings");
        if (bindings == null)
        {
            throw new InvalidOperationException(
                "No existe la colección serializada bindings de formación 4F.");
        }

        bindings.arraySize = profile.Trainings.Count;
        for (int index = 0; index < profile.Trainings.Count; index++)
        {
            BistroBuilderStaffTrainingDefinition training = profile.Trainings[index];
            SerializedProperty item = bindings.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("trainingId").stringValue = training.trainingId;
            item.FindPropertyRelative("displayName").stringValue = training.displayName;
            item.FindPropertyRelative("skillGain").intValue = training.skillGain;
            item.FindPropertyRelative("financialCostCents").longValue =
                training.financialCostCents;
            item.FindPropertyRelative("button").objectReferenceValue = buttons[index];
            item.FindPropertyRelative("label").objectReferenceValue = labels[index];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var matches = new List<T>();
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
            {
                matches.Add(all[index]);
            }
        }
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                "Se esperaba exactamente un " + typeof(T).Name +
                " y hay " + matches.Count + ".");
        }
        return matches[0];
    }

    private static void DestroyChildIfPresent(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private static RectTransform FindRect(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        RectTransform rect = found != null ? found.GetComponent<RectTransform>() : null;
        if (rect == null)
        {
            throw new InvalidOperationException("No existe " + name + " en EmployeeDetail.");
        }
        return rect;
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        GameObject go = NewUi(name, parent);
        SetAnchors(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Image image = Undo.AddComponent<Image>(go);
        image.color = new Color(0.055f, 0.064f, 0.075f, 0.995f);
        return go;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        GameObject go = NewUi(name, parent);
        SetAnchors(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        Image image = Undo.AddComponent<Image>(go);
        image.color = new Color(0.12f, 0.14f, 0.16f, 1f);
        Button button = Undo.AddComponent<Button>(go);
        button.targetGraphic = image;
        CreateText(go.transform, "Label", label, 16f, 0f, 0f, 1f, 1f);
        return button;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        GameObject go = NewUi(name, parent);
        SetAnchors(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        TextMeshProUGUI label = Undo.AddComponent<TextMeshProUGUI>(go);
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private static GameObject NewUi(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Crear UI formación 4F");
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetAnchors(
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

    private static void SetObject(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad " + propertyName + " en TrainingPanel.");
        }
        property.objectReferenceValue = value;
    }
}
