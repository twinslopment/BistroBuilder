using UnityEngine;

/// <summary>
/// Regla que bloquea el modo edición mientras existe un servicio
/// operativo en curso.
///
/// También cierra de forma segura el modo edición si el servicio
/// comienza mientras el jugador estaba modificando el restaurante.
///
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Service Edit Mode Availability Rule"
)]
public sealed class RestaurantServiceEditModeAvailabilityRule :
    MonoBehaviour,
    IRestaurantEditModeAvailabilityRule
{
    [Header("Dependencias")]

    [Tooltip(
        "Servicio que mantiene la fase operativa del restaurante."
    )]
    [SerializeField]
    private RestaurantServiceStateService
        serviceStateService;

    [Tooltip(
        "Servicio global del modo edición."
    )]
    [SerializeField]
    private RestaurantEditModeService
        editModeService;

    [Header("Cambio de estado")]

    [Tooltip(
        "Cancela una colocación provisional si comienza el servicio."
    )]
    [SerializeField]
    private bool cancelActivePlacementWhenServiceStarts = true;

    [Header("Depuración")]

    [Tooltip(
        "Escribe en la Console el cierre automático del modo edición."
    )]
    [SerializeField]
    private bool logAutomaticExit = true;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        SubscribeToServiceState();
    }

    private void OnDisable()
    {
        UnsubscribeFromServiceState();
    }

    /// <summary>
    /// Permite entrar únicamente cuando el restaurante está Closed.
    /// </summary>
    public bool CanEnterEditMode(
        out string rejectionMessage
    )
    {
        if (serviceStateService == null)
        {
            rejectionMessage =
                "No se puede comprobar el estado del servicio.";

            return false;
        }

        if (!serviceStateService.BlocksEditMode)
        {
            rejectionMessage =
                string.Empty;

            return true;
        }

        rejectionMessage =
            BuildRejectionMessage(
                serviceStateService.CurrentState
            );

        return false;
    }

    /// <summary>
    /// Si una transición comienza a bloquear la edición, cierra
    /// inmediatamente el modo y restaura cualquier colocación
    /// provisional.
    /// </summary>
    private void HandleServiceStateChanged(
        RestaurantServiceState previousState,
        RestaurantServiceState currentState
    )
    {
        if (serviceStateService == null ||
            !serviceStateService.BlocksEditMode)
        {
            return;
        }

        if (editModeService == null ||
            !editModeService.IsEditModeActive)
        {
            return;
        }

        RestaurantEditModeFailureReason failureReason;

        bool exited =
            editModeService.TryExitEditMode(
                cancelActivePlacementWhenServiceStarts,
                out failureReason
            );

        if (!exited)
        {
            Debug.LogError(
                "No se pudo cerrar automáticamente el modo edición " +
                "al comenzar el servicio. Motivo: " +
                failureReason +
                ".",
                this
            );

            return;
        }

        if (logAutomaticExit)
        {
            Debug.Log(
                "Modo edición cerrado automáticamente porque el " +
                "servicio cambió de " +
                previousState +
                " a " +
                currentState +
                ".",
                this
            );
        }
    }

    private static string BuildRejectionMessage(
        RestaurantServiceState state
    )
    {
        switch (state)
        {
            case RestaurantServiceState.Preparing:

                return
                    "No se puede modificar el restaurante mientras " +
                    "se prepara el servicio.";

            case RestaurantServiceState.Open:

                return
                    "No se puede modificar el restaurante durante " +
                    "el servicio.";

            case RestaurantServiceState.Closing:

                return
                    "No se puede modificar el restaurante hasta " +
                    "completar el cierre del servicio.";

            default:

                return
                    "El modo edición no está disponible en el " +
                    "estado operativo actual.";
        }
    }

    private void SubscribeToServiceState()
    {
        if (serviceStateService == null)
        {
            return;
        }

        serviceStateService.StateChanged -=
            HandleServiceStateChanged;

        serviceStateService.StateChanged +=
            HandleServiceStateChanged;
    }

    private void UnsubscribeFromServiceState()
    {
        if (serviceStateService == null)
        {
            return;
        }

        serviceStateService.StateChanged -=
            HandleServiceStateChanged;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (serviceStateService == null)
        {
            TryGetComponent(
                out serviceStateService
            );
        }

        if (editModeService == null)
        {
            TryGetComponent(
                out editModeService
            );
        }
    }

    private void ValidateDependencies()
    {
        if (serviceStateService == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantServiceEditModeAvailabilityRule
                ) +
                " necesita un " +
                nameof(RestaurantServiceStateService) +
                ".",
                this
            );
        }

        if (editModeService == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantServiceEditModeAvailabilityRule
                ) +
                " necesita un " +
                nameof(RestaurantEditModeService) +
                ".",
                this
            );
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
