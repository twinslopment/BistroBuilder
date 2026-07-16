using UnityEngine;

/// <summary>
/// Contrato común de cualquier operación confirmada del modo edición.
///
/// Las implementaciones permanentes pueden representar:
/// - Movimiento y rotación.
/// - Creación.
/// - Eliminación.
/// - Sustitución.
/// - Cambios de configuración.
///
/// El historial no necesita conocer el tipo concreto de operación.
/// </summary>
public interface IRestaurantEditHistoryCommand
{
    RestaurantEditHistoryCommandType CommandType
    {
        get;
    }

    string Description
    {
        get;
    }

    Object PrimaryTarget
    {
        get;
    }

    bool IsValid
    {
        get;
    }

    bool TryUndo(
        out RestaurantEditHistoryCommandResult result
    );

    bool TryRedo(
        out RestaurantEditHistoryCommandResult result
    );

    /// <summary>
    /// Libera recursos retenidos exclusivamente por el historial.
    ///
    /// Es imprescindible para comandos de creación y eliminación:
    /// una instancia retirada puede permanecer inactiva mientras
    /// exista la posibilidad de rehacerla. Cuando el comando sale
    /// definitivamente del historial, esa instancia debe destruirse.
    /// </summary>
    void ReleaseResources();
}

/// <summary>
/// Resultado genérico de ejecutar un comando del editor.
/// </summary>
public readonly struct RestaurantEditHistoryCommandResult
{
    public bool Succeeded
    {
        get;
    }

    public RestaurantEditHistoryCommandFailureReason
        FailureReason
    {
        get;
    }

    public Object AffectedObject
    {
        get;
    }

    public RestaurantAreaMember AffectedMember
    {
        get;
    }

    public RestaurantPlacementValidationResult
        ValidationResult
    {
        get;
    }

    public string Message
    {
        get;
    }

    private RestaurantEditHistoryCommandResult(
        bool succeeded,
        RestaurantEditHistoryCommandFailureReason failureReason,
        Object affectedObject,
        RestaurantAreaMember affectedMember,
        RestaurantPlacementValidationResult validationResult,
        string message
    )
    {
        Succeeded =
            succeeded;

        FailureReason =
            failureReason;

        AffectedObject =
            affectedObject;

        AffectedMember =
            affectedMember;

        ValidationResult =
            validationResult;

        Message =
            message ?? string.Empty;
    }

    public static RestaurantEditHistoryCommandResult Success(
        Object affectedObject,
        RestaurantAreaMember affectedMember,
        string message = ""
    )
    {
        return new RestaurantEditHistoryCommandResult(
            true,
            RestaurantEditHistoryCommandFailureReason.None,
            affectedObject,
            affectedMember,
            default,
            message
        );
    }

    public static RestaurantEditHistoryCommandResult Failure(
        RestaurantEditHistoryCommandFailureReason failureReason,
        Object affectedObject,
        RestaurantAreaMember affectedMember,
        RestaurantPlacementValidationResult validationResult = default,
        string message = ""
    )
    {
        return new RestaurantEditHistoryCommandResult(
            false,
            failureReason,
            affectedObject,
            affectedMember,
            validationResult,
            message
        );
    }
}

