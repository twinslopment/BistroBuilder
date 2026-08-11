#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reparación quirúrgica 2.3C4 del único formato comercial residual detectado
/// por la auditoría 2.3C3.
///
/// Caso admitido exclusivamente:
/// - ingredient_azucar
/// - package_ingredient_azucar_379f7dc0 ("Nuevo formato", 1 g)
/// - exactamente dos ofertas que lo referencian: Mercado Central y Hostelería Express
/// - package_ingredient_azucar_pack_1kg existe y está activo
///
/// La reparación elimina el formato residual y migra esas dos ofertas al
/// formato canónico de 1 kg, regenerando únicamente el precio base y la
/// identidad de producto que dependen del formato. Conserva el resto de las
/// condiciones comerciales editables de cada oferta.
///
/// NO publica supplier.catalog. Tras reparar debe usarse el publicador 2.3B3
/// para restablecer la convergencia canónica con backup/rollback propio.
/// </summary>
public sealed class BistroBuilderSuppliers23C4ResidualSugarPackageRepairWindow : EditorWindow
{
    private const string IngredientId = "ingredient_azucar";
    private const string ResidualPackageId = "package_ingredient_azucar_379f7dc0";
    private const string CanonicalPackageId = "package_ingredient_azucar_pack_1kg";
    private const long ResidualExpectedMicrounits = 1000000L; // 1 g

