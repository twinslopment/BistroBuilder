#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23DRuntimeFunctionalTestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;
    private int runtimeErrors;
    private bool running;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3D - Prueba funcional runtime")]
    public static void Open()
    {
        BistroBuilderSuppliers23DRuntimeFunctionalTestWindow window =
            GetWindow<BistroBuilderSuppliers23DRuntimeFunctionalTestWindow>("Prueba runtime 2.3D");
        window.minSize = new Vector2(900f, 620f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("PRUEBA FUNCIONAL RUNTIME 2.3D", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Debe ejecutarse en Play Mode. La prueba respalda mercado + motor comercial, usa semillas controladas, " +
            "simula 60 días, verifica promociones/cotizaciones/determinismo y restaura ambos snapshots al finalizar.",
            MessageType.Info);

        GUI.enabled = EditorApplication.isPlaying && !running;
        if (GUILayout.Button("Ejecutar prueba completa", GUILayout.Height(34f)))
        {
            RunTest();
        }
        GUI.enabled = true;

        EditorGUILayout.LabelField(
            "Correctos: " + passed + "  Fallos: " + failed +
            "  Errores/Excepciones/Asserts: " + runtimeErrors,
            EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < results.Count; index++)
        {
            EditorGUILayout.LabelField(results[index], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunTest()
    {
        results.Clear();
        passed = 0;
        failed = 0;
        runtimeErrors = 0;
        running = true;
        Application.logMessageReceived += HandleLog;

        BistroBuilderSupplierMarketService marketService = null;
        BistroBuilderSupplierCommercialIntelligenceService commercialService = null;
        BistroBuilderSupplierMarketSnapshot originalMarket = null;
        BistroBuilderSupplierCommercialIntelligenceSnapshot originalCommercial = null;

        try
        {
            BistroBuilderSupplierMarketService[] markets =
                UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierMarketService>(FindObjectsSortMode.None);
            BistroBuilderSupplierCommercialIntelligenceService[] commercials =
                UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierCommercialIntelligenceService>(FindObjectsSortMode.None);

            Check(markets.Length == 1,
                "Existe exactamente una autoridad runtime BistroBuilderSupplierMarketService.");
            Check(commercials.Length == 1,
                "Existe exactamente una autoridad runtime BistroBuilderSupplierCommercialIntelligenceService.");
            if (markets.Length != 1 || commercials.Length != 1)
            {
                return;
            }

            marketService = markets[0];
            commercialService = commercials[0];
            Check(marketService.IsInitialized, "El mercado 2.3C está inicializado.");
            Check(commercialService.IsInitialized, "El Motor Comercial Inteligente 2.3D está inicializado.");
            Check(string.IsNullOrEmpty(commercialService.LastInitializationError),
                "2.3D no conserva error residual de inicialización.");

            originalMarket = marketService.CreateSnapshot();
            originalCommercial = commercialService.CreateSnapshot();
            Check(originalMarket != null, "Se captura snapshot original de 2.3C.");
            Check(originalCommercial != null, "Se captura snapshot original de 2.3D.");
            if (originalMarket == null || originalCommercial == null)
            {
                return;
            }

            BistroBuilderSupplierAuthoringDatabase suppliers =
                Resources.Load<BistroBuilderSupplierAuthoringDatabase>(
                    BistroBuilderSupplierCommercialIntelligenceService.SupplierAuthoringResourcePath);
            BistroBuilderIngredientAuthoringDatabase ingredients =
                Resources.Load<BistroBuilderIngredientAuthoringDatabase>(
                    BistroBuilderSupplierCommercialIntelligenceService.IngredientAuthoringResourcePath);
            BistroBuilderSupplierMarketSettings marketSettings =
                Resources.Load<BistroBuilderSupplierMarketSettings>(
                    BistroBuilderSupplierMarketService.MarketSettingsResourcePath);
            BistroBuilderSupplierCommercialIntelligenceSettings settings =
                Resources.Load<BistroBuilderSupplierCommercialIntelligenceSettings>(
                    BistroBuilderSupplierCommercialIntelligenceService.SettingsResourcePath);

            Check(suppliers != null, "Runtime localiza supplier.authoring.");
            Check(ingredients != null, "Runtime localiza ingredient.authoring.");
            Check(marketSettings != null, "Runtime localiza supplier.market.settings.");
            Check(settings != null, "Runtime localiza supplier.commercial.settings.");
            if (suppliers == null || ingredients == null || marketSettings == null || settings == null)
            {
                return;
            }

            Check(marketSettings.ReviewEveryGameDays == 5,
                "2.3D consume el ciclo de mercado de 5 días.");
            Check(settings.SchemaVersion == 1,
                "Runtime consume schema v1 de supplier.commercial.settings.");

            int supplierRevisionBefore = suppliers.ContentRevision;
            int ingredientRevisionBefore = ingredients.ContentRevision;
            ulong marketSeed = BistroBuilderSupplierMarketEngine.StableSeedFromText(
                "runtime-functional-23d-market",
                marketSettings.DeterministicSalt);
            ulong commercialSeed = BistroBuilderSupplierMarketEngine.StableSeedFromText(
                "runtime-functional-23d-commercial",
                settings.DeterministicSalt);

            Check(marketService.TryInitializeFresh(marketSeed),
                "2.3C acepta la semilla controlada de la prueba 2.3D.");
            Check(commercialService.TryInitializeFresh(commercialSeed),
                "2.3D acepta una semilla comercial controlada.");
            Check(marketService.MarketSeed == marketSeed,
                "La semilla de mercado runtime coincide con la controlada.");
            Check(commercialService.CommercialSeed == commercialSeed,
                "La semilla comercial runtime coincide con la controlada.");
            Check(commercialService.SourceMarketSeed == marketSeed,
                "2.3D queda vinculado a la semilla real de 2.3C.");
            Check(commercialService.CurrentGameDay == 1,
                "La simulación comercial controlada comienza en día 1.");
            Check(commercialService.ActivePromotionCount == 0,
                "No existen promociones ficticias al iniciar.");
            Check(commercialService.PromotionHistoryCount == 0,
                "No existe historial promocional ficticio al iniciar.");

            int reviewEvents = 0;
            int startedEvents = 0;
            int endedEvents = 0;
            Action<BistroBuilderSupplierCommercialReviewOutcome> reviewHandler =
                delegate { reviewEvents++; };
            Action<BistroBuilderSupplierPromotionRecord> startedHandler =
                delegate { startedEvents++; };
            Action<BistroBuilderSupplierPromotionRecord> endedHandler =
                delegate { endedEvents++; };
            commercialService.CommercialReviewProcessed += reviewHandler;
            commercialService.PromotionStarted += startedHandler;
            commercialService.PromotionEnded += endedHandler;

            string error;
            Check(marketService.TryAdvanceToGameDay(4, out error),
                "El mercado avanza al día 4 sin error.");
            Check(commercialService.TrySynchronizeCurrentMarketState(out error),
                "2.3D se sincroniza con día 4 sin error.");
            Check(reviewEvents == 0,
                "Día 4 no produce revisión comercial.");
            Check(commercialService.ActivePromotionCount == 0,
                "Día 4 continúa sin promociones.");

            Check(marketService.TryAdvanceToGameDay(5, out error),
                "El mercado ejecuta la primera revisión del día 5.");
            Check(commercialService.TrySynchronizeCurrentMarketState(out error),
                "2.3D queda sincronizado después de la revisión del día 5.");
            Check(reviewEvents == 1,
                "La primera revisión de mercado dispara exactamente una revisión comercial.");

            BistroBuilderSupplierCommercialIntelligenceSnapshot day5 =
                commercialService.CreateSnapshot();
            Check(day5 != null && day5.lastProcessedMarketReviewDay == 5,
                "El snapshot comercial registra la revisión del día 5.");
            Check(day5 != null && day5.reviews.Count == 1,
                "El snapshot día 5 contiene una revisión comercial.");
            Check(day5 != null && day5.reviews[0].suppliersEvaluated == 6,
                "La revisión runtime evalúa exactamente seis proveedores.");

            bool quoteTested = false;
            bool defensivePromotionTested = false;
            int activePeak = commercialService.ActivePromotionCount;
            for (int day = 6; day <= 60; day++)
            {
                if (!marketService.TryAdvanceToGameDay(day, out error))
                {
                    Check(false, "Mercado avanza de forma continua hasta día 60: " + error);
                    break;
                }
                if (!commercialService.TrySynchronizeCurrentMarketState(out error))
                {
                    Check(false, "2.3D sincroniza de forma continua hasta día 60: " + error);
                    break;
                }

                activePeak = Math.Max(activePeak, commercialService.ActivePromotionCount);
                if (!quoteTested && commercialService.ActivePromotionCount > 0)
                {
                    List<BistroBuilderSupplierPromotionRecord> active =
                        new List<BistroBuilderSupplierPromotionRecord>();
                    commercialService.CopyActivePromotions(active);
                    if (active.Count > 0)
                    {
                        BistroBuilderSupplierPromotionRecord promotion = active[0];
                        BistroBuilderSupplierCommercialQuote quote;
                        bool quoteOk = commercialService.TryGetCommercialQuote(
                            promotion.supplierOfferId,
                            out quote);
                        Check(quoteOk, "La API 2.3D devuelve una cotización por SupplierOfferId.");
                        Check(quoteOk && quote.hasActivePromotion,
                            "La cotización identifica la promoción activa.");
                        Check(quoteOk && quote.effectivePriceCents <= quote.marketPriceCents,
                            "El precio efectivo nunca supera el precio de mercado durante una promoción.");
                        Check(quoteOk && quote.effectivePriceCents > 0,
                            "El precio comercial efectivo siempre es positivo.");
                        Check(quoteOk && quote.promotionId == promotion.promotionId,
                            "La cotización conserva PromotionId trazable.");
                        Check(quoteOk && !string.IsNullOrWhiteSpace(quote.reasonCode) &&
                            !string.IsNullOrWhiteSpace(quote.reasonText),
                            "La cotización explica por qué existe la promoción.");
                        Check(quoteOk && quote.availableForNewOrders ==
                            (quote.availability != BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado),
                            "La promoción no anula las reglas de disponibilidad de 2.3C.");
                        Check(commercialService.GetEffectivePriceCents(promotion.supplierOfferId) ==
                            quote.effectivePriceCents,
                            "GetEffectivePriceCents coincide con la cotización comercial.");
                        quoteTested = true;

                        long originalPromoPrice = promotion.promotionalPriceCents;
                        promotion.promotionalPriceCents++;
                        BistroBuilderSupplierPromotionRecord queriedAgain;
                        bool againOk = commercialService.TryGetActivePromotion(
                            promotion.supplierOfferId,
                            out queriedAgain);
                        Check(againOk && queriedAgain.promotionalPriceCents == originalPromoPrice,
                            "TryGetActivePromotion devuelve copia defensiva.");
                        defensivePromotionTested = true;
                    }
                }
            }

            Check(marketService.CurrentGameDay == 60,
                "La prueba alcanza el día 60 de mercado.");
            Check(commercialService.CurrentGameDay == 60,
                "El Motor Comercial Inteligente alcanza el día 60.");
            Check(reviewEvents == 12,
                "Día 60 acumula exactamente 12 revisiones comerciales de cinco días.");
            Check(startedEvents > 0,
                "La simulación runtime inicia promociones temporales reales.");
            Check(endedEvents > 0,
                "La simulación runtime finaliza promociones temporales reales.");
            Check(quoteTested,
                "Durante la simulación existió al menos una promoción consultable.");
            Check(defensivePromotionTested,
                "Se verificó copia defensiva de una promoción runtime.");
            Check(activePeak <= 6 * settings.MaximumActivePromotionsPerSupplier,
                "Runtime respeta el máximo derivado de promociones simultáneas.");

            BistroBuilderSupplierMarketSnapshot marketDay60 = marketService.CreateSnapshot();
            BistroBuilderSupplierCommercialIntelligenceSnapshot commercialDay60 =
                commercialService.CreateSnapshot();
            Check(marketDay60 != null && marketDay60.lastReviewGameDay == 60,
                "El snapshot de mercado queda revisado hasta día 60.");
            Check(commercialDay60 != null && commercialDay60.lastProcessedMarketReviewDay == 60,
                "El snapshot comercial queda procesado hasta día 60.");
            Check(commercialDay60 != null &&
                commercialDay60.activePromotions.Count + commercialDay60.promotionHistory.Count == startedEvents,
                "Cada PromotionStarted termina representado exactamente una vez en el snapshot.");
            Check(commercialDay60 != null && commercialDay60.promotionHistory.Count == endedEvents,
                "Cada PromotionEnded queda archivado exactamente una vez.");
            Check(BistroBuilderSupplierCommercialIntelligenceEngine.ValidateSnapshotAgainstAuthoringAndMarket(
                    commercialDay60,
                    marketDay60,
                    suppliers,
                    out error),
                "El snapshot runtime 2.3D converge con mercado y supplier.authoring.");
            Check(suppliers.ContentRevision == supplierRevisionBefore,
                "2.3D runtime no modifica supplier.authoring.");
            Check(ingredients.ContentRevision == ingredientRevisionBefore,
                "2.3D runtime no modifica ingredient.authoring.");

            string fingerprintDay60 =
                BistroBuilderSupplierCommercialIntelligenceEngine.BuildFingerprint(commercialDay60);
            Check(marketService.TryInitializeFresh(marketSeed),
                "El mercado puede reiniciarse para repetir determinismo.");
            Check(commercialService.TryInitializeFresh(commercialSeed),
                "El motor comercial puede reiniciarse con la misma semilla.");
            for (int day = 2; day <= 60; day++)
            {
                marketService.TryAdvanceToGameDay(day, out error);
                commercialService.TrySynchronizeCurrentMarketState(out error);
            }
            BistroBuilderSupplierCommercialIntelligenceSnapshot repeated =
                commercialService.CreateSnapshot();
            Check(BistroBuilderSupplierCommercialIntelligenceEngine.BuildFingerprint(repeated) == fingerprintDay60,
                "Mismas semillas y días reproducen exactamente las mismas decisiones comerciales.");

            commercialService.CommercialReviewProcessed -= reviewHandler;
            commercialService.PromotionStarted -= startedHandler;
            commercialService.PromotionEnded -= endedHandler;
        }
        catch (Exception exception)
        {
            failed++;
            results.Add("[FALLO] Excepción de la prueba: " + exception);
        }
        finally
        {
            if (marketService != null && originalMarket != null)
            {
                string restoreMarketError;
                bool restoredMarket = marketService.TryRestoreSnapshot(originalMarket, out restoreMarketError);
                Check(restoredMarket, "La prueba restaura el snapshot original de mercado 2.3C.");
                if (!restoredMarket)
                {
                    results.Add("[FALLO] Restore mercado: " + restoreMarketError);
                }
            }

            if (commercialService != null && originalCommercial != null)
            {
                string restoreCommercialError;
                bool restoredCommercial = commercialService.TryRestoreSnapshot(
                    originalCommercial,
                    out restoreCommercialError);
                Check(restoredCommercial, "La prueba restaura el snapshot original del Motor Comercial 2.3D.");
                if (!restoredCommercial)
                {
                    results.Add("[FALLO] Restore comercial: " + restoreCommercialError);
                }
            }

            Application.logMessageReceived -= HandleLog;
            Check(runtimeErrors == 0,
                "La ejecución termina sin Error, Exception ni Assert capturados por 2.3D.");
            running = false;
            Debug.Log(
                "PRUEBA FUNCIONAL RUNTIME 2.3D — " + passed +
                " superadas / " + failed + " fallidas / " + runtimeErrors +
                " Error-Exception-Assert.");
            Repaint();
        }
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (!running) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            runtimeErrors++;
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
}
#endif
