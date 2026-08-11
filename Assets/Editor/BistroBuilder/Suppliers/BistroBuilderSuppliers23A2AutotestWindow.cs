#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest aislado de la nueva capa de autoría 2.3A.
/// No necesita Play Mode y no modifica assets reales del proyecto.
/// </summary>
public sealed class BistroBuilderSuppliers23A2AutotestWindow : EditorWindow
{
    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;
    private Vector2 scroll;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3A2 - Autotest de autoría", priority = 41)]
    public static void OpenWindow()
    {
        BistroBuilderSuppliers23A2AutotestWindow window =
            GetWindow<BistroBuilderSuppliers23A2AutotestWindow>();
        window.titleContent = new GUIContent("Autotest 2.3A");
        window.minSize = new Vector2(720f, 460f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "2.3A — Autotest de modelo y autoría",
            EditorStyles.boldLabel);

        if (GUILayout.Button("Ejecutar autotest", GUILayout.Height(32f)))
        {
            RunTests();
        }

        if (passed + failed == 0)
        {
            EditorGUILayout.HelpBox(
                "La prueba crea bases temporales en memoria; no modifica el proyecto ni el catálogo runtime.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "Superadas: " + passed + "   |   Fallidas: " + failed,
            failed == 0 ? MessageType.Info : MessageType.Error);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < results.Count; index++)
        {
            EditorGUILayout.LabelField(results[index], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunTests()
    {
        passed = 0;
        failed = 0;
        results.Clear();

        BistroBuilderSupplierAuthoringDatabase suppliers =
            ScriptableObject.CreateInstance<BistroBuilderSupplierAuthoringDatabase>();
        BistroBuilderIngredientAuthoringDatabase ingredients =
            ScriptableObject.CreateInstance<BistroBuilderIngredientAuthoringDatabase>();

        try
        {
            suppliers.EditorEnsureSchema();
            ingredients.EditorEnsureSchema();

            List<BistroBuilderSupplierAuthoringRecord> seed =
                BistroBuilderSuppliers23A2SeedFactory.CreateSixProvisionalSuppliers();

            for (int index = 0; index < seed.Count; index++)
            {
                suppliers.EditorSuppliers.Add(seed[index]);
            }

            Check(seed.Count == 6, "La semilla crea exactamente seis arquetipos provisionales.");
            Check(AllSupplierIdsUnique(seed), "Los seis SupplierId son estables y únicos.");
            Check(AllReviewEveryFiveDays(seed), "Los seis perfiles de precios parten de revisión cada 5 días.");
            Check(AllHaveDeliveryWindows(seed), "Todos los proveedores tienen al menos una ventana de entrega.");
            Check(AllProfilesPresent(seed), "Todos los perfiles estructurales 2.3 están presentes.");
            Check(ContainsSupplier(seed, "supplier_mercado_central"), "Existe Mercado Central provisional.");
            Check(ContainsSupplier(seed, "supplier_distribuciones_norte"), "Existe Distribuciones Norte provisional.");
            Check(ContainsSupplier(seed, "supplier_hosteleria_express"), "Existe Hostelería Express provisional.");
            Check(ContainsSupplier(seed, "supplier_huerta_clara"), "Existe Huerta Clara provisional.");
            Check(ContainsSupplier(seed, "supplier_carnes_selectas"), "Existe Carnes Selectas provisional.");
            Check(ContainsSupplier(seed, "supplier_costa_fresca"), "Existe Costa Fresca provisional.");

            BistroBuilderSupplierAuthoringRecord original = seed[0];
            BistroBuilderSupplierAuthoringRecord clone = original.DeepClone(true);
            Check(!ReferenceEquals(original, clone), "DeepClone no reutiliza la instancia del proveedor.");
            Check(string.Equals(original.SupplierId, clone.SupplierId, StringComparison.Ordinal), "DeepClone con identidad conserva SupplierId.");
            Check(!ReferenceEquals(original.deliveryWindows, clone.deliveryWindows), "DeepClone copia la colección de ventanas.");
            Check(!ReferenceEquals(original.promotionProfile, clone.promotionProfile), "DeepClone copia el perfil promocional.");

            BistroBuilderSupplierAuthoringRecord duplicate = original.DeepClone(false);
            duplicate.AssignStableIdOnce("autotest_duplicate");
            Check(!string.Equals(original.SupplierId, duplicate.SupplierId, StringComparison.Ordinal), "Duplicar genera una identidad distinta.");
            string duplicateIdBefore = duplicate.SupplierId;
            duplicate.AssignStableIdOnce("should_not_replace");
            Check(string.Equals(duplicateIdBefore, duplicate.SupplierId, StringComparison.Ordinal), "AssignStableIdOnce impide cambiar un ID ya asignado.");

            BistroBuilderIngredientAuthoringRecord ingredient =
                new BistroBuilderIngredientAuthoringRecord();
            ingredient.AssignStableIdOnce("ingredient_tomato");
            ingredient.RefreshCanonicalSnapshot("Tomate", "kg", "Verduras");

            BistroBuilderCommercialPackageAuthoringRecord package =
                new BistroBuilderCommercialPackageAuthoringRecord();
            package.AssignStableIdOnce("tomato_box_5kg");
            package.displayName = "Caja 5 kg";
            package.netQuantityMicrounits = 5000000L;
            ingredient.commercialPackages.Add(package);
            ingredients.EditorIngredients.Add(ingredient);

            Check(Math.Abs(package.NetQuantityInBaseUnits - 5.0) < 0.0000001, "El formato comercial conserva cantidad exacta en micro-unidades.");
            Check(string.Equals(ingredient.IngredientId, "ingredient_tomato", StringComparison.Ordinal), "La capa visual conserva el IngredientId canónico sin inventar otro dominio.");

            BistroBuilderAuthoringValidationReport validReport =
                BistroBuilderSupplierAuthoringValidator.Validate(suppliers, ingredients);
            Check(validReport.ErrorCount == 0, "La semilla estructural válida no genera errores (logos/imágenes pendientes son warnings).");
            Check(validReport.WarningCount >= 1, "El validador diferencia contenido visual pendiente como advertencia.");

            BistroBuilderSupplierAuthoringRepository supplierRepository =
                new BistroBuilderSupplierAuthoringRepository(suppliers);
            List<BistroBuilderSupplierAuthoringRecord> supplierBuffer =
                new List<BistroBuilderSupplierAuthoringRecord>();
            supplierBuffer.Add(null);
            int copiedSuppliers = supplierRepository.CopySuppliers(supplierBuffer, false);
            Check(copiedSuppliers == 6 && supplierBuffer.Count == 6, "Repository limpia el buffer y copia la cardinalidad esperada.");
            Check(!ReferenceEquals(supplierBuffer[0], seed[0]), "Repository de proveedores no filtra referencias editables al asset maestro.");

            BistroBuilderIngredientAuthoringRepository ingredientRepository =
                new BistroBuilderIngredientAuthoringRepository(ingredients);
            List<BistroBuilderIngredientAuthoringRecord> ingredientBuffer =
                new List<BistroBuilderIngredientAuthoringRecord>();
            ingredientBuffer.Add(null);
            int copiedIngredients = ingredientRepository.CopyIngredients(ingredientBuffer, false);
            Check(copiedIngredients == 1 && ingredientBuffer.Count == 1, "Repository de ingredientes limpia el buffer y copia datos.");
            Check(!ReferenceEquals(ingredientBuffer[0], ingredient), "Repository de ingredientes devuelve copia profunda, no el registro maestro.");

            // Casos adversariales del validador.
            BistroBuilderSupplierAuthoringRecord invalidSupplier = seed[1];
            float originalReliability = invalidSupplier.reliabilityValue;
            invalidSupplier.reliabilityValue = 2f;
            BistroBuilderAuthoringValidationReport invalidReliabilityReport =
                BistroBuilderSupplierAuthoringValidator.Validate(suppliers, ingredients);
            Check(invalidReliabilityReport.ErrorCount > 0, "El validador rechaza fiabilidad fuera de rango.");
            invalidSupplier.reliabilityValue = originalReliability;

            int originalReviewCycle = invalidSupplier.priceEvolutionProfile.reviewEveryGameDays;
            invalidSupplier.priceEvolutionProfile.reviewEveryGameDays = 4;
            BistroBuilderAuthoringValidationReport reviewReport =
                BistroBuilderSupplierAuthoringValidator.Validate(suppliers, ingredients);
            Check(reviewReport.ErrorCount == 0 && reviewReport.WarningCount > validReport.WarningCount, "Un ciclo distinto de 5 días se informa como warning, no corrupción.");
            invalidSupplier.priceEvolutionProfile.reviewEveryGameDays = originalReviewCycle;

            long originalQuantity = package.netQuantityMicrounits;
            package.netQuantityMicrounits = 0;
            BistroBuilderAuthoringValidationReport invalidPackageReport =
                BistroBuilderSupplierAuthoringValidator.Validate(suppliers, ingredients);
            Check(invalidPackageReport.ErrorCount > 0, "El validador rechaza formatos comerciales con cantidad cero.");
            package.netQuantityMicrounits = originalQuantity;

            BistroBuilderSupplierAuthoringRecord deactivated = seed[2];
            deactivated.isActive = false;
            supplierBuffer.Clear();
            int activeCount = supplierRepository.CopySuppliers(supplierBuffer, true);
            Check(activeCount == 5, "Repository puede filtrar proveedores inactivos sin modificar la base.");
            deactivated.isActive = true;

            Check(
                BistroBuilderSupplierAuthoringRecord.NormalizeId("Mercado Central", "supplier") == "supplier_mercado_central",
                "Normalización de SupplierId es determinista.");
            Check(
                BistroBuilderIngredientAuthoringRecord.NormalizeIngredientId(" Ingredient_Tomato ") == "ingredient_tomato",
                "Normalización de IngredientId respeta la identidad canónica.");
        }
        catch (Exception exception)
        {
            failed++;
            results.Add("[FALLO] Excepción no esperada: " + exception);
        }
        finally
        {
            DestroyImmediate(suppliers);
            DestroyImmediate(ingredients);
        }

        string summary =
            "AUTOTEST 2.3A " + (failed == 0 ? "SUPERADO" : "FALLIDO") +
            "\nSuperadas: " + passed +
            "\nFallidas: " + failed;

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
            results.Add("[OK] " + description);
        }
        else
        {
            failed++;
            results.Add("[FALLO] " + description);
        }
    }

    private static bool AllSupplierIdsUnique(
        List<BistroBuilderSupplierAuthoringRecord> suppliers)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier == null ||
                string.IsNullOrWhiteSpace(supplier.SupplierId) ||
                !ids.Add(supplier.SupplierId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllReviewEveryFiveDays(
        List<BistroBuilderSupplierAuthoringRecord> suppliers)
    {
        for (int index = 0; index < suppliers.Count; index++)
        {
            if (suppliers[index] == null ||
                suppliers[index].priceEvolutionProfile == null ||
                suppliers[index].priceEvolutionProfile.reviewEveryGameDays != 5)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllHaveDeliveryWindows(
        List<BistroBuilderSupplierAuthoringRecord> suppliers)
    {
        for (int index = 0; index < suppliers.Count; index++)
        {
            if (suppliers[index] == null ||
                suppliers[index].deliveryWindows == null ||
                suppliers[index].deliveryWindows.Count == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllProfilesPresent(
        List<BistroBuilderSupplierAuthoringRecord> suppliers)
    {
        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier == null ||
                supplier.promotionProfile == null ||
                supplier.priceEvolutionProfile == null ||
                supplier.availabilityProfile == null ||
                supplier.logisticsProfile == null ||
                supplier.unlockProfile == null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsSupplier(
        List<BistroBuilderSupplierAuthoringRecord> suppliers,
        string supplierId)
    {
        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier != null &&
                string.Equals(supplier.SupplierId, supplierId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
