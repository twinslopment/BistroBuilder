using UnityEngine;

/// <summary>
/// Comando permanente de creación de un artículo colocable.
///
/// El objeto ya está creado y activo cuando el comando se registra.
/// Deshacer lo retira sin destruirlo; rehacer restaura exactamente su
/// identidad, área, jerarquía y pose.
///
/// Si el comando se descarta mientras la instancia permanece retirada,
/// libera definitivamente el GameObject.
/// </summary>
public sealed class RestaurantCreatePlaceableHistoryCommand :
    IRestaurantEditHistoryCommand
{
    private readonly RestaurantPlaceableLifecycleService
        lifecycleService;

    private readonly RestaurantPlaceableObject placeable;

    private readonly RestaurantPlacementStateSnapshot
        createdState;

    public RestaurantEditHistoryCommandType CommandType
    {
        get
        {
            return RestaurantEditHistoryCommandType.Create;
        }
    }

    public string Description
    {
        get
        {
            return
                "Crear " +
                ResolveDisplayName();
        }
    }

    public Object PrimaryTarget
    {
        get
        {
            return placeable;
        }
    }

    public bool IsValid
    {
        get
        {
            return lifecycleService != null &&
                   placeable != null &&
                   createdState.IsValid;
        }
    }

    public RestaurantCreatePlaceableHistoryCommand(
        RestaurantPlaceableLifecycleService lifecycleService,
        RestaurantPlaceableObject placeable,
        RestaurantPlacementStateSnapshot createdState
    )
    {
        this.lifecycleService =
            lifecycleService;

        this.placeable =
            placeable;

        this.createdState =
            createdState;
    }

    public bool TryUndo(
        out RestaurantEditHistoryCommandResult result
    )
    {
        if (!ValidateCommand(
                out result
            ))
        {
            return false;
        }

        bool deactivated =
            lifecycleService.TryDeactivateInstance(
                placeable,
                out _,
                out RestaurantPlaceableLifecycleResult
                    lifecycleResult
            );

        result =
            ConvertLifecycleResult(
                deactivated,
                lifecycleResult,
                "Creación deshecha."
            );

        return deactivated;
    }

    public bool TryRedo(
        out RestaurantEditHistoryCommandResult result
    )
    {
        if (!ValidateCommand(
                out result
            ))
        {
            return false;
        }

        bool activated =
            lifecycleService.TryActivateInstance(
                placeable,
                createdState,
                out RestaurantPlaceableLifecycleResult
                    lifecycleResult
            );

        result =
            ConvertLifecycleResult(
                activated,
                lifecycleResult,
                "Creación rehecha."
            );

        return activated;
    }

    public void ReleaseResources()
    {
        if (lifecycleService == null ||
            placeable == null ||
            lifecycleService.IsRegistered(
                placeable
            ))
        {
            return;
        }

        lifecycleService.TryPermanentlyDestroyInstance(
            placeable,
            out _
        );
    }

    private bool ValidateCommand(
        out RestaurantEditHistoryCommandResult result
    )
    {
        if (lifecycleService == null)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .LifecycleSystemUnavailable,
                    placeable,
                    ResolveMember(),
                    default,
                    "El servicio de ciclo de vida no está disponible."
                );

            return false;
        }

        if (placeable == null)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .TargetUnavailable,
                    null,
                    null,
                    default,
                    "La instancia creada ya no está disponible."
                );

            return false;
        }

        if (!createdState.IsValid)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .StateInvalid,
                    placeable,
                    ResolveMember(),
                    default,
                    "El estado de creación no es válido."
                );

            return false;
        }

        result =
            default;

        return true;
    }

    private RestaurantAreaMember ResolveMember()
    {
        if (placeable == null)
        {
            return null;
        }

        placeable.TryGetComponent(
            out RestaurantAreaMember member
        );

        return member;
    }

    private string ResolveDisplayName()
    {
        return placeable != null
            ? placeable.DisplayName
            : "artículo no disponible";
    }

    private RestaurantEditHistoryCommandResult
        ConvertLifecycleResult(
            bool succeeded,
            RestaurantPlaceableLifecycleResult lifecycleResult,
            string successMessage
        )
    {
        RestaurantAreaMember member =
            ResolveMember();

        if (succeeded)
        {
            return
                RestaurantEditHistoryCommandResult.Success(
                    placeable,
                    member,
                    successMessage
                );
        }

        return
            RestaurantEditHistoryCommandResult.Failure(
                MapFailureReason(
                    lifecycleResult.FailureReason
                ),
                placeable,
                member,
                default,
                lifecycleResult.Message
            );
    }

    private static RestaurantEditHistoryCommandFailureReason
        MapFailureReason(
            RestaurantPlaceableLifecycleFailureReason reason
        )
    {
        switch (reason)
        {
            case RestaurantPlaceableLifecycleFailureReason
                .IdentityConflict:

                return
                    RestaurantEditHistoryCommandFailureReason
                        .IdentityConflict;

            case RestaurantPlaceableLifecycleFailureReason
                .RegistryUnavailable:

                return
                    RestaurantEditHistoryCommandFailureReason
                        .LifecycleSystemUnavailable;

            case RestaurantPlaceableLifecycleFailureReason
                .MemberRegistrationFailed:

            case RestaurantPlaceableLifecycleFailureReason
                .PlaceableRegistrationFailed:

                return
                    RestaurantEditHistoryCommandFailureReason
                        .RegistrationFailed;

            case RestaurantPlaceableLifecycleFailureReason
                .StateRestoreFailed:

                return
                    RestaurantEditHistoryCommandFailureReason
                        .StateInvalid;

            case RestaurantPlaceableLifecycleFailureReason
                .InstanceUnavailable:

                return
                    RestaurantEditHistoryCommandFailureReason
                        .TargetUnavailable;

            default:

                return
                    RestaurantEditHistoryCommandFailureReason
                        .ApplyFailed;
        }
    }
}

