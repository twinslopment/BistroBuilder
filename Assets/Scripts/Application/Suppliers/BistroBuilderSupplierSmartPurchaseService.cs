using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public sealed class BistroBuilderSupplierSmartPurchaseService : MonoBehaviour
{
    public const string SupplierAuthoringResourcePath = BistroBuilderSupplierCommercialIntelligenceService.SupplierAuthoringResourcePath;
    public const string IngredientAuthoringResourcePath = BistroBuilderSupplierCommercialIntelligenceService.IngredientAuthoringResourcePath;
    public const string SettingsResourcePath = "BistroBuilder/Suppliers/BistroBuilderSupplierSmartPurchaseSettings";

    private static BistroBuilderSupplierSmartPurchaseService instance;
    private BistroBuilderSupplierAuthoringDatabase supplierDatabase;
    private BistroBuilderIngredientAuthoringDatabase ingredientDatabase;
    private BistroBuilderSupplierSmartPurchaseSettings settings;
    private BistroBuilderSupplierCommercialIntelligenceService commercialService;
    private BistroBuilderSupplierPurchaseOrderService orderService;
    private BistroBuilderSupplierProgressionService progressionService;
    private long generationSequence;
    private string lastInitializationError;

    public static BistroBuilderSupplierSmartPurchaseService Instance => instance;
    public bool IsInitialized => supplierDatabase != null && ingredientDatabase != null && settings != null && commercialService != null && commercialService.IsInitialized && string.IsNullOrEmpty(lastInitializationError);
    public string LastInitializationError => lastInitializationError;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeAuthority()
    {
        if (UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierSmartPurchaseService>() != null) return;
        GameObject host = new GameObject("BistroBuilderSupplierSmartPurchaseService");
        DontDestroyOnLoad(host);
        host.AddComponent<BistroBuilderSupplierSmartPurchaseService>();
    }

    private void Awake()
    {
        if(instance!=null && instance!=this){ Destroy(gameObject); return; }
        instance=this; DontDestroyOnLoad(gameObject); TryInitialize();
    }
    private void OnDestroy(){ if(instance==this) instance=null; }

    public bool TryInitialize()
    {
        lastInitializationError=null;
        supplierDatabase=Resources.Load<BistroBuilderSupplierAuthoringDatabase>(SupplierAuthoringResourcePath);
        ingredientDatabase=Resources.Load<BistroBuilderIngredientAuthoringDatabase>(IngredientAuthoringResourcePath);
        settings=Resources.Load<BistroBuilderSupplierSmartPurchaseSettings>(SettingsResourcePath);
        commercialService=BistroBuilderSupplierCommercialIntelligenceService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierCommercialIntelligenceService>();
        orderService=BistroBuilderSupplierPurchaseOrderService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        progressionService=BistroBuilderSupplierProgressionService.Instance ?? UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierProgressionService>();
        if(supplierDatabase==null) lastInitializationError="Falta supplier.authoring.";
        else if(ingredientDatabase==null) lastInitializationError="Falta ingredient.authoring.";
        else if(settings==null) lastInitializationError="Falta supplier.smart_purchase.settings. Ejecuta el instalador 2.3F.";
        else if(commercialService==null || !commercialService.IsInitialized) lastInitializationError="El Motor Comercial 2.3D no está disponible/inicializado.";
        return string.IsNullOrEmpty(lastInitializationError);
    }

    public bool TryBuildRecommendations(out BistroBuilderSmartPurchaseReport report, out string error)
    {
        report=null; error=null;
        if(!IsInitialized && !TryInitialize()){ error=lastInitializationError; return false; }
        int day=orderService!=null && orderService.IsInitialized ? orderService.CurrentGameDay : (BistroBuilderSupplierMarketService.Instance!=null ? BistroBuilderSupplierMarketService.Instance.CurrentGameDay : 1);
        List<BistroBuilderSmartPurchaseIngredientFact> facts;
        List<string> diagnostics;
        if(!BistroBuilderSupplierSmartPurchaseRuntimeResolver.TryCaptureFacts(ingredientDatabase,orderService,day,out facts,out diagnostics))
        {
            error="No se pudo resolver una fachada de lectura del Inventario canónico. " + string.Join(" ",diagnostics.ToArray());
            return false;
        }
        List<BistroBuilderSmartPurchaseOfferFact> offers=BuildOfferFacts();
        if(offers.Count==0){ error="No hay ofertas comerciales cotizables desde 2.3D."; return false; }
        generationSequence++;
        report=BistroBuilderSupplierSmartPurchaseEngine.BuildReport(day,generationSequence,facts,offers,settings,diagnostics);
        return true;
    }

    public bool TryCreateDraftFromPlan(BistroBuilderSmartPurchasePlan plan, out List<string> createdPurchaseOrderIds, out string error)
    {
        createdPurchaseOrderIds=new List<string>(); error=null;
        if(plan==null){ error="Plan nulo."; return false; }
        if(orderService==null || !orderService.IsInitialized){ error="2.3E no está disponible."; return false; }

        Dictionary<string,List<BistroBuilderSmartPurchaseCandidate>> bySupplier=new Dictionary<string,List<BistroBuilderSmartPurchaseCandidate>>(StringComparer.Ordinal);
        for(int i=0;i<plan.ingredients.Count;i++)
        {
            BistroBuilderSmartPurchaseCandidate c=plan.ingredients[i]?.selected;
            if(c==null || c.packageCount<=0) continue;
            List<BistroBuilderSmartPurchaseCandidate> list;
            if(!bySupplier.TryGetValue(c.supplierId,out list)){ list=new List<BistroBuilderSmartPurchaseCandidate>(); bySupplier.Add(c.supplierId,list); }
            list.Add(c);
        }

        foreach(KeyValuePair<string,List<BistroBuilderSmartPurchaseCandidate>> pair in bySupplier)
        {
            if(!IsSupplierUnlockedForPlayer(pair.Key))
            {
                error="2.3I bloquea el proveedor " + pair.Key + ". El plan debe regenerarse con proveedores desbloqueados.";
                return false;
            }
            BistroBuilderPurchaseOrderRecord draft;
            if(!orderService.TryCreateDraft(pair.Key,out draft,out error)) return false;
            for(int i=0;i<pair.Value.Count;i++)
            {
                BistroBuilderPurchaseOrderRecord updated;
                if(!orderService.TrySetDraftLine(draft.purchaseOrderId,pair.Value[i].supplierOfferId,pair.Value[i].packageCount,out updated,out error)) return false;
            }
            createdPurchaseOrderIds.Add(draft.purchaseOrderId);
        }
        return true;
    }

    private List<BistroBuilderSmartPurchaseOfferFact> BuildOfferFacts()
    {
        List<BistroBuilderSmartPurchaseOfferFact> result=new List<BistroBuilderSmartPurchaseOfferFact>();
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers=supplierDatabase.Suppliers;
        for(int s=0;s<suppliers.Count;s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier=suppliers[s];
            if(supplier==null || !supplier.isActive || supplier.baseOffers==null) continue;
            if(!IsSupplierUnlockedForPlayer(supplier.SupplierId)) continue;
            for(int o=0;o<supplier.baseOffers.Count;o++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer=supplier.baseOffers[o];
                if(offer==null || !offer.isActive) continue;
                BistroBuilderSupplierCommercialQuote quote;
                if(!commercialService.TryGetCommercialQuote(offer.SupplierOfferId,out quote) || quote==null) continue;
                BistroBuilderIngredientAuthoringRecord ingredient;
                if(!ingredientDatabase.TryGetIngredient(offer.ingredientId,out ingredient) || ingredient==null) continue;
                BistroBuilderCommercialPackageAuthoringRecord package=null;
                if(ingredient.commercialPackages!=null)
                {
                    for(int p=0;p<ingredient.commercialPackages.Count;p++)
                    {
                        BistroBuilderCommercialPackageAuthoringRecord candidate=ingredient.commercialPackages[p];
                        if(candidate!=null && candidate.isActive && candidate.PackageFormatId==offer.packageFormatId){ package=candidate; break; }
                    }
                }
                if(package==null) continue;
                result.Add(new BistroBuilderSmartPurchaseOfferFact
                {
                    supplierOfferId=offer.SupplierOfferId,
                    supplierId=supplier.SupplierId,
                    supplierDisplayName=supplier.displayName,
                    ingredientId=offer.ingredientId,
                    packageFormatId=offer.packageFormatId,
                    packageDisplayName=package.displayName,
                    packageNetQuantityMicrounits=package.netQuantityMicrounits,
                    minimumPackageCount=Math.Max(1,offer.minimumPackageCount),
                    orderIncrement=Math.Max(1,offer.orderIncrement),
                    effectiveUnitPriceCents=Math.Max(1L,quote.effectivePriceCents),
                    marketUnitPriceCents=Math.Max(1L,quote.marketPriceCents),
                    hasPromotion=quote.hasActivePromotion,
                    promotionId=quote.promotionId,
                    discountBasisPoints=quote.discountBasisPoints,
                    availability=quote.availability,
                    availableForNewOrders=quote.availableForNewOrders,
                    leadTimeGameHours=offer.overrideLeadTime ? Math.Max(0.1f,offer.leadTimeOverrideGameHours) : Math.Max(0.1f,supplier.defaultLeadTimeGameHours),
                    reliability01=Math.Max(0f,Math.Min(1f,supplier.reliabilityValue)),
                    supplierMinimumOrderCents=Math.Max(0L,supplier.minimumOrderValueCents),
                    shippingCostCents=Math.Max(0L,supplier.shippingCostCents),
                    freeShippingEnabled=supplier.freeShippingEnabled,
                    freeShippingThresholdCents=Math.Max(0L,supplier.freeShippingThresholdCents)
                });
            }
        }
        return result;
    }
    private bool IsSupplierUnlockedForPlayer(string supplierId)
    {
        if(progressionService==null)
        {
            progressionService=BistroBuilderSupplierProgressionService.Instance ??
                UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierProgressionService>();
        }

        // Compatibilidad defensiva: 2.3F conserva su comportamiento anterior si 2.3I
        // no está instalado/inicializado. Una vez 2.3I está activo, sus desbloqueos
        // gobiernan recomendaciones y creación de Draft desde planes inteligentes.
        return progressionService==null || !progressionService.IsInitialized ||
               progressionService.IsSupplierUnlocked(supplierId);
    }

}
