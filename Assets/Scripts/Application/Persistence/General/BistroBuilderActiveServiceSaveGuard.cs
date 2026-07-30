using UnityEngine;

/// <summary>
/// Garantiza que guardar y cargar durante un servicio activo solo se permita
/// cuando está registrado el proveedor autoritativo service.runtime. En 368EF
/// ese proveedor forma parte obligatoria de la instalación y la regla deja de
/// forzar al jugador a terminar el servicio antes de salir.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/Active Service Save Guard"
)]
public sealed class BistroBuilderActiveServiceSaveGuard :
    MonoBehaviour,
    IBistroBuilderSaveOperationGuard
{
    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private RestaurantServiceStateService serviceStateService;

    [SerializeField]
    private string requiredRuntimeSectionId =
        BistroBuilderGeneralGameSaveSectionProvider
            .FutureActiveServiceSectionId;

    [SerializeField]
    private string requiredInventorySectionId =
        BistroBuilderInventorySaveSectionProvider.StableSectionId;

    [SerializeField]
    private int priority = 500;

    public int Priority => priority;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool CanSave(out string rejectionMessage)
    {
        CacheDependenciesIfNeeded();

        if (serviceStateService == null || saveGameService == null)
        {
            rejectionMessage =
                "No están disponibles los servicios generales de " +
                "persistencia.";
            return false;
        }

        if (!serviceStateService.IsServiceInProgress)
        {
            rejectionMessage = string.Empty;
            return true;
        }

        bool hasRuntime = saveGameService.HasProvider(
            requiredRuntimeSectionId
        );
        bool hasInventory = saveGameService.HasProvider(
            requiredInventorySectionId
        );

        if (hasRuntime && hasInventory)
        {
            rejectionMessage = string.Empty;
            return true;
        }

        rejectionMessage =
            "No puede guardarse este servicio activo porque faltan " +
            "secciones autoritativas de 368EF. service.runtime=" +
            hasRuntime + ", inventory.canonical=" + hasInventory +
            ". Reinstala 368EF antes de continuar para no perder clientes, " +
            "comandas, cocina, reservas ni tareas en curso.";
        return false;
    }

    public bool CanLoad(out string rejectionMessage)
    {
        CacheDependenciesIfNeeded();

        if (serviceStateService == null || saveGameService == null)
        {
            rejectionMessage =
                "No están disponibles los servicios generales de " +
                "persistencia.";
            return false;
        }

        bool hasRuntime = saveGameService.HasProvider(
            requiredRuntimeSectionId
        );
        bool hasInventory = saveGameService.HasProvider(
            requiredInventorySectionId
        );

        if (!serviceStateService.IsServiceInProgress ||
            hasRuntime && hasInventory)
        {
            rejectionMessage = string.Empty;
            return true;
        }

        rejectionMessage =
            "No puede cargarse de forma segura durante el servicio porque " +
            "faltan secciones autoritativas de 368EF. service.runtime=" +
            hasRuntime + ", inventory.canonical=" + hasInventory +
            ". Reinstala 368EF; cerrar el restaurante no debe ser un requisito.";
        return false;
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (saveGameService == null)
        {
            error = "Falta BistroBuilderSaveGameService.";
            return false;
        }

        if (serviceStateService == null)
        {
            error = "Falta RestaurantServiceStateService.";
            return false;
        }

        if (!string.Equals(
                requiredRuntimeSectionId,
                BistroBuilderGeneralGameSaveSectionProvider
                    .FutureActiveServiceSectionId,
                System.StringComparison.Ordinal
            ) ||
            !string.Equals(
                requiredInventorySectionId,
                BistroBuilderInventorySaveSectionProvider.StableSectionId,
                System.StringComparison.Ordinal
            ))
        {
            error = "La regla de servicio activo no exige exactamente " +
                    "service.runtime e inventory.canonical.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (saveGameService == null)
        {
            TryGetComponent(out saveGameService);
        }

        if (serviceStateService == null)
        {
            TryGetComponent(out serviceStateService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        requiredRuntimeSectionId = string.IsNullOrWhiteSpace(
            requiredRuntimeSectionId
        )
            ? BistroBuilderGeneralGameSaveSectionProvider
                .FutureActiveServiceSectionId
            : requiredRuntimeSectionId.Trim().ToLowerInvariant();

        requiredInventorySectionId = string.IsNullOrWhiteSpace(
            requiredInventorySectionId
        )
            ? BistroBuilderInventorySaveSectionProvider.StableSectionId
            : requiredInventorySectionId.Trim().ToLowerInvariant();

        CacheDependenciesIfNeeded();
    }
#endif
}
