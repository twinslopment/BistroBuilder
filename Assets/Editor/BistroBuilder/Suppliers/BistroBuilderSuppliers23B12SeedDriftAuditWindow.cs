#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auditoría no destructiva de la semilla editorial de 2.3B1+B2.
///
/// Compara las ofertas actualmente persistidas en supplier.authoring con el
/// conjunto que produciría HOY la semilla recomendada, usando exactamente las
/// reglas de BistroBuilderSuppliers23B12ContentSeed y los formatos comerciales
/// actuales. Una oferta "extra" no se considera corrupción: puede ser contenido
/// manual intencionado. Por eso la herramienta nunca elimina nada sin una
/// confirmación explícita del desarrollador.
/// </summary>
public sealed class BistroBuilderSuppliers23B12SeedDriftAuditWindow : EditorWindow
{
    private sealed class DriftEntry
    {
        public string supplierId;
        public string supplierName;
        public string ingredientId;
        public string ingredientName;
        public string packageFormatId;
        public string packageName;
        public string offerId;
    }

    private readonly List<DriftEntry> extras = new List<DriftEntry>();
    private readonly List<DriftEntry> missing = new List<DriftEntry>();
    private Vector2 scroll;
    private int currentOfferCount;
    private int expectedOfferCount;
    private string lastRunUtc;

