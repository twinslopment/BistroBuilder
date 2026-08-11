#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor de Ingredientes de 2.3A.
///
/// No sustituye el catálogo canónico de ingredientes. Esta ventana sincroniza
/// sus IDs/nombre/unidad/categoría y permite enriquecerlos con la imagen visual
/// y los formatos comerciales reutilizables por Proveedores.
/// </summary>
public sealed class BistroBuilderIngredientEditor23A2Window : EditorWindow
{
    internal const string WindowTitle = "Editor de Ingredientes";
    private enum Tab
    {
        Identidad = 0,
        Imagen = 1,
        Formatos = 2,
        Relaciones = 3,
        Futuro = 4,
        Validacion = 5
    }

    private BistroBuilderIngredientAuthoringDatabase database;
    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private int selectedIndex = -1;
    private string search = string.Empty;
    private Vector2 leftScroll;
    private Vector2 centerScroll;
    private Vector2 rightScroll;
    private Tab activeTab;
    private BistroBuilderAuthoringValidationReport lastValidation;

    [MenuItem("Tools/Bistro Builder/Inventario/Editor de Ingredientes", priority = 22)]
    public static void OpenWindow()
    {
        BistroBuilderIngredientEditor23A2Window window =
            GetWindow<BistroBuilderIngredientEditor23A2Window>();

        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(980f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent(WindowTitle);
        ReloadDatabases();
        Undo.undoRedoPerformed += HandleUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= HandleUndoRedo;
    }

    private void HandleUndoRedo()
    {
        ReloadDatabases();
        Repaint();
    }

    private void ReloadDatabases()
    {
        database = BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();
        supplierDatabase = BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();

        if (database != null && database.Ingredients.Count > 0)
        {
            selectedIndex = Mathf.Clamp(
                selectedIndex < 0 ? 0 : selectedIndex,
                0,
                database.Ingredients.Count - 1);
        }
        else
        {
            selectedIndex = -1;
        }
    }

    private void OnGUI()
    {
        DrawTopToolbar();

        if (database == null)
        {
            EditorGUILayout.Space(16f);
            EditorGUILayout.HelpBox(
                "2.3A todavía no está instalado. La base visual/comercial de ingredientes se crea con el instalador de Proveedores.",
                MessageType.Info);

            if (GUILayout.Button("Instalar 2.3A", GUILayout.Height(34f)))
            {
                BistroBuilderSuppliers23A2Installer.InstallOrUpdate();
                ReloadDatabases();
            }

            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawLeftLibrary();
        DrawCenterEditor();
        DrawRightPreview();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(24f));

        if (GUILayout.Button("Sincronizar canónicos", EditorStyles.toolbarButton, GUILayout.Width(132f)))
        {
            SynchronizeCanonical(true);
        }

        using (new EditorGUI.DisabledScope(database == null))
        {
            if (GUILayout.Button("Guardar", EditorStyles.toolbarButton, GUILayout.Width(62f)))
            {
                SaveDatabase();
            }

            if (GUILayout.Button("Validar", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                ValidateAll(true);
            }
        }

        GUILayout.Space(8f);
        GUILayout.Label("Buscar", GUILayout.Width(44f));
        search = GUILayout.TextField(
            search ?? string.Empty,
            EditorStyles.toolbarSearchField,
            GUILayout.MinWidth(160f));

        GUILayout.FlexibleSpace();

        if (database == null)
        {
            GUILayout.Label("No instalado", EditorStyles.miniLabel);
        }
        else if (EditorUtility.IsDirty(database))
        {
            GUI.color = new Color(1f, 0.72f, 0.25f);
            GUILayout.Label("● Cambios sin guardar", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = new Color(0.45f, 0.90f, 0.55f);
            GUILayout.Label("● Guardado", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeftLibrary()
    {
        float width = Mathf.Max(235f, position.width * 0.24f);
        EditorGUILayout.BeginVertical(GUILayout.Width(width));
        DrawSectionHeader(
            "Biblioteca de Ingredientes",
            database.Ingredients.Count + " registros sincronizados");

        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredients =
            database.Ingredients;

        string normalizedSearch = (search ?? string.Empty).Trim();

        for (int index = 0; index < ingredients.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients[index];
            if (ingredient == null || !MatchesSearch(ingredient, normalizedSearch))
            {
                continue;
            }

            Rect row = EditorGUILayout.GetControlRect(false, 58f);
            DrawIngredientRow(row, ingredient, index == selectedIndex);

            if (Event.current.type == EventType.MouseDown &&
                row.Contains(Event.current.mousePosition))
            {
                selectedIndex = index;
                GUI.FocusControl(null);
                Repaint();
                Event.current.Use();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawIngredientRow(
        Rect rect,
        BistroBuilderIngredientAuthoringRecord ingredient,
        bool selected)
    {
        Color background = selected
            ? new Color(0.22f, 0.42f, 0.30f, 0.75f)
            : new Color(0.16f, 0.16f, 0.16f, 0.55f);

        EditorGUI.DrawRect(rect, background);

        Rect imageRect = new Rect(rect.x + 5f, rect.y + 5f, 48f, 48f);
        BistroBuilderSupplierEditor23A2Window.DrawSpriteOrPlaceholder(
            imageRect,
            ingredient.displayImage,
            new Color(0.28f, 0.30f, 0.27f, 1f),
            "IMG");

        GUI.Label(
            new Rect(rect.x + 61f, rect.y + 6f, rect.width - 66f, 18f),
            string.IsNullOrWhiteSpace(ingredient.displayNameSnapshot)
                ? ingredient.IngredientId
                : ingredient.displayNameSnapshot,
            EditorStyles.boldLabel);

        GUI.Label(
            new Rect(rect.x + 61f, rect.y + 25f, rect.width - 66f, 15f),
            BistroBuilderSupplierAuthoringPresentation23A3.IngredientSummary(ingredient),
            EditorStyles.miniLabel);

        GUI.Label(
            new Rect(rect.x + 61f, rect.y + 41f, rect.width - 66f, 14f),
            ingredient.commercialPackages.Count + " formato(s) comercial(es)",
            EditorStyles.miniLabel);
    }

    private void DrawCenterEditor()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        if (!TryGetSelected(out BistroBuilderIngredientAuthoringRecord selected))
        {
            EditorGUILayout.HelpBox(
                "No hay ingredientes sincronizados. Abre la escena principal o entra en Play Mode y pulsa 'Sincronizar canónicos'.",
                MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        string[] labels =
        {
            "Identidad",
            "Imagen",
            "Formatos comerciales",
            "Relaciones",
            "Futuro",
            "Validación"
        };

        activeTab = (Tab)GUILayout.Toolbar(
            (int)activeTab,
            labels,
            GUILayout.Height(26f));

        centerScroll = EditorGUILayout.BeginScrollView(centerScroll);

        SerializedObject serializedDatabase = new SerializedObject(database);
        SerializedProperty ingredientsProperty =
            serializedDatabase.FindProperty("ingredients");

        if (selectedIndex < 0 || selectedIndex >= ingredientsProperty.arraySize)
        {
            EditorGUILayout.HelpBox(
                "La selección quedó fuera de rango. Vuelve a sincronizar la biblioteca.",
                MessageType.Warning);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        serializedDatabase.Update();
        SerializedProperty ingredientProperty =
            ingredientsProperty.GetArrayElementAtIndex(selectedIndex);

        EditorGUI.BeginChangeCheck();

        switch (activeTab)
        {
            case Tab.Identidad:
                DrawIdentityTab(ingredientProperty);
                break;

            case Tab.Imagen:
                DrawImageTab(ingredientProperty);
                break;

            case Tab.Formatos:
                DrawPackagesTab(ingredientProperty, selected);
                break;

            case Tab.Relaciones:
                DrawRelationsTab(selected);
                break;

            case Tab.Futuro:
                DrawFutureTab(ingredientProperty);
                break;

            case Tab.Validacion:
                DrawValidationTab(selected);
                break;
        }

        bool changed = EditorGUI.EndChangeCheck();
        serializedDatabase.ApplyModifiedProperties();

        if (changed &&
            activeTab != Tab.Relaciones &&
            activeTab != Tab.Validacion)
        {
            database.EditorTouchRevision();
            EditorUtility.SetDirty(database);
            lastValidation = null;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawIdentityTab(SerializedProperty ingredient)
    {
        DrawSectionHeader(
            "Identidad canónica",
            "El ID, nombre, unidad y categoría se leen del dominio ya existente; este editor no crea una segunda autoridad.");

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(
                ingredient.FindPropertyRelative("ingredientId"),
                new GUIContent("IngredientId"));

            EditorGUILayout.PropertyField(
                ingredient.FindPropertyRelative("displayNameSnapshot"),
                new GUIContent("Nombre canónico"));

            EditorGUILayout.PropertyField(
                ingredient.FindPropertyRelative("canonicalUnitSnapshot"),
                new GUIContent("Unidad interna canónica"));

            EditorGUILayout.PropertyField(
                ingredient.FindPropertyRelative("categorySnapshot"),
                new GUIContent("Categoría canónica"));
        }

        string rawUnit = ingredient.FindPropertyRelative("canonicalUnitSnapshot").stringValue;
        string rawCategory = ingredient.FindPropertyRelative("categorySnapshot").stringValue;
        EditorGUILayout.LabelField(
            "Presentación en UI",
            BistroBuilderSupplierAuthoringPresentation23A3.Unit(rawUnit) + " · " +
            BistroBuilderSupplierAuthoringPresentation23A3.Category(rawCategory),
            EditorStyles.miniLabel);

        EditorGUILayout.PropertyField(
            ingredient.FindPropertyRelative("isActive"),
            new GUIContent("Visible en autoría 2.3"));

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Actualizar desde el catálogo canónico", GUILayout.Height(28f)))
        {
            SynchronizeCanonical(true);
        }

        EditorGUILayout.HelpBox(
            "Cambiar la unidad interna de un ingrediente que ya participa en recetas/inventario requiere una migración canónica. " +
            "2.3A la muestra y protege; no la sobrescribe desde una base paralela.",
            MessageType.Info);
    }

    private void DrawImageTab(SerializedProperty ingredient)
    {
        DrawSectionHeader(
            "Imagen principal",
            "Debe permitir reconocer el ingrediente antes de leer su nombre.");

        EditorGUILayout.PropertyField(
            ingredient.FindPropertyRelative("displayImage"),
            new GUIContent("Imagen del ingrediente"));

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Estándar visual recomendado: Sprite cuadrado 1:1, 512×512 o superior, producto centrado, fondo coherente, " +
            "iluminación neutra, sin texto, logos, manos ni decoración que dificulte identificarlo.",
            MessageType.Info);

        Sprite sprite = ingredient.FindPropertyRelative("displayImage").objectReferenceValue as Sprite;
        if (sprite != null)
        {
            Texture2D texture = sprite.texture;
            if (texture != null)
            {
                EditorGUILayout.LabelField(
                    "Resolución de textura",
                    texture.width + " × " + texture.height,
                    EditorStyles.miniLabel);

                if (texture.width != texture.height)
                {
                    EditorGUILayout.HelpBox(
                        "La imagen no es cuadrada. Puede utilizarse durante producción, pero el contenido final debería seguir el estándar 1:1.",
                        MessageType.Warning);
                }

                if (Mathf.Min(texture.width, texture.height) < 512)
                {
                    EditorGUILayout.HelpBox(
                        "La resolución es inferior a 512 px. Sustitúyela antes de publicar contenido final.",
                        MessageType.Warning);
                }
            }
        }
    }

    private void DrawPackagesTab(
        SerializedProperty ingredient,
        BistroBuilderIngredientAuthoringRecord selected)
    {
        DrawSectionHeader(
            "Formatos comerciales",
            "Inventario conserva su unidad interna; los proveedores venden estos formatos reales.");

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            selected.commercialPackages.Count + " formato(s) definido(s)",
            EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Completar formatos recomendados", GUILayout.Width(210f)))
        {
            Undo.RecordObject(database, "Completar formatos 2.3B1");
            int added =
                BistroBuilderSuppliers23B12ContentSeed.EnsureFormatsForIngredient(
                    selected);
            if (added > 0)
            {
                database.EditorTouchRevision();
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }

            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();

        SerializedProperty packages =
            ingredient.FindPropertyRelative("commercialPackages");

        for (int index = 0; index < packages.arraySize; index++)
        {
            SerializedProperty package = packages.GetArrayElementAtIndex(index);
            string packageId =
                package.FindPropertyRelative("packageFormatId").stringValue;

            int referenceCount =
                BistroBuilderSuppliers23B12ContentSeed.CountReferencesToPackage(
                    supplierDatabase,
                    packageId);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                "Formato " + (index + 1),
                EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Duplicar", GUILayout.Width(68f)))
            {
                serializedDuplicatePackage(packages, index, selected);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            using (new EditorGUI.DisabledScope(referenceCount > 0))
            {
                if (GUILayout.Button("Eliminar", GUILayout.Width(64f)))
                {
                    packages.DeleteArrayElementAtIndex(index);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(
                    package.FindPropertyRelative("packageFormatId"),
                    new GUIContent("PackageFormatId"));
            }

            EditorGUILayout.PropertyField(
                package.FindPropertyRelative("displayName"),
                new GUIContent("Nombre"));

            EditorGUILayout.PropertyField(
                package.FindPropertyRelative("packageType"),
                new GUIContent("Tipo de envase"));

            using (new EditorGUI.DisabledScope(referenceCount > 0))
            {
                DrawQuantity(
                    package.FindPropertyRelative("netQuantityMicrounits"),
                    selected.canonicalUnitSnapshot);
            }

            DrawHumanizedEnumPopup(
                package.FindPropertyRelative("logisticSize"),
                "Clase logística");

            EditorGUILayout.PropertyField(
                package.FindPropertyRelative("packageImage"),
                new GUIContent("Imagen del formato (opcional)"));

            using (new EditorGUI.DisabledScope(referenceCount > 0))
            {
                EditorGUILayout.PropertyField(
                    package.FindPropertyRelative("isActive"),
                    new GUIContent("Activo"));
            }

            if (referenceCount > 0)
            {
                EditorGUILayout.HelpBox(
                    "Este formato está referenciado por " + referenceCount +
                    " oferta(s) base. Su cantidad y eliminación están protegidas; " +
                    "cambiar esa estructura requeriría migrar las ofertas.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        if (GUILayout.Button("+ Añadir formato comercial", GUILayout.Height(28f)))
        {
            int newIndex = packages.arraySize;
            packages.InsertArrayElementAtIndex(newIndex);
            SerializedProperty created = packages.GetArrayElementAtIndex(newIndex);
            ResetPackageSerialized(created, selected);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "El formato pertenece al ingrediente y se reutiliza entre proveedores. " +
            "El precio, mínimo, disponibilidad y plazo específico pertenecen a la oferta del proveedor.",
            MessageType.Info);
    }

    private void DrawRelationsTab(BistroBuilderIngredientAuthoringRecord ingredient)
    {
        DrawSectionHeader(
            "Relaciones",
            "Vista de mantenimiento y navegación cruzada.");

        EditorGUILayout.LabelField("IngredientId", ingredient.IngredientId);
        EditorGUILayout.LabelField("Formatos comerciales", ingredient.commercialPackages.Count.ToString());
        EditorGUILayout.LabelField(
            "Proveedores con oferta activa",
            BistroBuilderSuppliers23B12ContentSeed.CountActiveOffersForIngredient(
                supplierDatabase,
                ingredient.IngredientId).ToString());

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "2.3B ya enlaza este ingrediente con formatos y ofertas base. " +
            "Pedidos, promociones y estado dinámico se activan en hitos posteriores.",
            MessageType.Info);

        if (supplierDatabase != null && GUILayout.Button("Abrir Editor de Proveedores"))
        {
            BistroBuilderSupplierEditor23A2Window.OpenWindow();
        }
    }

    private void DrawFutureTab(SerializedProperty ingredient)
    {
        DrawSectionHeader(
            "Calidad, origen y productor — reserva arquitectónica",
            "Estos campos existen para evitar rehacer el modelo cuando se active la capa premium; 2.3A no les asigna efectos jugables.");

        SerializedProperty future = ingredient.FindPropertyRelative("futureSourcing");
        EditorGUILayout.PropertyField(
            future.FindPropertyRelative("qualityTierId"),
            new GUIContent("QualityTierId (futuro)"));
        EditorGUILayout.PropertyField(
            future.FindPropertyRelative("originId"),
            new GUIContent("OriginId (futuro)"));
        EditorGUILayout.PropertyField(
            future.FindPropertyRelative("producerId"),
            new GUIContent("ProducerId (futuro)"));
        EditorGUILayout.PropertyField(
            future.FindPropertyRelative("certificationIds"),
            new GUIContent("Certificaciones (futuro)"),
            true);

        EditorGUILayout.HelpBox(
            "Sin efecto en gameplay en 2.3. Estos datos no cambian calidad de plato, satisfacción, precio ni disponibilidad hasta que un hito futuro lo active explícitamente.",
            MessageType.Info);
    }

    private void DrawValidationTab(BistroBuilderIngredientAuthoringRecord selected)
    {
        DrawSectionHeader(
            "Validación",
            "Errores estructurales bloquean el contenido; una imagen pendiente es advertencia durante producción.");

        if (GUILayout.Button("Validar bases de autoría", GUILayout.Height(28f)))
        {
            ValidateAll(false);
        }

        if (lastValidation == null)
        {
            EditorGUILayout.HelpBox("Pulsa Validar para obtener un informe.", MessageType.Info);
            return;
        }

        MessageType overallType = lastValidation.ErrorCount > 0
            ? MessageType.Error
            : lastValidation.WarningCount > 0
                ? MessageType.Warning
                : MessageType.Info;

        EditorGUILayout.HelpBox(
            "Errores: " + lastValidation.ErrorCount +
            " | Advertencias: " + lastValidation.WarningCount +
            " | Información: " + lastValidation.InfoCount,
            overallType);

        IReadOnlyList<BistroBuilderAuthoringValidationIssue> issues =
            lastValidation.Issues;

        for (int index = 0; index < issues.Count; index++)
        {
            BistroBuilderAuthoringValidationIssue issue = issues[index];
            if (!string.IsNullOrWhiteSpace(issue.recordId) &&
                !string.Equals(
                    issue.recordId,
                    selected.IngredientId,
                    StringComparison.Ordinal) &&
                !issue.recordId.StartsWith(selected.IngredientId + "/", StringComparison.Ordinal))
            {
                continue;
            }

            EditorGUILayout.LabelField(
                "[" + issue.severity + "] " + issue.code,
                issue.message,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(3f);
        }
    }

    private void DrawRightPreview()
    {
        float width = Mathf.Max(245f, position.width * 0.25f);
        EditorGUILayout.BeginVertical(GUILayout.Width(width));
        DrawSectionHeader("Vista previa", "Lectura del ingrediente en UI");

        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

        if (TryGetSelected(out BistroBuilderIngredientAuthoringRecord ingredient))
        {
            Rect card = EditorGUILayout.GetControlRect(false, 255f);
            DrawIngredientPreviewCard(card, ingredient);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Datos canónicos", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID", ingredient.IngredientId, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Unidad", BistroBuilderSupplierAuthoringPresentation23A3.Unit(ingredient.canonicalUnitSnapshot), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Categoría", BistroBuilderSupplierAuthoringPresentation23A3.Category(ingredient.categorySnapshot), EditorStyles.miniLabel);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Navegación cruzada", EditorStyles.boldLabel);
            if (GUILayout.Button("Abrir Editor de Proveedores"))
            {
                BistroBuilderSupplierEditor23A2Window.OpenWindow();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static void DrawIngredientPreviewCard(
        Rect rect,
        BistroBuilderIngredientAuthoringRecord ingredient)
    {
        Color background = new Color(0.11f, 0.12f, 0.11f, 1f);
        Color accent = new Color(0.72f, 0.58f, 0.29f, 1f);
        EditorGUI.DrawRect(rect, background);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 5f), accent);

        Rect imageRect = new Rect(rect.x + 18f, rect.y + 20f, 112f, 112f);
        BistroBuilderSupplierEditor23A2Window.DrawSpriteOrPlaceholder(
            imageRect,
            ingredient.displayImage,
            new Color(0.25f, 0.27f, 0.24f, 1f),
            ingredient.displayImage == null ? "SIN IMAGEN" : "INGREDIENTE");

        GUIStyle title = new GUIStyle(EditorStyles.boldLabel);
        title.fontSize = 18;
        title.normal.textColor = Color.white;

        GUIStyle body = new GUIStyle(EditorStyles.wordWrappedLabel);
        body.normal.textColor = new Color(0.86f, 0.86f, 0.83f, 1f);

        string name = string.IsNullOrWhiteSpace(ingredient.displayNameSnapshot)
            ? ingredient.IngredientId
            : ingredient.displayNameSnapshot;

        GUI.Label(
            new Rect(rect.x + 148f, rect.y + 28f, rect.width - 164f, 48f),
            name,
            title);

        GUI.Label(
            new Rect(rect.x + 148f, rect.y + 78f, rect.width - 164f, 58f),
            "Unidad interna: " + BistroBuilderSupplierAuthoringPresentation23A3.Unit(ingredient.canonicalUnitSnapshot) +
            "\nCategoría: " + BistroBuilderSupplierAuthoringPresentation23A3.Category(ingredient.categorySnapshot),
            body);

        GUI.Label(
            new Rect(rect.x + 18f, rect.y + 154f, rect.width - 36f, 82f),
            "Formatos comerciales definidos: " + ingredient.commercialPackages.Count +
            "\n\nLa futura pantalla de Proveedores reutilizará esta imagen y estos formatos sin duplicar el ingrediente canónico.",
            body);
    }

    private void SynchronizeCanonical(bool showDialog)
    {
        if (database == null)
        {
            return;
        }

        string previousId = TryGetSelected(out BistroBuilderIngredientAuthoringRecord selected)
            ? selected.IngredientId
            : string.Empty;

        BistroBuilderCanonicalIngredientAuthoringDiscovery23A2.TrySynchronizeIntoDatabase(
            database,
            showDialog,
            out _);

        ReloadDatabases();

        if (!string.IsNullOrWhiteSpace(previousId))
        {
            for (int index = 0; index < database.Ingredients.Count; index++)
            {
                BistroBuilderIngredientAuthoringRecord candidate = database.Ingredients[index];
                if (candidate != null &&
                    string.Equals(candidate.IngredientId, previousId, StringComparison.Ordinal))
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        lastValidation = null;
        Repaint();
    }

    private void SaveDatabase()
    {
        if (database == null)
        {
            return;
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Repaint();
    }

    private void ValidateAll(bool showDialog)
    {
        lastValidation = BistroBuilderSupplierAuthoringValidator.Validate(
            supplierDatabase,
            database);

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                lastValidation.ErrorCount == 0
                    ? "Validación 2.3A"
                    : "Validación 2.3A con errores",
                "Errores: " + lastValidation.ErrorCount + "\n" +
                "Advertencias: " + lastValidation.WarningCount + "\n" +
                "Información: " + lastValidation.InfoCount + "\n\n" +
                "Las imágenes todavía no asignadas se consideran advertencias durante la fase de autoría.",
                "Aceptar");
        }

        Repaint();
    }

    private bool TryGetSelected(out BistroBuilderIngredientAuthoringRecord ingredient)
    {
        ingredient = null;
        if (database == null ||
            selectedIndex < 0 ||
            selectedIndex >= database.Ingredients.Count)
        {
            return false;
        }

        ingredient = database.Ingredients[selectedIndex];
        return ingredient != null;
    }

    private static bool MatchesSearch(
        BistroBuilderIngredientAuthoringRecord ingredient,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return ContainsIgnoreCase(ingredient.IngredientId, query) ||
               ContainsIgnoreCase(ingredient.displayNameSnapshot, query) ||
               ContainsIgnoreCase(ingredient.canonicalUnitSnapshot, query) ||
               ContainsIgnoreCase(ingredient.categorySnapshot, query);
    }

    private static bool ContainsIgnoreCase(string value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(
                   query,
                   StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private static void DrawQuantity(
        SerializedProperty microunits,
        string unit)
    {
        double baseUnits = microunits.longValue / 1000000.0;
        EditorGUI.BeginChangeCheck();
        baseUnits = EditorGUILayout.DoubleField(
            "Cantidad neta (" + (string.IsNullOrWhiteSpace(unit) ? "unidad base" : BistroBuilderSupplierAuthoringPresentation23A3.Unit(unit)) + ")",
            baseUnits);

        if (EditorGUI.EndChangeCheck())
        {
            baseUnits = Math.Max(0.000001, baseUnits);
            microunits.longValue = Math.Max(
                1L,
                (long)Math.Round(
                    baseUnits * 1000000.0,
                    MidpointRounding.AwayFromZero));
        }

        EditorGUILayout.LabelField(
            "Lectura",
            BistroBuilderSupplierAuthoringPresentation23A3.FormatQuantityFriendly(
                microunits.longValue,
                unit),
            EditorStyles.miniLabel);
    }

    private static void DrawHumanizedEnumPopup(SerializedProperty property, string label)
    {
        if (property == null || property.propertyType != SerializedPropertyType.Enum)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            return;
        }

        string[] display = new string[property.enumNames.Length];
        for (int index = 0; index < property.enumNames.Length; index++)
        {
            display[index] =
                BistroBuilderSupplierAuthoringPresentation23A3.HumanizeToken(
                    property.enumNames[index]);
        }

        property.enumValueIndex = EditorGUILayout.Popup(
            label,
            Mathf.Clamp(property.enumValueIndex, 0, Math.Max(0, display.Length - 1)),
            display);
    }

    private static void ResetPackageSerialized(
        SerializedProperty package,
        BistroBuilderIngredientAuthoringRecord ingredient)
    {
        package.FindPropertyRelative("packageFormatId").stringValue =
            BistroBuilderSupplierAuthoringRecord.NormalizeId(
                (ingredient != null ? ingredient.IngredientId : "ingredient") +
                "_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                "package");

        package.FindPropertyRelative("displayName").stringValue = "Nuevo formato";
        package.FindPropertyRelative("packageType").stringValue = "Caja";
        package.FindPropertyRelative("netQuantityMicrounits").longValue = 1000000L;
        package.FindPropertyRelative("packageImage").objectReferenceValue = null;
        package.FindPropertyRelative("logisticSize").enumValueIndex =
            (int)BistroBuilderCommercialPackageLogisticSize.Medio;
        package.FindPropertyRelative("isActive").boolValue = true;
    }

    private static void serializedDuplicatePackage(
        SerializedProperty packages,
        int sourceIndex,
        BistroBuilderIngredientAuthoringRecord ingredient)
    {
        packages.InsertArrayElementAtIndex(sourceIndex + 1);
        SerializedProperty clone = packages.GetArrayElementAtIndex(sourceIndex + 1);
        clone.FindPropertyRelative("packageFormatId").stringValue =
            BistroBuilderSupplierAuthoringRecord.NormalizeId(
                (ingredient != null ? ingredient.IngredientId : "ingredient") +
                "_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                "package");

        string displayName = clone.FindPropertyRelative("displayName").stringValue;
        clone.FindPropertyRelative("displayName").stringValue =
            (string.IsNullOrWhiteSpace(displayName) ? "Formato" : displayName) + " (copia)";
    }

    private static void DrawSectionHeader(string title, string subtitle)
    {
        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            GUIStyle wrappedMini = new GUIStyle(EditorStyles.miniLabel);
            wrappedMini.wordWrap = true;
            EditorGUILayout.LabelField(subtitle, wrappedMini);
        }
        EditorGUILayout.Space(4f);
    }
}
#endif
