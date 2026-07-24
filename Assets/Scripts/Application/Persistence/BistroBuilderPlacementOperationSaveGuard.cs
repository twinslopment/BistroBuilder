using UnityEngine;

/// <summary>
/// Impide guardar o cargar en mitad de una transacción de colocación.
///
/// Evita persistir previsualizaciones provisionales o cargar mientras el
/// historial mantiene una operación incompleta.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/Placement Operation Save Guard"
)]
public sealed class BistroBuilderPlacementOperationSaveGuard :
    MonoBehaviour,
    IBistroBuilderSaveOperationGuard
{
    [SerializeField]
    private RestaurantPlacementTransactionService transactionService;

    [SerializeField]
    private RestaurantPlaceableCreationService creationService;

    [SerializeField]
    private int priority = 100;

    public int Priority => priority;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool CanSave(out string rejectionMessage)
    {
        return Evaluate(out rejectionMessage);
    }

    public bool CanLoad(out string rejectionMessage)
    {
        return Evaluate(out rejectionMessage);
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (transactionService == null)
        {
            error = "Falta RestaurantPlacementTransactionService.";
            return false;
        }

        if (creationService == null)
        {
            error = "Falta RestaurantPlaceableCreationService.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool Evaluate(out string rejectionMessage)
    {
        CacheDependenciesIfNeeded();

        if (transactionService == null || creationService == null)
        {
            rejectionMessage =
                "El control de operaciones de colocación no está disponible.";
            return false;
        }

        if (transactionService.HasActiveTransaction ||
            creationService.HasActiveCreation)
        {
            rejectionMessage =
                "Confirma o cancela la colocación actual antes de " +
                "guardar o cargar.";
            return false;
        }

        rejectionMessage = string.Empty;
        return true;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (transactionService == null)
        {
            TryGetComponent(out transactionService);
        }

        if (creationService == null)
        {
            TryGetComponent(out creationService);
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
