#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Diagnóstico no destructivo de coherencia económica para 2.3C.
///
/// Compara los precios base existentes en supplier.authoring con el estimador
/// determinista usado por la semilla 2.3B1+B2. Su finalidad es detectar
/// valores claramente corruptos o heredados de una versión anterior sin
/// confundir pequeños ajustes manuales de balance con errores.
///
/// La reparación, si se usa, afecta exclusivamente a anomalías SEVERAS y
/// requiere confirmación explícita. Nunca publica supplier.catalog; después
/// de una reparación debe utilizarse el publicador canónico 2.3B3.
/// </summary>
public sealed class BistroBuilderSuppliers23C2BasePriceSanityWindow : EditorWindow
{
    private sealed class Finding
    {
        public BistroBuilderSupplierBaseOfferAuthoringRecord offer;
        public BistroBuilderSupplierAuthoringRecord supplier;
        public BistroBuilderIngredientAuthoringRecord ingredient;
        public BistroBuilderCommercialPackageAuthoringRecord package;
        public long actual;
        public long expected;
        public double ratio;
        public bool severe;
        public bool review;
    }

    private readonly List<Finding> findings = new List<Finding>();
    private Vector2 scroll;
    private string report = "Pulsa Analizar. No se modificará ningún asset.";
    private bool hasAnalysis;
    private int severeCount;
    private int reviewCount;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3C2 - Auditar coherencia de precios base")]
    public static void Open()
    {
        BistroBuilderSuppliers23C2BasePriceSanityWindow window =
            GetWindow<BistroBuilderSuppliers23C2BasePriceSanityWindow>("Auditoría precios 2.3C2");
        window.minSize = new Vector2(920f, 560f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.3C2 — Auditoría de coherencia de precios base",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Diagnóstico no destructivo. Compara los precios actuales de supplier.authoring " +
            "con la estimación de semilla 2.3B para localizar outliers graves antes de cerrar 2.3C. " +
            "No modifica supplier.catalog, Inventario, Recepciones ni el estado de mercado.",
            MessageType.Info);

        if (GUILayout.Button("Analizar precios base", GUILayout.Height(30f)))
        {
            Analyze();
        }

        using (new EditorGUI.DisabledScope(!hasAnalysis || severeCount <= 0))
        {
            if (GUILayout.Button(
                    "Reparar SOLO anomalías severas y guardar supplier.authoring",
                    GUILayout.Height(26f)))
            {
                RepairSevere();
            }
        }

        if (hasAnalysis)
        {
            MessageType type = severeCount > 0
                ? MessageType.Error
                : reviewCount > 0
                    ? MessageType.Warning
                    : MessageType.Info;

            EditorGUILayout.HelpBox(
                "Anomalías severas: " + severeCount +
                " | Revisar: " + reviewCount +
                ". La reparación automática solo toca las severas.",
                type);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Analyze()
    {
        findings.Clear();
        severeCount = 0;
        reviewCount = 0;
        hasAnalysis = false;

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

        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredientRecords = ingredients.Ingredients;
        for (int i = 0; i < ingredientRecords.Count; i++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredientRecords[i];
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.IngredientId))
            {
                continue;
            }

            ingredientById[ingredient.IngredientId] = ingredient;

            if (ingredient.commercialPackages == null)
            {
                continue;
            }

            for (int p = 0; p < ingredient.commercialPackages.Count; p++)
            {
                BistroBuilderCommercialPackageAuthoringRecord package =
                    ingredient.commercialPackages[p];
                if (package != null && !string.IsNullOrWhiteSpace(package.PackageFormatId))
                {
                    packageById[package.PackageFormatId] = package;
                }
            }
        }

        int activeOffers = 0;
        int compared = 0;
        int missingLinks = 0;
        long minimumActual = long.MaxValue;
        long maximumActual = 0L;
        double minimumRatio = double.PositiveInfinity;
        double maximumRatio = 0d;

        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> supplierRecords = suppliers.Suppliers;
        for (int s = 0; s < supplierRecords.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = supplierRecords[s];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null)
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

                BistroBuilderIngredientAuthoringRecord ingredient;
                BistroBuilderCommercialPackageAuthoringRecord package;
                if (!ingredientById.TryGetValue(offer.ingredientId ?? string.Empty, out ingredient) ||
                    !packageById.TryGetValue(offer.packageFormatId ?? string.Empty, out package) ||
                    ingredient == null || package == null)
                {
                    missingLinks++;
                    continue;
                }

                long expected = BistroBuilderSuppliers23B12ContentSeed.EstimateBasePriceCents(
                    supplier.SupplierId,
                    ingredient,
                    package);
                long actual = offer.basePriceCents;
                double ratio = expected > 0 ? (double)actual / expected : 0d;

                // SEVERA: precio no positivo o separado por más de x4 / ÷4 del
                // estimador. Es lo bastante amplio para no pisar balance manual.
                bool severe = actual <= 0L || ratio < 0.25d || ratio > 4.0d;

                // REVISAR: desviación notable, pero todavía plausible como tuning.
                bool review = !severe && (ratio < 0.50d || ratio > 2.0d);

                Finding finding = new Finding
                {
                    offer = offer,
                    supplier = supplier,
                    ingredient = ingredient,
                    package = package,
                    actual = actual,
                    expected = expected,
                    ratio = ratio,
                    severe = severe,
                    review = review
                };
                findings.Add(finding);

                if (severe) severeCount++;
                if (review) reviewCount++;
                compared++;
                minimumActual = Math.Min(minimumActual, actual);
                maximumActual = Math.Max(maximumActual, actual);
                minimumRatio = Math.Min(minimumRatio, ratio);
                maximumRatio = Math.Max(maximumRatio, ratio);
            }
        }

        findings.Sort((left, right) =>
        {
            int severity = right.severe.CompareTo(left.severe);
            if (severity != 0) return severity;
            int reviewSeverity = right.review.CompareTo(left.review);
            if (reviewSeverity != 0) return reviewSeverity;
            return Math.Abs(Math.Log(Math.Max(0.000001d, right.ratio))).CompareTo(
                Math.Abs(Math.Log(Math.Max(0.000001d, left.ratio))));
        });

        StringBuilder builder = new StringBuilder(8192);
        builder.AppendLine("AUDITORÍA 2.3C2 — COHERENCIA DE PRECIOS BASE");
        builder.AppendLine("No se ha modificado ningún asset.");
        builder.AppendLine();
        builder.AppendLine("RESUMEN");
        builder.AppendLine("Proveedores activos: " + supplierRecords.Count);
        builder.AppendLine("Ofertas activas: " + activeOffers);
        builder.AppendLine("Ofertas comparadas: " + compared);
        builder.AppendLine("Enlaces ingrediente/formato ausentes: " + missingLinks);
        builder.AppendLine("Anomalías SEVERAS: " + severeCount);
        builder.AppendLine("Valores a REVISAR: " + reviewCount);
        if (compared > 0)
        {
            builder.AppendLine("Precio base mínimo leído: " + Money(minimumActual));
            builder.AppendLine("Precio base máximo leído: " + Money(maximumActual));
            builder.AppendLine("Ratio mínimo actual/estimado: " + minimumRatio.ToString("0.000") + "x");
            builder.AppendLine("Ratio máximo actual/estimado: " + maximumRatio.ToString("0.000") + "x");
        }

        builder.AppendLine();
        builder.AppendLine("HALLAZGOS");

        int written = 0;
        for (int i = 0; i < findings.Count; i++)
        {
            Finding finding = findings[i];
            if (!finding.severe && !finding.review)
            {
                continue;
            }

            string level = finding.severe ? "SEVERA" : "REVISAR";
            builder.AppendLine(
                "[" + level + "] " +
                finding.offer.SupplierOfferId +
                " | proveedor=" + finding.supplier.SupplierId +
                " | ingrediente=" + finding.ingredient.IngredientId +
                " | formato=" + finding.package.displayName +
                " | cantidad=" + finding.package.NetQuantityInBaseUnits.ToString("0.###") +
                " " + finding.ingredient.canonicalUnitSnapshot +
                " | actual=" + Money(finding.actual) +
                " | estimado=" + Money(finding.expected) +
                " | ratio=" + finding.ratio.ToString("0.000") + "x");
            written++;
        }

        if (written == 0)
        {
            builder.AppendLine("Ninguno. Todos los precios están dentro del rango de coherencia amplio.");
        }

        builder.AppendLine();
        if (severeCount > 0)
        {
            builder.AppendLine(
                "RESULTADO: BLOQUEAR cierre de 2.3C hasta revisar/reparar las anomalías severas.");
        }
        else if (reviewCount > 0)
        {
            builder.AppendLine(
                "RESULTADO: sin corrupción evidente; existen desviaciones de balance que conviene revisar.");
        }
        else
        {
            builder.AppendLine(
                "RESULTADO: PRECIOS BASE COHERENTES. No se detectan outliers relevantes.");
        }

        report = builder.ToString();
        hasAnalysis = true;
    }

    private void RepairSevere()
    {
        if (!hasAnalysis || severeCount <= 0)
        {
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "2.3C2 — Reparar anomalías severas",
                "Se sustituirá exclusivamente el precio base de las " + severeCount +
                " ofertas SEVERAS por el valor calculado por la semilla 2.3B.\n\n" +
                "No se tocarán las ofertas marcadas solo como REVISAR.\n" +
                "Después será obligatorio volver a publicar supplier.catalog mediante 2.3B3.\n\n" +
                "¿Continuar?",
                "Reparar",
                "Cancelar"))
        {
            return;
        }

        BistroBuilderSupplierAuthoringDatabase suppliers =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        if (suppliers == null)
        {
            report = "ERROR: supplier.authoring ya no está disponible.";
            return;
        }

        Undo.RecordObject(suppliers, "2.3C2 reparar precios base severos");

        int repaired = 0;
        StringBuilder changes = new StringBuilder();
        for (int i = 0; i < findings.Count; i++)
        {
            Finding finding = findings[i];
            if (!finding.severe || finding.offer == null || finding.expected <= 0L)
            {
                continue;
            }

            long previous = finding.offer.basePriceCents;
            if (previous == finding.expected)
            {
                continue;
            }

            finding.offer.basePriceCents = finding.expected;
            repaired++;
            changes.AppendLine(
                finding.offer.SupplierOfferId + ": " +
                Money(previous) + " -> " + Money(finding.expected));
        }

        if (repaired > 0)
        {
            suppliers.EditorTouchRevision();
            EditorUtility.SetDirty(suppliers);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        report =
            "REPARACIÓN 2.3C2 COMPLETADA\n" +
            "Ofertas reparadas: " + repaired + "\n\n" +
            changes + "\n" +
            "SIGUIENTE PASO OBLIGATORIO: volver a ejecutar el publicador 2.3B3 para " +
            "converger supplier.catalog y después repetir su validación/autotest.";

        // Fuerza un nuevo análisis antes de permitir otra reparación.
        hasAnalysis = false;
        severeCount = 0;
        reviewCount = 0;
    }

    private static string Money(long cents)
    {
        return (cents / 100.0).ToString("0.00") + " €";
    }
}
#endif
