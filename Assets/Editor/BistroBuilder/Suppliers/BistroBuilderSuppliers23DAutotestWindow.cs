#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23DAutotestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3D - Autotest Motor Comercial Inteligente")]
    public static void Open()
    {
        BistroBuilderSuppliers23DAutotestWindow window =
            GetWindow<BistroBuilderSuppliers23DAutotestWindow>("Autotest 2.3D");
        window.minSize = new Vector2(860f, 560f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "AUTOTEST 2.3D — Motor Comercial Inteligente",
            EditorStyles.boldLabel);

        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("Ejecutar autotest", GUILayout.Height(32f)))
        {
            Run();
        }
        GUI.enabled = true;

        EditorGUILayout.LabelField(
            "Pruebas superadas: " + passed + " / Pruebas fallidas: " + failed,
            EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < results.Count; index++)
        {
            EditorGUILayout.LabelField(results[index], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        results.Clear();
        passed = 0;
        failed = 0;

        try
        {
            Check(!EditorApplication.isPlaying, "Autotest ejecutado en Edit Mode.");

            BistroBuilderSupplierAuthoringDatabase suppliers =
                AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(
                    BistroBuilderSuppliers23DPaths.SupplierDatabasePath);
            BistroBuilderIngredientAuthoringDatabase ingredients =
                AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientAuthoringDatabase>(
                    BistroBuilderSuppliers23DPaths.IngredientDatabasePath);
            BistroBuilderSupplierMarketSettings marketSettings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierMarketSettings>(
                    BistroBuilderSuppliers23DPaths.MarketSettingsPath);
            BistroBuilderSupplierCommercialIntelligenceSettings settings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierCommercialIntelligenceSettings>(
                    BistroBuilderSuppliers23DPaths.CommercialSettingsPath);

            Check(suppliers != null, "supplier.authoring existe.");
            Check(ingredients != null, "ingredient.authoring existe.");
            Check(marketSettings != null, "supplier.market.settings de 2.3C existe.");
            Check(settings != null, "supplier.commercial.settings de 2.3D existe.");
            if (suppliers == null || ingredients == null || marketSettings == null || settings == null)
            {
                return;
            }

            Check(settings.SchemaId == BistroBuilderSupplierCommercialIntelligenceSettings.CurrentSchemaId,
                "supplier.commercial.settings usa schemaId canónico.");
            Check(settings.SchemaVersion == BistroBuilderSupplierCommercialIntelligenceSettings.CurrentSchemaVersion,
                "supplier.commercial.settings usa schemaVersion canónico.");
            Check(marketSettings.ReviewEveryGameDays == 5,
                "2.3D consume el ciclo cerrado de 5 días de 2.3C.");
            Check(settings.MaximumActivePromotionsPerSupplier >= 1,
                "Existe un límite positivo de promociones activas por proveedor.");
            Check(settings.OfferReuseCooldownDays >= 0,
                "El cooldown de reutilización de ofertas es válido.");
            Check(settings.HighCampaignChance > settings.LowCampaignChance,
                "Frecuencia Alta tiene mayor probabilidad que Baja.");
            Check(settings.MediumCampaignChance > settings.VeryLowCampaignChance,
                "Frecuencia Media tiene mayor probabilidad que Muy Baja.");

            int supplierRevisionBefore = suppliers.ContentRevision;
            int ingredientRevisionBefore = ingredients.ContentRevision;
            int activeSupplierCount;
            int activeOfferCount;
            int eligibleOfferCount;
            CountAuthoring(suppliers, out activeSupplierCount, out activeOfferCount, out eligibleOfferCount);
            Check(activeSupplierCount == 6, "Hay exactamente 6 proveedores activos.");
            Check(activeOfferCount == 66, "Hay exactamente 66 ofertas base activas.");
            Check(eligibleOfferCount > 0, "Existe catálogo elegible para promociones.");
            Check(AllSupplierPromotionProfilesValid(suppliers),
                "Todos los proveedores activos tienen un perfil promocional válido.");
            Check(AllActiveOfferIdsUnique(suppliers),
                "Todos los SupplierOfferId activos son únicos.");

            ulong marketSeed = BistroBuilderSupplierMarketEngine.StableSeedFromText(
                "23d-autotest",
                marketSettings.DeterministicSalt);
            BistroBuilderSupplierMarketSnapshot market =
                BistroBuilderSupplierMarketEngine.CreateInitialSnapshot(
                    suppliers,
                    marketSettings,
                    marketSeed,
                    1);
            BistroBuilderSupplierCommercialIntelligenceSnapshot commercial =
                BistroBuilderSupplierCommercialIntelligenceEngine.CreateInitialSnapshot(
                    market,
                    settings,
                    null);

            Check(market != null, "Se crea un mercado determinista base.");
            Check(commercial != null, "Se crea el snapshot inicial del Motor Comercial Inteligente.");
            Check(commercial.schemaId == BistroBuilderSupplierCommercialIntelligenceSnapshot.CurrentSchemaId,
                "El snapshot comercial usa schema canónico.");
            Check(commercial.sourceMarketSeed == market.marketSeed,
                "El snapshot comercial queda vinculado a la semilla de mercado.");
            Check(commercial.commercialSeed != 0UL,
                "El Motor Comercial Inteligente tiene semilla determinista propia.");
            Check(commercial.currentGameDay == 1,
                "El estado comercial comienza en día 1.");
            Check(commercial.activePromotions.Count == 0,
                "Una partida nueva no inventa promociones antes de la primera revisión.");
            Check(commercial.promotionHistory.Count == 0,
                "Una partida nueva comienza sin historial promocional ficticio.");
            Check(commercial.lastProcessedMarketReviewDay == 0,
                "No existe revisión comercial ficticia al iniciar.");

            string error;
            List<BistroBuilderSupplierPromotionRecord> expired;
            Check(BistroBuilderSupplierCommercialIntelligenceEngine.TryAdvanceToGameDay(
                    commercial, settings, 4, out expired, out error),
                "2.3D avanza al día 4 sin error.");
            Check(expired.Count == 0,
                "Antes de la primera revisión no caduca ninguna promoción.");
            Check(commercial.activePromotions.Count == 0,
                "Día 4 sigue sin promociones comerciales.");

            List<BistroBuilderSupplierMarketReviewOutcome> marketOutcomes;
            Check(BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
                    market, suppliers, marketSettings, 5, out marketOutcomes, out error),
                "2.3C alcanza la revisión real del día 5.");
            Check(marketOutcomes.Count == 1 && marketOutcomes[0].reviewDay == 5,
                "El día 5 contiene exactamente una revisión de mercado.");

            string marketFingerprintBeforeD = BistroBuilderSupplierMarketEngine.BuildFingerprint(market);
            BistroBuilderSupplierCommercialReviewOutcome firstOutcome;
            List<BistroBuilderSupplierPromotionRecord> firstStarted;
            List<BistroBuilderSupplierPromotionRecord> firstExpired;
            Check(BistroBuilderSupplierCommercialIntelligenceEngine.TryProcessMarketReview(
                    commercial,
                    market,
                    suppliers,
                    ingredients,
                    settings,
                    5,
                    out firstOutcome,
                    out firstStarted,
                    out firstExpired,
                    out error),
                "2.3D procesa la revisión comercial del día 5.");
            Check(firstOutcome.processed,
                "La primera revisión comercial queda marcada como procesada.");
            Check(firstOutcome.suppliersEvaluated == 6,
                "La revisión comercial evalúa los seis proveedores.");
            Check(commercial.lastProcessedMarketReviewDay == 5,
                "2.3D registra día 5 como última revisión comercial procesada.");
            Check(BistroBuilderSupplierMarketEngine.BuildFingerprint(market) == marketFingerprintBeforeD,
                "2.3D no muta el snapshot de mercado que analiza.");

            long commercialRevisionAfterFirst = commercial.commercialRevision;
            int activeAfterFirst = commercial.activePromotions.Count;
            int reviewCountAfterFirst = commercial.reviews.Count;
            BistroBuilderSupplierCommercialReviewOutcome duplicateOutcome;
            List<BistroBuilderSupplierPromotionRecord> duplicateStarted;
            List<BistroBuilderSupplierPromotionRecord> duplicateExpired;
            Check(BistroBuilderSupplierCommercialIntelligenceEngine.TryProcessMarketReview(
                    commercial,
                    market,
                    suppliers,
                    ingredients,
                    settings,
                    5,
                    out duplicateOutcome,
                    out duplicateStarted,
                    out duplicateExpired,
                    out error),
                "Reprocesar el mismo día es una operación válida e idempotente.");
            Check(!duplicateOutcome.processed,
                "Una revisión ya procesada no se vuelve a ejecutar.");
            Check(duplicateStarted.Count == 0,
                "La idempotencia no duplica promociones.");
            Check(commercial.activePromotions.Count == activeAfterFirst,
                "La idempotencia conserva la cardinalidad de promociones activas.");
            Check(commercial.reviews.Count == reviewCountAfterFirst,
                "La idempotencia no duplica el historial de revisiones.");
            Check(commercial.commercialRevision == commercialRevisionAfterFirst,
                "La idempotencia no incrementa CommercialRevision.");


            BistroBuilderSupplierMarketSnapshot jumpMarket =
                BistroBuilderSupplierMarketEngine.CreateInitialSnapshot(
                    suppliers, marketSettings, marketSeed, 1);
            BistroBuilderSupplierCommercialIntelligenceSnapshot jumpCommercial =
                BistroBuilderSupplierCommercialIntelligenceEngine.CreateInitialSnapshot(
                    jumpMarket, settings, commercial.commercialSeed);
            List<BistroBuilderSupplierMarketReviewOutcome> jumpOutcomes;
            Check(BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
                    jumpMarket, suppliers, marketSettings, 16, out jumpOutcomes, out error),
                "2.3C soporta un salto controlado del día 1 al 16.");
            Check(jumpOutcomes.Count == 3,
                "El salto 1→16 contiene las revisiones 5, 10 y 15.");
            for (int jumpIndex = 0; jumpIndex < jumpOutcomes.Count; jumpIndex++)
            {
                int jumpReviewDay = jumpOutcomes[jumpIndex].reviewDay;
                BistroBuilderSupplierMarketSnapshot scoped =
                    BistroBuilderSupplierCommercialIntelligenceEngine.CreateReviewScopedMarketSnapshot(
                        jumpMarket, jumpReviewDay);
                BistroBuilderSupplierCommercialReviewOutcome jumpCommercialOutcome;
                List<BistroBuilderSupplierPromotionRecord> jumpStarted;
                List<BistroBuilderSupplierPromotionRecord> jumpExpired;
                if (!BistroBuilderSupplierCommercialIntelligenceEngine.TryProcessMarketReview(
                        jumpCommercial, scoped, suppliers, ingredients, settings, jumpReviewDay,
                        out jumpCommercialOutcome, out jumpStarted, out jumpExpired, out error))
                {
                    Check(false, "2.3D procesa cada revisión intermedia de un salto multicíclo: " + error);
                    break;
                }
            }
            Check(jumpCommercial.reviews.Count == 3,
                "2.3D conserva las tres revisiones comerciales de un salto 1→16.");
            Check(jumpCommercial.lastProcessedMarketReviewDay == 15,
                "Tras el salto multicíclo, la última revisión comercial real es día 15.");

            BistroBuilderSuppliers23DSimulationResult simulation;
            Check(BistroBuilderSuppliers23DSimulation.TryRun(
                    suppliers,
                    ingredients,
                    marketSettings,
                    settings,
                    "23d-autotest-long",
                    120,
                    out simulation,
                    out error),
                "La simulación determinista completa a día 120 se ejecuta.");
            if (simulation == null)
            {
                return;
            }

            Check(simulation.reviews == 24,
                "A día 120 se han procesado exactamente 24 revisiones comerciales.");
            Check(simulation.promotionsStarted > 0,
                "El Motor Comercial Inteligente genera promociones reales.");
            Check(simulation.promotionsExpired > 0,
                "Las promociones temporales finalizan y pasan a historial.");
            Check(simulation.campaigns > 0,
                "Se generan campañas comerciales de proveedor.");
            Check(simulation.maximumSimultaneousPromotions <=
                    activeSupplierCount * settings.MaximumActivePromotionsPerSupplier,
                "Nunca se supera el máximo global derivado de promociones simultáneas.");
            Check(simulation.minimumDiscountBasisPoints > 0,
                "Todos los descuentos observados son positivos.");
            Check(simulation.maximumDiscountBasisPoints < 10000,
                "Ningún descuento observado alcanza o supera el 100%. ");
            Check(simulation.minimumDurationDays >= 1,
                "Todas las promociones observadas duran al menos un día.");
            Check(simulation.maximumDurationDays <= 30,
                "No aparecen duraciones promocionales descontroladas.");
            Check(simulation.commercial.activePromotions.Count +
                    simulation.commercial.promotionHistory.Count == simulation.promotionsStarted,
                "Toda promoción iniciada termina activa o archivada exactamente una vez.");
            Check(AllPromotionsStructurallyValid(simulation.commercial, suppliers),
                "Todas las promociones respetan precio, descuento, duración, FK y perfil del proveedor.");
            Check(NoOverlappingOfferPromotions(simulation.commercial),
                "Nunca existen promociones solapadas para una misma SupplierOfferId.");
            Check(AllPromotionIdsUnique(simulation.commercial),
                "Todos los PromotionId son únicos.");
            Check(AllReviewDaysUseFiveDayCycle(simulation.commercial),
                "Todas las revisiones comerciales ocurren sobre el ciclo 5/10/15/... de 2.3C.");
            Check(AllReasonsExplainable(simulation.commercial),
                "Todas las promociones publican reasonCode y explicación legible.");
            Check(BistroBuilderSupplierCommercialIntelligenceEngine.ValidateSnapshotAgainstAuthoringAndMarket(
                    simulation.commercial,
                    simulation.market,
                    suppliers,
                    out error),
                "El snapshot comercial final converge con mercado y autoría.");

            BistroBuilderSuppliers23DSimulationResult repeated;
            Check(BistroBuilderSuppliers23DSimulation.TryRun(
                    suppliers,
                    ingredients,
                    marketSettings,
                    settings,
                    "23d-autotest-long",
                    120,
                    out repeated,
                    out error),
                "La simulación puede repetirse con la misma semilla.");
            Check(repeated != null &&
                    BistroBuilderSupplierCommercialIntelligenceEngine.BuildFingerprint(repeated.commercial) ==
                    BistroBuilderSupplierCommercialIntelligenceEngine.BuildFingerprint(simulation.commercial),
                "Misma semilla + mismos datos + mismos días producen exactamente el mismo comportamiento comercial.");

            BistroBuilderSuppliers23DSimulationResult differentSeed;
            Check(BistroBuilderSuppliers23DSimulation.TryRun(
                    suppliers,
                    ingredients,
                    marketSettings,
                    settings,
                    "23d-autotest-different-seed",
                    120,
                    out differentSeed,
                    out error),
                "El motor acepta una semilla de partida distinta.");
            Check(differentSeed != null &&
                    BistroBuilderSupplierCommercialIntelligenceEngine.BuildFingerprint(differentSeed.commercial) !=
                    BistroBuilderSupplierCommercialIntelligenceEngine.BuildFingerprint(simulation.commercial),
                "Una semilla distinta produce una trayectoria comercial distinta.");

            BistroBuilderSupplierCommercialIntelligenceSnapshot clone = simulation.commercial.DeepClone();
            Check(clone != null && !ReferenceEquals(clone, simulation.commercial),
                "DeepClone crea un snapshot comercial independiente.");
            Check(clone.activePromotions != simulation.commercial.activePromotions,
                "DeepClone no comparte la lista de promociones activas.");
            Check(clone.promotionHistory != simulation.commercial.promotionHistory,
                "DeepClone no comparte el historial promocional.");
            string fingerprintBeforeMutation =
                BistroBuilderSupplierCommercialIntelligenceEngine.BuildFingerprint(simulation.commercial);
            if (clone.activePromotions.Count > 0)
            {
                clone.activePromotions[0].promotionalPriceCents++;
            }
            else if (clone.promotionHistory.Count > 0)
            {
                clone.promotionHistory[0].promotionalPriceCents++;
            }
            Check(BistroBuilderSupplierCommercialIntelligenceEngine.BuildFingerprint(simulation.commercial) ==
                    fingerprintBeforeMutation,
                "Modificar un clon no altera el snapshot original.");

            Check(suppliers.ContentRevision == supplierRevisionBefore,
                "El autotest no modifica ContentRevision de supplier.authoring.");
            Check(ingredients.ContentRevision == ingredientRevisionBefore,
                "El autotest no modifica ContentRevision de ingredient.authoring.");
            Check(AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierCommercialIntelligenceSettings>(
                    BistroBuilderSuppliers23DPaths.CommercialSettingsPath) == settings,
                "El autotest no reemplaza supplier.commercial.settings.");
        }
        catch (Exception exception)
        {
            failed++;
            results.Add("[FALLO] Excepción del autotest: " + exception);
        }
        finally
        {
            Debug.Log("AUTOTEST 2.3D — " + passed + " superadas / " + failed + " fallidas.");
            Repaint();
        }
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

    private static void CountAuthoring(
        BistroBuilderSupplierAuthoringDatabase suppliers,
        out int activeSuppliers,
        out int activeOffers,
        out int eligibleOffers)
    {
        activeSuppliers = 0;
        activeOffers = 0;
        eligibleOffers = 0;
        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive) continue;
            activeSuppliers++;
            if (supplier.baseOffers == null) continue;
            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                if (offer == null || !offer.isActive) continue;
                activeOffers++;
                if (offer.promotionEligible) eligibleOffers++;
            }
        }
    }

    private static bool AllSupplierPromotionProfilesValid(BistroBuilderSupplierAuthoringDatabase suppliers)
    {
        for (int index = 0; index < suppliers.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[index];
            if (supplier == null || !supplier.isActive) continue;
            BistroBuilderSupplierPromotionProfileAuthoring profile = supplier.promotionProfile;
            if (profile == null || profile.minimumDiscountPercent < 0f ||
                profile.maximumDiscountPercent < profile.minimumDiscountPercent ||
                profile.maximumDiscountPercent >= 100f ||
                profile.minimumDurationDays < 1 ||
                profile.maximumDurationDays < profile.minimumDurationDays ||
                profile.eligibleCatalogs == BistroBuilderSupplierCatalogFlags.None)
            {
                return false;
            }
        }
        return true;
    }

    private static bool AllActiveOfferIdsUnique(BistroBuilderSupplierAuthoringDatabase suppliers)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null) continue;
            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                if (offer != null && offer.isActive &&
                    (string.IsNullOrWhiteSpace(offer.SupplierOfferId) || !ids.Add(offer.SupplierOfferId)))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool AllPromotionsStructurallyValid(
        BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot,
        BistroBuilderSupplierAuthoringDatabase suppliers)
    {
        Dictionary<string, BistroBuilderSupplierAuthoringRecord> supplierById =
            new Dictionary<string, BistroBuilderSupplierAuthoringRecord>(StringComparer.Ordinal);
        Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord> offerById =
            new Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord>(StringComparer.Ordinal);
        for (int supplierIndex = 0; supplierIndex < suppliers.Suppliers.Count; supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[supplierIndex];
            if (supplier == null || !supplier.isActive) continue;
            supplierById[supplier.SupplierId] = supplier;
            if (supplier.baseOffers == null) continue;
            for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[offerIndex];
                if (offer != null && offer.isActive) offerById[offer.SupplierOfferId] = offer;
            }
        }

        List<BistroBuilderSupplierPromotionRecord> all =
            new List<BistroBuilderSupplierPromotionRecord>();
        all.AddRange(snapshot.activePromotions);
        all.AddRange(snapshot.promotionHistory);
        for (int index = 0; index < all.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = all[index];
            BistroBuilderSupplierAuthoringRecord supplier;
            BistroBuilderSupplierBaseOfferAuthoringRecord offer;
            if (promotion == null ||
                !supplierById.TryGetValue(promotion.supplierId, out supplier) ||
                !offerById.TryGetValue(promotion.supplierOfferId, out offer) ||
                supplier.promotionProfile == null || !offer.promotionEligible)
            {
                return false;
            }
            int minBp = Mathf.RoundToInt(supplier.promotionProfile.minimumDiscountPercent * 100f);
            int maxBp = Mathf.RoundToInt(supplier.promotionProfile.maximumDiscountPercent * 100f);
            if (promotion.discountBasisPoints < Math.Max(1, minBp) ||
                promotion.discountBasisPoints > maxBp ||
                promotion.promotionalPriceCents <= 0 ||
                promotion.promotionalPriceCents >= promotion.referenceMarketPriceCents ||
                promotion.DurationDays < supplier.promotionProfile.minimumDurationDays ||
                promotion.DurationDays > supplier.promotionProfile.maximumDurationDays ||
                promotion.sourceAvailabilityAtStart != BistroBuilderSupplierOfferAvailability.Disponible ||
                promotion.ingredientId != offer.ingredientId ||
                promotion.packageFormatId != offer.packageFormatId)
            {
                return false;
            }
        }
        return true;
    }

    private static bool NoOverlappingOfferPromotions(BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot)
    {
        List<BistroBuilderSupplierPromotionRecord> all =
            new List<BistroBuilderSupplierPromotionRecord>();
        all.AddRange(snapshot.activePromotions);
        all.AddRange(snapshot.promotionHistory);
        for (int left = 0; left < all.Count; left++)
        {
            if (all[left] == null) continue;
            for (int right = left + 1; right < all.Count; right++)
            {
                if (all[right] == null || all[left].supplierOfferId != all[right].supplierOfferId) continue;
                bool overlaps = all[left].startGameDay < all[right].endGameDayExclusive &&
                                all[right].startGameDay < all[left].endGameDayExclusive;
                if (overlaps) return false;
            }
        }
        return true;
    }

    private static bool AllPromotionIdsUnique(BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < snapshot.activePromotions.Count; index++)
        {
            if (snapshot.activePromotions[index] == null ||
                !ids.Add(snapshot.activePromotions[index].promotionId)) return false;
        }
        for (int index = 0; index < snapshot.promotionHistory.Count; index++)
        {
            if (snapshot.promotionHistory[index] == null ||
                !ids.Add(snapshot.promotionHistory[index].promotionId)) return false;
        }
        return true;
    }

    private static bool AllReviewDaysUseFiveDayCycle(BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot)
    {
        for (int index = 0; index < snapshot.reviews.Count; index++)
        {
            if (snapshot.reviews[index] == null || snapshot.reviews[index].gameDay % 5 != 0)
                return false;
        }
        return true;
    }

    private static bool AllReasonsExplainable(BistroBuilderSupplierCommercialIntelligenceSnapshot snapshot)
    {
        for (int index = 0; index < snapshot.activePromotions.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = snapshot.activePromotions[index];
            if (promotion == null || string.IsNullOrWhiteSpace(promotion.reasonCode) ||
                string.IsNullOrWhiteSpace(promotion.reasonText)) return false;
        }
        for (int index = 0; index < snapshot.promotionHistory.Count; index++)
        {
            BistroBuilderSupplierPromotionRecord promotion = snapshot.promotionHistory[index];
            if (promotion == null || string.IsNullOrWhiteSpace(promotion.reasonCode) ||
                string.IsNullOrWhiteSpace(promotion.reasonText)) return false;
        }
        return true;
    }
}
#endif
