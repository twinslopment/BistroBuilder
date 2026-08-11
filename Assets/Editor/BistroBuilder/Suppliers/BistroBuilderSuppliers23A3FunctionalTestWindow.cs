#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueba funcional automática de los ajustes de presentación 2.3A3.
/// No modifica bases, proveedores, ingredientes ni gameplay.
/// </summary>
public sealed class BistroBuilderSuppliers23A3FunctionalTestWindow : EditorWindow
{
    private readonly List<string> lines = new List<string>();
    private Vector2 scroll;
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3A3 - Prueba funcional de editores", priority = 34)]
    public static void Open()
    {
        BistroBuilderSuppliers23A3FunctionalTestWindow window =
            GetWindow<BistroBuilderSuppliers23A3FunctionalTestWindow>();
        window.titleContent = new GUIContent("Prueba 2.3A3");
        window.minSize = new Vector2(720f, 520f);
        window.Show();
        window.RunAutomaticTest();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3A3 — Prueba funcional de presentación de editores", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        if (GUILayout.Button("Ejecutar prueba", GUILayout.Height(28f)))
        {
            RunAutomaticTest();
        }

        EditorGUILayout.HelpBox(
            "Superadas: " + passed + "    Fallidas: " + failed,
            failed == 0 ? MessageType.Info : MessageType.Error);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < lines.Count; index++)
        {
            EditorGUILayout.LabelField(lines[index], GetLineStyle());
        }
        EditorGUILayout.EndScrollView();
    }

    public void RunAutomaticTest()
    {
        lines.Clear();
        passed = 0;
        failed = 0;

        BistroBuilderSupplierAuthoringDatabase suppliers =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredients =
            BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        int supplierRevisionBefore = suppliers != null ? suppliers.ContentRevision : -1;
        int ingredientRevisionBefore = ingredients != null ? ingredients.ContentRevision : -1;

        Check(suppliers != null, "Existe la base de autoría de proveedores.");
        Check(ingredients != null, "Existe la base visual/comercial de ingredientes.");

        if (suppliers != null)
        {
            Check(suppliers.Suppliers.Count == 6, "La semilla conserva exactamente seis proveedores provisionales.");
            Check(suppliers.SchemaId == BistroBuilderSupplierAuthoringDatabase.CurrentSchemaId, "supplier.authoring conserva su schema canónico.");
            Check(suppliers.SchemaVersion == BistroBuilderSupplierAuthoringDatabase.CurrentSchemaVersion, "supplier.authoring conserva su versión de schema.");
            Check(AllSupplierIdsUnique(suppliers), "Todos los SupplierId siguen siendo estables y únicos.");
            Check(AllSupplierPresentationReadable(suppliers), "La clasificación visible de todos los proveedores es legible y no expone tokens CamelCase conocidos.");
        }

        if (ingredients != null)
        {
            Check(ingredients.Ingredients.Count == 22, "Se conservan los 22 ingredientes canónicos sincronizados.");
            Check(ingredients.SchemaId == BistroBuilderIngredientAuthoringDatabase.CurrentSchemaId, "ingredient.authoring conserva su schema canónico.");
            Check(ingredients.SchemaVersion == BistroBuilderIngredientAuthoringDatabase.CurrentSchemaVersion, "ingredient.authoring conserva su versión de schema.");
            Check(AllIngredientIdsUnique(ingredients), "Todos los IngredientId siguen siendo estables y únicos.");
            Check(AllIngredientPresentationReadable(ingredients), "Todos los ingredientes producen unidad/categoría de presentación no vacías.");
        }

        Check(BistroBuilderIngredientEditor23A2Window.WindowTitle == "Editor de Ingredientes", "El título oficial del Editor de Ingredientes es correcto.");
        Check(BistroBuilderSupplierEditor23A2Window.WindowTitle == "Editor de Proveedores", "El título oficial del Editor de Proveedores es correcto.");

        Check(BistroBuilderSupplierAuthoringPresentation23A3.Unit("Gram") == "g", "Gram se presenta como g.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Unit("Milliliter") == "ml", "Milliliter se presenta como ml.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Unit("Unit") == "ud.", "Unit se presenta como ud.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Unit("Kilogram") == "kg", "Kilogram se presenta como kg.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Category("Meat") == "Carnes", "Meat se presenta como Carnes.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Category("Produce") == "Frutas y verduras", "Produce se presenta como Frutas y verduras.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Category("DryGoods") == "Productos secos", "DryGoods se presenta como Productos secos.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Category("DairyAndEggs") == "Lácteos y huevos", "DairyAndEggs se presenta como Lácteos y huevos.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Category("FishAndSeafood") == "Pescados y mariscos", "FishAndSeafood se presenta como Pescados y mariscos.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Category("Condiment") == "Condimentos", "Condiment se presenta como Condimentos.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.Category("PreparedProduct") == "Productos preparados", "PreparedProduct se presenta como Productos preparados.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.HumanizeToken("ProductorLocal") == "Productor local", "ProductorLocal se humaniza correctamente.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.HumanizeToken("CamionLigero") == "Camión ligero", "CamionLigero se humaniza correctamente.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.HumanizeToken("MuyEstable") == "Muy estable", "MuyEstable se humaniza correctamente.");
        Check(BistroBuilderSupplierAuthoringPresentation23A3.HumanizeToken("Economico") == "Económico", "Economico se presenta con acento.");

        string combo = BistroBuilderSupplierAuthoringPresentation23A3.Flags(
            BistroBuilderSupplierCommercialModelFlags.Especialista |
            BistroBuilderSupplierCommercialModelFlags.ProductorLocal);
        Check(combo.Contains("Especialista") && combo.Contains("Productor local") && !combo.Contains("ProductorLocal"),
            "Los flags combinados se presentan de forma humana.");

        if (suppliers != null && ingredients != null)
        {
            BistroBuilderAuthoringValidationReport report =
                BistroBuilderSupplierAuthoringValidator.Validate(suppliers, ingredients);
            Check(report != null, "El validador estructural sigue disponible.");
            Check(report != null && report.ErrorCount == 0, "2.3A3 no introduce errores estructurales en las bases existentes.");
        }

        Check(suppliers == null || suppliers.ContentRevision == supplierRevisionBefore,
            "La prueba no modifica la revisión de supplier.authoring.");
        Check(ingredients == null || ingredients.ContentRevision == ingredientRevisionBefore,
            "La prueba no modifica la revisión de ingredient.authoring.");

        string summary = failed == 0
            ? "PRUEBA FUNCIONAL 2.3A3 SUPERADA — " + passed + "/0"
            : "PRUEBA FUNCIONAL 2.3A3 CON FALLOS — " + passed + " correctas, " + failed + " fallidas";

        if (failed == 0)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }

        Repaint();
    }

    private void Check(bool condition, string description)
    {
        if (condition)
        {
            passed++;
            lines.Add("[OK] " + description);
        }
        else
        {
            failed++;
            lines.Add("[FALLO] " + description);
        }
    }

    private static bool AllSupplierIdsUnique(BistroBuilderSupplierAuthoringDatabase database)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < database.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = database.Suppliers[index];
            if (supplier == null || string.IsNullOrWhiteSpace(supplier.SupplierId) || !ids.Add(supplier.SupplierId))
            {
                return false;
            }
        }
        return true;
    }

    private static bool AllIngredientIdsUnique(BistroBuilderIngredientAuthoringDatabase database)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < database.Ingredients.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = database.Ingredients[index];
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.IngredientId) || !ids.Add(ingredient.IngredientId))
            {
                return false;
            }
        }
        return true;
    }

    private static bool AllSupplierPresentationReadable(BistroBuilderSupplierAuthoringDatabase database)
    {
        for (int index = 0; index < database.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = database.Suppliers[index];
            if (supplier == null)
            {
                return false;
            }

            string model = BistroBuilderSupplierAuthoringPresentation23A3.Flags(supplier.commercialModelFlags);
            string scope = BistroBuilderSupplierAuthoringPresentation23A3.Flags(supplier.scopeFlags);
            if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(scope) || model.Contains("ProductorLocal"))
            {
                return false;
            }
        }
        return true;
    }

    private static bool AllIngredientPresentationReadable(BistroBuilderIngredientAuthoringDatabase database)
    {
        for (int index = 0; index < database.Ingredients.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = database.Ingredients[index];
            if (ingredient == null ||
                string.IsNullOrWhiteSpace(BistroBuilderSupplierAuthoringPresentation23A3.Unit(ingredient.canonicalUnitSnapshot)) ||
                string.IsNullOrWhiteSpace(BistroBuilderSupplierAuthoringPresentation23A3.Category(ingredient.categorySnapshot)))
            {
                return false;
            }
        }
        return true;
    }

    private static GUIStyle GetLineStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.wordWrappedLabel);
        style.richText = false;
        return style;
    }
}
#endif
