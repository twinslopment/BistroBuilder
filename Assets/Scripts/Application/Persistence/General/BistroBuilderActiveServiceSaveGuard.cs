using UnityEngine;

/// <summary>
/// Impide guardar un servicio activo hasta que exista el proveedor de
/// runtime completo. Cuando service.runtime se instale, esta misma regla
/// permitirá el guardado sin sustituir la plataforma 366B.
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

        if (saveGameService.HasProvider(requiredRuntimeSectionId))
        {
            rejectionMessage = string.Empty;
            return true;
        }

        rejectionMessage =
            "Todavía no puede guardarse con el restaurante abierto. " +
            "Falta la sección de runtime " + requiredRuntimeSectionId +
            ", que restaurará clientes, comandas, cocina y tareas en " +
            "curso.";
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

        if (!serviceStateService.IsServiceInProgress ||
            saveGameService.HasProvider(requiredRuntimeSectionId))
        {
            rejectionMessage = string.Empty;
            return true;
        }

        rejectionMessage =
            "No puede cargarse otra partida mientras el servicio está " +
            "activo hasta instalar " + requiredRuntimeSectionId +
            ". Cierra primero el restaurante.";
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

        if (string.IsNullOrWhiteSpace(requiredRuntimeSectionId))
        {
            error = "No se ha definido la sección de runtime requerida.";
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

        CacheDependenciesIfNeeded();
    }
#endif
}
