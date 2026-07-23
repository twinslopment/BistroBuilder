using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Comando atómico de historial para una raíz y todos sus elementos
/// enlazados.
///
/// Aplica primero todas las poses de destino, sincroniza física y valida
/// después el conjunto completo. Si cualquier miembro falla, restaura el
/// estado previo de todos los objetos y no altera la rama de historial.
/// </summary>
public sealed class RestaurantMoveLinkedGroupHistoryCommand :
    IRestaurantEditHistoryCommand
{
    private readonly RestaurantAreaMember rootMember;

    private readonly RestaurantAreaMember[] members;

    private readonly RestaurantPlacementStateSnapshot[] before;

    private readonly RestaurantPlacementStateSnapshot[] after;

    private readonly RestaurantPlacementValidationService
        validationService;

    private readonly RestaurantPlacementLinkedGroupService
        linkedGroupService;

    private readonly bool validateDestinationBeforeApplying;

    public RestaurantEditHistoryCommandType CommandType =>
        RestaurantEditHistoryCommandType.Move;

    public string Description
    {
        get
        {
            string targetName =
                rootMember != null
                    ? rootMember.name
                    : "conjunto no disponible";

            return
                "Mover conjunto " +
                targetName;
        }
    }

    public Object PrimaryTarget =>
        rootMember;

    public bool IsValid
    {
        get
        {
            if (rootMember == null ||
                members == null ||
                before == null ||
                after == null ||
                members.Length < 2 ||
                members.Length != before.Length ||
                members.Length != after.Length)
            {
                return false;
            }

            bool hasMeaningfulChange = false;

            for (int index = 0;
                 index < members.Length;
                 index++)
            {
                if (members[index] == null ||
                    !before[index].IsValid ||
                    !after[index].IsValid)
                {
                    return false;
                }

                if (before[index].IsMeaningfullyDifferentFrom(
                        after[index]
                    ))
                {
                    hasMeaningfulChange = true;
                }
            }

            return hasMeaningfulChange;
        }
    }

    public RestaurantMoveLinkedGroupHistoryCommand(
        RestaurantAreaMember rootMember,
        RestaurantAreaMember[] members,
        RestaurantPlacementStateSnapshot[] before,
        RestaurantPlacementStateSnapshot[] after,
        RestaurantPlacementValidationService validationService,
        RestaurantPlacementLinkedGroupService linkedGroupService,
        bool validateDestinationBeforeApplying
    )
    {
        this.rootMember = rootMember;
        this.members = members;
        this.before = before;
        this.after = after;
        this.validationService = validationService;
        this.linkedGroupService = linkedGroupService;
        this.validateDestinationBeforeApplying =
            validateDestinationBeforeApplying;
    }

    public bool TryUndo(
        out RestaurantEditHistoryCommandResult result
    )
    {
        return TryApplySnapshots(
            before,
            "Movimiento de conjunto deshecho.",
            out result
        );
    }

    public bool TryRedo(
        out RestaurantEditHistoryCommandResult result
    )
    {
        return TryApplySnapshots(
            after,
            "Movimiento de conjunto rehecho.",
            out result
        );
    }

    public void ReleaseResources()
    {
        /*
         * El comando no posee las instancias. Solo conserva referencias y
         * capturas de estado de objetos pertenecientes al restaurante.
         */
    }

    private bool TryApplySnapshots(
        RestaurantPlacementStateSnapshot[] destination,
        string successMessage,
        out RestaurantEditHistoryCommandResult result
    )
    {
        if (!ValidateCommandState(
                destination,
                out result
            ))
        {
            return false;
        }

        RestaurantPlacementStateSnapshot[] rollback =
            new RestaurantPlacementStateSnapshot[members.Length];

        for (int index = 0;
             index < members.Length;
             index++)
        {
            rollback[index] =
                RestaurantPlacementStateSnapshot.Capture(
                    members[index]
                );
        }

        if (!RestoreAll(destination))
        {
            RestoreAll(rollback);

            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .ApplyFailed,
                    rootMember,
                    rootMember,
                    default,
                    "No se pudo aplicar una de las poses del conjunto."
                );

            return false;
        }

        Physics.SyncTransforms();

        if (validateDestinationBeforeApplying &&
            !ValidateCurrentGroup(
                out RestaurantPlacementValidationResult
                    invalidResult,
                out RestaurantAreaMember invalidMember
            ))
        {
            RestoreAll(rollback);
            Physics.SyncTransforms();

            linkedGroupService?.NotifyConfirmedGroupPoseApplied(
                rootMember,
                members
            );

            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .DestinationInvalid,
                    rootMember,
                    invalidMember ?? rootMember,
                    invalidResult,
                    "La posición de destino del conjunto ya no es válida."
                );

            return false;
        }

        linkedGroupService?.NotifyConfirmedGroupPoseApplied(
            rootMember,
            members
        );

        result =
            RestaurantEditHistoryCommandResult.Success(
                rootMember,
                rootMember,
                successMessage
            );

        return true;
    }

    private bool ValidateCommandState(
        RestaurantPlacementStateSnapshot[] destination,
        out RestaurantEditHistoryCommandResult result
    )
    {
        if (rootMember == null ||
            members == null ||
            destination == null ||
            members.Length != destination.Length)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .CommandInvalid,
                    rootMember,
                    rootMember,
                    default,
                    "El comando de conjunto no contiene datos coherentes."
                );

            return false;
        }

        if (validateDestinationBeforeApplying &&
            validationService == null)
        {
            result =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .ValidationSystemUnavailable,
                    rootMember,
                    rootMember,
                    default,
                    "No está disponible el sistema de validación."
                );

            return false;
        }

        for (int index = 0;
             index < members.Length;
             index++)
        {
            if (members[index] == null)
            {
                result =
                    RestaurantEditHistoryCommandResult.Failure(
                        RestaurantEditHistoryCommandFailureReason
                            .TargetUnavailable,
                        rootMember,
                        rootMember,
                        default,
                        "Uno de los elementos del conjunto ya no existe."
                    );

                return false;
            }

            if (!destination[index].IsValid)
            {
                result =
                    RestaurantEditHistoryCommandResult.Failure(
                        RestaurantEditHistoryCommandFailureReason
                            .StateInvalid,
                        rootMember,
                        members[index],
                        default,
                        "Una captura de estado del conjunto no es válida."
                    );

                return false;
            }
        }

        result = default;
        return true;
    }

    private bool RestoreAll(
        RestaurantPlacementStateSnapshot[] snapshots
    )
    {
        if (snapshots == null ||
            snapshots.Length != members.Length)
        {
            return false;
        }

        bool restoredAll = true;

        for (int index = 0;
             index < members.Length;
             index++)
        {
            restoredAll &=
                snapshots[index].Restore(
                    members[index]
                );
        }

        return restoredAll;
    }

    private bool ValidateCurrentGroup(
        out RestaurantPlacementValidationResult invalidResult,
        out RestaurantAreaMember invalidMember
    )
    {
        invalidResult = default;
        invalidMember = null;

        for (int index = 0;
             index < members.Length;
             index++)
        {
            RestaurantAreaMember member =
                members[index];

            RestaurantPlacementValidationResult validation =
                validationService.ValidateCurrentPlacement(
                    member
                );

            if (validation.IsValid)
            {
                continue;
            }

            invalidResult = validation;
            invalidMember = member;
            return false;
        }

        return true;
    }
}
