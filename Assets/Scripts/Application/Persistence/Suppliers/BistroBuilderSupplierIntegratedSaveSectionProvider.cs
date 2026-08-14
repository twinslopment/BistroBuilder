using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Sección atómica de persistencia para el cierre 2.3JKL.
///
/// No crea ninguna autoridad paralela: captura/restaura los snapshots públicos
/// de 2.3C, 2.3D, 2.3E, 2.3G, 2.3H y 2.3I. 2.3F es una vista derivada y se
/// reconstruye después de Load desde Inventario + mercado + pedidos.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Supplier Integrated Save Provider 2.3JKL")]
public sealed class BistroBuilderSupplierIntegratedSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "supplier.integrated.runtime";
    public const int StableSectionVersion = 1;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    private BistroBuilderSupplierMarketService marketService;
    private BistroBuilderSupplierCommercialIntelligenceService commercialService;
    private BistroBuilderSupplierPurchaseOrderService orderService;
    private BistroBuilderSupplierLogisticsService logisticsService;
    private BistroBuilderSupplierDeliveryPresentationService deliveryService;
    private BistroBuilderSupplierProgressionService progressionService;

    private bool sectionAppliedThisLoad;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 230;
    public bool IsRequired => false; // compatibilidad con saves anteriores a 2.3JKL
    public Type StateType => typeof(BistroBuilderSupplierIntegratedSaveState);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    // Se aplica después de inventario/estado general, pero el proveedor controla
    // internamente el orden estricto entre subdominios de Proveedores.
    public int PrepareOrder => 230;
    public int ApplyOrder => 230;
    public int FinalizeOrder => 9230;

    public BistroBuilderSaveGameService SaveGameService => saveGameService;

    private void Awake()
    {
        CacheSaveService();
        ResolveAuthorities();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheSaveService();
        if (saveGameService == null)
        {
            error = "Falta BistroBuilderSaveGameService en GameSystems.";
            return false;
        }
        return true;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        // Las autoridades AfterSceneLoad pueden necesitar uno o dos frames para
        // converger tras una carga/entrada en Play Mode. No serializamos una foto
        // parcialmente vinculada: damos una ventana pequeña y determinista.
        BistroBuilderSupplierIntegratedSaveState captured = null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            if (TryCaptureIntegratedState(out captured, out error))
            {
                context.Complete(captured);
                yield break;
            }
            yield return null;
        }

        context.Fail("No se pudo estabilizar supplier.integrated.runtime para guardar. " + error);
    }

    public bool ValidateState(object state, out string error)
    {
        error = string.Empty;
        BistroBuilderSupplierIntegratedSaveState typed =
            state as BistroBuilderSupplierIntegratedSaveState;
        if (typed == null)
        {
            error = "supplier.integrated.runtime no tiene el tipo esperado.";
            return false;
        }
        return typed.TryValidateBasic(out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        sectionAppliedThisLoad = false;
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        // La plataforma universal ya ha capturado rollback antes de Apply. Aquí
        // solo garantizamos que las autoridades receptoras existan y estén listas.
        for (int attempt = 0; attempt < 120; attempt++)
        {
            if (EnsureAuthoritiesReady(out error))
            {
                yield break;
            }
            yield return null;
        }

        context.Fail("Las autoridades de Proveedores no estuvieron listas para Load. " + error);
    }

    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        if (!ValidateState(state, out string error))
        {
            context.Fail(error);
            yield break;
        }

        BistroBuilderSupplierIntegratedSaveState snapshot =
            ((BistroBuilderSupplierIntegratedSaveState)state).DeepClone();

        if (!TryRestoreIntegratedState(snapshot, out error))
        {
            context.Fail(error);
            yield break;
        }

        sectionAppliedThisLoad = true;
        context.SharedData.Set("save.loaded_section." + StableSectionId, true);
        yield break;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed || sectionAppliedThisLoad)
        {
            return;
        }

        // Save anterior a 2.3J: nunca se permite arrastrar mercado/pedidos de la
        // partida que estaba abierta antes de cargar. Se crea un estado fresco y
        // coherente, igual que hacen otras secciones opcionales de la plataforma.
        if (!TryResetFreshIntegratedState(out string error))
        {
            context.Fail(
                "No se pudo inicializar Proveedores para una partida anterior a 2.3JKL. " +
                error
            );
        }
    }

    /// <summary>API diagnóstica segura usada por validadores/functional tests.</summary>
    public bool TryCaptureIntegratedState(
        out BistroBuilderSupplierIntegratedSaveState snapshot,
        out string error)
    {
        snapshot = null;
        error = string.Empty;

        if (!EnsureAuthoritiesReady(out error))
        {
            return false;
        }

        // 2.3D debe reflejar la última revisión disponible de 2.3C antes de
        // congelar el checkpoint integrado.
        if (!commercialService.TrySynchronizeCurrentMarketState(out string syncError))
        {
            error = "2.3D no pudo sincronizarse antes del guardado: " + syncError;
            return false;
        }

        if (!progressionService.RefreshNow())
        {
            error = "2.3I no pudo sincronizar progresión antes del guardado: " + progressionService.LastInitializationError;
            return false;
        }

        snapshot = new BistroBuilderSupplierIntegratedSaveState
        {
            market = marketService.CreateSnapshot(),
            commercial = commercialService.CreateSnapshot(),
            orders = orderService.CreateSnapshot(),
            logistics = logisticsService.CreateSnapshot(),
            deliveryPresentation = deliveryService.CreateSnapshot(),
            progression = progressionService.CreateSnapshot()
        };

        if (!snapshot.TryValidateBasic(out error))
        {
            snapshot = null;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Restauración transaccional a nivel de dominio. Si falla una subfase, se
    /// intenta volver al estado previo capturado antes de iniciar la operación.
    /// </summary>
    public bool TryRestoreIntegratedState(
        BistroBuilderSupplierIntegratedSaveState candidate,
        out string error)
    {
        error = string.Empty;
        if (candidate == null || !candidate.TryValidateBasic(out error))
        {
            return false;
        }
        if (!EnsureAuthoritiesReady(out error))
        {
            return false;
        }

        BistroBuilderSupplierIntegratedSaveState rollback;
        string captureError;
        if (!TryCaptureIntegratedState(out rollback, out captureError))
        {
            error = "No se pudo preparar rollback de supplier.integrated.runtime: " + captureError;
            return false;
        }

        if (ApplyInCanonicalOrder(candidate, out error))
        {
            return true;
        }

        string originalError = error;
        string rollbackError;
        if (!ApplyInCanonicalOrder(rollback, out rollbackError))
        {
            error = originalError + " | ROLLBACK FALLIDO: " + rollbackError;
            return false;
        }
        error = originalError;
        return false;
    }

    public bool TryResetFreshIntegratedState(out string error)
    {
        error = string.Empty;
        ResolveAuthorities();

        if (marketService == null || commercialService == null || orderService == null ||
            logisticsService == null || deliveryService == null || progressionService == null)
        {
            error = "Falta una autoridad runtime de Proveedores para resetear el estado.";
            return false;
        }

        if (!marketService.TryInitializeFresh(null))
        {
            error = marketService.LastInitializationError;
            return false;
        }
        if (!commercialService.TryInitializeFresh(null))
        {
            error = commercialService.LastInitializationError;
            return false;
        }
        if (!orderService.TryInitializeFresh())
        {
            error = orderService.LastInitializationError;
            return false;
        }
        if (!logisticsService.TryInitializeFresh())
        {
            error = logisticsService.LastInitializationError;
            return false;
        }
        if (!deliveryService.TryInitializeFresh())
        {
            error = deliveryService.LastInitializationError;
            return false;
        }
        if (!progressionService.TryInitializeFresh())
        {
            error = progressionService.LastInitializationError;
            return false;
        }
        return true;
    }

    private bool ApplyInCanonicalOrder(
        BistroBuilderSupplierIntegratedSaveState snapshot,
        out string error)
    {
        error = string.Empty;

        if (!marketService.TryRestoreSnapshot(snapshot.market.DeepClone(), out error))
        {
            error = "2.3C: " + error;
            return false;
        }
        if (!commercialService.TryRestoreSnapshot(snapshot.commercial.DeepClone(), out error))
        {
            error = "2.3D: " + error;
            return false;
        }
        if (!orderService.TryRestoreSnapshot(snapshot.orders.DeepClone(), out error))
        {
            error = "2.3E: " + error;
            return false;
        }
        if (!logisticsService.TryRestoreSnapshot(snapshot.logistics.DeepClone(), out error))
        {
            error = "2.3G: " + error;
            return false;
        }
        if (!deliveryService.TryRestoreSnapshot(
                snapshot.deliveryPresentation.DeepClone(),
                out error))
        {
            error = "2.3H: " + error;
            return false;
        }
        if (!progressionService.TryRestoreSnapshot(snapshot.progression.DeepClone(), out error))
        {
            error = "2.3I: " + error;
            return false;
        }

        // 2.3F no posee snapshot. Se reinicializa como proyección derivada del
        // estado que acabamos de restaurar.
        BistroBuilderSupplierSmartPurchaseService smart =
            BistroBuilderSupplierSmartPurchaseService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierSmartPurchaseService>();
        if (smart != null)
        {
            smart.TryInitialize();
        }

        return true;
    }

    private bool EnsureAuthoritiesReady(out string error)
    {
        error = string.Empty;
        ResolveAuthorities();

        if (marketService == null || commercialService == null || orderService == null ||
            logisticsService == null || deliveryService == null || progressionService == null)
        {
            error = BuildMissingAuthorityMessage();
            return false;
        }

        if (!marketService.IsInitialized && !marketService.TryInitializeFresh(null))
        {
            error = "2.3C: " + marketService.LastInitializationError;
            return false;
        }
        if (!commercialService.IsInitialized && !commercialService.TryInitializeFresh(null))
        {
            error = "2.3D: " + commercialService.LastInitializationError;
            return false;
        }
        if (!orderService.IsInitialized && !orderService.TryInitializeFresh())
        {
            error = "2.3E: " + orderService.LastInitializationError;
            return false;
        }
        if (!logisticsService.IsInitialized && !logisticsService.TryInitializeFresh())
        {
            error = "2.3G: " + logisticsService.LastInitializationError;
            return false;
        }
        if (!deliveryService.IsInitialized && !deliveryService.TryInitializeFresh())
        {
            error = "2.3H: " + deliveryService.LastInitializationError;
            return false;
        }
        if (!progressionService.IsInitialized && !progressionService.TryInitializeFresh())
        {
            error = "2.3I: " + progressionService.LastInitializationError;
            return false;
        }

        return true;
    }

    private void ResolveAuthorities()
    {
        marketService = BistroBuilderSupplierMarketService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierMarketService>();
        commercialService = BistroBuilderSupplierCommercialIntelligenceService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierCommercialIntelligenceService>();
        orderService = BistroBuilderSupplierPurchaseOrderService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        logisticsService = BistroBuilderSupplierLogisticsService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierLogisticsService>();
        deliveryService = BistroBuilderSupplierDeliveryPresentationService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierDeliveryPresentationService>();
        progressionService = BistroBuilderSupplierProgressionService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierProgressionService>();
    }

    private string BuildMissingAuthorityMessage()
    {
        return "Autoridades ausentes: " +
               (marketService == null ? "2.3C " : string.Empty) +
               (commercialService == null ? "2.3D " : string.Empty) +
               (orderService == null ? "2.3E " : string.Empty) +
               (logisticsService == null ? "2.3G " : string.Empty) +
               (deliveryService == null ? "2.3H " : string.Empty) +
               (progressionService == null ? "2.3I" : string.Empty);
    }

    private void CacheSaveService()
    {
        if (saveGameService == null)
        {
            TryGetComponent(out saveGameService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheSaveService();
    }

    private void OnValidate()
    {
        CacheSaveService();
    }
#endif
}
