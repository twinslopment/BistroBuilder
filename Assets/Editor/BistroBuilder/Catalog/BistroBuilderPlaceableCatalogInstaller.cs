using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Instala o repara el primer catálogo runtime de artículos.
///
/// Automatiza:
/// - Creación y actualización del asset de catálogo.
/// - Registro de todas las definiciones de artículos existentes.
/// - Instalación del servicio en GameSystems.
/// - Creación del Canvas y panel de catálogo.
/// - Creación de plantillas de categorías y artículos.
/// - Conexión de referencias.
///
/// La operación es idempotente y solo administra objetos con nombres
/// reservados por Bistro Builder.
/// </summary>
public static class BistroBuilderPlaceableCatalogInstaller
{
    private const string MenuPath =
        "Tools/Bistro Builder/Catalog/" +
        "Install or Repair Runtime Catalog";

    private const string CatalogAssetPath =
        "Assets/Data/Restaurant/EditMode/Catalog/" +
        "RestaurantPlaceableCatalog_Main.asset";

    private const string CanvasObjectName =
        "Canvas_BistroBuilder_EditMode";

    private const string ControllerObjectName =
        "EditModePlaceableCatalog";

    private const string ContentObjectName =
        "CatalogContent";

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

    private static readonly Color MutedText =
        new Color32(185, 189, 180, 255);

    [MenuItem(MenuPath, false, 200)]
    private static void InstallOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play antes de instalar el catálogo.",
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

