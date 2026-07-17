using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Instala o repara el servicio de eliminación y el panel contextual
/// de artículos colocables.
///
/// La operación es idempotente y solo administra objetos reservados
/// por Bistro Builder.
/// </summary>
public static class BistroBuilderPlaceableContextInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Edit Mode/" +
        "Install or Repair Context Panel";

    private const string CanvasObjectName =
        "Canvas_BistroBuilder_EditMode";

    private const string ControllerObjectName =
        "PlaceableContextController";

    private const string ContentObjectName =
        "PlaceableContextContent";

    private static readonly Color Charcoal =
        new Color32(29, 34, 32, 247);

    private static readonly Color CharcoalLight =
        new Color32(45, 51, 48, 255);

    private static readonly Color Green =
        new Color32(75, 103, 85, 255);

    private static readonly Color Gold =
        new Color32(183, 155, 91, 255);

    private static readonly Color Beige =
        new Color32(232, 226, 211, 255);

    private static readonly Color Muted =
        new Color32(185, 189, 180, 255);

    private static readonly Color Danger =
        new Color32(132, 66, 58, 255);

    [MenuItem(MenuPath, false, 220)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de instalar el panel contextual.",
                "Aceptar"
            );

            return;
        }

        GameObject gameSystems =
            GameObject.Find("GameSystems");

        if (gameSystems == null)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "No se encontró GameSystems en la escena activa.",
                "Aceptar"
            );

            return;
        }

        RestaurantEditModeService editModeService =
            gameSystems.GetComponent<
                RestaurantEditModeService
            >();

        RestaurantEditInteractionController interactionController =
            gameSystems.GetComponent<
                RestaurantEditInteractionController
            >();

        RestaurantPlacementTransactionService transactionService =
            gameSystems.GetComponent<
                RestaurantPlacementTransactionService
            >();

        RestaurantPlaceableLifecycleService lifecycleService =
            gameSystems.GetComponent<
                RestaurantPlaceableLifecycleService
            >();

        RestaurantPlacementHistoryService historyService =
            gameSystems.GetComponent<
                RestaurantPlacementHistoryService
            >();

        if (editModeService == null ||
            interactionController == null ||
            transactionService == null ||
            lifecycleService == null ||
            historyService == null)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "GameSystems no contiene todos los servicios " +
                "necesarios para selección y eliminación.",
                "Aceptar"
            );

            return;
        }

        Undo.IncrementCurrentGroup();

        int undoGroup =
            Undo.GetCurrentGroup();

        Undo.SetCurrentGroupName(
            "Instalar panel contextual"
        );

        try
        {
            RestaurantPlaceableDeletionService deletionService =
                EnsureComponent<
                    RestaurantPlaceableDeletionService
                >(gameSystems);

            AssignReference(
                deletionService,
                "editModeService",
                editModeService
            );

            AssignReference(
                deletionService,
                "transactionService",
                transactionService
            );

            AssignReference(
                deletionService,
                "lifecycleService",
                lifecycleService
            );

            AssignReference(
                deletionService,
                "historyService",
                historyService
            );

            Canvas canvas =
                EnsureCanvas();

            RestaurantPlaceableContextPanel panel =
                EnsureContextPanel(
                    canvas.transform
                );

            AssignReference(
                panel,
                "editModeService",
                editModeService
            );

            AssignReference(
                panel,
                "interactionController",
                interactionController
            );

            AssignReference(
                panel,
                "deletionService",
                deletionService
            );

            EditorUtility.SetDirty(gameSystems);
            EditorUtility.SetDirty(canvas.gameObject);
            EditorUtility.SetDirty(panel.gameObject);

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene()
            );

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();

            Undo.CollapseUndoOperations(
                undoGroup
            );

            Debug.Log(
                "Panel contextual instalado. Seleccionar un artículo " +
                "ya no inicia movimiento automático."
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Panel contextual instalado.\n\n" +
                "En Play:\n" +
                "1. Pulsa F2.\n" +
                "2. Haz clic en una mesa.\n" +
                "3. Debe aparecer el panel de la derecha.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "No se pudo completar la instalación.\n\n" +
                "Consulta el primer error rojo de Console.",
                "Aceptar"
            );
        }
    }

    private static Canvas EnsureCanvas()
    {
        GameObject canvasObject =
            GameObject.Find(CanvasObjectName);

        if (canvasObject == null)
        {
            canvasObject =
                new GameObject(
                    CanvasObjectName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster)
                );

            Undo.RegisterCreatedObjectUndo(
                canvasObject,
                "Crear Canvas de modo edición"
            );
        }

        Canvas canvas =
            EnsureComponent<Canvas>(
                canvasObject
            );

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder =
            80;

        CanvasScaler scaler =
            EnsureComponent<CanvasScaler>(
                canvasObject
            );

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        scaler.matchWidthOrHeight =
            0.5f;

        EnsureComponent<GraphicRaycaster>(
            canvasObject
        );

        return canvas;
    }

    private static RestaurantPlaceableContextPanel
        EnsureContextPanel(
            Transform canvasTransform
        )
    {
        Transform existingController =
            canvasTransform.Find(
                ControllerObjectName
            );

        GameObject controllerObject;

        if (existingController == null)
        {
            controllerObject =
                CreateUIObject(
                    ControllerObjectName,
                    canvasTransform
                );

            StretchFull(
                controllerObject.GetComponent<RectTransform>()
            );
        }
        else
        {
            controllerObject =
                existingController.gameObject;
        }

        RestaurantPlaceableContextPanel panel =
            EnsureComponent<
                RestaurantPlaceableContextPanel
            >(controllerObject);

        Transform existingContent =
            controllerObject.transform.Find(
                ContentObjectName
            );

        GameObject contentObject =
            existingContent != null
                ? existingContent.gameObject
                : BuildContent(
                    controllerObject.transform
                );

        Text nameText =
            FindRequired<Text>(
                contentObject.transform,
                "Header/Name"
            );

        Text categoryText =
            FindRequired<Text>(
                contentObject.transform,
                "Header/Category"
            );

        Text statusText =
            FindRequired<Text>(
                contentObject.transform,
                "Status"
            );

        Button moveButton =
            FindRequired<Button>(
                contentObject.transform,
                "Actions/Move"
            );

        Button deleteButton =
            FindRequired<Button>(
                contentObject.transform,
                "Actions/Delete"
            );

        Text moveLabel =
            FindRequired<Text>(
                contentObject.transform,
                "Actions/Move/Label"
            );

        Text deleteLabel =
            FindRequired<Text>(
                contentObject.transform,
                "Actions/Delete/Label"
            );

        moveLabel.text =
            "Mover";

        deleteLabel.text =
            "Eliminar";

        EditorUtility.SetDirty(
            moveLabel
        );

        EditorUtility.SetDirty(
            deleteLabel
        );

        AssignReference(
            panel,
            "contentRoot",
            contentObject
        );

        AssignReference(
            panel,
            "nameText",
            nameText
        );

        AssignReference(
            panel,
            "categoryText",
            categoryText
        );

        AssignReference(
            panel,
            "statusText",
            statusText
        );

        AssignReference(
            panel,
            "moveButton",
            moveButton
        );

        AssignReference(
            panel,
            "deleteButton",
            deleteButton
        );

        contentObject.SetActive(false);

        return panel;
    }

    private static GameObject BuildContent(
        Transform controllerTransform
    )
    {
        Font font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        GameObject content =
            CreateUIObject(
                ContentObjectName,
                controllerTransform
            );

        RectTransform contentRect =
            content.GetComponent<RectTransform>();

        contentRect.anchorMin =
            new Vector2(1f, 0.5f);

        contentRect.anchorMax =
            new Vector2(1f, 0.5f);

        contentRect.pivot =
            new Vector2(1f, 0.5f);

        contentRect.anchoredPosition =
            new Vector2(-18f, 60f);

        contentRect.sizeDelta =
            new Vector2(310f, 220f);

        Image background =
            Undo.AddComponent<Image>(
                content
            );

        background.color =
            Charcoal;

        Shadow shadow =
            Undo.AddComponent<Shadow>(
                content
            );

        shadow.effectColor =
            new Color(0f, 0f, 0f, 0.55f);

        shadow.effectDistance =
            new Vector2(-4f, -4f);

        GameObject accent =
            CreateUIObject(
                "Accent",
                content.transform
            );

        RectTransform accentRect =
            accent.GetComponent<RectTransform>();

        accentRect.anchorMin =
            new Vector2(0f, 1f);

        accentRect.anchorMax =
            new Vector2(1f, 1f);

        accentRect.pivot =
            new Vector2(0.5f, 1f);

        accentRect.offsetMin =
            new Vector2(0f, -3f);

        accentRect.offsetMax =
            Vector2.zero;

        Image accentImage =
            Undo.AddComponent<Image>(
                accent
            );

        accentImage.color =
            Gold;

        GameObject header =
            CreateUIObject(
                "Header",
                content.transform
            );

        RectTransform headerRect =
            header.GetComponent<RectTransform>();

        headerRect.anchorMin =
            new Vector2(0f, 1f);

        headerRect.anchorMax =
            new Vector2(1f, 1f);

        headerRect.pivot =
            new Vector2(0.5f, 1f);

        headerRect.offsetMin =
            new Vector2(18f, -72f);

        headerRect.offsetMax =
            new Vector2(-18f, -14f);

        Text name =
            CreateText(
                "Name",
                header.transform,
                font,
                20,
                FontStyle.Bold,
                Beige,
                TextAnchor.UpperLeft
            );

        RectTransform nameRect =
            name.rectTransform;

        nameRect.anchorMin =
            new Vector2(0f, 0.45f);

        nameRect.anchorMax =
            new Vector2(1f, 1f);

        nameRect.offsetMin =
            Vector2.zero;

        nameRect.offsetMax =
            Vector2.zero;

        Text category =
            CreateText(
                "Category",
                header.transform,
                font,
                13,
                FontStyle.Normal,
                Muted,
                TextAnchor.LowerLeft
            );

        RectTransform categoryRect =
            category.rectTransform;

        categoryRect.anchorMin =
            new Vector2(0f, 0f);

        categoryRect.anchorMax =
            new Vector2(1f, 0.45f);

        categoryRect.offsetMin =
            Vector2.zero;

        categoryRect.offsetMax =
            Vector2.zero;

        Text status =
            CreateText(
                "Status",
                content.transform,
                font,
                14,
                FontStyle.Normal,
                Muted,
                TextAnchor.UpperLeft
            );

        RectTransform statusRect =
            status.rectTransform;

        statusRect.anchorMin =
            new Vector2(0f, 0f);

        statusRect.anchorMax =
            new Vector2(1f, 1f);

        statusRect.offsetMin =
            new Vector2(18f, 82f);

        statusRect.offsetMax =
            new Vector2(-18f, -86f);

        status.horizontalOverflow =
            HorizontalWrapMode.Wrap;

        status.verticalOverflow =
            VerticalWrapMode.Truncate;

        GameObject actions =
            CreateUIObject(
                "Actions",
                content.transform
            );

        RectTransform actionsRect =
            actions.GetComponent<RectTransform>();

        actionsRect.anchorMin =
            new Vector2(0f, 0f);

        actionsRect.anchorMax =
            new Vector2(1f, 0f);

        actionsRect.pivot =
            new Vector2(0.5f, 0f);

        actionsRect.offsetMin =
            new Vector2(18f, 18f);

        actionsRect.offsetMax =
            new Vector2(-18f, 68f);

        HorizontalLayoutGroup layout =
            Undo.AddComponent<HorizontalLayoutGroup>(
                actions
            );

        layout.spacing =
            10f;

        layout.childAlignment =
            TextAnchor.MiddleCenter;

        layout.childControlWidth =
            true;

        layout.childControlHeight =
            true;

        layout.childForceExpandWidth =
            true;

        layout.childForceExpandHeight =
            true;

        CreateButton(
            "Move",
            "Mover",
            actions.transform,
            font,
            Green
        );

        CreateButton(
            "Delete",
            "Eliminar",
            actions.transform,
            font,
            Danger
        );

        return content;
    }

    private static Button CreateButton(
        string objectName,
        string label,
        Transform parent,
        Font font,
        Color normalColor
    )
    {
        GameObject buttonObject =
            CreateUIObject(
                objectName,
                parent
            );

        Image image =
            Undo.AddComponent<Image>(
                buttonObject
            );

        image.color =
            normalColor;

        Button button =
            Undo.AddComponent<Button>(
                buttonObject
            );

        ColorBlock colors =
            button.colors;

        colors.normalColor =
            normalColor;

        colors.highlightedColor =
            Color.Lerp(
                normalColor,
                Color.white,
                0.14f
            );

        colors.pressedColor =
            Gold;

        colors.selectedColor =
            colors.highlightedColor;

        colors.disabledColor =
            new Color(
                normalColor.r,
                normalColor.g,
                normalColor.b,
                0.35f
            );

        button.colors =
            colors;

        Text text =
            CreateText(
                "Label",
                buttonObject.transform,
                font,
                15,
                FontStyle.Bold,
                Beige,
                TextAnchor.MiddleCenter
            );

        text.text =
            label;

        StretchFull(
            text.rectTransform
        );

        return button;
    }

    private static T EnsureComponent<T>(
        GameObject gameObject
    )
        where T : Component
    {
        T existing =
            gameObject.GetComponent<T>();

        if (existing != null)
        {
            return existing;
        }

        return Undo.AddComponent<T>(
            gameObject
        );
    }

    private static GameObject CreateUIObject(
        string objectName,
        Transform parent
    )
    {
        GameObject gameObject =
            new GameObject(
                objectName,
                typeof(RectTransform)
            );

        Undo.RegisterCreatedObjectUndo(
            gameObject,
            "Crear " + objectName
        );

        gameObject.transform.SetParent(
            parent,
            false
        );

        return gameObject;
    }

    private static Text CreateText(
        string objectName,
        Transform parent,
        Font font,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment
    )
    {
        GameObject gameObject =
            CreateUIObject(
                objectName,
                parent
            );

        Text text =
            Undo.AddComponent<Text>(
                gameObject
            );

        text.font =
            font;

        text.fontSize =
            fontSize;

        text.fontStyle =
            fontStyle;

        text.color =
            color;

        text.alignment =
            alignment;

        text.raycastTarget =
            false;

        return text;
    }

    private static T FindRequired<T>(
        Transform root,
        string relativePath
    )
        where T : Component
    {
        Transform found =
            root.Find(relativePath);

        if (found == null)
        {
            throw new InvalidOperationException(
                "No se encontró " +
                relativePath +
                " dentro del panel contextual."
            );
        }

        T component =
            found.GetComponent<T>();

        if (component == null)
        {
            throw new InvalidOperationException(
                relativePath +
                " no contiene " +
                typeof(T).Name +
                "."
            );
        }

        return component;
    }

    private static void AssignReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serializedObject =
            new SerializedObject(target);

        SerializedProperty property =
            serializedObject.FindProperty(
                propertyName
            );

        if (property == null)
        {
            throw new InvalidOperationException(
                target.name +
                " no contiene la propiedad " +
                propertyName +
                "."
            );
        }

        property.objectReferenceValue =
            value;

        serializedObject.ApplyModifiedProperties();
    }

    private static void StretchFull(
        RectTransform rectTransform
    )
    {
        rectTransform.anchorMin =
            Vector2.zero;

        rectTransform.anchorMax =
            Vector2.one;

        rectTransform.offsetMin =
            Vector2.zero;

        rectTransform.offsetMax =
            Vector2.zero;
    }
}
