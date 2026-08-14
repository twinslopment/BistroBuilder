using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public sealed class BistroBuilderSupplierSmartPurchaseService : MonoBehaviour
{
    public const string SupplierAuthoringResourcePath =
        BistroBuilderSupplierCommercialIntelligenceService
            .SupplierAuthoringResourcePath;

    public const string IngredientAuthoringResourcePath =
        BistroBuilderSupplierCommercialIntelligenceService
            .IngredientAuthoringResourcePath;

    public const string SettingsResourcePath =
        "BistroBuilder/Suppliers/BistroBuilderSupplierSmartPurchaseSettings";

    private static BistroBuilderSupplierSmartPurchaseService instance;

    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private BistroBuilderIngredientAuthoringDatabase ingredientDatabase;
    private BistroBuilderSupplierSmartPurchaseSettings settings;
    private BistroBuilderSupplierCommercialIntelligenceService
        commercialService;
    private BistroBuilderSupplierPurchaseOrderService orderService;
    private BistroBuilderSupplierProgressionService progressionService;
    private long generationSequence;
    private string lastInitializationError;

    public static BistroBuilderSupplierSmartPurchaseService Instance =>
        instance;

    public bool IsInitialized =>
        supplierDatabase != null &&
        ingredientDatabase != null &&
        settings != null &&
        commercialService != null &&
        commercialService.IsInitialized &&
        string.IsNullOrEmpty(lastInitializationError);

    public string LastInitializationError => lastInitializationError;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeAuthority()
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSupplierSmartPurchaseService>() != null)
        {
            return;
        }

        GameObject host =
            new GameObject("BistroBuilderSupplierSmartPurchaseService");
        DontDestroyOnLoad(host);
        host.AddComponent<BistroBuilderSupplierSmartPurchaseService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        TryInitialize();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public bool TryInitialize()
    {
        lastInitializationError = null;

        supplierDatabase =
            Resources.Load<BistroBuilderSupplierAuthoringDatabase>(
                SupplierAuthoringResourcePath
            );

        ingredientDatabase =
            Resources.Load<BistroBuilderIngredientAuthoringDatabase>(
                IngredientAuthoringResourcePath
            );

        settings =
            Resources.Load<BistroBuilderSupplierSmartPurchaseSettings>(
                SettingsResourcePath
            );

        commercialService =
            BistroBuilderSupplierCommercialIntelligenceService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSupplierCommercialIntelligenceService>();

        orderService =
            BistroBuilderSupplierPurchaseOrderService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSupplierPurchaseOrderService>();

        progressionService =
            BistroBuilderSupplierProgressionService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSupplierProgressionService>();

        if (supplierDatabase == null)
        {
            lastInitializationError = "Falta supplier.authoring.";
        }
        else if (ingredientDatabase == null)
        {
            lastInitializationError = "Falta ingredient.authoring.";
        }
        else if (settings == null)
        {
            lastInitializationError =
                "Falta supplier.smart_purchase.settings. " +
                "Ejecuta el instalador 2.3F.";
        }
        else if (commercialService == null ||
                 !commercialService.IsInitialized)
        {
            lastInitializationError =
                "El Motor Comercial 2.3D no está disponible/inicializado.";
        }

        return string.IsNullOrEmpty(lastInitializationError);
    }

    public bool TryBuildRecommendations(
        out BistroBuilderSmartPurchaseReport report,
        out string error
    )
    {
        report = null;
        error = null;

        if (!IsInitialized && !TryInitialize())
        {
            error = lastInitializationError;
            return false;
        }

        int day =
            orderService != null && orderService.IsInitialized
                ? orderService.CurrentGameDay
                : (BistroBuilderSupplierMarketService.Instance != null
                    ? BistroBuilderSupplierMarketService.Instance
                        .CurrentGameDay
                    : 1);

        List<BistroBuilderSmartPurchaseIngredientFact> facts;
        List<string> diagnostics;

        if (!BistroBuilderSupplierSmartPurchaseRuntimeResolver
                .TryCaptureFacts(
                    ingredientDatabase,
                    orderService,
                    day,
                    out facts,
                    out diagnostics
                ))
        {
            error =
                "No se pudo resolver una fachada de lectura del " +
                "Inventario canónico. " +
                string.Join(" ", diagnostics.ToArray());
            return false;
        }

        // 2.3F6 / JKL-C1.4:
        // El resolver reflexivo se conserva como fallback de compatibilidad,
        // pero la autoridad vigente de stock mínimo, previsión y estado
        // agregado es 2.2C. Sus DTO usan CanonicalMilliUnits, mientras que
        // el dominio 2.3F trabaja en microunits. Esta superposición explícita
        // evita depender de coincidencias de nombres y convierte 1 milli =
        // 1000 micro de forma exacta.
        OverlayCanonicalPlanningFacts(facts, diagnostics);

        List<BistroBuilderSmartPurchaseOfferFact> offers =
            BuildOfferFacts();

        if (offers.Count == 0)
        {
            error =
                "No hay ofertas comerciales cotizables desde 2.3D.";
            return false;
        }

        generationSequence++;

        report = BistroBuilderSupplierSmartPurchaseEngine.BuildReport(
            day,
            generationSequence,
            facts,
            offers,
            settings,
            diagnostics
        );

        return true;
    }

    public bool TryCreateDraftFromPlan(
        BistroBuilderSmartPurchasePlan plan,
        out List<string> createdPurchaseOrderIds,
        out string error
    )
    {
        createdPurchaseOrderIds = new List<string>();
        error = null;

        if (plan == null)
        {
            error = "Plan nulo.";
            return false;
        }

        if (orderService == null || !orderService.IsInitialized)
        {
            error = "2.3E no está disponible.";
            return false;
        }

        Dictionary<
            string,
            List<BistroBuilderSmartPurchaseCandidate>
        > bySupplier =
            new Dictionary<
                string,
                List<BistroBuilderSmartPurchaseCandidate>
            >(StringComparer.Ordinal);

        for (int i = 0; i < plan.ingredients.Count; i++)
        {
            BistroBuilderSmartPurchaseCandidate candidate =
                plan.ingredients[i]?.selected;

            if (candidate == null || candidate.packageCount <= 0)
            {
                continue;
            }

            List<BistroBuilderSmartPurchaseCandidate> list;

            if (!bySupplier.TryGetValue(candidate.supplierId, out list))
            {
                list =
                    new List<BistroBuilderSmartPurchaseCandidate>();
                bySupplier.Add(candidate.supplierId, list);
            }

            list.Add(candidate);
        }

        foreach (
            KeyValuePair<
                string,
                List<BistroBuilderSmartPurchaseCandidate>
            > pair in bySupplier
        )
        {
            if (!IsSupplierUnlockedForPlayer(pair.Key))
            {
                error =
                    "2.3I bloquea el proveedor " + pair.Key +
                    ". El plan debe regenerarse con proveedores " +
                    "desbloqueados.";
                return false;
            }

            BistroBuilderPurchaseOrderRecord draft;

            if (!orderService.TryCreateDraft(
                    pair.Key,
                    out draft,
                    out error
                ))
            {
                return false;
            }

            for (int i = 0; i < pair.Value.Count; i++)
            {
                BistroBuilderPurchaseOrderRecord updated;

                if (!orderService.TrySetDraftLine(
                        draft.purchaseOrderId,
                        pair.Value[i].supplierOfferId,
                        pair.Value[i].packageCount,
                        out updated,
                        out error
                    ))
                {
                    return false;
                }
            }

            createdPurchaseOrderIds.Add(draft.purchaseOrderId);
        }

        return true;
    }

    private void OverlayCanonicalPlanningFacts(
        List<BistroBuilderSmartPurchaseIngredientFact> facts,
        List<string> diagnostics
    )
    {
        if (facts == null || facts.Count == 0)
        {
            return;
        }

        BistroBuilderInventoryPlanningService planning =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderInventoryPlanningService>();

        if (planning == null)
        {
            if (diagnostics != null)
            {
                diagnostics.Add(
                    "2.3F6: 2.2C no localizado; se conserva el " +
                    "fallback de lectura del resolver."
                );
            }

            return;
        }

        if (!planning.IsInitialized &&
            !planning.EnsureInitialized(out string planningError))
        {
            if (diagnostics != null)
            {
                diagnostics.Add(
                    "2.3F6: 2.2C localizado pero no inicializado: " +
                    planningError
                );
            }

            return;
        }

        int applied = 0;

        for (int index = 0; index < facts.Count; index++)
        {
            BistroBuilderSmartPurchaseIngredientFact fact =
                facts[index];

            if (fact == null ||
                string.IsNullOrWhiteSpace(fact.ingredientId))
            {
                continue;
            }

            if (!planning.TryGetPlanningSnapshot(
                    fact.ingredientId,
                    out BistroBuilderInventoryPlanningSnapshot snapshot
                ))
            {
                continue;
            }

            fact.stockMicrounits = MilliToMicro(
                snapshot.OnHandCanonicalMilliUnits
            );

            fact.reservedMicrounits = MilliToMicro(
                snapshot.ReservedCanonicalMilliUnits
            );

            fact.availableMicrounits = MilliToMicro(
                snapshot.AvailableCanonicalMilliUnits
            );

            fact.minimumStockMicrounits = MilliToMicro(
                snapshot.MinimumStockCanonicalMilliUnits
            );

            fact.forecastDailyConsumptionMicrounits =
                MilliToMicro(
                    snapshot.AverageDailyConsumptionCanonicalMilliUnits
                );

            fact.expiringSoonMicrounits = MilliToMicro(
                snapshot.NearExpiryAvailableCanonicalMilliUnits
            );

            fact.earliestExpiryGameDay =
                Math.Max(0, snapshot.NextExpirationDayIndex);

            // La existencia del snapshot canónico resuelve explícitamente
            // estas fachadas incluso cuando el valor legítimo sea cero.
            fact.inventoryResolved = true;
            fact.policyResolved = true;
            fact.forecastResolved = true;
            fact.expiryResolved = true;

            applied++;
        }

        if (diagnostics != null)
        {
            diagnostics.Add(
                "2.3F6: overlay canónico 2.2C aplicado a " +
                applied + "/" + facts.Count +
                " ingredientes; CanonicalMilliUnits -> microunits " +
                "x1000 exacto."
            );
        }
    }

    private static long MilliToMicro(long milliUnits)
    {
        if (milliUnits <= 0L)
        {
            return 0L;
        }

        if (milliUnits > long.MaxValue / 1000L)
        {
            return long.MaxValue;
        }

        return milliUnits * 1000L;
    }

    private static long MilliToMicro(double milliUnits)
    {
        if (double.IsNaN(milliUnits) ||
            double.IsInfinity(milliUnits) ||
            milliUnits <= 0d)
        {
            return 0L;
        }

        double micro = milliUnits * 1000d;

        if (micro >= long.MaxValue)
        {
            return long.MaxValue;
        }

        return (long)Math.Round(
            micro,
            MidpointRounding.AwayFromZero
        );
    }

    private List<BistroBuilderSmartPurchaseOfferFact>
        BuildOfferFacts()
    {
        List<BistroBuilderSmartPurchaseOfferFact> result =
            new List<BistroBuilderSmartPurchaseOfferFact>();

        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers =
            supplierDatabase.Suppliers;

        for (int supplierIndex = 0;
             supplierIndex < suppliers.Count;
             supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier =
                suppliers[supplierIndex];

            if (supplier == null ||
                !supplier.isActive ||
                supplier.baseOffers == null)
            {
                continue;
            }

            if (!IsSupplierUnlockedForPlayer(supplier.SupplierId))
            {
                continue;
            }

            for (int offerIndex = 0;
                 offerIndex < supplier.baseOffers.Count;
                 offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                    supplier.baseOffers[offerIndex];

                if (offer == null || !offer.isActive)
                {
                    continue;
                }

                BistroBuilderSupplierCommercialQuote quote;

                if (!commercialService.TryGetCommercialQuote(
                        offer.SupplierOfferId,
                        out quote
                    ) ||
                    quote == null)
                {
                    continue;
                }

                BistroBuilderIngredientAuthoringRecord ingredient;

                if (!ingredientDatabase.TryGetIngredient(
                        offer.ingredientId,
                        out ingredient
                    ) ||
                    ingredient == null)
                {
                    continue;
                }

                BistroBuilderCommercialPackageAuthoringRecord package =
                    null;

                if (ingredient.commercialPackages != null)
                {
                    for (int packageIndex = 0;
                         packageIndex <
                         ingredient.commercialPackages.Count;
                         packageIndex++)
                    {
                        BistroBuilderCommercialPackageAuthoringRecord
                            candidate =
                                ingredient.commercialPackages[
                                    packageIndex
                                ];

                        if (candidate != null &&
                            candidate.isActive &&
                            candidate.PackageFormatId ==
                            offer.packageFormatId)
                        {
                            package = candidate;
                            break;
                        }
                    }
                }

                if (package == null)
                {
                    continue;
                }

                result.Add(
                    new BistroBuilderSmartPurchaseOfferFact
                    {
                        supplierOfferId = offer.SupplierOfferId,
                        supplierId = supplier.SupplierId,
                        supplierDisplayName = supplier.displayName,
                        ingredientId = offer.ingredientId,
                        packageFormatId = offer.packageFormatId,
                        packageDisplayName = package.displayName,
                        packageNetQuantityMicrounits =
                            package.netQuantityMicrounits,
                        minimumPackageCount =
                            Math.Max(
                                1,
                                offer.minimumPackageCount
                            ),
                        orderIncrement =
                            Math.Max(1, offer.orderIncrement),
                        effectiveUnitPriceCents =
                            Math.Max(
                                1L,
                                quote.effectivePriceCents
                            ),
                        marketUnitPriceCents =
                            Math.Max(
                                1L,
                                quote.marketPriceCents
                            ),
                        hasPromotion =
                            quote.hasActivePromotion,
                        promotionId = quote.promotionId,
                        discountBasisPoints =
                            quote.discountBasisPoints,
                        availability = quote.availability,
                        availableForNewOrders =
                            quote.availableForNewOrders,
                        leadTimeGameHours =
                            offer.overrideLeadTime
                                ? Math.Max(
                                    0.1f,
                                    offer
                                        .leadTimeOverrideGameHours
                                )
                                : Math.Max(
                                    0.1f,
                                    supplier
                                        .defaultLeadTimeGameHours
                                ),
                        reliability01 =
                            Math.Max(
                                0f,
                                Math.Min(
                                    1f,
                                    supplier.reliabilityValue
                                )
                            ),
                        supplierMinimumOrderCents =
                            Math.Max(
                                0L,
                                supplier.minimumOrderValueCents
                            ),
                        shippingCostCents =
                            Math.Max(
                                0L,
                                supplier.shippingCostCents
                            ),
                        freeShippingEnabled =
                            supplier.freeShippingEnabled,
                        freeShippingThresholdCents =
                            Math.Max(
                                0L,
                                supplier
                                    .freeShippingThresholdCents
                            )
                    }
                );
            }
        }

        return result;
    }

    private bool IsSupplierUnlockedForPlayer(string supplierId)
    {
        if (progressionService == null)
        {
            progressionService =
                BistroBuilderSupplierProgressionService.Instance ??
                UnityEngine.Object.FindFirstObjectByType<
                    BistroBuilderSupplierProgressionService>();
        }

        // Compatibilidad defensiva: 2.3F conserva su comportamiento anterior
        // si 2.3I no está instalado/inicializado. Una vez 2.3I está activo,
        // sus desbloqueos gobiernan recomendaciones y creación de Draft.
        return progressionService == null ||
               !progressionService.IsInitialized ||
               progressionService.IsSupplierUnlocked(supplierId);
    }
}
