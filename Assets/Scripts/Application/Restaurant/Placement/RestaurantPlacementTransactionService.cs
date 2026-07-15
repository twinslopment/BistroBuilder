using System;
using UnityEngine;

/// <summary>
/// Gestiona operaciones transaccionales de colocación.
///
/// Permite:
/// - Iniciar la edición de un objeto existente.
/// - Evaluar y mostrar posiciones candidatas.
/// - Confirmar únicamente colocaciones válidas.
/// - Cancelar y restaurar exactamente el estado original.
/// - Actualizar el área asignada al confirmar.
/// - Publicar cambios confirmados para el historial de deshacer.
///
/// El servicio no interpreta controles de ratón ni dibuja la UI.
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestaurantPlacementTransactionService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Servicio que valida áreas, capacidades, límites, " +
        "solapamientos, obstáculos y separación mínima."
    )]
    [SerializeField]
    private RestaurantPlacementValidationService
        validationService;

    private RestaurantAreaMember activeMember;

    private RestaurantPlacementStateSnapshot
        originalState;

    private RestaurantPlacementValidationResult
        lastValidationResult;

    private bool hasActiveTransaction;

    private bool hasEvaluatedPreview;

    /// <summary>
    /// Se ejecuta cuando comienza una operación de edición.
    /// </summary>
    public event Action<
        RestaurantAreaMember,
        RestaurantPlacementValidationResult
    > PlacementStarted;

    /// <summary>
    /// Se ejecuta cada vez que cambia la pose candidata.
    /// </summary>
    public event Action<
        RestaurantAreaMember,
        RestaurantPlacementValidationResult
    > PlacementPreviewChanged;

    /// <summary>
    /// Se ejecuta cuando una colocación válida se confirma.
    ///
    /// Se conserva por compatibilidad con consumidores existentes.
    /// </summary>
    public event Action<
        RestaurantAreaMember,
        RestaurantPlacementValidationResult
    > PlacementCommitted;

    /// <summary>
    /// Publica el estado anterior y posterior de una confirmación.
    ///
    /// El historial utiliza este evento para registrar únicamente
    /// cambios realmente confirmados.
    /// </summary>
    public event Action<
        RestaurantPlacementCommittedChange
    > PlacementCommittedWithHistory;

    /// <summary>
    /// Se ejecuta cuando una confirmación es rechazada.
    /// </summary>
    public event Action<
        RestaurantAreaMember,
        RestaurantPlacementValidationResult
    > PlacementCommitRejected;

    /// <summary>
    /// Se ejecuta cuando se cancela la operación y se restaura
    /// el estado original.
    /// </summary>
    public event Action<RestaurantAreaMember>
        PlacementCancelled;

    public bool HasActiveTransaction
    {
        get
        {
            return hasActiveTransaction;
        }
    }

    public bool HasEvaluatedPreview
    {
        get
        {
            return hasEvaluatedPreview;
        }
    }

    public RestaurantAreaMember ActiveMember
    {
        get
        {
            return activeMember;
        }
    }

    public RestaurantPlacementValidationResult
        LastValidationResult
    {
        get
        {
            return lastValidationResult;
        }
    }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void OnDisable()
    {
        /*
         * Nunca debe quedar un objeto abandonado en una posición
         * provisional al desactivar el sistema.
         */
        if (hasActiveTransaction)
        {
            CancelPlacement();
        }
    }

    /// <summary>
    /// Inicia la edición de un objeto existente.
    ///
    /// Guarda:
    /// - Área asignada.
    /// - Padre.
    /// - Índice entre hermanos.
    /// - Posición local y mundial.
    /// - Rotación local y mundial.
    /// - Escala local.
    ///
    /// Solo puede existir una transacción activa a la vez.
    /// </summary>
    public bool TryBeginPlacement(
        RestaurantAreaMember member,
        out RestaurantPlacementTransactionFailureReason
            failureReason
    )
    {
        failureReason =
            RestaurantPlacementTransactionFailureReason.None;

        if (member == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .InvalidMember;

            return false;
        }

        if (validationService == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .ValidationSystemUnavailable;

            return false;
        }

        if (hasActiveTransaction)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .OperationAlreadyActive;

            return false;
        }

        activeMember =
            member;

        originalState =
            RestaurantPlacementStateSnapshot.Capture(
                member
            );

        lastValidationResult =
            validationService.ValidateCurrentPlacement(
                member
            );

        hasActiveTransaction = true;
        hasEvaluatedPreview = false;

        PlacementStarted?.Invoke(
            member,
            lastValidationResult
        );

        return true;
    }

    /// <summary>
    /// Evalúa y aplica una pose candidata.
    ///
    /// La pose se aplica aunque sea inválida para que el jugador
    /// pueda verla resaltada y continuar moviéndola.
    ///
    /// No modifica todavía el área asignada.
    /// </summary>
    public bool TryPreviewPlacement(
        Vector3 candidateWorldPosition,
        Quaternion candidateWorldRotation,
        out RestaurantPlacementValidationResult result,
        out RestaurantPlacementTransactionFailureReason
            failureReason
    )
    {
        result = default;

        failureReason =
            RestaurantPlacementTransactionFailureReason.None;

        RestaurantAreaMember member;

        if (!TryGetValidActiveMember(
                out member,
                out failureReason
            ))
        {
            return false;
        }

        if (validationService == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .ValidationSystemUnavailable;

            return false;
        }

        result =
            validationService.ValidatePlacement(
                member,
                candidateWorldPosition,
                candidateWorldRotation
            );

        /*
         * La pose se aplica después de validar. También se muestran
         * físicamente las posiciones inválidas mientras la operación
         * permanezca abierta.
         */
        member.transform.SetPositionAndRotation(
            candidateWorldPosition,
            candidateWorldRotation
        );

        lastValidationResult =
            result;

        hasEvaluatedPreview =
            true;

        PlacementPreviewChanged?.Invoke(
            member,
            result
        );

        return true;
    }

    /// <summary>
    /// Revalida la pose actual y confirma la colocación.
    ///
    /// La confirmación:
    /// - Se rechaza si la posición no es válida.
    /// - Actualiza el área asignada.
    /// - Captura el estado final.
    /// - Publica un cambio para el historial.
    /// - Cierra la transacción.
    /// </summary>
    public bool TryCommitPlacement(
        out RestaurantPlacementValidationResult result,
        out RestaurantPlacementTransactionFailureReason
            failureReason
    )
    {
        result = default;

        failureReason =
            RestaurantPlacementTransactionFailureReason.None;

        RestaurantAreaMember member;

        if (!TryGetValidActiveMember(
                out member,
                out failureReason
            ))
        {
            return false;
        }

        if (validationService == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .ValidationSystemUnavailable;

            return false;
        }

        /*
         * Se valida otra vez para no confiar en un resultado
         * anterior que pueda haber quedado obsoleto.
         */
        result =
            validationService.ValidateCurrentPlacement(
                member
            );

        lastValidationResult =
            result;

        hasEvaluatedPreview =
            true;

        if (!result.IsValid ||
            result.CandidateArea == null)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .PlacementInvalid;

            PlacementCommitRejected?.Invoke(
                member,
                result
            );

            return false;
        }

        /*
         * SetArea dispara AreaChanged. Los registros de miembros
         * y colocación actualizan sus índices mediante eventos.
         */
        member.SetArea(
            result.CandidateArea
        );

        RestaurantPlacementStateSnapshot finalState =
            RestaurantPlacementStateSnapshot.Capture(
                member
            );

        RestaurantPlacementCommittedChange committedChange =
            new RestaurantPlacementCommittedChange(
                member,
                originalState,
                finalState,
                result
            );

        RestaurantAreaMember committedMember =
            member;

        ClearActiveTransaction();

        PlacementCommitted?.Invoke(
            committedMember,
            result
        );

        /*
         * No se registra una confirmación que no haya producido
         * ningún cambio real de posición, rotación, jerarquía,
         * escala o área.
         */
        if (committedChange.HasMeaningfulChange)
        {
            PlacementCommittedWithHistory?.Invoke(
                committedChange
            );
        }

        return true;
    }

    /// <summary>
    /// Cancela la operación y restaura exactamente:
    /// - Jerarquía.
    /// - Posición.
    /// - Rotación.
    /// - Escala.
    /// - Área asignada.
    /// </summary>
    public bool CancelPlacement()
    {
        if (!hasActiveTransaction)
        {
            return false;
        }

        RestaurantAreaMember cancelledMember =
            activeMember;

        if (cancelledMember != null)
        {
            originalState.Restore(
                cancelledMember
            );
        }

        ClearActiveTransaction();

        PlacementCancelled?.Invoke(
            cancelledMember
        );

        return true;
    }

    /// <summary>
    /// Devuelve la pose mundial original de la operación activa.
    /// </summary>
    public bool TryGetOriginalWorldPose(
        out Vector3 worldPosition,
        out Quaternion worldRotation
    )
    {
        worldPosition = default;
        worldRotation = Quaternion.identity;

        if (!hasActiveTransaction ||
            activeMember == null ||
            !originalState.IsValid)
        {
            return false;
        }

        originalState.GetWorldPose(
            out worldPosition,
            out worldRotation
        );

        return true;
    }

    /// <summary>
    /// Comprueba que exista una operación activa y que su miembro
    /// siga disponible.
    /// </summary>
    private bool TryGetValidActiveMember(
        out RestaurantAreaMember member,
        out RestaurantPlacementTransactionFailureReason
            failureReason
    )
    {
        member = null;

        failureReason =
            RestaurantPlacementTransactionFailureReason.None;

        if (!hasActiveTransaction)
        {
            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .NoActiveOperation;

            return false;
        }

        if (activeMember == null)
        {
            ClearActiveTransaction();

            failureReason =
                RestaurantPlacementTransactionFailureReason
                    .ActiveMemberUnavailable;

            return false;
        }

        member =
            activeMember;

        return true;
    }

    /// <summary>
    /// Limpia los datos internos de la transacción sin modificar
    /// el objeto editado.
    /// </summary>
    private void ClearActiveTransaction()
    {
        activeMember = null;
        originalState = default;
        lastValidationResult = default;

        hasActiveTransaction = false;
        hasEvaluatedPreview = false;
    }

    /// <summary>
    /// Recupera automáticamente dependencias situadas en el mismo
    /// GameObject.
    /// </summary>
    private void CacheDependenciesIfNeeded()
    {
        if (validationService == null)
        {
            TryGetComponent(
                out validationService
            );
        }
    }

    /// <summary>
    /// Comprueba que las dependencias obligatorias estén
    /// disponibles.
    /// </summary>
    private void ValidateDependencies()
    {
        if (validationService != null)
        {
            return;
        }

        Debug.LogError(
            nameof(RestaurantPlacementTransactionService) +
            " necesita un " +
            nameof(RestaurantPlacementValidationService) +
            ".",
            this
        );
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

/// <summary>
/// Estado completo de un objeto colocable en un instante concreto.
///
/// Se almacena tanto en coordenadas locales como mundiales para
/// restaurar con precisión incluso si el objeto no tiene padre.
/// </summary>
public readonly struct RestaurantPlacementStateSnapshot
{
    private readonly bool isValid;

    private readonly Transform parent;

    private readonly int siblingIndex;

    private readonly Vector3 localPosition;

    private readonly Quaternion localRotation;

    private readonly Vector3 localScale;

    private readonly Vector3 worldPosition;

    private readonly Quaternion worldRotation;

    private readonly RestaurantArea assignedArea;

    public bool IsValid
    {
        get
        {
            return isValid;
        }
    }

    public RestaurantArea AssignedArea
    {
        get
        {
            return assignedArea;
        }
    }

    private RestaurantPlacementStateSnapshot(
        bool isValid,
        Transform parent,
        int siblingIndex,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Vector3 worldPosition,
        Quaternion worldRotation,
        RestaurantArea assignedArea
    )
    {
        this.isValid =
            isValid;

        this.parent =
            parent;

        this.siblingIndex =
            siblingIndex;

        this.localPosition =
            localPosition;

        this.localRotation =
            localRotation;

        this.localScale =
            localScale;

        this.worldPosition =
            worldPosition;

        this.worldRotation =
            worldRotation;

        this.assignedArea =
            assignedArea;
    }

    /// <summary>
    /// Captura el estado del miembro y su Transform.
    /// </summary>
    public static RestaurantPlacementStateSnapshot Capture(
        RestaurantAreaMember member
    )
    {
        if (member == null)
        {
            return default;
        }

        Transform target =
            member.transform;

        return new RestaurantPlacementStateSnapshot(
            true,
            target.parent,
            target.GetSiblingIndex(),
            target.localPosition,
            target.localRotation,
            target.localScale,
            target.position,
            target.rotation,
            member.AssignedArea
        );
    }

    /// <summary>
    /// Devuelve la pose mundial capturada.
    /// </summary>
    public void GetWorldPose(
        out Vector3 position,
        out Quaternion rotation
    )
    {
        position =
            worldPosition;

        rotation =
            worldRotation;
    }

    /// <summary>
    /// Restaura jerarquía, Transform y área.
    /// </summary>
    public bool Restore(
        RestaurantAreaMember member
    )
    {
        if (!isValid ||
            member == null)
        {
            return false;
        }

        Transform target =
            member.transform;

        if (parent != null)
        {
            target.SetParent(
                parent,
                false
            );

            target.localPosition =
                localPosition;

            target.localRotation =
                localRotation;

            target.localScale =
                localScale;

            int maximumSiblingIndex =
                Mathf.Max(
                    0,
                    parent.childCount - 1
                );

            target.SetSiblingIndex(
                Mathf.Clamp(
                    siblingIndex,
                    0,
                    maximumSiblingIndex
                )
            );
        }
        else
        {
            target.SetParent(
                null,
                true
            );

            target.SetPositionAndRotation(
                worldPosition,
                worldRotation
            );

            target.localScale =
                localScale;
        }

        if (assignedArea != null)
        {
            member.SetArea(
                assignedArea
            );
        }
        else
        {
            member.ClearArea();
        }

        return true;
    }

    /// <summary>
    /// Indica si dos capturas representan estados distintos.
    /// </summary>
    public bool IsMeaningfullyDifferentFrom(
        RestaurantPlacementStateSnapshot other
    )
    {
        if (!isValid ||
            !other.isValid)
        {
            return isValid !=
                   other.isValid;
        }

        if (parent != other.parent ||
            siblingIndex != other.siblingIndex ||
            assignedArea != other.assignedArea)
        {
            return true;
        }

        const float positionTolerance =
            0.0001f;

        const float scaleTolerance =
            0.0001f;

        const float rotationToleranceDegrees =
            0.01f;

        if ((
                localPosition -
                other.localPosition
            ).sqrMagnitude >
            positionTolerance *
            positionTolerance)
        {
            return true;
        }

        if (Quaternion.Angle(
                localRotation,
                other.localRotation
            ) >
            rotationToleranceDegrees)
        {
            return true;
        }

        return (
                   localScale -
                   other.localScale
               ).sqrMagnitude >
               scaleTolerance *
               scaleTolerance;
    }
}

/// <summary>
/// Cambio confirmado que puede deshacerse y rehacerse.
/// </summary>
public readonly struct RestaurantPlacementCommittedChange
{
    public RestaurantAreaMember Member { get; }

    public RestaurantPlacementStateSnapshot Before { get; }

    public RestaurantPlacementStateSnapshot After { get; }

    public RestaurantPlacementValidationResult
        ValidationResult
    { get; }

    public bool HasMeaningfulChange
    {
        get
        {
            return Before.IsMeaningfullyDifferentFrom(
                After
            );
        }
    }

    public RestaurantPlacementCommittedChange(
        RestaurantAreaMember member,
        RestaurantPlacementStateSnapshot before,
        RestaurantPlacementStateSnapshot after,
        RestaurantPlacementValidationResult validationResult
    )
    {
        Member =
            member;

        Before =
            before;

        After =
            after;

        ValidationResult =
            validationResult;
    }
}

/// <summary>
/// Motivo por el que una operación transaccional no puede
/// completarse.
/// </summary>
public enum RestaurantPlacementTransactionFailureReason
{
    None = 0,
    InvalidMember = 1,
    OperationAlreadyActive = 2,
    NoActiveOperation = 3,
    ActiveMemberUnavailable = 4,
    ValidationSystemUnavailable = 5,
    PlacementInvalid = 6
}