    private static readonly HashSet<string> ExpectedSuppliers =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "supplier_mercado_central",
            "supplier_hosteleria_express"
        };

    private sealed class OfferReference
    {
        public BistroBuilderSupplierAuthoringRecord supplier;
        public BistroBuilderSupplierBaseOfferAuthoringRecord offer;
        public int index;
    }

    private sealed class Preflight
    {
        public BistroBuilderSupplierAuthoringDatabase supplierDb;
        public BistroBuilderIngredientAuthoringDatabase ingredientDb;
        public BistroBuilderIngredientAuthoringRecord ingredient;
        public BistroBuilderCommercialPackageAuthoringRecord residualPackage;
        public BistroBuilderCommercialPackageAuthoringRecord canonicalPackage;
        public readonly List<OfferReference> residualRefs = new List<OfferReference>();
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public int packageCount;
        public int activeOfferCount;
        public int expectedPackageCount;
        public int extraPackageCount;
        public int missingPackageCount;
        public bool IsSafe => errors.Count == 0;
    }

    private Vector2 scroll;
    private string report = "Pulsa Analizar reparación. No se modificará ningún asset.";
    private bool lastAnalysisSafe;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3C4 - Reparar formato residual de azúcar")]
    public static void Open()
    {
        BistroBuilderSuppliers23C4ResidualSugarPackageRepairWindow window =
            GetWindow<BistroBuilderSuppliers23C4ResidualSugarPackageRepairWindow>(
                "Reparación 2.3C4");
        window.minSize = new Vector2(980f, 650f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.3C4 — Reparación quirúrgica del formato residual de azúcar",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Esta herramienta SOLO actúa si vuelve a reconocer exactamente el caso auditado: " +
            "1 formato residual de Azúcar de 1 g, 2 ofertas concretas que lo usan y el formato " +
            "canónico Paquete 1 kg disponible. Si cualquier dato difiere, la reparación se bloquea. " +
            "No toca Inventario, Recepciones, market.settings ni supplier.catalog.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analizar reparación", GUILayout.Height(30f)))
        {
            Analyze();
        }

        using (new EditorGUI.DisabledScope(!lastAnalysisSafe))
        {
            if (GUILayout.Button("Aplicar reparación exacta", GUILayout.Height(30f)))
            {
                RepairWithConfirmation();
            }
        }
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Analyze()
    {
        Preflight preflight = BuildPreflight();
        lastAnalysisSafe = preflight.IsSafe;
        report = BuildPreflightReport(preflight);
        Repaint();
    }

    private void RepairWithConfirmation()
    {
        Preflight preflight = BuildPreflight();
        lastAnalysisSafe = preflight.IsSafe;
        if (!preflight.IsSafe)
        {
            report = BuildPreflightReport(preflight);
            return;
        }

        StringBuilder preview = new StringBuilder();
        for (int i = 0; i < preflight.residualRefs.Count; i++)
        {
            OfferReference reference = preflight.residualRefs[i];
            long newPrice = BistroBuilderSuppliers23B12ContentSeed.EstimateBasePriceCents(
                reference.supplier.SupplierId,
                preflight.ingredient,
                preflight.canonicalPackage);

            preview.Append("• ");
            preview.Append(reference.supplier.displayName);
            preview.Append(": ");
            preview.Append(reference.offer.SupplierOfferId);
            preview.Append(" (" + Money(reference.offer.basePriceCents) + ")");
            preview.Append(" → ");
            preview.Append(ExpectedOfferId(reference.supplier.SupplierId));
            preview.Append(" (" + Money(newPrice) + ")\n");
        }

        bool accepted = EditorUtility.DisplayDialog(
            "Aplicar reparación 2.3C4",
            "Se eliminará EXCLUSIVAMENTE:\n" +
            "• " + ResidualPackageId + " (1 g)\n\n" +
            "Y se migrarán exactamente estas dos ofertas al formato canónico Paquete 1 kg:\n" +
            preview +
            "\nLa operación crea backup JSON en Library/BistroBuilderBackups, registra Undo y " +
            "verifica 44 formatos / 66 ofertas / FK válidas antes de conservar los cambios.\n\n" +
            "supplier.catalog NO se publica automáticamente; se hará después con 2.3B3.",
            "Reparar",
            "Cancelar");

        if (!accepted)
        {
            return;
        }

        ApplyRepair(preflight);
    }

    private void ApplyRepair(Preflight preflight)
    {
        string supplierJson = EditorJsonUtility.ToJson(preflight.supplierDb, true);
        string ingredientJson = EditorJsonUtility.ToJson(preflight.ingredientDb, true);
        string backupDirectory = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Library",
            "BistroBuilderBackups");
        Directory.CreateDirectory(backupDirectory);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string supplierBackup = Path.Combine(
            backupDirectory,
            "2.3C4_supplier.authoring_" + stamp + ".json");
        string ingredientBackup = Path.Combine(
            backupDirectory,
            "2.3C4_ingredient.authoring_" + stamp + ".json");
        File.WriteAllText(supplierBackup, supplierJson, Encoding.UTF8);
        File.WriteAllText(ingredientBackup, ingredientJson, Encoding.UTF8);

        Undo.RecordObjects(
            new UnityEngine.Object[] { preflight.supplierDb, preflight.ingredientDb },
            "2.3C4 reparar formato residual de azúcar");

        List<string> migrations = new List<string>();

        try
        {
            // Sustituimos en el mismo índice para preservar el orden visual del catálogo.
            for (int i = 0; i < preflight.residualRefs.Count; i++)
            {
                OfferReference reference = preflight.residualRefs[i];
                BistroBuilderSupplierBaseOfferAuthoringRecord oldOffer = reference.offer;

                BistroBuilderSupplierBaseOfferAuthoringRecord replacement =
                    BistroBuilderSuppliers23B12ContentSeed.CreateBaseOffer(
                        reference.supplier,
                        preflight.ingredient,
                        preflight.canonicalPackage);

                // Se conserva tuning comercial que no depende del tamaño/precio del formato.
                replacement.minimumPackageCount = oldOffer.minimumPackageCount;
                replacement.orderIncrement = oldOffer.orderIncrement;
                replacement.initialAvailability = oldOffer.initialAvailability;
                replacement.promotionEligible = oldOffer.promotionEligible;
                replacement.overrideLeadTime = oldOffer.overrideLeadTime;
                replacement.leadTimeOverrideGameHours = oldOffer.leadTimeOverrideGameHours;
                replacement.minimumMarketVariationPercent = oldOffer.minimumMarketVariationPercent;
                replacement.maximumMarketVariationPercent = oldOffer.maximumMarketVariationPercent;
                replacement.sortOrder = oldOffer.sortOrder;
                replacement.isActive = oldOffer.isActive;

                reference.supplier.baseOffers[reference.index] = replacement;

                migrations.Add(
                    reference.supplier.SupplierId + " | " +
                    oldOffer.SupplierOfferId + " | " + Money(oldOffer.basePriceCents) +
                    " -> " + replacement.SupplierOfferId + " | " +
                    Money(replacement.basePriceCents));
            }

            bool removed = preflight.ingredient.commercialPackages.Remove(preflight.residualPackage);
            if (!removed)
            {
                throw new InvalidOperationException(
                    "No se pudo retirar el formato residual de ingredient.authoring.");
            }

            preflight.supplierDb.EditorTouchRevision();
            preflight.ingredientDb.EditorTouchRevision();
            EditorUtility.SetDirty(preflight.supplierDb);
            EditorUtility.SetDirty(preflight.ingredientDb);
            AssetDatabase.SaveAssets();

            Preflight after = BuildPreflightAfterRepair();
            if (!after.IsSafe)
            {
                throw new InvalidOperationException(
                    "La verificación posterior a la reparación no superó todos los invariantes:\n" +
                    string.Join("\n", after.errors.ToArray()));
            }

            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("REPARACIÓN 2.3C4 — SUPERADA");
            builder.AppendLine();
            builder.AppendLine("Formato residual eliminado: " + ResidualPackageId);
            builder.AppendLine("Formato canónico destino: " + CanonicalPackageId);
            builder.AppendLine("Ofertas migradas: " + migrations.Count);
            for (int i = 0; i < migrations.Count; i++)
            {
                builder.AppendLine("  - " + migrations[i]);
            }
            builder.AppendLine();
            builder.AppendLine("VERIFICACIÓN POSTERIOR");
            builder.AppendLine("Formatos actuales: " + after.packageCount + " (esperado: 44)");
            builder.AppendLine("Formatos extra: " + after.extraPackageCount);
            builder.AppendLine("Formatos de semilla ausentes: " + after.missingPackageCount);
            builder.AppendLine("Ofertas activas: " + after.activeOfferCount + " (esperado: 66)");
            builder.AppendLine("Referencias al formato residual: 0");
            builder.AppendLine("FK de ofertas a formatos: válidas");
            builder.AppendLine();
            builder.AppendLine("BACKUP");
            builder.AppendLine(supplierBackup);
            builder.AppendLine(ingredientBackup);
            builder.AppendLine();
            builder.AppendLine("IMPORTANTE: supplier.catalog NO se ha modificado. " +
                "Ahora debe republicarse mediante 2.3B3 para recuperar la convergencia canónica.");

            report = builder.ToString();
            lastAnalysisSafe = false;

            Debug.Log(
                "2.3C4 reparado: 1 formato residual eliminado, 2 ofertas migradas, " +
                "44 formatos y 66 ofertas activas. supplier.catalog pendiente de republicación 2.3B3.");
        }
        catch (Exception exception)
        {
            // Rollback in-memory + persistido si cualquier gate posterior falla.
            EditorJsonUtility.FromJsonOverwrite(supplierJson, preflight.supplierDb);
            EditorJsonUtility.FromJsonOverwrite(ingredientJson, preflight.ingredientDb);
            EditorUtility.SetDirty(preflight.supplierDb);
            EditorUtility.SetDirty(preflight.ingredientDb);
            AssetDatabase.SaveAssets();

            report =
                "REPARACIÓN 2.3C4 — ROLLBACK EJECUTADO\n\n" +
                "No se han conservado cambios porque falló un gate posterior.\n" +
                "ERROR: " + exception.Message + "\n\n" +
                "Backups:\n" + supplierBackup + "\n" + ingredientBackup;
            lastAnalysisSafe = false;
            Debug.LogError("2.3C4: rollback ejecutado. " + exception);
        }

        AssetDatabase.Refresh();
        Repaint();
    }

    private static Preflight BuildPreflight()
    {
        Preflight result = BuildCommonState();
        if (result.supplierDb == null || result.ingredientDb == null)
        {
            return result;
        }

        if (result.ingredient == null)
        {
            result.errors.Add("No existe el ingrediente canónico " + IngredientId + ".");
            return result;
        }

        if (result.residualPackage == null)
        {
            result.errors.Add("No existe el formato residual esperado " + ResidualPackageId + ".");
        }
        else
        {
            if (result.residualPackage.netQuantityMicrounits != ResidualExpectedMicrounits)
            {
                result.errors.Add(
                    "El formato residual ya no mide 1 g. Microunits actuales: " +
                    result.residualPackage.netQuantityMicrounits + ".");
            }

            if (!result.residualPackage.isActive)
            {
                result.errors.Add("El formato residual ya no está activo; el caso ha cambiado.");
            }
        }

        if (result.canonicalPackage == null)
        {
            result.errors.Add("No existe el formato canónico destino " + CanonicalPackageId + ".");
        }
        else
        {
            if (!result.canonicalPackage.isActive)
            {
                result.errors.Add("El formato canónico Paquete 1 kg no está activo.");
            }

            if (result.canonicalPackage.netQuantityMicrounits != 1000L * 1000000L)
            {
                result.errors.Add(
                    "El formato canónico pack_1kg no contiene exactamente 1000 g. Microunits: " +
                    result.canonicalPackage.netQuantityMicrounits + ".");
            }
        }

        if (result.residualRefs.Count != 2)
        {
            result.errors.Add(
                "El formato residual tiene " + result.residualRefs.Count +
                " referencias de oferta; se esperaban exactamente 2.");
        }

        HashSet<string> actualSuppliers = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < result.residualRefs.Count; i++)
        {
            OfferReference reference = result.residualRefs[i];
            actualSuppliers.Add(reference.supplier.SupplierId);

            if (!string.Equals(reference.offer.ingredientId, IngredientId, StringComparison.Ordinal))
            {
                result.errors.Add(
                    reference.offer.SupplierOfferId +
                    ": referencia el formato residual pero su IngredientId no es Azúcar.");
            }

            if (HasOfferForPackage(reference.supplier, CanonicalPackageId))
            {
                result.errors.Add(
                    reference.supplier.SupplierId +
                    " ya tiene una oferta para " + CanonicalPackageId +
                    "; la migración podría duplicar catálogo.");
            }
        }

        if (!actualSuppliers.SetEquals(ExpectedSuppliers))
        {
            result.errors.Add(
                "Los proveedores que usan el formato residual no son exactamente " +
                "Mercado Central y Hostelería Express.");
        }

        if (result.packageCount != 45)
        {
            result.errors.Add(
                "Hay " + result.packageCount +
                " formatos actuales; se esperaban exactamente 45 antes de reparar.");
        }

        if (result.expectedPackageCount != 44 || result.extraPackageCount != 1 ||
            result.missingPackageCount != 0)
        {
            result.errors.Add(
                "La topología de formatos ya no coincide con el diagnóstico 2.3C3 " +
                "(esperados=44, extras=1, faltantes=0).");
        }

        if (result.activeOfferCount != 66)
        {
            result.errors.Add(
                "Hay " + result.activeOfferCount +
                " ofertas activas; se esperaban exactamente 66 antes de reparar.");
        }

        return result;
    }

    /// <summary>
    /// Estado posterior esperado: ya NO debe existir el residual y deben existir
    /// las dos nuevas ofertas canónicas. Se reutiliza Preflight para reportar.
    /// </summary>
    private static Preflight BuildPreflightAfterRepair()
    {
        Preflight result = BuildCommonState();
        if (result.supplierDb == null || result.ingredientDb == null)
        {
            return result;
        }

        if (result.ingredient == null)
        {
            result.errors.Add("Azúcar desapareció tras la reparación.");
            return result;
        }

        if (result.residualPackage != null)
        {
            result.errors.Add("El formato residual sigue presente tras la reparación.");
        }

        if (result.canonicalPackage == null || !result.canonicalPackage.isActive)
        {
            result.errors.Add("El formato canónico Paquete 1 kg no está disponible tras reparar.");
        }

        if (result.residualRefs.Count != 0)
        {
            result.errors.Add("Persisten ofertas que referencian el formato residual.");
        }

        foreach (string supplierId in ExpectedSuppliers)
        {
            if (!result.supplierDb.TryGetSupplier(
                    supplierId,
                    out BistroBuilderSupplierAuthoringRecord supplier) ||
                supplier == null)
            {
                result.errors.Add("No se localiza " + supplierId + " tras reparar.");
                continue;
            }

            string expectedOfferId = ExpectedOfferId(supplierId);
            BistroBuilderSupplierBaseOfferAuthoringRecord match = null;
            int matches = 0;
            if (supplier.baseOffers != null)
            {
                for (int i = 0; i < supplier.baseOffers.Count; i++)
                {
                    BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[i];
                    if (offer != null && string.Equals(
                            offer.SupplierOfferId,
                            expectedOfferId,
                            StringComparison.Ordinal))
                    {
                        match = offer;
                        matches++;
                    }
                }
            }

            if (matches != 1 || match == null)
            {
                result.errors.Add(
                    supplierId + ": se esperaba exactamente una nueva oferta " + expectedOfferId + ".");
            }
            else
            {
                if (!string.Equals(match.packageFormatId, CanonicalPackageId, StringComparison.Ordinal) ||
                    !string.Equals(match.ingredientId, IngredientId, StringComparison.Ordinal) ||
                    match.basePriceCents <= 0)
                {
                    result.errors.Add(
                        expectedOfferId + ": contenido canónico inválido tras migración.");
                }
            }
        }

        if (result.packageCount != 44)
        {
            result.errors.Add("Tras reparar debe haber 44 formatos; actuales: " + result.packageCount + ".");
        }

        if (result.expectedPackageCount != 44 || result.extraPackageCount != 0 ||
            result.missingPackageCount != 0)
        {
            result.errors.Add(
                "Tras reparar la topología debe ser exacta (44 esperados, 0 extras, 0 faltantes).");
        }

        if (result.activeOfferCount != 66)
        {
            result.errors.Add(
                "Tras reparar deben seguir existiendo 66 ofertas activas; actuales: " +
                result.activeOfferCount + ".");
        }

        ValidateAllOfferPackageForeignKeys(result);
        return result;
    }

    private static Preflight BuildCommonState()
    {
        Preflight result = new Preflight();
        result.supplierDb = BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        result.ingredientDb = BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        if (result.supplierDb == null)
        {
            result.errors.Add("No se localiza supplier.authoring.");
        }
        if (result.ingredientDb == null)
        {
            result.errors.Add("No se localiza ingredient.authoring.");
        }
        if (result.supplierDb == null || result.ingredientDb == null)
        {
            return result;
        }

        result.ingredientDb.TryGetIngredient(IngredientId, out result.ingredient);

        HashSet<string> expectedPackageIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> actualPackageIds = new HashSet<string>(StringComparer.Ordinal);

        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredients = result.ingredientDb.Ingredients;
        for (int i = 0; i < ingredients.Count; i++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients[i];
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.IngredientId))
            {
                continue;
            }

            List<BistroBuilderSuppliers23B12ContentSeed.PackageSeed> seeds =
                BistroBuilderSuppliers23B12ContentSeed.CreatePackageSeeds(ingredient);
            for (int s = 0; s < seeds.Count; s++)
            {
                string expectedId = BistroBuilderSupplierAuthoringRecord.NormalizeId(
                    ingredient.IngredientId + "_" + seeds[s].code,
                    "package");
                expectedPackageIds.Add(expectedId);
            }

            if (ingredient.commercialPackages == null)
            {
                continue;
            }

            for (int p = 0; p < ingredient.commercialPackages.Count; p++)
            {
                BistroBuilderCommercialPackageAuthoringRecord package = ingredient.commercialPackages[p];
                if (package == null || string.IsNullOrWhiteSpace(package.PackageFormatId))
                {
                    continue;
                }

                result.packageCount++;
                actualPackageIds.Add(package.PackageFormatId);

                if (string.Equals(package.PackageFormatId, ResidualPackageId, StringComparison.Ordinal))
                {
                    result.residualPackage = package;
                }
                if (string.Equals(package.PackageFormatId, CanonicalPackageId, StringComparison.Ordinal))
                {
                    result.canonicalPackage = package;
                }
            }
        }

        result.expectedPackageCount = expectedPackageIds.Count;
        foreach (string actual in actualPackageIds)
        {
            if (!expectedPackageIds.Contains(actual))
            {
                result.extraPackageCount++;
            }
        }
        foreach (string expected in expectedPackageIds)
        {
            if (!actualPackageIds.Contains(expected))
            {
                result.missingPackageCount++;
            }
        }

        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = result.supplierDb.Suppliers;
        for (int s = 0; s < suppliers.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[s];
            if (supplier == null || supplier.baseOffers == null)
            {
                continue;
            }

            for (int o = 0; o < supplier.baseOffers.Count; o++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[o];
                if (offer == null)
                {
                    continue;
                }

                if (offer.isActive)
                {
                    result.activeOfferCount++;
                }

                if (string.Equals(offer.packageFormatId, ResidualPackageId, StringComparison.Ordinal))
                {
                    result.residualRefs.Add(new OfferReference
                    {
                        supplier = supplier,
                        offer = offer,
                        index = o
                    });
                }
            }
        }

        return result;
    }

    private static void ValidateAllOfferPackageForeignKeys(Preflight result)
    {
        HashSet<string> packageIds = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredients = result.ingredientDb.Ingredients;
        for (int i = 0; i < ingredients.Count; i++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients[i];
            if (ingredient == null || ingredient.commercialPackages == null)
            {
                continue;
            }
            for (int p = 0; p < ingredient.commercialPackages.Count; p++)
            {
                BistroBuilderCommercialPackageAuthoringRecord package = ingredient.commercialPackages[p];
                if (package != null && !string.IsNullOrWhiteSpace(package.PackageFormatId))
                {
                    packageIds.Add(package.PackageFormatId);
                }
            }
        }

        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = result.supplierDb.Suppliers;
        for (int s = 0; s < suppliers.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[s];
            if (supplier == null || supplier.baseOffers == null)
            {
                continue;
            }
            for (int o = 0; o < supplier.baseOffers.Count; o++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[o];
                if (offer != null && offer.isActive &&
                    !packageIds.Contains(offer.packageFormatId ?? string.Empty))
                {
                    result.errors.Add(
                        offer.SupplierOfferId + ": FK PackageFormatId no válida tras reparación.");
                }
            }
        }
    }

    private static bool HasOfferForPackage(
        BistroBuilderSupplierAuthoringRecord supplier,
        string packageId)
    {
        if (supplier == null || supplier.baseOffers == null)
        {
            return false;
        }

        for (int i = 0; i < supplier.baseOffers.Count; i++)
        {
            BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[i];
            if (offer != null && string.Equals(
                    offer.packageFormatId,
                    packageId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string ExpectedOfferId(string supplierId)
    {
        return BistroBuilderSupplierAuthoringRecord.NormalizeId(
            supplierId + "_" + CanonicalPackageId,
            "offer");
    }

    private static string BuildPreflightReport(Preflight preflight)
    {
        StringBuilder builder = new StringBuilder(8192);
        builder.AppendLine("ANÁLISIS 2.3C4 — REPARACIÓN DEL FORMATO RESIDUAL DE AZÚCAR");
        builder.AppendLine("No se ha modificado ningún asset.");
        builder.AppendLine();
        builder.AppendLine("CASO ESPERADO");
        builder.AppendLine("Ingrediente: " + IngredientId);
        builder.AppendLine("Residual: " + ResidualPackageId + " | 1 g");
        builder.AppendLine("Destino: " + CanonicalPackageId + " | 1000 g");
        builder.AppendLine("Proveedores afectados esperados: Mercado Central + Hostelería Express");
        builder.AppendLine();
        builder.AppendLine("ESTADO ACTUAL");
        builder.AppendLine("Formatos actuales: " + preflight.packageCount);
        builder.AppendLine("Formatos esperados por semilla: " + preflight.expectedPackageCount);
        builder.AppendLine("Extras: " + preflight.extraPackageCount);
        builder.AppendLine("Faltantes: " + preflight.missingPackageCount);
        builder.AppendLine("Ofertas activas: " + preflight.activeOfferCount);
        builder.AppendLine("Referencias al residual: " + preflight.residualRefs.Count);

        for (int i = 0; i < preflight.residualRefs.Count; i++)
        {
            OfferReference reference = preflight.residualRefs[i];
            long newPrice = preflight.canonicalPackage != null && preflight.ingredient != null
                ? BistroBuilderSuppliers23B12ContentSeed.EstimateBasePriceCents(
                    reference.supplier.SupplierId,
                    preflight.ingredient,
                    preflight.canonicalPackage)
                : 0L;
            builder.AppendLine(
                "  - " + reference.supplier.SupplierId + " | " +
                reference.offer.SupplierOfferId + " | " + Money(reference.offer.basePriceCents) +
                " -> " + ExpectedOfferId(reference.supplier.SupplierId) + " | " +
                (newPrice > 0 ? Money(newPrice) : "?"));
        }

        builder.AppendLine();
        if (preflight.errors.Count > 0)
        {
            builder.AppendLine("ERRORES / BLOQUEOS");
            for (int i = 0; i < preflight.errors.Count; i++)
            {
                builder.AppendLine("[BLOQUEO] " + preflight.errors[i]);
            }
        }
        else
        {
            builder.AppendLine("PREFLIGHT 2.3C4 SUPERADO");
            builder.AppendLine("La reparación exacta es apta. No se modificará supplier.catalog automáticamente.");
        }

        return builder.ToString();
    }

    private static string Money(long cents)
    {
        return (cents / 100d).ToString("0.00") + " €";
    }
}
#endif
