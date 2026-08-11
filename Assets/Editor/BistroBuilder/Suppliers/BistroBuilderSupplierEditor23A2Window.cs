#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor de Proveedores de 2.3A.
///
/// Esta primera fase implementa Biblioteca, Identidad, Clasificación,
/// Condiciones y Validación. Las pestañas de mercado, catálogo de SKU,
/// logística jugable y desbloqueos se activarán en los hitos 2.3B-2.3I,
/// pero sus perfiles estructurales ya quedan almacenados desde 2.3A.
/// </summary>
public sealed class BistroBuilderSupplierEditor23A2Window : EditorWindow
{
    internal const string WindowTitle = "Editor de Proveedores";
    private enum Tab
    {
        Identidad = 0,
        Clasificacion = 1,
        Condiciones = 2,
        Catalogo = 3,
        Perfiles = 4,
        Validacion = 5
    }

    private BistroBuilderSupplierAuthoringDatabase database;
    private BistroBuilderIngredientAuthoringDatabase ingredientDatabase;
    private int selectedIndex = -1;
    private string search = string.Empty;
    private Vector2 leftScroll;
    private Vector2 centerScroll;
    private Vector2 rightScroll;
    private Tab activeTab;
    private BistroBuilderAuthoringValidationReport lastValidation;

    [MenuItem("Tools/Bistro Builder/Proveedores/Editor de Proveedores", priority = 20)]
    public static void OpenWindow()
    {
        BistroBuilderSupplierEditor23A2Window window = GetWindow<BistroBuilderSupplierEditor23A2Window>();
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
        Repaint();
    }

