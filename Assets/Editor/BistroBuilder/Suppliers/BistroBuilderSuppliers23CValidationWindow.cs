#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23CValidationWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> information = new List<string>();

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3C - Validar mercado, precios y disponibilidad")]
    public static void OpenAndValidate()
    {
        BistroBuilderSuppliers23CValidationWindow window =
            GetWindow<BistroBuilderSuppliers23CValidationWindow>("Validación 2.3C");
        window.minSize = new Vector2(760f, 460f);
        window.RunValidation();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3C — Mercado, precios, disponibilidad y ciclo de 5 días", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Validar de nuevo", GUILayout.Height(28f)))
        {
            RunValidation();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Errores: " + errors.Count + "   Advertencias: " + warnings.Count +
            "   Información: " + information.Count,
            EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSection("ERRORES", errors, MessageType.Error);
        DrawSection("ADVERTENCIAS", warnings, MessageType.Warning);
        DrawSection("INFORMACIÓN", information, MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    public void RunValidation()
    {
        errors.Clear();
        warnings.Clear();
        information.Clear();

        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSuppliers23CPaths.SupplierAuthoringAsset);
        BistroBuilderSupplierMarketSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierMarketSettings>(
                BistroBuilderSuppliers23CPaths.MarketSettingsAsset);

        if (suppliers == null)
        {
            errors.Add("Falta supplier.authoring. 2.3B debe permanecer instalado.");
            Finish();
            return;
        }

        if (settings == null)
        {
            errors.Add("Falta supplier.market.settings. Ejecuta el instalador 2.3C.");
            Finish();
            return;
        }

        if (settings.SchemaId != BistroBuilderSupplierMarketSettings.CurrentSchemaId ||
            settings.SchemaVersion != BistroBuilderSupplierMarketSettings.CurrentSchemaVersion)
        {
            errors.Add("supplier.market.settings usa un schema incompatible.");
        }

        if (settings.ReviewEveryGameDays != 5)
        {
            errors.Add("El ciclo global de mercado debe ser exactamente de 5 días en 2.3C.");
        }

        HashSet<string> supplierIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> offerIds = new HashSet<string>(StringComparer.Ordinal);
        int activeSuppliers = 0;
        int activeOffers = 0;

        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive)
            {
                continue;
            }

            activeSuppliers++;
            if (string.IsNullOrWhiteSpace(supplier.SupplierId) || !supplierIds.Add(supplier.SupplierId))
            {
                errors.Add("SupplierId vacío o duplicado en proveedor activo.");
            }

            if (supplier.priceEvolutionProfile == null)
            {
                errors.Add(supplier.SupplierId + ": falta PriceEvolutionProfile.");
            }
            else
            {
                if (supplier.priceEvolutionProfile.reviewEveryGameDays != 5)
                {
                    errors.Add(supplier.SupplierId + ": revisión distinta de 5 días.");
                }

                if (supplier.priceEvolutionProfile.minimumVariationPercent >
                    supplier.priceEvolutionProfile.maximumVariationPercent)
                {
                    errors.Add(supplier.SupplierId + ": límites de precio del proveedor invertidos.");
                }
            }

            if (supplier.availabilityProfile == null)
            {
                errors.Add(supplier.SupplierId + ": falta AvailabilityProfile.");
            }
            else
            {
                float sum = supplier.availabilityProfile.limitedStockWeight +
                            supplier.availabilityProfile.temporaryOutOfStockWeight;
                if (supplier.availabilityProfile.limitedStockWeight < 0f ||
                    supplier.availabilityProfile.temporaryOutOfStockWeight < 0f || sum > 1f)
                {
                    errors.Add(supplier.SupplierId + ": pesos de disponibilidad inválidos.");
                }
            }

            if (supplier.baseOffers == null)
            {
                errors.Add(supplier.SupplierId + ": catálogo base nulo.");
                continue;
            }

            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                if (offer == null || !offer.isActive)
                {
                    continue;
                }

                activeOffers++;
                if (string.IsNullOrWhiteSpace(offer.SupplierOfferId) ||
                    !offerIds.Add(offer.SupplierOfferId))
                {
                    errors.Add("SupplierOfferId vacío o duplicado en " + supplier.SupplierId + ".");
                }

                if (offer.basePriceCents <= 0)
                {
                    errors.Add(offer.SupplierOfferId + ": precio base no positivo.");
                }

                float supplierMin = supplier.priceEvolutionProfile != null
                    ? supplier.priceEvolutionProfile.minimumVariationPercent : -8f;
                float supplierMax = supplier.priceEvolutionProfile != null
                    ? supplier.priceEvolutionProfile.maximumVariationPercent : 12f;
                float effectiveMin = Mathf.Max(supplierMin, offer.minimumMarketVariationPercent);
                float effectiveMax = Mathf.Min(supplierMax, offer.maximumMarketVariationPercent);
                if (effectiveMin > effectiveMax)
                {
                    errors.Add(offer.SupplierOfferId + ": no existe intersección válida de límites de precio.");
                }
            }
        }

        if (activeSuppliers < 6)
        {
            errors.Add("Se esperaban al menos 6 proveedores activos tras 2.3B.");
        }

        if (activeOffers < 66)
        {
            errors.Add("Se esperaban al menos 66 ofertas activas tras 2.3B.");
        }

        if (errors.Count == 0)
        {
            int revisionBefore = suppliers.ContentRevision;
            ulong seed = BistroBuilderSupplierMarketEngine.StableSeedFromText("validation-23c", settings.DeterministicSalt);
            BistroBuilderSupplierMarketSnapshot initial =
                BistroBuilderSupplierMarketEngine.CreateInitialSnapshot(suppliers, settings, seed, 1);

            if (initial.offerStates.Count != activeOffers)
            {
                errors.Add("El mercado inicial no coincide con la cardinalidad de ofertas activas.");
            }

            string marketError;
            List<BistroBuilderSupplierMarketReviewOutcome> outcomes;
            if (!BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
                    initial, suppliers, settings, 30, out outcomes, out marketError))
            {
                errors.Add("Simulación determinista falló: " + marketError);
            }
            else
            {
                if (outcomes.Count != 6)
                {
                    errors.Add("Entre los días 1 y 30 deben ejecutarse exactamente 6 revisiones: 5,10,15,20,25,30.");
                }

                if (initial.lastReviewGameDay != 30 || initial.nextReviewGameDay != 35)
                {
                    errors.Add("Planificación del ciclo de 5 días incoherente tras día 30.");
                }

                ValidateSimulatedBounds(initial, suppliers);
                information.Add(
                    "Simulación día 30: " + initial.offerStates.Count +
                    " ofertas, " + initial.changes.Count + " cambios registrados, " +
                    initial.reviews.Count + " revisiones.");
            }

            if (suppliers.ContentRevision != revisionBefore)
            {
                errors.Add("La simulación modificó supplier.authoring. Debe ser no destructiva.");
            }
        }

        if (activeOffers != 66)
        {
            warnings.Add("La línea base cerrada de 2.3B tenía 66 ofertas; ahora hay " + activeOffers +
                ". Es válido si el catálogo se amplió intencionadamente.");
        }

        information.Add("Proveedores activos: " + activeSuppliers + ".");
        information.Add("Ofertas activas: " + activeOffers + ".");
        information.Add("Ciclo global de revisión: cada " + settings.ReviewEveryGameDays + " días.");
        information.Add("2.3C no genera promociones ni pedidos y no escribe en Inventario/Recepciones.");
        information.Add("La persistencia integral se conectará en 2.3J; 2.3C ya expone CreateSnapshot/TryRestoreSnapshot.");

        Finish();
    }

    private void ValidateSimulatedBounds(
        BistroBuilderSupplierMarketSnapshot snapshot,
        BistroBuilderSupplierAuthoringDatabase suppliers)
    {
        Dictionary<string, BistroBuilderSupplierAuthoringRecord> supplierById =
            new Dictionary<string, BistroBuilderSupplierAuthoringRecord>(StringComparer.Ordinal);
        Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord> offerById =
            new Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord>(StringComparer.Ordinal);

        for (int s = 0; s < suppliers.Suppliers.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[s];
            if (supplier == null || !supplier.isActive)
            {
                continue;
            }

            supplierById[supplier.SupplierId] = supplier;
            if (supplier.baseOffers == null) continue;
            for (int o = 0; o < supplier.baseOffers.Count; o++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[o];
                if (offer != null && offer.isActive) offerById[offer.SupplierOfferId] = offer;
            }
        }

        for (int index = 0; index < snapshot.offerStates.Count; index++)
        {
            BistroBuilderSupplierMarketOfferState state = snapshot.offerStates[index];
            BistroBuilderSupplierAuthoringRecord supplier;
            BistroBuilderSupplierBaseOfferAuthoringRecord offer;
            if (state == null || !supplierById.TryGetValue(state.supplierId, out supplier) ||
                !offerById.TryGetValue(state.supplierOfferId, out offer))
            {
                errors.Add("Estado de mercado sin autoría durante validación de límites.");
                continue;
            }

            float minPct = Mathf.Max(
                supplier.priceEvolutionProfile.minimumVariationPercent,
                offer.minimumMarketVariationPercent);
            float maxPct = Mathf.Min(
                supplier.priceEvolutionProfile.maximumVariationPercent,
                offer.maximumMarketVariationPercent);
            long min = Math.Max(1L, (long)Math.Round(offer.basePriceCents * (1.0 + minPct / 100.0)));
            long max = Math.Max(min, (long)Math.Round(offer.basePriceCents * (1.0 + maxPct / 100.0)));

            if (state.currentPriceCents < min || state.currentPriceCents > max)
            {
                errors.Add(state.supplierOfferId + ": precio de mercado fuera de límites.");
            }
        }
    }

    private void Finish()
    {
        Debug.Log(
            "VALIDACIÓN 2.3C — Errores: " + errors.Count +
            ", advertencias: " + warnings.Count +
            ", información: " + information.Count + ".");
        Repaint();
    }

    private static void DrawSection(string title, List<string> lines, MessageType type)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (lines.Count == 0)
        {
            EditorGUILayout.HelpBox("Ninguno.", MessageType.None);
            return;
        }

        for (int index = 0; index < lines.Count; index++)
        {
            EditorGUILayout.HelpBox(lines[index], type);
        }
    }
}
#endif