    [MenuItem(
        "Tools/Bistro Builder/Proveedores/2.3B1+B2 - Auditar desviaciones de semilla",
        priority = 56)]
    public static void OpenWindow()
    {
        BistroBuilderSuppliers23B12SeedDriftAuditWindow window =
            GetWindow<BistroBuilderSuppliers23B12SeedDriftAuditWindow>();
        window.titleContent = new GUIContent("Auditoría 2.3B1+B2");
        window.minSize = new Vector2(760f, 520f);
        window.RunAudit();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.3B1+B2 — Auditoría de desviaciones respecto a la semilla",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Esta ventana NO compara contra Inventario ni supplier.catalog runtime. " +
            "Solo compara el contenido de autoría actual con la semilla recomendada de B1+B2. " +
            "Una oferta extra puede ser perfectamente válida si fue añadida de forma intencionada.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Volver a auditar", GUILayout.Height(28f)))
        {
            RunAudit();
        }

        if (GUILayout.Button("Copiar informe", GUILayout.Height(28f)))
        {
            EditorGUIUtility.systemCopyBuffer = BuildTextReport();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Ofertas actuales", currentOfferCount.ToString());
        EditorGUILayout.LabelField("Ofertas esperadas por semilla", expectedOfferCount.ToString());
        EditorGUILayout.LabelField("Extras respecto a semilla", extras.Count.ToString());
        EditorGUILayout.LabelField("Faltantes respecto a semilla", missing.Count.ToString());
        if (!string.IsNullOrWhiteSpace(lastRunUtc))
        {
            EditorGUILayout.LabelField("Última auditoría", lastRunUtc, EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        if (extras.Count == 0 && missing.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "La autoría coincide exactamente con la semilla recomendada actual.",
                MessageType.Info);
        }
        else if (extras.Count > 0 && missing.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Hay contenido adicional respecto a la semilla. Revísalo antes de eliminarlo: " +
                "puede ser una oferta manual válida.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "La autoría difiere de la semilla en ambas direcciones. Las ofertas faltantes " +
                "pueden restaurarse ejecutando el instalador idempotente B1+B2; esta herramienta " +
                "no crea ofertas automáticamente.",
                MessageType.Warning);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawEntries("EXTRAS respecto a semilla", extras, new Color(0.95f, 0.68f, 0.18f));
        DrawEntries("FALTANTES respecto a semilla", missing, new Color(0.90f, 0.35f, 0.30f));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(extras.Count == 0 || missing.Count > 0))
        {
            if (GUILayout.Button(
                    "Eliminar SOLO las ofertas extra mostradas",
                    GUILayout.Height(34f)))
            {
                RemoveExtrasWithConfirmation();
            }
        }

        if (missing.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "El borrado de extras queda bloqueado mientras haya ofertas esperadas faltantes. " +
                "Ejecuta primero '2.3B1+B2 - Instalar formatos y catálogo base' y vuelve a auditar.",
                MessageType.Info);
        }
    }

    private static void DrawEntries(
        string title,
        List<DriftEntry> entries,
        Color accent)
    {
        EditorGUILayout.Space(8f);
        Rect header = EditorGUILayout.GetControlRect(false, 24f);
        EditorGUI.DrawRect(new Rect(header.x, header.y, 4f, header.height), accent);
        GUI.Label(
            new Rect(header.x + 10f, header.y + 2f, header.width - 10f, header.height),
            title + " (" + entries.Count + ")",
            EditorStyles.boldLabel);

        if (entries.Count == 0)
        {
            EditorGUILayout.LabelField("Ninguna.", EditorStyles.miniLabel);
            return;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            DriftEntry entry = entries[index];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                entry.supplierName + "  →  " + entry.ingredientName,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Formato",
                entry.packageName + "  [" + entry.packageFormatId + "]");
            EditorGUILayout.LabelField("SupplierOfferId", entry.offerId);
            EditorGUILayout.EndVertical();
        }
    }

    private void RunAudit()
    {
        extras.Clear();
        missing.Clear();
        currentOfferCount = 0;
        expectedOfferCount = 0;
        lastRunUtc = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        BistroBuilderSupplierAuthoringDatabase supplierDatabase =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase =
            BistroBuilderSuppliers23A2Paths.LoadIngredientDatabase();

        if (supplierDatabase == null || ingredientDatabase == null)
        {
            Repaint();
            return;
        }

        Dictionary<string, BistroBuilderIngredientAuthoringRecord> ingredients =
            BuildIngredientIndex(ingredientDatabase);

        for (int supplierIndex = 0;
             supplierIndex < supplierDatabase.Suppliers.Count;
             supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier =
                supplierDatabase.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive)
            {
                continue;
            }

            Dictionary<string, DriftEntry> expectedForSupplier =
                BuildExpectedForSupplier(supplier, ingredientDatabase);
            expectedOfferCount += expectedForSupplier.Count;

            HashSet<string> actualIds = new HashSet<string>(StringComparer.Ordinal);
            if (supplier.baseOffers != null)
            {
                for (int offerIndex = 0;
                     offerIndex < supplier.baseOffers.Count;
                     offerIndex++)
                {
                    BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                        supplier.baseOffers[offerIndex];
                    if (offer == null)
                    {
                        continue;
                    }

                    currentOfferCount++;
                    string offerId = offer.SupplierOfferId ?? string.Empty;
                    actualIds.Add(offerId);

                    if (!expectedForSupplier.ContainsKey(offerId))
                    {
                        extras.Add(BuildEntryFromActual(
                            supplier,
                            offer,
                            ingredients));
                    }
                }
            }

            foreach (KeyValuePair<string, DriftEntry> expected in expectedForSupplier)
            {
                if (!actualIds.Contains(expected.Key))
                {
                    missing.Add(expected.Value);
                }
            }
        }

        Debug.Log(
            "AUDITORÍA 2.3B1+B2 — actuales: " + currentOfferCount +
            ", esperadas por semilla: " + expectedOfferCount +
            ", extras: " + extras.Count +
            ", faltantes: " + missing.Count + ".");

        Repaint();
    }

    private static Dictionary<string, DriftEntry> BuildExpectedForSupplier(
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase)
    {
        Dictionary<string, DriftEntry> expected =
            new Dictionary<string, DriftEntry>(StringComparer.Ordinal);

        for (int ingredientIndex = 0;
             ingredientIndex < ingredientDatabase.Ingredients.Count;
             ingredientIndex++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient =
                ingredientDatabase.Ingredients[ingredientIndex];

            if (ingredient == null || !ingredient.isActive ||
                !BistroBuilderSuppliers23B12ContentSeed.ShouldSupplierSell(
                    supplier.SupplierId,
                    ingredient))
            {
                continue;
            }

            BistroBuilderCommercialPackageAuthoringRecord package =
                BistroBuilderSuppliers23B12ContentSeed.SelectPackageForSupplier(
                    supplier.SupplierId,
                    ingredient);

            if (package == null || string.IsNullOrWhiteSpace(package.PackageFormatId))
            {
                continue;
            }

            string offerId = BistroBuilderSupplierAuthoringRecord.NormalizeId(
                supplier.SupplierId + "_" + package.PackageFormatId,
                "offer");

            expected[offerId] = new DriftEntry
            {
                supplierId = supplier.SupplierId,
                supplierName = supplier.displayName,
                ingredientId = ingredient.IngredientId,
                ingredientName = ingredient.displayNameSnapshot,
                packageFormatId = package.PackageFormatId,
                packageName = package.displayName,
                offerId = offerId
            };
        }

        return expected;
    }

    private static Dictionary<string, BistroBuilderIngredientAuthoringRecord> BuildIngredientIndex(
        BistroBuilderIngredientAuthoringDatabase database)
    {
        Dictionary<string, BistroBuilderIngredientAuthoringRecord> result =
            new Dictionary<string, BistroBuilderIngredientAuthoringRecord>(StringComparer.Ordinal);

        for (int index = 0; index < database.Ingredients.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = database.Ingredients[index];
            if (ingredient != null && !string.IsNullOrWhiteSpace(ingredient.IngredientId))
            {
                result[ingredient.IngredientId] = ingredient;
            }
        }

        return result;
    }

    private static DriftEntry BuildEntryFromActual(
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderSupplierBaseOfferAuthoringRecord offer,
        Dictionary<string, BistroBuilderIngredientAuthoringRecord> ingredients)
    {
        BistroBuilderIngredientAuthoringRecord ingredient = null;
        ingredients.TryGetValue(offer.ingredientId ?? string.Empty, out ingredient);

        BistroBuilderCommercialPackageAuthoringRecord package =
            FindPackage(ingredient, offer.packageFormatId);

        return new DriftEntry
        {
            supplierId = supplier.SupplierId,
            supplierName = supplier.displayName,
            ingredientId = offer.ingredientId,
            ingredientName = ingredient != null
                ? ingredient.displayNameSnapshot
                : (offer.ingredientId ?? "Ingrediente desconocido"),
            packageFormatId = offer.packageFormatId,
            packageName = package != null
                ? package.displayName
                : "Formato no localizado",
            offerId = offer.SupplierOfferId
        };
    }

    private static BistroBuilderCommercialPackageAuthoringRecord FindPackage(
        BistroBuilderIngredientAuthoringRecord ingredient,
        string packageFormatId)
    {
        if (ingredient == null || ingredient.commercialPackages == null ||
            string.IsNullOrWhiteSpace(packageFormatId))
        {
            return null;
        }

        for (int index = 0; index < ingredient.commercialPackages.Count; index++)
        {
            BistroBuilderCommercialPackageAuthoringRecord package =
                ingredient.commercialPackages[index];
            if (package != null && string.Equals(
                    package.PackageFormatId,
                    packageFormatId,
                    StringComparison.Ordinal))
            {
                return package;
            }
        }

        return null;
    }

    private void RemoveExtrasWithConfirmation()
    {
        if (extras.Count == 0 || missing.Count > 0)
        {
            return;
        }

        StringBuilder preview = new StringBuilder();
        int previewCount = Math.Min(8, extras.Count);
        for (int index = 0; index < previewCount; index++)
        {
            DriftEntry entry = extras[index];
            preview.Append("• ");
            preview.Append(entry.supplierName);
            preview.Append(" → ");
            preview.Append(entry.ingredientName);
            preview.Append(" → ");
            preview.Append(entry.packageName);
            preview.Append('\n');
        }

        if (extras.Count > previewCount)
        {
            preview.Append("… y ");
            preview.Append(extras.Count - previewCount);
            preview.Append(" más.\n");
        }

        bool accepted = EditorUtility.DisplayDialog(
            "Eliminar ofertas extra respecto a la semilla",
            "Se eliminarán EXCLUSIVAMENTE las ofertas listadas como extra respecto " +
            "a la semilla recomendada actual.\n\n" + preview +
            "\nEsta acción modifica supplier.authoring, queda registrada en Undo y NO toca " +
            "Inventario, Recepciones ni supplier.catalog runtime.\n\n" +
            "Continúa solo si has confirmado que estas ofertas no eran contenido manual intencionado.",
            "Eliminar extras",
            "Cancelar");

        if (!accepted)
        {
            return;
        }

        BistroBuilderSupplierAuthoringDatabase supplierDatabase =
            BistroBuilderSuppliers23A2Paths.LoadSupplierDatabase();
        if (supplierDatabase == null)
        {
            return;
        }

        HashSet<string> idsToRemove = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < extras.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(extras[index].offerId))
            {
                idsToRemove.Add(extras[index].offerId);
            }
        }

        Undo.RecordObject(supplierDatabase, "Eliminar desviaciones extra 2.3B1+B2");
        int removed = 0;

        for (int supplierIndex = 0;
             supplierIndex < supplierDatabase.EditorSuppliers.Count;
             supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier =
                supplierDatabase.EditorSuppliers[supplierIndex];
            if (supplier == null || supplier.baseOffers == null)
            {
                continue;
            }

            for (int offerIndex = supplier.baseOffers.Count - 1;
                 offerIndex >= 0;
                 offerIndex--)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                    supplier.baseOffers[offerIndex];
                if (offer != null && idsToRemove.Contains(offer.SupplierOfferId))
                {
                    supplier.baseOffers.RemoveAt(offerIndex);
                    removed++;
                }
            }
        }

        if (removed > 0)
        {
            supplierDatabase.EditorTouchRevision();
            EditorUtility.SetDirty(supplierDatabase);
            AssetDatabase.SaveAssets();
        }

        Debug.Log(
            "REPARACIÓN 2.3B1+B2 — ofertas extra eliminadas: " + removed +
            ". No se ha modificado Inventario, Recepciones ni supplier.catalog runtime.");

        RunAudit();
    }

    private string BuildTextReport()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("AUDITORÍA 2.3B1+B2 — DESVIACIONES DE SEMILLA");
        builder.AppendLine("Actuales: " + currentOfferCount);
        builder.AppendLine("Esperadas: " + expectedOfferCount);
        builder.AppendLine("Extras: " + extras.Count);
        builder.AppendLine("Faltantes: " + missing.Count);
        builder.AppendLine();

        AppendEntries(builder, "EXTRAS", extras);
        AppendEntries(builder, "FALTANTES", missing);
        return builder.ToString();
    }

    private static void AppendEntries(
        StringBuilder builder,
        string title,
        List<DriftEntry> entries)
    {
        builder.AppendLine(title + ":");
        if (entries.Count == 0)
        {
            builder.AppendLine("- ninguna");
            builder.AppendLine();
            return;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            DriftEntry entry = entries[index];
            builder.Append("- ");
            builder.Append(entry.supplierName);
            builder.Append(" | ");
            builder.Append(entry.ingredientName);
            builder.Append(" | ");
            builder.Append(entry.packageName);
            builder.Append(" | ");
            builder.AppendLine(entry.offerId);
        }

        builder.AppendLine();
    }
}
#endif