/// <summary>
/// Comando permanente de eliminación de un artículo colocable.
///
/// El objeto ya está retirado cuando el comando se registra.
/// Deshacer lo reactiva; rehacer vuelve a retirarlo.
/// </summary>
public sealed class RestaurantDeletePlaceableHistoryCommand :
    IRestaurantEditHistoryCommand
{
    private readonly RestaurantPlaceableLifecycleService
        lifecycleService;

    private readonly RestaurantPlaceableObject placeable;

    private readonly RestaurantPlacementStateSnapshot
        deletedState;

    public RestaurantEditHistoryCommandType CommandType
    {
        get
        {
            return RestaurantEditHistoryCommandType.Delete;
        }
    }

    public string Description
    {
        get
        {
            return
                "Eliminar " +
                ResolveDisplayName();
        }
    }

    public Object PrimaryTarget
    {
        get
        {
            return placeable;
        }
    }

    public bool IsValid
    {
        get
        {
            return lifecycleService != null &&
                   placeable != null &&
                   deletedState.IsValid;
        }
    }

    public RestaurantDeletePlaceableHistoryCommand(
        RestaurantPlaceableLifecycleService lifecycleService,
        RestaurantPlaceableObject placeable,
        RestaurantPlacementStateSnapshot deletedState
    )
    {
        this.lifecycleService =
            lifecycleService;

        this.placeable =
            placeable;

        this.deletedState =
            deletedState;
    }

    public bool TryUndo(
        out RestaurantEditHistoryCommandResult result
    )
    {
        if (!ValidateCommand(
                out result
            ))
        {
            return false;
        }

        bool activated =
            lifecycleService.TryActivateInstance(
                placeable,
                deletedState,
                out RestaurantPlaceableLifecycleResult
                    lifecycleResult
            );

        result =
            ConvertLifecycleResult(
                activated,
                lifecycleResult,
                "Eliminación deshecha."
            );

        return activated;
    }

    public bool TryRedo(
        out RestaurantEditHistoryCommandResult result
    )
    {
        if (!ValidateCommand(
                out result
            ))
        {
            return false;
        }

        bool deactivated =
            lifecycleService.TryDeactivateInstance(
                placeable,
                out _,
                out RestaurantPlaceableLifecycleResult
                    lifecycleResult
            );

        result =
            ConvertLifecycleResult(
                deactivated,
                lifecycleResult,
                "Eliminación rehecha."
            );

        return deactivated;
    }

    public void ReleaseResources()
    {
        if (lifecycleService == null ||
            placeable == null ||
            lifecycleService.IsRegistered(
                placeable
            ))
        {
            return;
        }

        lifecycleService.TryPermanentlyDestroyInstance(
            placeable,
            out _
        );
    }

    private bool ValidateCommand(
        out RestaurantEditHistoryCommandResult result
    )
    {
        if (lifecycleService == null)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .LifecycleSystemUnavailable,
                    placeable,
                    ResolveMember(),
                    default,
                    "El servicio de ciclo de vida no está disponible."
                );

            return false;
        }

        if (placeable == null)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .TargetUnavailable,
                    null,
                    null,
                    default,
                    "La instancia eliminada ya no está disponible."
                );

            return false;
        }

        if (!deletedState.IsValid)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .StateInvalid,
                    placeable,
                    ResolveMember(),
                    default,
                    "El estado anterior a la eliminación no es válido."
                );

            return false;
        }

        result =
            default;

        return true;
    }

    private RestaurantAreaMember ResolveMember()
    {
        if (placeable == null)
        {
            return null;
        }

        placeable.TryGetComponent(
            out RestaurantAreaMember member
        );

        return member;
    }

    private string ResolveDisplayName()
    {
        return placeable != null
            ? placeable.DisplayName
            : "artículo no disponible";
    }

    private RestaurantEditHistoryCommandResult
        ConvertLifecycleResult(
            bool succeeded,
            RestaurantPlaceableLifecycleResult lifecycleResult,
            string successMessage
        )
    {
        RestaurantAreaMember member =
            ResolveMember();

        if (succeeded)
        {
            return
                RestaurantEditHistoryCommandResult.Success(
                    placeable,
                    member,
                    successMessage
                );
        }

        return
            RestaurantEditHistoryCommandResult.Failure(
                RestaurantEditHistoryCommandFailureReason
                    .ApplyFailed,
                placeable,
                member,
                default,
                lifecycleResult.Message
            );
    }
}
