using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada de lectura de 3G para resultados por servicio y por día.
///
/// No posee ledger ni persiste resúmenes: proyecta siempre desde las
/// autoridades canónicas de Finanzas 3A y Coste de Producto 3D.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Financial Results Service")]
public sealed class BistroBuilderFinancialResultsService : MonoBehaviour
{
    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderProductCostService productCostService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderMenuOfferService menuOfferService;
    [SerializeField] private RestaurantServiceStateService serviceStateService;

    private readonly List<BistroBuilderPurchaseOrderRecord> purchaseOrderBuffer =
        new List<BistroBuilderPurchaseOrderRecord>(64);

    private bool hasCapturedOpenService;
    private BistroBuilderMealServiceAvailability capturedOpenMealService =
        BistroBuilderMealServiceAvailability.None;

    public event Action ResultsChanged;
    public event Action<BistroBuilderServiceFinancialResult> ServiceResultClosed;

    public BistroBuilderFinanceService FinanceService => financeService;
    public BistroBuilderProductCostService ProductCostService => productCostService;
    public BistroBuilderGeneralGameStateService GeneralGameStateService =>
        generalGameStateService;
    public BistroBuilderMenuOfferService MenuOfferService => menuOfferService;
    public RestaurantServiceStateService ServiceStateService => serviceStateService;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();