    private void ReloadDatabases()
    {
        database = BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        ingredientDatabase = BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        if (database != null && database.Suppliers.Count > 0)
        {
            selectedIndex = Mathf.Clamp(selectedIndex < 0 ? 0 : selectedIndex, 0, database.Suppliers.Count - 1);
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
                "2.3A todavía no está instalado. Pulsa el botón para crear la base de autoría y los seis proveedores provisionales.",
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

        if (GUILayout.Button("Nuevo", EditorStyles.toolbarButton, GUILayout.Width(58f)))
        {
            CreateSupplier();
        }

        using (new EditorGUI.DisabledScope(database == null || selectedIndex < 0))
        {
            if (GUILayout.Button("Duplicar", EditorStyles.toolbarButton, GUILayout.Width(66f)))
            {
                DuplicateSupplier();
            }

            if (GUILayout.Button("Guardar", EditorStyles.toolbarButton, GUILayout.Width(62f)))
            {
                SaveDatabase();
            }

            if (GUILayout.Button("Validar", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                ValidateAll(true);
            }

            if (GUILayout.Button("Eliminar", EditorStyles.toolbarButton, GUILayout.Width(62f)))
            {
                DeleteSelected();
            }
        }

        GUILayout.Space(8f);
        GUILayout.Label("Buscar", GUILayout.Width(44f));
        search = GUILayout.TextField(search ?? string.Empty, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));

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
        EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Max(235f, position.width * 0.24f)));
        DrawSectionHeader("Biblioteca de Proveedores", database.Suppliers.Count + " registros");

        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        string normalizedSearch = (search ?? string.Empty).Trim();
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = database.Suppliers;

        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier == null)
            {
                continue;
            }

            if (!MatchesSearch(supplier, normalizedSearch))
            {
                continue;
            }

            Rect row = EditorGUILayout.GetControlRect(false, 70f);
            bool selected = index == selectedIndex;
            DrawSupplierRow(row, supplier, selected);

            if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
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

    private void DrawSupplierRow(Rect rect, BistroBuilderSupplierAuthoringRecord supplier, bool selected)
    {
        Color background = selected
            ? new Color(0.22f, 0.42f, 0.30f, 0.75f)
            : new Color(0.16f, 0.16f, 0.16f, 0.55f);

        EditorGUI.DrawRect(rect, background);

        Rect logoRect = new Rect(rect.x + 5f, rect.y + 7f, 52f, 52f);
        DrawSpriteOrPlaceholder(logoRect, supplier.logo, supplier.primaryBrandColor, "SIN LOGO");

        Rect nameRect = new Rect(rect.x + 65f, rect.y + 6f, rect.width - 70f, 18f);
        GUI.Label(nameRect, supplier.displayName ?? "Proveedor", EditorStyles.boldLabel);

        Rect tagRect = new Rect(rect.x + 65f, rect.y + 25f, rect.width - 70f, 16f);
        GUI.Label(tagRect, BuildRowTag(supplier), EditorStyles.miniLabel);

        Rect stateRect = new Rect(rect.x + 65f, rect.y + 41f, rect.width - 70f, 13f);
        GUI.color = supplier.isActive ? new Color(0.50f, 0.95f, 0.60f) : Color.gray;
        GUI.Label(stateRect, supplier.isActive ? "● Activo" : "● Inactivo", EditorStyles.miniLabel);
        GUI.color = Color.white;

        Rect contentRect = new Rect(rect.x + 65f, rect.y + 55f, rect.width - 70f, 13f);
        GUI.Label(
            contentRect,
            BistroBuilderSupplierAuthoringPresentation23A3.SupplierVisualStatus(supplier),
            EditorStyles.miniLabel);
    }

    private void DrawCenterEditor()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        if (!TryGetSelected(out BistroBuilderSupplierAuthoringRecord selected))
        {
            EditorGUILayout.HelpBox("Selecciona un proveedor o crea uno nuevo.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        string[] tabLabels = { "Identidad", "Clasificación", "Condiciones", "Catálogo", "Perfiles 2.3", "Validación" };
        activeTab = (Tab)GUILayout.Toolbar((int)activeTab, tabLabels, GUILayout.Height(26f));

        centerScroll = EditorGUILayout.BeginScrollView(centerScroll);

        SerializedObject serializedDatabase = new SerializedObject(database);
        SerializedProperty suppliersProperty = serializedDatabase.FindProperty("suppliers");

        if (selectedIndex < 0 || selectedIndex >= suppliersProperty.arraySize)
        {
            EditorGUILayout.HelpBox("La selección quedó fuera de rango. Recarga el editor.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        SerializedProperty supplierProperty = suppliersProperty.GetArrayElementAtIndex(selectedIndex);
        serializedDatabase.Update();

        EditorGUI.BeginChangeCheck();

        switch (activeTab)
        {
            case Tab.Identidad:
                DrawIdentityTab(supplierProperty);
                break;
            case Tab.Clasificacion:
                DrawClassificationTab(supplierProperty);
                break;
            case Tab.Condiciones:
                DrawCommercialConditionsTab(supplierProperty);
                break;
            case Tab.Catalogo:
                DrawCatalogTab(selected);
                break;
            case Tab.Perfiles:
                DrawProfilesTab(supplierProperty);
                break;
            case Tab.Validacion:
                DrawValidationTab(selected);
                break;
        }

        bool changed = EditorGUI.EndChangeCheck();
        serializedDatabase.ApplyModifiedProperties();

        if (changed &&
            activeTab != Tab.Catalogo &&
            activeTab != Tab.Validacion)
        {
            database.EditorTouchRevision();
            EditorUtility.SetDirty(database);
            lastValidation = null;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawIdentityTab(SerializedProperty supplier)
    {
        DrawSectionHeader("Identidad", "Datos estables y presencia visual");

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(supplier.FindPropertyRelative("supplierId"), new GUIContent("SupplierId"));
        }

        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("displayName"), new GUIContent("Nombre comercial"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("shortName"), new GUIContent("Nombre corto"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("description"), new GUIContent("Descripción"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("isActive"), new GUIContent("Activo"));

        EditorGUILayout.Space(10f);
        DrawSectionHeader("Identidad visual", "El logo y los colores se diseñan desde aquí");
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("logo"), new GUIContent("Logo"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("primaryBrandColor"), new GUIContent("Color principal"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("secondaryBrandColor"), new GUIContent("Color secundario"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("textContrastColor"), new GUIContent("Color de texto/contraste"));

        EditorGUILayout.HelpBox(
            "Los nombres de los seis proveedores iniciales son provisionales. Cambiar DisplayName no altera SupplierId.",
            MessageType.Info);
    }

    private void DrawClassificationTab(SerializedProperty supplier)
    {
        DrawSectionHeader("Clasificación multidimensional", "Un proveedor puede pertenecer a varias familias a la vez");
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("catalogFlags"), new GUIContent("Catálogo"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("commercialModelFlags"), new GUIContent("Modelo comercial"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("scopeFlags"), new GUIContent("Alcance"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("positioningFlags"), new GUIContent("Posicionamiento"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("customTags"), new GUIContent("Tags personalizados"), true);
    }

    private void DrawCommercialConditionsTab(SerializedProperty supplier)
    {
        DrawSectionHeader("Condiciones comerciales", "Base que utilizarán pedidos y comparación de proveedores");

        DrawMoneyCents(supplier.FindPropertyRelative("minimumOrderValueCents"), "Pedido mínimo");
        DrawMoneyCents(supplier.FindPropertyRelative("shippingCostCents"), "Gastos de transporte");
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("freeShippingEnabled"), new GUIContent("Permite transporte gratuito"));
        DrawMoneyCents(supplier.FindPropertyRelative("freeShippingThresholdCents"), "Gratis desde");

        EditorGUILayout.Space(8f);
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("reliabilityTier"), new GUIContent("Fiabilidad visible"));
        EditorGUILayout.Slider(supplier.FindPropertyRelative("reliabilityValue"), 0f, 1f, new GUIContent("Fiabilidad interna"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("defaultLeadTimeGameHours"), new GUIContent("Plazo normal (horas de juego)"));
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("deliveryWindows"), new GUIContent("Ventanas de entrega"), true);

        EditorGUILayout.HelpBox(
            "La fiabilidad nunca hará desaparecer una entrega confirmada. 2.3G solo podrá desplazar la ventana estimada.",
            MessageType.Info);
    }

    private void DrawCatalogTab(BistroBuilderSupplierAuthoringRecord supplier)
    {
        DrawSectionHeader(
            "Catálogo de ingredientes",
            "2.3A muestra la biblioteca canónica/visual disponible. La selección de SKU, formato y precio por proveedor se activa en 2.3B.");

        if (ingredientDatabase == null)
        {
            EditorGUILayout.HelpBox(
                "No existe todavía la base visual/comercial de ingredientes.",
                MessageType.Warning);

            if (GUILayout.Button("Abrir Editor de Ingredientes"))
            {
                BistroBuilderIngredientEditor23A2Window.OpenWindow();
            }

            return;
        }

        EditorGUILayout.LabelField(
            "Ingredientes sincronizados",
            ingredientDatabase.Ingredients.Count.ToString());

        EditorGUILayout.LabelField(
            "Clasificación declarada del proveedor",
            BistroBuilderSupplierAuthoringPresentation23A3.Flags(supplier.catalogFlags),
            GetWrappedMiniLabelStyle());

        EditorGUILayout.Space(6f);

        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredients =
            ingredientDatabase.Ingredients;

        int shown = Mathf.Min(ingredients.Count, 24);
        for (int index = 0; index < shown; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients[index];
            if (ingredient == null)
            {
                continue;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            Rect imageRect = GUILayoutUtility.GetRect(38f, 38f, GUILayout.Width(38f), GUILayout.Height(38f));
            DrawSpriteOrPlaceholder(
                imageRect,
                ingredient.displayImage,
                new Color(0.25f, 0.27f, 0.24f, 1f),
                "IMG");

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(ingredient.displayNameSnapshot)
                    ? ingredient.IngredientId
                    : ingredient.displayNameSnapshot,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                BistroBuilderSupplierAuthoringPresentation23A3.Unit(ingredient.canonicalUnitSnapshot) + " · " +
                ingredient.commercialPackages.Count + " formato(s)",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        if (ingredients.Count > shown)
        {
            EditorGUILayout.LabelField(
                "… y " + (ingredients.Count - shown) + " ingrediente(s) más.",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "No se crean asignaciones proveedor→ingrediente en esta fase para no duplicar el supplier.catalog runtime que ya existe. " +
            "2.3B añadirá el contrato de SKU/formato/precio sobre la autoridad existente.",
            MessageType.Info);

        if (GUILayout.Button("Abrir Editor de Ingredientes"))
        {
            BistroBuilderIngredientEditor23A2Window.OpenWindow();
        }
    }

    private void DrawProfilesTab(SerializedProperty supplier)
    {
        DrawSectionHeader("Perfiles preparados para los siguientes hitos", "Se almacenan ahora; su comportamiento se activa en 2.3C-2.3I");
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("priceEvolutionProfile"), new GUIContent("Precios / revisión cada 5 días"), true);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("promotionProfile"), new GUIContent("Promociones / Motor Comercial Inteligente"), true);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("availabilityProfile"), new GUIContent("Disponibilidad"), true);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("logisticsProfile"), new GUIContent("Logística futura"), true);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(supplier.FindPropertyRelative("unlockProfile"), new GUIContent("Desbloqueo futuro"), true);

        EditorGUILayout.HelpBox(
            "2.3A solo define y valida estos perfiles. No mueve precios, no crea promociones, no genera pedidos y no modifica Inventario.",
            MessageType.None);
    }

    private void DrawValidationTab(BistroBuilderSupplierAuthoringRecord selected)
    {
        DrawSectionHeader("Validación", "Errores estructurales bloquean publicación; visuales pendientes se muestran como advertencia");

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
            : lastValidation.WarningCount > 0 ? MessageType.Warning : MessageType.Info;

        EditorGUILayout.HelpBox(
            "Errores: " + lastValidation.ErrorCount +
            " | Advertencias: " + lastValidation.WarningCount +
            " | Información: " + lastValidation.InfoCount,
            overallType);

        IReadOnlyList<BistroBuilderAuthoringValidationIssue> issues = lastValidation.Issues;
        for (int index = 0; index < issues.Count; index++)
        {
            BistroBuilderAuthoringValidationIssue issue = issues[index];
            if (!string.IsNullOrWhiteSpace(issue.recordId) &&
                !string.Equals(issue.recordId, selected.SupplierId, StringComparison.Ordinal))
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
        DrawSectionHeader("Vista previa", "Tarjeta aproximada para la futura UI");

        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

        if (TryGetSelected(out BistroBuilderSupplierAuthoringRecord supplier))
        {
            Rect card = EditorGUILayout.GetControlRect(false, 260f);
            DrawSupplierPreviewCard(card, supplier);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Resumen", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("SupplierId", supplier.SupplierId, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Catálogo", BistroBuilderSupplierAuthoringPresentation23A3.Flags(supplier.catalogFlags), GetWrappedMiniLabelStyle());
            EditorGUILayout.LabelField("Modelo", BistroBuilderSupplierAuthoringPresentation23A3.Flags(supplier.commercialModelFlags), GetWrappedMiniLabelStyle());
            EditorGUILayout.LabelField("Fiabilidad", BistroBuilderSupplierAuthoringPresentation23A3.HumanizeToken(supplier.reliabilityTier.ToString()), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Plazo", supplier.defaultLeadTimeGameHours.ToString("0.#") + " h", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Contenido", BistroBuilderSupplierAuthoringPresentation23A3.SupplierVisualStatus(supplier), GetWrappedMiniLabelStyle());

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Navegación cruzada", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(ingredientDatabase == null))
            {
                if (GUILayout.Button("Abrir Editor de Ingredientes"))
                {
                    BistroBuilderIngredientEditor23A2Window.OpenWindow();
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSupplierPreviewCard(Rect rect, BistroBuilderSupplierAuthoringRecord supplier)
    {
        EditorGUI.DrawRect(rect, supplier.primaryBrandColor);
        Rect accent = new Rect(rect.x, rect.y, rect.width, 6f);
        EditorGUI.DrawRect(accent, supplier.secondaryBrandColor);

        Rect logoRect = new Rect(rect.x + 16f, rect.y + 22f, 82f, 82f);
        DrawSpriteOrPlaceholder(logoRect, supplier.logo, supplier.secondaryBrandColor, "SIN LOGO");

        GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            normal = { textColor = supplier.textContrastColor }
        };

        GUIStyle bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            normal = { textColor = supplier.textContrastColor }
        };

        GUI.Label(new Rect(rect.x + 112f, rect.y + 24f, rect.width - 128f, 52f), supplier.displayName, nameStyle);
        GUI.Label(new Rect(rect.x + 112f, rect.y + 74f, rect.width - 128f, 34f), BuildRowTag(supplier), bodyStyle);
        GUI.Label(new Rect(rect.x + 16f, rect.y + 122f, rect.width - 32f, 78f), supplier.description ?? string.Empty, bodyStyle);

        string footer =
            "Fiabilidad: " + BistroBuilderSupplierAuthoringPresentation23A3.HumanizeToken(supplier.reliabilityTier.ToString()) +
            "   ·   Pedido mín.: " + FormatMoney(supplier.minimumOrderValueCents) +
            "\nEntrega habitual: " + supplier.defaultLeadTimeGameHours.ToString("0.#") + " h";

        GUI.Label(new Rect(rect.x + 16f, rect.y + 205f, rect.width - 32f, 44f), footer, bodyStyle);
    }

    private void CreateSupplier()
    {
        EnsureDatabase();
        Undo.RecordObject(database, "Crear proveedor");

        BistroBuilderSupplierAuthoringRecord supplier = new BistroBuilderSupplierAuthoringRecord();
        supplier.AssignStableIdOnce("supplier_" + Guid.NewGuid().ToString("N").Substring(0, 10));
        supplier.displayName = "Nuevo proveedor";
        supplier.shortName = "Proveedor";
        supplier.deliveryWindows.Add(new BistroBuilderSupplierDeliveryWindowAuthoring());
        supplier.priceEvolutionProfile.reviewEveryGameDays = 5;

        database.EditorSuppliers.Add(supplier);
        selectedIndex = database.EditorSuppliers.Count - 1;
        database.EditorTouchRevision();
        EditorUtility.SetDirty(database);
        lastValidation = null;
    }

    private void DuplicateSupplier()
    {
        if (!TryGetSelected(out BistroBuilderSupplierAuthoringRecord source))
        {
            return;
        }

        Undo.RecordObject(database, "Duplicar proveedor");
        BistroBuilderSupplierAuthoringRecord clone = source.DeepClone(false);
        clone.AssignStableIdOnce("supplier_" + Guid.NewGuid().ToString("N").Substring(0, 10));
        clone.displayName = (source.displayName ?? "Proveedor") + " (copia)";
        database.EditorSuppliers.Add(clone);
        selectedIndex = database.EditorSuppliers.Count - 1;
        database.EditorTouchRevision();
        EditorUtility.SetDirty(database);
        lastValidation = null;
    }

    private void DeleteSelected()
    {
        if (!TryGetSelected(out BistroBuilderSupplierAuthoringRecord supplier))
        {
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Eliminar proveedor",
                "Se eliminará '" + supplier.displayName + "' de la base de autoría.\n\n" +
                "En 2.3B+ esta operación quedará bloqueada si existen ofertas/pedidos que lo referencien.",
                "Eliminar",
                "Cancelar"))
        {
            return;
        }

        Undo.RecordObject(database, "Eliminar proveedor");
        database.EditorSuppliers.RemoveAt(selectedIndex);
        selectedIndex = database.EditorSuppliers.Count == 0 ? -1 : Mathf.Clamp(selectedIndex, 0, database.EditorSuppliers.Count - 1);
        database.EditorTouchRevision();
        EditorUtility.SetDirty(database);
        lastValidation = null;
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
        lastValidation = BistroBuilderSupplierAuthoringValidator.Validate(database, ingredientDatabase);

        if (showDialog)
        {
            string title = lastValidation.ErrorCount == 0 ? "Validación 2.3A" : "Validación 2.3A con errores";
            EditorUtility.DisplayDialog(
                title,
                "Errores: " + lastValidation.ErrorCount + "\n" +
                "Advertencias: " + lastValidation.WarningCount + "\n" +
                "Información: " + lastValidation.InfoCount + "\n\n" +
                "Las advertencias de logos/imágenes son esperables mientras se prepara el contenido visual; los errores estructurales no.",
                "Aceptar");
        }

        Repaint();
    }

    private bool TryGetSelected(out BistroBuilderSupplierAuthoringRecord supplier)
    {
        supplier = null;
        if (database == null || selectedIndex < 0 || selectedIndex >= database.Suppliers.Count)
        {
            return false;
        }

        supplier = database.Suppliers[selectedIndex];
        return supplier != null;
    }

    private void EnsureDatabase()
    {
        if (database != null)
        {
            return;
        }

        BistroBuilderSuppliers23A2Installer.InstallOrUpdate();
        ReloadDatabases();
    }

    private static bool MatchesSearch(BistroBuilderSupplierAuthoringRecord supplier, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return ContainsIgnoreCase(supplier.displayName, query) ||
               ContainsIgnoreCase(supplier.shortName, query) ||
               ContainsIgnoreCase(supplier.SupplierId, query) ||
               ContainsIgnoreCase(supplier.catalogFlags.ToString(), query) ||
               ContainsIgnoreCase(supplier.commercialModelFlags.ToString(), query);
    }

    private static bool ContainsIgnoreCase(string value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private static string BuildRowTag(BistroBuilderSupplierAuthoringRecord supplier)
    {
        return BistroBuilderSupplierAuthoringPresentation23A3.Flags(supplier.commercialModelFlags) +
               " · " +
               BistroBuilderSupplierAuthoringPresentation23A3.Flags(supplier.scopeFlags);
    }

    private static GUIStyle GetWrappedMiniLabelStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
        style.wordWrap = true;
        return style;
    }

    private static void DrawSectionHeader(string title, string subtitle)
    {
        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
        }
        EditorGUILayout.Space(4f);
    }

    private static void DrawMoneyCents(SerializedProperty property, string label)
    {
        double euros = property.longValue / 100.0;
        EditorGUI.BeginChangeCheck();
        euros = EditorGUILayout.DoubleField(label + " (€)", euros);
        if (EditorGUI.EndChangeCheck())
        {
            property.longValue = Math.Max(0L, (long)Math.Round(euros * 100.0, MidpointRounding.AwayFromZero));
        }
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100.0).ToString("0.00") + " €";
    }

    internal static void DrawSpriteOrPlaceholder(Rect rect, Sprite sprite, Color background, string label)
    {
        EditorGUI.DrawRect(rect, background);

        if (sprite != null && sprite.texture != null)
        {
            Rect textureRect = sprite.textureRect;
            Rect uv = new Rect(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);

            GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
        }
        else
        {
            GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, label, style);
        }
    }
}
#endif