/// <summary>
/// Comando permanente para un movimiento o rotación confirmados.
///
/// Conserva el estado anterior y posterior completos, incluida el área,
/// la jerarquía, la escala y la pose. Puede utilizarse con cualquier
/// RestaurantAreaMember colocable, no solo con mesas.
/// </summary>
public sealed class RestaurantMovePlaceableHistoryCommand :
    IRestaurantEditHistoryCommand
{
    private readonly RestaurantAreaMember member;

    private readonly RestaurantPlacementStateSnapshot before;

    private readonly RestaurantPlacementStateSnapshot after;

    private readonly RestaurantPlacementValidationService
        validationService;

    private readonly bool validateDestinationBeforeApplying;

    public RestaurantEditHistoryCommandType CommandType
    {
        get
        {
            return RestaurantEditHistoryCommandType.Move;
        }
    }

    public string Description
    {
        get
        {
            string targetName =
                member != null
                    ? member.name
                    : "objeto no disponible";

            return
                "Mover " +
                targetName;
        }
    }

    public Object PrimaryTarget
    {
        get
        {
            return member;
        }
    }

    public bool IsValid
    {
        get
        {
            return member != null &&
                   before.IsValid &&
                   after.IsValid &&
                   before.IsMeaningfullyDifferentFrom(
                       after
                   );
        }
    }

    public RestaurantMovePlaceableHistoryCommand(
        RestaurantAreaMember member,
        RestaurantPlacementStateSnapshot before,
        RestaurantPlacementStateSnapshot after,
        RestaurantPlacementValidationService validationService,
        bool validateDestinationBeforeApplying
    )
    {
        this.member =
            member;

        this.before =
            before;

        this.after =
            after;

        this.validationService =
            validationService;

        this.validateDestinationBeforeApplying =
            validateDestinationBeforeApplying;
    }

    public bool TryUndo(
        out RestaurantEditHistoryCommandResult result
    )
    {
        return TryApplySnapshot(
            before,
            "Movimiento deshecho.",
            out result
        );
    }

    public bool TryRedo(
        out RestaurantEditHistoryCommandResult result
    )
    {
        return TryApplySnapshot(
            after,
            "Movimiento rehecho.",
            out result
        );
    }

    public void ReleaseResources()
    {
        /*
         * Un comando de movimiento no posee la instancia.
         * El objeto continúa perteneciendo al restaurante.
         */
    }

    private bool TryApplySnapshot(
        RestaurantPlacementStateSnapshot snapshot,
        string successMessage,
        out RestaurantEditHistoryCommandResult result
    )
    {
        if (member == null)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .TargetUnavailable,
                    null,
                    null,
                    default,
                    "El objeto asociado al comando ya no existe."
                );

            return false;
        }

        if (!snapshot.IsValid)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .StateInvalid,
                    member,
                    member,
                    default,
                    "La captura de estado no es válida."
                );

            return false;
        }

        RestaurantPlacementValidationResult
            validationResult =
                default;

        if (validateDestinationBeforeApplying)
        {
            if (validationService == null)
            {
                result =
                    RestaurantEditHistoryCommandResult.Failure(
                        RestaurantEditHistoryCommandFailureReason
                            .ValidationSystemUnavailable,
                        member,
                        member,
                        validationResult,
                        "No está disponible el sistema de validación."
                    );

                return false;
            }

            snapshot.GetWorldPose(
                out Vector3 destinationPosition,
                out Quaternion destinationRotation
            );

            validationResult =
                validationService.ValidatePlacement(
                    member,
                    destinationPosition,
                    destinationRotation
                );

            if (!validationResult.IsValid)
            {
                result =
                    RestaurantEditHistoryCommandResult.Failure(
                        RestaurantEditHistoryCommandFailureReason
                            .DestinationInvalid,
                        member,
                        member,
                        validationResult,
                        "La posición de destino ya no es válida."
                    );

                return false;
            }
        }

        if (!snapshot.Restore(member))
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .ApplyFailed,
                    member,
                    member,
                    validationResult,
                    "No se pudo restaurar el estado del objeto."
                );

            return false;
        }

        result =
            RestaurantEditHistoryCommandResult.Success(
                member,
                member,
                successMessage
            );

        return true;
    }
}

/// <summary>
/// Tipos permanentes de operación que puede conservar el historial.
/// </summary>
public enum RestaurantEditHistoryCommandType
{
    Unknown = 0,
    Move = 1,
    Create = 2,
    Delete = 3,
    Replace = 4,
    Configure = 5
}

/// <summary>
/// Motivo genérico por el que un comando no puede aplicarse.
/// </summary>
public enum RestaurantEditHistoryCommandFailureReason
{
    None = 0,
    CommandUnavailable = 1,
    CommandInvalid = 2,
    TargetUnavailable = 3,
    StateInvalid = 4,
    ValidationSystemUnavailable = 5,
    DestinationInvalid = 6,
    LifecycleSystemUnavailable = 7,
    IdentityConflict = 8,
    RegistrationFailed = 9,
    ApplyFailed = 10
}
