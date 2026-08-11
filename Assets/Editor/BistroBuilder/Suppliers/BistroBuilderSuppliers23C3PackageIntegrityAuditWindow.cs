#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auditoría no destructiva 2.3C3.
///
/// Comprueba la integridad de los formatos comerciales frente a la semilla
/// canónica 2.3B. Está pensada para detectar formatos residuales de pruebas o
/// autoría previa que, aun siendo estructuralmente válidos, puedan alterar la
/// selección de formato de un proveedor y producir precios base absurdamente
/// bajos/altos.
///
/// No modifica ningún asset.
/// </summary>
public sealed class BistroBuilderSuppliers23C3PackageIntegrityAuditWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Pulsa Analizar formatos. No se modificará ningún asset.";

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3C3 - Auditar integridad de formatos comerciales")]
    public static void Open()
    {
        BistroBuilderSuppliers23C3PackageIntegrityAuditWindow window =
            GetWindow<BistroBuilderSuppliers23C3PackageIntegrityAuditWindow>("Auditoría formatos 2.3C3");
        window.minSize = new Vector2(980f, 620f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.3C3 — Auditoría de integridad de formatos comerciales",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Diagnóstico NO destructivo. Contrasta los formatos actuales con los 44 formatos " +
            "esperados por la semilla 2.3B y localiza formatos extra/residuales, referencias " +
            "desde ofertas y su posible impacto económico. No cambia supplier.authoring, " +
            "ingredient.authoring, supplier.catalog ni market.settings.",
            MessageType.Info);

        if (GUILayout.Button("Analizar formatos comerciales", GUILayout.Height(30f)))
        {
            Analyze();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Analyze()
    {
        BistroBuilderSupplierAuthoringDatabase suppliers =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredients =
            BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        if (suppliers == null || ingredients == null)
        {
            report = "ERROR: faltan supplier.authoring o ingredient.authoring.";
            return;
        }

        Dictionary<string, BistroBuilderIngredientAuthoringRecord> ingredientById =
            new Dictionary<string, BistroBuilderIngredientAuthoringRecord>(StringComparer.Ordinal);
        Dictionary<string, BistroBuilderCommercialPackageAuthoringRecord> packageById =
            new Dictionary<string, BistroBuilderCommercialPackageAuthoringRecord>(StringComparer.Ordinal);
        Dictionary<string, string> packageIngredientById =
            new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> expectedPackageIds = new HashSet<string>(StringComparer.Ordinal);

        int actualPackages = 0;
        int activePackages = 0;
        int expectedPackages = 0;
        int malformedPackages = 0;

        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredientRecords = ingredients.Ingredients;
        for (int i = 0; i < ingredientRecords.Count; i++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredientRecords[i];
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.IngredientId))
            {
                continue;
            }

            ingredientById[ingredient.IngredientId] = ingredient;

            List<BistroBuilderSuppliers23B12ContentSeed.PackageSeed> seeds =
                BistroBuilderSuppliers23B12ContentSeed.CreatePackageSeeds(ingredient);
            for (int s = 0; s < seeds.Count; s++)
            {
                BistroBuilderSuppliers23B12ContentSeed.PackageSeed seed = seeds[s];
                string expectedId = BistroBuilderSupplierAuthoringRecord.NormalizeId(
                    ingredient.IngredientId + "_" + seed.code,
                    "package");
                if (expectedPackageIds.Add(expectedId))
                {
                    expectedPackages++;
                }
            }

            if (ingredient.commercialPackages == null)
            {
                continue;
            }

            for (int p = 0; p < ingredient.commercialPackages.Count; p++)
            {
                BistroBuilderCommercialPackageAuthoringRecord package =
                    ingredient.commercialPackages[p];
                if (package == null)
                {
                    malformedPackages++;
                    continue;
                }

                actualPackages++;
                if (package.isActive) activePackages++;

                if (string.IsNullOrWhiteSpace(package.PackageFormatId))
                {
                    malformedPackages++;
                    continue;
                }

                packageById[package.PackageFormatId] = package;
                packageIngredientById[package.PackageFormatId] = ingredient.IngredientId;
            }
        }

        List<string> extras = new List<string>();
        foreach (KeyValuePair<string, BistroBuilderCommercialPackageAuthoringRecord> pair in packageById)
        {
            if (!expectedPackageIds.Contains(pair.Key))
            {
                extras.Add(pair.Key);
            }
        }
        extras.Sort(StringComparer.Ordinal);

        List<string> missing = new List<string>();
        foreach (string expected in expectedPackageIds)
        {
            if (!packageById.ContainsKey(expected))
            {
                missing.Add(expected);
            }
        }
        missing.Sort(StringComparer.Ordinal);

        Dictionary<string, List<string>> offerRefs =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord> offerById =
            new Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord>(StringComparer.Ordinal);

        int activeOffers = 0;
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> supplierRecords = suppliers.Suppliers;
        for (int s = 0; s < supplierRecords.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = supplierRecords[s];
            if (supplier == null || supplier.baseOffers == null)
            {
                continue;
            }

            for (int o = 0; o < supplier.baseOffers.Count; o++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[o];
                if (offer == null || !offer.isActive)
                {
                    continue;
                }

                activeOffers++;
                if (!string.IsNullOrWhiteSpace(offer.SupplierOfferId))
                {
                    offerById[offer.SupplierOfferId] = offer;
                }

                string packageId = offer.packageFormatId ?? string.Empty;
                if (!offerRefs.TryGetValue(packageId, out List<string> refs))
                {
                    refs = new List<string>();
                    offerRefs[packageId] = refs;
                }

                refs.Add(
                    supplier.SupplierId + " | " +
                    offer.SupplierOfferId + " | base=" + Money(offer.basePriceCents));
            }
        }

        StringBuilder builder = new StringBuilder(16384);
        builder.AppendLine("AUDITORÍA 2.3C3 — INTEGRIDAD DE FORMATOS COMERCIALES");
        builder.AppendLine("No se ha modificado ningún asset.");
        builder.AppendLine();
        builder.AppendLine("RESUMEN");
        builder.AppendLine("Ingredientes canónicos: " + ingredientById.Count);
        builder.AppendLine("Formatos esperados por semilla 2.3B: " + expectedPackages);
        builder.AppendLine("Formatos actuales: " + actualPackages);
        builder.AppendLine("Formatos activos: " + activePackages);
        builder.AppendLine("Formatos extra/no pertenecientes a la semilla: " + extras.Count);
        builder.AppendLine("Formatos de semilla ausentes: " + missing.Count);
        builder.AppendLine("Formatos nulos/sin ID: " + malformedPackages);
        builder.AppendLine("Ofertas activas: " + activeOffers);
        builder.AppendLine();

        builder.AppendLine("FORMATOS EXTRA / RESIDUALES");
        if (extras.Count == 0)
        {
            builder.AppendLine("Ninguno.");
        }
        else
        {
            for (int i = 0; i < extras.Count; i++)
            {
                string id = extras[i];
                BistroBuilderCommercialPackageAuthoringRecord package = packageById[id];
                string ingredientId = packageIngredientById.TryGetValue(id, out string linkedIngredient)
                    ? linkedIngredient
                    : "?";
                BistroBuilderIngredientAuthoringRecord ingredient =
                    ingredientById.TryGetValue(ingredientId, out BistroBuilderIngredientAuthoringRecord ing)
                        ? ing
                        : null;

                int refs = offerRefs.TryGetValue(id, out List<string> references)
                    ? references.Count
                    : 0;

                builder.AppendLine("[EXTRA] " + id);
                builder.AppendLine("  Ingrediente: " + ingredientId +
                    (ingredient != null ? " | " + ingredient.displayNameSnapshot : string.Empty));
                builder.AppendLine("  Nombre formato: " + (package.displayName ?? "") +
                    " | tipo=" + (package.packageType ?? "") +
                    " | activo=" + package.isActive);
                builder.AppendLine("  Cantidad: " + package.NetQuantityInBaseUnits.ToString("0.######") +
                    " " + (ingredient != null ? ingredient.canonicalUnitSnapshot : "unidad base") +
                    " | microunits=" + package.netQuantityMicrounits);
                builder.AppendLine("  Referencias activas desde ofertas: " + refs);

                if (references != null)
                {
                    for (int r = 0; r < references.Count; r++)
                    {
                        builder.AppendLine("    -> " + references[r]);
                    }
                }

                if (ingredient != null)
                {
                    List<BistroBuilderSuppliers23B12ContentSeed.PackageSeed> seeds =
                        BistroBuilderSuppliers23B12ContentSeed.CreatePackageSeeds(ingredient);
                    builder.AppendLine("  Formatos canónicos esperados para este ingrediente:");
                    for (int s = 0; s < seeds.Count; s++)
                    {
                        BistroBuilderSuppliers23B12ContentSeed.PackageSeed seed = seeds[s];
                        string expectedId = BistroBuilderSupplierAuthoringRecord.NormalizeId(
                            ingredient.IngredientId + "_" + seed.code,
                            "package");
                        builder.AppendLine(
                            "    - " + expectedId + " | " + seed.displayName +
                            " | " + (seed.netQuantityMicrounits / 1000000d).ToString("0.###") +
                            " " + ingredient.canonicalUnitSnapshot +
                            " | activo=" + (packageById.TryGetValue(expectedId, out BistroBuilderCommercialPackageAuthoringRecord expectedRecord)
                                ? expectedRecord.isActive.ToString()
                                : "AUSENTE"));
                    }
                }

                builder.AppendLine();
            }
        }

        builder.AppendLine("FORMATOS DE SEMILLA AUSENTES");
        if (missing.Count == 0)
        {
            builder.AppendLine("Ninguno.");
        }
        else
        {
            for (int i = 0; i < missing.Count; i++)
            {
                builder.AppendLine("[FALTA] " + missing[i]);
            }
        }

        builder.AppendLine();
        builder.AppendLine("DIAGNÓSTICO");
        if (extras.Count == 0 && missing.Count == 0 && malformedPackages == 0)
        {
            builder.AppendLine(
                "La topología de formatos coincide con la semilla 2.3B. Si persiste un precio absurdo, " +
                "hay que auditar la tabla de precios de referencia, no los formatos.");
        }
        else if (extras.Count > 0)
        {
            builder.AppendLine(
                "Hay formatos extra que la semilla 2.3B preservó por diseño. Si alguno está referenciado " +
                "por una oferta, puede cambiar qué formato selecciona un proveedor y hacer que el estimador " +
                "considere coherente un precio absoluto incorrecto. Revisar esos extras antes de cerrar 2.3C.");
        }
        else
        {
            builder.AppendLine(
                "Hay diferencias de topología frente a la semilla 2.3B. Deben revisarse antes de cerrar 2.3C.");
        }

        report = builder.ToString();
    }

    private static string Money(long cents)
    {
        return (cents / 100d).ToString("0.00") + " €";
    }
}
#endif