        if (editModeService == null ||
            interactionController == null)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "GameSystems no contiene RestaurantEditModeService " +
                "y RestaurantEditInteractionController.",
                "Aceptar"
            );

            return;
        }

        Undo.IncrementCurrentGroup();

        int undoGroup =
            Undo.GetCurrentGroup();

        Undo.SetCurrentGroupName(
            "Instalar catálogo de artículos"
        );

        try
        {
            RestaurantPlaceableCatalogDefinition catalogDefinition =
                CreateOrUpdateCatalogAsset(
                    out int itemCount
                );

            RestaurantPlaceableCatalogService catalogService =
                EnsureComponent<
                    RestaurantPlaceableCatalogService
                >(gameSystems);

            AssignObjectReference(
                catalogService,
                "catalogDefinition",
                catalogDefinition
            );

            Canvas canvas =
                EnsureCatalogCanvas();

            EnsureEventSystem();

            RestaurantPlaceableCatalogPanel panel =
                EnsureCatalogPanel(
                    canvas.transform
                );

            WirePanelReferences(
                panel,
                editModeService,
                interactionController,
                catalogService
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
                "Catálogo runtime instalado. Artículos: " +
                itemCount +
                ". Servicio: GameSystems. UI: " +
                CanvasObjectName +
                "."
            );

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Catálogo instalado correctamente.\n\n" +
                "Artículos registrados: " + itemCount + "\n" +
                "Servicio: GameSystems\n" +
                "UI: " + CanvasObjectName + "\n\n" +
                "En Play, pulsa F2 para mostrarlo.",
                "Aceptar"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación no pudo completarse.\n\n" +
                "Consulta el primer error rojo de Console.",
                "Aceptar"
            );
        }
    }

    private static RestaurantPlaceableCatalogDefinition
        CreateOrUpdateCatalogAsset(
            out int itemCount
        )
    {
        EnsureAssetFolderExists(
            Path.GetDirectoryName(CatalogAssetPath)
        );

        RestaurantPlaceableCatalogDefinition catalog =
            AssetDatabase.LoadAssetAtPath<
                RestaurantPlaceableCatalogDefinition
            >(CatalogAssetPath);

        if (catalog == null)
        {
            catalog =
                ScriptableObject.CreateInstance<
                    RestaurantPlaceableCatalogDefinition
                >();

            AssetDatabase.CreateAsset(
                catalog,
                CatalogAssetPath
            );
        }

        string[] itemGuids =
            AssetDatabase.FindAssets(
                "t:RestaurantPlaceableItemDefinition"
            );

        List<RestaurantPlaceableItemDefinition> definitions =
            new List<RestaurantPlaceableItemDefinition>(
                itemGuids.Length
            );

        foreach (string itemGuid in itemGuids)
        {
            string itemPath =
                AssetDatabase.GUIDToAssetPath(
                    itemGuid
                );

            RestaurantPlaceableItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    RestaurantPlaceableItemDefinition
                >(itemPath);

            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort(
            CompareDefinitions
        );

        SerializedObject serializedCatalog =
            new SerializedObject(catalog);

        SerializedProperty itemsProperty =
            serializedCatalog.FindProperty("items");

        itemsProperty.arraySize =
            definitions.Count;

        for (int index = 0;
             index < definitions.Count;
             index++)
        {
            itemsProperty
                .GetArrayElementAtIndex(index)
                .objectReferenceValue =
                    definitions[index];
        }

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(catalog);

        itemCount =
            definitions.Count;

        return catalog;
    }

    private static Canvas EnsureCatalogCanvas()
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
                "Crear Canvas de catálogo"
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

    private static void EnsureEventSystem()
    {
        EventSystem existingEventSystem =
            UnityEngine.Object.FindFirstObjectByType<
                EventSystem
            >(
                FindObjectsInactive.Include
            );

        if (existingEventSystem != null)
        {
            if (existingEventSystem.GetComponent<
                    InputSystemUIInputModule
                >() == null)
            {
                EnsureComponent<
                    InputSystemUIInputModule
                >(existingEventSystem.gameObject);
            }

            return;
        }

        GameObject eventSystemObject =
            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );

        Undo.RegisterCreatedObjectUndo(
            eventSystemObject,
            "Crear EventSystem"
        );
    }

    private static RestaurantPlaceableCatalogPanel
        EnsureCatalogPanel(
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

        RestaurantPlaceableCatalogPanel panel =
            EnsureComponent<
                RestaurantPlaceableCatalogPanel
            >(controllerObject);

        Transform existingContent =
            controllerObject.transform.Find(
                ContentObjectName
            );

        GameObject contentObject;

        if (existingContent == null)
        {
            contentObject =
                BuildCatalogContent(
                    controllerObject.transform
                );
        }
        else
        {
            contentObject =
                existingContent.gameObject;
        }

        RectTransform categoryContainer =
            FindRequiredRectTransform(
                contentObject.transform,
                "CategoryBar"
            );

        RectTransform itemContainer =
            FindRequiredRectTransform(
                contentObject.transform,
                "ItemsScroll/Viewport/Items"
            );

        RestaurantPlaceableCatalogCategoryView categoryTemplate =
            FindRequiredComponent<
                RestaurantPlaceableCatalogCategoryView
            >(
                controllerObject.transform,
                "Templates/CategoryTemplate"
            );

        RestaurantPlaceableCatalogItemView itemTemplate =
            FindRequiredComponent<
                RestaurantPlaceableCatalogItemView
            >(
                controllerObject.transform,
                "Templates/ItemTemplate"
            );

        Text titleText =
            FindRequiredComponent<Text>(
                contentObject.transform,
                "Header/Title"
            );

        Text statusText =
            FindRequiredComponent<Text>(
                contentObject.transform,
                "Header/Status"
            );

        AssignObjectReference(
            panel,
            "contentRoot",
            contentObject
        );

        AssignObjectReference(
            panel,
            "categoryContainer",
            categoryContainer
        );

        AssignObjectReference(
            panel,
            "itemContainer",
            itemContainer
        );

        AssignObjectReference(
            panel,
            "categoryTemplate",
            categoryTemplate
        );

        AssignObjectReference(
            panel,
            "itemTemplate",
            itemTemplate
        );

        AssignObjectReference(
            panel,
            "titleText",
            titleText
        );

        AssignObjectReference(
            panel,
            "statusText",
            statusText
        );

        contentObject.SetActive(false);

        return panel;
    }

    private static GameObject BuildCatalogContent(
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
            new Vector2(0f, 0f);

        contentRect.anchorMax =
            new Vector2(1f, 0f);

        contentRect.pivot =
            new Vector2(0.5f, 0f);

        contentRect.offsetMin =
            new Vector2(18f, 18f);

        contentRect.offsetMax =
            new Vector2(-18f, 278f);

        Image background =
            content.AddComponent<Image>();

        background.color =
            Charcoal;

        Shadow shadow =
            content.AddComponent<Shadow>();

        shadow.effectColor =
            new Color(0f, 0f, 0f, 0.55f);

        shadow.effectDistance =
            new Vector2(0f, -4f);

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
            accent.AddComponent<Image>();

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
            new Vector2(20f, -50f);

        headerRect.offsetMax =
            new Vector2(-20f, -10f);

        Text title =
            CreateText(
                "Title",
                header.transform,
                font,
                22,
                FontStyle.Bold,
                Beige,
                TextAnchor.MiddleLeft
            );

        RectTransform titleRect =
            title.rectTransform;

        titleRect.anchorMin =
            new Vector2(0f, 0f);

        titleRect.anchorMax =
            new Vector2(0.45f, 1f);

        titleRect.offsetMin =
            Vector2.zero;

        titleRect.offsetMax =
            Vector2.zero;

        Text status =
            CreateText(
                "Status",
                header.transform,
                font,
                15,
                FontStyle.Normal,
                MutedText,
                TextAnchor.MiddleRight
            );

        RectTransform statusRect =
            status.rectTransform;

        statusRect.anchorMin =
            new Vector2(0.45f, 0f);

        statusRect.anchorMax =
            new Vector2(1f, 1f);

        statusRect.offsetMin =
            Vector2.zero;

        statusRect.offsetMax =
            Vector2.zero;

        GameObject categoryBar =
            CreateUIObject(
                "CategoryBar",
                content.transform
            );

        RectTransform categoryRect =
            categoryBar.GetComponent<RectTransform>();

        categoryRect.anchorMin =
            new Vector2(0f, 1f);

        categoryRect.anchorMax =
            new Vector2(1f, 1f);

        categoryRect.pivot =
            new Vector2(0.5f, 1f);

        categoryRect.offsetMin =
            new Vector2(20f, -90f);

        categoryRect.offsetMax =
            new Vector2(-20f, -54f);

        HorizontalLayoutGroup categoryLayout =
            categoryBar.AddComponent<
                HorizontalLayoutGroup
            >();

        categoryLayout.spacing =
            8f;

        categoryLayout.childAlignment =
            TextAnchor.MiddleLeft;

        categoryLayout.childControlWidth =
            false;

        categoryLayout.childControlHeight =
            true;

        categoryLayout.childForceExpandWidth =
            false;

        categoryLayout.childForceExpandHeight =
            true;

        GameObject scroll =
            CreateUIObject(
                "ItemsScroll",
                content.transform
            );

        RectTransform scrollRectTransform =
            scroll.GetComponent<RectTransform>();

        scrollRectTransform.anchorMin =
            new Vector2(0f, 0f);

        scrollRectTransform.anchorMax =
            new Vector2(1f, 1f);

        scrollRectTransform.offsetMin =
            new Vector2(20f, 18f);

        scrollRectTransform.offsetMax =
            new Vector2(-20f, -98f);

        ScrollRect scrollRect =
            scroll.AddComponent<ScrollRect>();

        scrollRect.horizontal =
            true;

        scrollRect.vertical =
            false;

        scrollRect.movementType =
            ScrollRect.MovementType.Clamped;

        scrollRect.scrollSensitivity =
            35f;

        GameObject viewport =
            CreateUIObject(
                "Viewport",
                scroll.transform
            );

        StretchFull(
            viewport.GetComponent<RectTransform>()
        );

        Image viewportImage =
            viewport.AddComponent<Image>();

        viewportImage.color =
            new Color(1f, 1f, 1f, 0.01f);

        Mask mask =
            viewport.AddComponent<Mask>();

        mask.showMaskGraphic =
            false;

        GameObject items =
            CreateUIObject(
                "Items",
                viewport.transform
            );

        RectTransform itemsRect =
            items.GetComponent<RectTransform>();

        itemsRect.anchorMin =
            new Vector2(0f, 0f);

        itemsRect.anchorMax =
            new Vector2(0f, 1f);

        itemsRect.pivot =
            new Vector2(0f, 0.5f);

        itemsRect.anchoredPosition =
            Vector2.zero;

        itemsRect.sizeDelta =
            new Vector2(0f, 0f);

        HorizontalLayoutGroup itemsLayout =
            items.AddComponent<HorizontalLayoutGroup>();

        itemsLayout.spacing =
            12f;

        itemsLayout.padding =
            new RectOffset(0, 12, 0, 0);

        itemsLayout.childAlignment =
            TextAnchor.MiddleLeft;

        itemsLayout.childControlWidth =
            false;

        itemsLayout.childControlHeight =
            true;

        itemsLayout.childForceExpandWidth =
            false;

        itemsLayout.childForceExpandHeight =
            true;

        ContentSizeFitter contentSizeFitter =
            items.AddComponent<ContentSizeFitter>();

        contentSizeFitter.horizontalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        contentSizeFitter.verticalFit =
            ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport =
            viewport.GetComponent<RectTransform>();

        scrollRect.content =
            itemsRect;

        GameObject templates =
            CreateUIObject(
                "Templates",
                controllerTransform
            );

        templates.SetActive(false);

        BuildCategoryTemplate(
            templates.transform,
            font
        );

        BuildItemTemplate(
            templates.transform,
            font
        );

        return content;
    }

    private static void BuildCategoryTemplate(
        Transform templatesRoot,
        Font font
    )
    {
        GameObject template =
            CreateUIObject(
                "CategoryTemplate",
                templatesRoot
            );

        RectTransform rect =
            template.GetComponent<RectTransform>();

        rect.sizeDelta =
            new Vector2(122f, 32f);

        Image background =
            template.AddComponent<Image>();

        background.color =
            CharcoalLight;

        Button button =
            template.AddComponent<Button>();

        ConfigureButtonColors(
            button,
            CharcoalLight,
            Green
        );

        LayoutElement layout =
            template.AddComponent<LayoutElement>();

        layout.preferredWidth =
            122f;

        layout.minWidth =
            92f;

        Text label =
            CreateText(
                "Label",
                template.transform,
                font,
                14,
                FontStyle.Bold,
                Beige,
                TextAnchor.MiddleCenter
            );

        StretchFull(
            label.rectTransform
        );

        RestaurantPlaceableCatalogCategoryView view =
            template.AddComponent<
                RestaurantPlaceableCatalogCategoryView
            >();

        AssignObjectReference(
            view,
            "button",
            button
        );

        AssignObjectReference(
            view,
            "backgroundImage",
            background
        );

        AssignObjectReference(
            view,
            "labelText",
            label
        );
    }

    private static void BuildItemTemplate(
        Transform templatesRoot,
        Font font
    )
    {
        GameObject template =
            CreateUIObject(
                "ItemTemplate",
                templatesRoot
            );

        RectTransform rect =
            template.GetComponent<RectTransform>();

        rect.sizeDelta =
            new Vector2(190f, 142f);

        Image background =
            template.AddComponent<Image>();

        background.color =
            CharcoalLight;

        Button button =
            template.AddComponent<Button>();

        ConfigureButtonColors(
            button,
            CharcoalLight,
            Green
        );

        LayoutElement layout =
            template.AddComponent<LayoutElement>();

        layout.preferredWidth =
            190f;

        layout.minWidth =
            190f;

        layout.preferredHeight =
            142f;

        GameObject iconRoot =
            CreateUIObject(
                "IconRoot",
                template.transform
            );

        RectTransform iconRootRect =
            iconRoot.GetComponent<RectTransform>();

        iconRootRect.anchorMin =
            new Vector2(0f, 1f);

        iconRootRect.anchorMax =
            new Vector2(0f, 1f);

        iconRootRect.pivot =
            new Vector2(0f, 1f);

        iconRootRect.anchoredPosition =
            new Vector2(12f, -12f);

        iconRootRect.sizeDelta =
            new Vector2(48f, 48f);

        Image iconBackground =
            iconRoot.AddComponent<Image>();

        iconBackground.color =
            Green;

        Image icon =
            CreateUIObject(
                "Icon",
                iconRoot.transform
            ).AddComponent<Image>();

        StretchFull(
            icon.rectTransform,
            5f
        );

        icon.preserveAspect =
            true;

        Text iconFallback =
            CreateText(
                "Fallback",
                iconRoot.transform,
                font,
                22,
                FontStyle.Bold,
                Beige,
                TextAnchor.MiddleCenter
            );

        StretchFull(
            iconFallback.rectTransform
        );

        Text name =
            CreateText(
                "Name",
                template.transform,
                font,
                16,
                FontStyle.Bold,
                Beige,
                TextAnchor.UpperLeft
            );

        RectTransform nameRect =
            name.rectTransform;

        nameRect.anchorMin =
            new Vector2(0f, 1f);

        nameRect.anchorMax =
            new Vector2(1f, 1f);

        nameRect.pivot =
            new Vector2(0f, 1f);

        nameRect.offsetMin =
            new Vector2(70f, -48f);

        nameRect.offsetMax =
            new Vector2(-10f, -10f);

        name.horizontalOverflow =
            HorizontalWrapMode.Wrap;

        name.verticalOverflow =
            VerticalWrapMode.Truncate;

        Text description =
            CreateText(
                "Description",
                template.transform,
                font,
                13,
                FontStyle.Normal,
                MutedText,
                TextAnchor.UpperLeft
            );

        RectTransform descriptionRect =
            description.rectTransform;

        descriptionRect.anchorMin =
            new Vector2(0f, 0f);

        descriptionRect.anchorMax =
            new Vector2(1f, 1f);

        descriptionRect.offsetMin =
            new Vector2(12f, 34f);

        descriptionRect.offsetMax =
            new Vector2(-12f, -68f);

        description.horizontalOverflow =
            HorizontalWrapMode.Wrap;

        description.verticalOverflow =
            VerticalWrapMode.Truncate;

        Text price =
            CreateText(
                "Price",
                template.transform,
                font,
                14,
                FontStyle.Bold,
                Gold,
                TextAnchor.MiddleRight
            );

        RectTransform priceRect =
            price.rectTransform;

        priceRect.anchorMin =
            new Vector2(0f, 0f);

        priceRect.anchorMax =
            new Vector2(1f, 0f);

        priceRect.pivot =
            new Vector2(0.5f, 0f);

        priceRect.offsetMin =
            new Vector2(12f, 8f);

        priceRect.offsetMax =
            new Vector2(-12f, 32f);

        RestaurantPlaceableCatalogItemView view =
            template.AddComponent<
                RestaurantPlaceableCatalogItemView
            >();

        AssignObjectReference(
            view,
            "button",
            button
        );

        AssignObjectReference(
            view,
            "iconImage",
            icon
        );

        AssignObjectReference(
            view,
            "iconFallbackText",
            iconFallback
        );

        AssignObjectReference(
            view,
            "nameText",
            name
        );

        AssignObjectReference(
            view,
            "descriptionText",
            description
        );

        AssignObjectReference(
            view,
            "priceText",
            price
        );
    }

    private static void WirePanelReferences(
        RestaurantPlaceableCatalogPanel panel,
        RestaurantEditModeService editModeService,
        RestaurantEditInteractionController interactionController,
        RestaurantPlaceableCatalogService catalogService
    )
    {
        AssignObjectReference(
            panel,
            "editModeService",
            editModeService
        );

        AssignObjectReference(
            panel,
            "interactionController",
            interactionController
        );

        AssignObjectReference(
            panel,
            "catalogService",
            catalogService
        );
    }

    private static T EnsureComponent<T>(
        GameObject gameObject
    )
        where T : Component
    {
        T component =
            gameObject.GetComponent<T>();

        if (component != null)
        {
            return component;
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
            gameObject.AddComponent<Text>();

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

    private static void ConfigureButtonColors(
        Button button,
        Color normalColor,
        Color highlightedColor
    )
    {
        ColorBlock colors =
            button.colors;

        colors.normalColor =
            normalColor;

        colors.highlightedColor =
            highlightedColor;

        colors.pressedColor =
            Gold;

        colors.selectedColor =
            highlightedColor;

        colors.disabledColor =
            new Color(
                normalColor.r,
                normalColor.g,
                normalColor.b,
                0.45f
            );

        colors.colorMultiplier =
            1f;

        colors.fadeDuration =
            0.08f;

        button.colors =
            colors;
    }

    private static RectTransform FindRequiredRectTransform(
        Transform root,
        string relativePath
    )
    {
        Transform found =
            root.Find(relativePath);

        if (found == null)
        {
            throw new InvalidOperationException(
                "No se encontró " +
                relativePath +
                " dentro del catálogo generado."
            );
        }

        return found.GetComponent<RectTransform>();
    }

    private static T FindRequiredComponent<T>(
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
                " dentro del catálogo generado."
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

    private static void AssignObjectReference(
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
                " no contiene la propiedad serializada " +
                propertyName +
                "."
            );
        }

        property.objectReferenceValue =
            value;

        serializedObject.ApplyModifiedProperties();
    }

    private static void StretchFull(
        RectTransform rectTransform,
        float inset = 0f
    )
    {
        rectTransform.anchorMin =
            Vector2.zero;

        rectTransform.anchorMax =
            Vector2.one;

        rectTransform.offsetMin =
            new Vector2(inset, inset);

        rectTransform.offsetMax =
            new Vector2(-inset, -inset);
    }

    private static void EnsureAssetFolderExists(
        string folderPath
    )
    {
        if (string.IsNullOrWhiteSpace(folderPath) ||
            AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string normalizedPath =
            folderPath.Replace("\\", "/");

        string[] segments =
            normalizedPath.Split('/');

        string currentPath =
            segments[0];

        for (int index = 1;
             index < segments.Length;
             index++)
        {
            string nextPath =
                currentPath +
                "/" +
                segments[index];

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(
                    currentPath,
                    segments[index]
                );
            }

            currentPath =
                nextPath;
        }
    }

    private static int CompareDefinitions(
        RestaurantPlaceableItemDefinition first,
        RestaurantPlaceableItemDefinition second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int categoryComparison =
            first.Category.CompareTo(
                second.Category
            );

        if (categoryComparison != 0)
        {
            return categoryComparison;
        }

        return string.Compare(
            first.DisplayName,
            second.DisplayName,
            StringComparison.CurrentCultureIgnoreCase
        );
    }
}
