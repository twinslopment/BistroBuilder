using UnityEngine;

/// <summary>
/// Traduce caducidad/merma física del inventario a gasto financiero.
/// No modifica stock ni mantiene un segundo estado económico.
///
/// La valoración usa el coste de referencia vigente del ingrediente porque el
/// libro 2.2 agrega la expiración por ingrediente y no expone la asignación de
/// coste por lote eliminada. El importe queda congelado en finance.runtime y
/// se identifica como write-off estimado, evitando pérdidas invisibles.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Inventory Loss Finance Bridge")]
public sealed class BistroBuilderInventoryLossFinanceBridge : MonoBehaviour
{
    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderInventoryService inventoryService;
    [SerializeField] private BistroBuilderRecipeCatalogService recipeCatalogService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private BistroBuilderSaveGameService saveGameService;

    public BistroBuilderFinanceService FinanceService => financeService;
    public BistroBuilderInventoryService InventoryService => inventoryService;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();
        if (financeService == null || inventoryService == null ||
            recipeCatalogService == null || generalGameStateService == null ||
            gameClock == null || saveGameService == null)
        {
            error = "La baja económica de inventario necesita Finanzas, Inventario, catálogo, calendario, reloj y SaveGame.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error) ||
            !inventoryService.ValidateConfiguration(out error) ||
            !recipeCatalogService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleInventoryTransaction(
        BistroBuilderInventoryTransactionSnapshot transaction)
    {
        if (!BistroBuilderInventoryLossFinancePolicy.IsRecognizableLoss(transaction) ||
            (saveGameService != null && saveGameService.IsBusy))
        {
            return;
        }

        if (!TryRecognizeLoss(transaction, out string error))
        {
            Debug.LogError(
                "No se pudo reconocer la baja económica de inventario. " + error,
                this);
        }
    }

    public bool TryRecognizeLoss(
        BistroBuilderInventoryTransactionSnapshot transaction,
        out string error)
    {
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!BistroBuilderInventoryLossFinancePolicy.IsRecognizableLoss(transaction))
        {
            error = "El movimiento de inventario no es caducidad ni merma.";
            return false;
        }

        if (!recipeCatalogService.TryGetIngredient(
                transaction.IngredientId,
                out BistroBuilderIngredientDefinition ingredient) ||
            ingredient == null ||
            !ingredient.TryCalculateCostMicroCents(
                transaction.QuantityCanonicalMilliUnits,
                out long estimatedMicroCents,
                out error))
        {
            return false;
        }

        long estimatedCents =
            BistroBuilderProductCostEngine.RoundMicroCentsToCents(
                estimatedMicroCents);

        if (!BistroBuilderInventoryLossFinancePolicy.TryBuildRequest(
                transaction,
                estimatedCents,
                Mathf.Max(1, generalGameStateService.DayIndex),
                Mathf.Clamp(gameClock.Hour, 0, 23) * 60 +
                Mathf.Clamp(gameClock.Minute, 0, 59),
                out BistroBuilderFinanceTransactionRequest request,
                out error))
        {
            return false;
        }

        if (request == null)
        {
            error = string.Empty;
            return true;
        }

        return financeService.TryPostTransaction(request, out _, out error);
    }

    private void Subscribe()
    {
        if (inventoryService == null)
        {
            return;
        }
        inventoryService.TransactionRecorded -= HandleInventoryTransaction;
        inventoryService.TransactionRecorded += HandleInventoryTransaction;
    }

    private void Unsubscribe()
    {
        if (inventoryService != null)
        {
            inventoryService.TransactionRecorded -= HandleInventoryTransaction;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (financeService == null)
        {
            financeService = FindFirstObjectByType<BistroBuilderFinanceService>();
        }
        if (inventoryService == null)
        {
            inventoryService = FindFirstObjectByType<BistroBuilderInventoryService>();
        }
        if (recipeCatalogService == null)
        {
            recipeCatalogService = FindFirstObjectByType<BistroBuilderRecipeCatalogService>();
        }
        if (generalGameStateService == null)
        {
            generalGameStateService = FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        }
        if (gameClock == null)
        {
            gameClock = FindFirstObjectByType<GameClock>();
        }
        if (saveGameService == null)
        {
            saveGameService = FindFirstObjectByType<BistroBuilderSaveGameService>();
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