        if (serviceStateService != null &&
            serviceStateService.CurrentState == RestaurantServiceState.Open)
        {
            CaptureCurrentOpenService();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        hasCapturedOpenService = false;
        capturedOpenMealService = BistroBuilderMealServiceAvailability.None;
        purchaseOrderBuffer.Clear();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (financeService == null ||
            productCostService == null ||
            generalGameStateService == null ||
            menuOfferService == null ||
            serviceStateService == null)
        {
            error = "3G necesita Finanzas 3A, Costes 3D, calendario, oferta y estado de servicio.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error) ||
            !productCostService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !menuOfferService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetServiceResult(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderServiceFinancialResult result,
        out string error)
    {
        result = null;

        if (!TryGetCanonicalSnapshots(
                out BistroBuilderFinanceSnapshot finance,
                out BistroBuilderProductCostSnapshot productCost,
                out error))
        {
            return false;
        }

        return BistroBuilderFinancialResultsEngine.TryBuildServiceResult(
            finance,
            productCost,
            dayIndex,
            mealService,
            out result,
            out error);
    }

    public bool TryGetDayResult(
        int dayIndex,
        out BistroBuilderDayFinancialResult result,
        out string error)
    {
        result = null;

        if (!TryGetCanonicalSnapshots(
                out BistroBuilderFinanceSnapshot finance,
                out BistroBuilderProductCostSnapshot productCost,
                out error))
        {
            return false;
        }

        CopyPurchaseOrdersIfAvailable();
        return BistroBuilderFinancialResultsEngine.TryBuildDayResult(
            finance,
            productCost,
            purchaseOrderBuffer,
            dayIndex,
            out result,
            out error);
    }

    /// <summary>
    /// Proyecta un intervalo completo capturando Finanzas/Costes una sola vez.
    /// 3H y 3J usan esta API para históricos y gráficos.
    /// </summary>
    public bool TryGetDayResults(
        int startDayIndex,
        int endDayIndex,
        List<BistroBuilderDayFinancialResult> destination,
        out string error)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();

        if (!TryGetCanonicalSnapshots(
                out BistroBuilderFinanceSnapshot finance,
                out BistroBuilderProductCostSnapshot productCost,
                out error))
        {
            return false;
        }

        CopyPurchaseOrdersIfAvailable();
        return BistroBuilderFinancialResultsEngine.TryBuildDayResultsRange(
            finance,
            productCost,
            purchaseOrderBuffer,
            startDayIndex,
            endDayIndex,
            destination,
            out error);
    }

    public bool TryGetCurrentServiceResult(
        out BistroBuilderServiceFinancialResult result,
        out string error)
    {
        result = null;

        if (generalGameStateService == null || menuOfferService == null)
        {
            error = "No existe contexto actual para el resultado de servicio.";
            return false;
        }

        return TryGetServiceResult(
            generalGameStateService.DayIndex,
            menuOfferService.CurrentMealService,
            out result,
            out error);
    }

    public bool TryGetCurrentDayResult(
        out BistroBuilderDayFinancialResult result,
        out string error)
    {
        result = null;

        if (generalGameStateService == null)
        {
            error = "No existe calendario actual para el resultado diario.";
            return false;
        }

        return TryGetDayResult(
            generalGameStateService.DayIndex,
            out result,
            out error);
    }

    private bool TryGetCanonicalSnapshots(
        out BistroBuilderFinanceSnapshot finance,
        out BistroBuilderProductCostSnapshot productCost,
        out string error)
    {
        finance = null;
        productCost = null;

        if (financeService == null || productCostService == null)
        {
            error = "Las autoridades financieras de 3G no están disponibles.";
            return false;
        }

        finance = financeService.CreateSnapshot();
        productCost = productCostService.CreateSnapshot();

        if (finance == null || productCost == null)
        {
            error = "Finanzas o Costes de Producto todavía no están inicializados.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void CopyPurchaseOrdersIfAvailable()
    {
        purchaseOrderBuffer.Clear();
        BistroBuilderSupplierPurchaseOrderService orderService =
            BistroBuilderSupplierPurchaseOrderService.Instance;
        if (orderService != null && orderService.IsInitialized)
        {
            orderService.CopyOrders(purchaseOrderBuffer);
        }
    }

    private void HandleFinanceChanged(BistroBuilderFinanceTransactionRecord _)
    {
        ResultsChanged?.Invoke();
    }

    private void HandleFinanceRestored()
    {
        ResultsChanged?.Invoke();
    }

    private void HandleLineCostRecorded(BistroBuilderConsumedLineCostRecord _)
    {
        ResultsChanged?.Invoke();
    }

    private void HandleCalendarChanged()
    {
        ResultsChanged?.Invoke();
    }

    private void HandleServiceOpened()
    {
        CaptureCurrentOpenService();
        ResultsChanged?.Invoke();
    }

    private void HandleServiceClosed()
    {
        BistroBuilderMealServiceAvailability mealService =
            hasCapturedOpenService
                ? capturedOpenMealService
                : menuOfferService != null
                    ? menuOfferService.CurrentMealService
                    : BistroBuilderMealServiceAvailability.None;

        hasCapturedOpenService = false;
        capturedOpenMealService = BistroBuilderMealServiceAvailability.None;

        if (generalGameStateService != null &&
            BistroBuilderFinancialResultsEngine.IsConcreteMealService(mealService) &&
            TryGetServiceResult(
                generalGameStateService.DayIndex,
                mealService,
                out BistroBuilderServiceFinancialResult closedResult,
                out _))
        {
            ServiceResultClosed?.Invoke(closedResult.DeepClone());
        }

        ResultsChanged?.Invoke();
    }

    private void CaptureCurrentOpenService()
    {
        if (menuOfferService == null ||
            !BistroBuilderFinancialResultsEngine.IsConcreteMealService(
                menuOfferService.CurrentMealService))
        {
            hasCapturedOpenService = false;
            capturedOpenMealService = BistroBuilderMealServiceAvailability.None;
            return;
        }

        capturedOpenMealService = menuOfferService.CurrentMealService;
        hasCapturedOpenService = true;
    }

    private void Subscribe()
    {
        if (financeService != null)
        {
            financeService.TransactionPosted -= HandleFinanceChanged;
            financeService.TransactionPosted += HandleFinanceChanged;
            financeService.StateRestored -= HandleFinanceRestored;
            financeService.StateRestored += HandleFinanceRestored;
        }

        if (productCostService != null)
        {
            productCostService.LineCostRecorded -= HandleLineCostRecorded;
            productCostService.LineCostRecorded += HandleLineCostRecorded;
        }

        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
            generalGameStateService.CalendarChanged += HandleCalendarChanged;
        }

        if (serviceStateService != null)
        {
            serviceStateService.ServiceOpened -= HandleServiceOpened;
            serviceStateService.ServiceOpened += HandleServiceOpened;
            serviceStateService.ServiceClosed -= HandleServiceClosed;
            serviceStateService.ServiceClosed += HandleServiceClosed;
        }
    }

    private void Unsubscribe()
    {
        if (financeService != null)
        {
            financeService.TransactionPosted -= HandleFinanceChanged;
            financeService.StateRestored -= HandleFinanceRestored;
        }

        if (productCostService != null)
        {
            productCostService.LineCostRecorded -= HandleLineCostRecorded;
        }

        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
        }

        if (serviceStateService != null)
        {
            serviceStateService.ServiceOpened -= HandleServiceOpened;
            serviceStateService.ServiceClosed -= HandleServiceClosed;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (financeService == null)
        {
            financeService = FindFirstObjectByType<BistroBuilderFinanceService>();
        }
        if (productCostService == null)
        {
            productCostService = FindFirstObjectByType<BistroBuilderProductCostService>();
        }
        if (generalGameStateService == null)
        {
            generalGameStateService =
                FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        }
        if (menuOfferService == null)
        {
            menuOfferService = FindFirstObjectByType<BistroBuilderMenuOfferService>();
        }
        if (serviceStateService == null)
        {
            serviceStateService = FindFirstObjectByType<RestaurantServiceStateService>();
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
